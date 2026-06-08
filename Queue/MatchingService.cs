using Maichess.MatchManager.V1;
using MaichessMatchMakerService.Streaming;

namespace MaichessMatchMakerService.Queue;

internal sealed class MatchingService(
    IQueueRepository queue,
    Matches.MatchesClient matchesClient,
    IMatchmakingNotifier socketNotifier,
    IUserRatingStore ratingStore,
    ILogger<MatchingService> logger)
{
    // Once a time-control pool reaches this many waiting players, pair by closest live
    // rating (read locally from the KTable store) instead of longest wait. Mirrors the
    // Match Maker matchmaking rule in the service CLAUDE.md.
    private const long SkillPairingThreshold = 10;

    // Picks the globally closest-rated waiting pair (minimum rating gap). Because the
    // minimum gap in a sorted set is always between neighbours, one sort + a neighbour
    // scan suffices. Returns null when fewer than two players carry a usable rating.
    internal static (string White, string Black)? ClosestRatedPair(IReadOnlyList<RatedPlayer> players)
    {
        if (players.Count < 2)
        {
            return null;
        }

        List<RatedPlayer> ordered = [.. players.OrderBy(p => p.Rating)];
        double bestGap = double.MaxValue;
        (string White, string Black)? best = null;
        for (int i = 1; i < ordered.Count; i++)
        {
            double gap = ordered[i].Rating - ordered[i - 1].Rating;
            if (gap < bestGap)
            {
                bestGap = gap;
                best = (ordered[i - 1].Token, ordered[i].Token);
            }
        }

        return best;
    }

    internal async Task TryMatchAsync(string timeFormatId, CancellationToken ct)
    {
        long count = await queue.GetQueueCountAsync(timeFormatId);
        if (count < 2)
        {
            return;
        }

        string[] tokens = await SelectPairAsync(timeFormatId, count);
        if (tokens.Length < 2)
        {
            return;
        }

        QueueEntry? white = await queue.GetEntryAsync(tokens[0]);
        QueueEntry? black = await queue.GetEntryAsync(tokens[1]);

        if (white is null || black is null)
        {
            logger.LogWarning(
                "Queue entry missing during matching — tokens: {White}, {Black}",
                tokens[0],
                tokens[1]);
            return;
        }

        socketNotifier.PlayersMatched(white.UserId, black.UserId, timeFormatId);

        try
        {
            var request = new CreateMatchRequest
            {
                White = new Player { UserId = white.UserId },
                Black = new Player { UserId = black.UserId },
                TimeFormat = TimeFormatRegistry.Resolve(timeFormatId),
            };

            CreateMatchResponse response = await matchesClient.CreateMatchAsync(request, cancellationToken: ct);

            string matchId = response.Match.Id;
            await queue.MarkMatchedAsync(tokens[0], white.UserId, matchId);
            await queue.MarkMatchedAsync(tokens[1], black.UserId, matchId);

            socketNotifier.NotifyMatched(white.UserId, matchId);
            socketNotifier.NotifyMatched(black.UserId, matchId);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to create match for tokens {White} and {Black}", tokens[0], tokens[1]);
        }
    }

    // Skill pairing once the pool is large enough: read each waiting player's live rating
    // locally from the KTable store, pair the closest, and remove exactly those two
    // tokens. Anything short of a clean skill pair (too few rated players, or the two
    // tokens slipped away between read and remove) falls back to FIFO.
    private async Task<string[]> SelectPairAsync(string timeFormatId, long count)
    {
        if (count >= SkillPairingThreshold)
        {
            IReadOnlyList<(string Token, string UserId)> waiting =
                await queue.GetWaitingPlayersAsync(timeFormatId);

            List<RatedPlayer> rated = [];
            foreach ((string token, string userId) in waiting)
            {
                if (ratingStore.TryGet(userId) is { HasRating: true, Flagged: false } rating)
                {
                    rated.Add(new RatedPlayer(token, rating.Rating));
                }
            }

            if (ClosestRatedPair(rated) is { } pair
                && await queue.DequeueSpecificPairAsync(timeFormatId, pair.White, pair.Black))
            {
                return [pair.White, pair.Black];
            }
        }

        return await queue.DequeueOldestPairAsync(timeFormatId);
    }

    // A waiting player's queue token paired with the live rating used to skill-match them.
    internal readonly record struct RatedPlayer(string Token, double Rating);
}
