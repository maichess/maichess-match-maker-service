using MaichessMatchMakerService.Streaming;

namespace MaichessMatchMakerService.Queue;

internal sealed class MatchingService(
    IQueueRepository queue,
    IMatchCreator matchCreator,
    IMatchmakingNotifier socketNotifier,
    IUserRatingStore ratingStore,
    ICheatFlagStore cheatFlags,
    ILogger<MatchingService> logger)
{
    // Once a time-control pool reaches this many waiting players, pair by closest live
    // rating (read locally from the KTable store) instead of longest wait. Mirrors the
    // Match Maker matchmaking rule in the service CLAUDE.md.
    private const long SkillPairingThreshold = 10;

    // Anti-cheat admissibility: a pair stands only when each side is acceptable to the
    // other — a flagged player needs the opponent's allow_flagged consent, in both
    // directions (a flagged searcher equally needs consent from a non-flagged
    // opponent). A matchmaking filter, not a ban: two flagged players who both allow
    // flagged opponents can still meet.
    internal static bool IsAdmissible(WaitingPlayer a, WaitingPlayer b) =>
        (!a.Flagged || b.AllowFlagged) && (!b.Flagged || a.AllowFlagged);

    // Picks the closest-rated admissible pair. Admissibility can rule out rating
    // neighbours, so all pairs are scanned (queues are small; O(n^2) is fine) instead
    // of the neighbour walk a pure min-gap search would allow.
    internal static (string White, string Black)? ClosestRatedPair(IReadOnlyList<RatedPlayer> players)
    {
        List<RatedPlayer> ordered = [.. players.OrderBy(p => p.Rating)];
        double bestGap = double.MaxValue;
        (string White, string Black)? best = null;
        for (int i = 0; i < ordered.Count; i++)
        {
            for (int j = i + 1; j < ordered.Count; j++)
            {
                double gap = ordered[j].Rating - ordered[i].Rating;
                if (gap < bestGap && IsAdmissible(ordered[i].Player, ordered[j].Player))
                {
                    bestGap = gap;
                    best = (ordered[i].Player.Token, ordered[j].Player.Token);
                }
            }
        }

        return best;
    }

    // FIFO with admissibility: the waiting list is oldest-first, so the first
    // admissible pair in scan order matches the longest-waiting player with the
    // oldest opponent acceptable to both.
    internal static (string A, string B)? OldestAdmissiblePair(IReadOnlyList<WaitingPlayer> players)
    {
        for (int i = 0; i < players.Count; i++)
        {
            for (int j = i + 1; j < players.Count; j++)
            {
                if (IsAdmissible(players[i], players[j]))
                {
                    return (players[i].Token, players[j].Token);
                }
            }
        }

        return null;
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
            string matchId = await matchCreator.CreateMatchAsync(
                new CommandPlayer(white.UserId, null),
                new CommandPlayer(black.UserId, null),
                timeFormatId,
                ct);

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

    // Pair selection: read the waiting list once, tag each player with their flag
    // state (local CheatFlagStore lookup), then try a skill pair (pool large enough)
    // before the FIFO fallback — both restricted to admissible pairs. Anything short
    // of a clean removal (no admissible pair, or the two tokens slipped away between
    // read and remove) matches nobody this tick.
    private async Task<string[]> SelectPairAsync(string timeFormatId, long count)
    {
        if (count >= SkillPairingThreshold)
        {
            List<RatedPlayer> rated = [];
            foreach (WaitingPlayer player in await ReadWaitingAsync(timeFormatId))
            {
                if (ratingStore.TryGet(player.UserId) is { HasRating: true } rating)
                {
                    rated.Add(new RatedPlayer(player, rating.Rating));
                }
            }

            if (ClosestRatedPair(rated) is { } pair
                && await queue.DequeueSpecificPairAsync(timeFormatId, pair.White, pair.Black))
            {
                return [pair.White, pair.Black];
            }
        }

        // Re-read for the FIFO fallback so a lost skill-pair race (the tokens were
        // claimed concurrently) sees them gone and pairs among whoever remains.
        return OldestAdmissiblePair(await ReadWaitingAsync(timeFormatId)) is { } fifo
            && await queue.DequeueSpecificPairAsync(timeFormatId, fifo.A, fifo.B)
            ? [fifo.A, fifo.B]
            : [];
    }

    // Waiting players (oldest-first) tagged with their live anti-cheat flag from the
    // local CheatFlagStore — no RPC on the pairing path.
    private async Task<IReadOnlyList<WaitingPlayer>> ReadWaitingAsync(string timeFormatId)
    {
        IReadOnlyList<(string Token, string UserId, bool AllowFlagged)> waiting =
            await queue.GetWaitingPlayersAsync(timeFormatId);
        return [.. waiting.Select(
            w => new WaitingPlayer(w.Token, w.UserId, w.AllowFlagged, cheatFlags.IsFlagged(w.UserId)))];
    }

    // A waiting player as the pairing pass sees them: queue token, anti-cheat toggle,
    // and current flag state.
    internal readonly record struct WaitingPlayer(string Token, string UserId, bool AllowFlagged, bool Flagged);

    // A waiting player paired with the live rating used to skill-match them.
    internal readonly record struct RatedPlayer(WaitingPlayer Player, double Rating);
}
