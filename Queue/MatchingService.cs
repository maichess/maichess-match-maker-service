using Maichess.MatchManager.V1;

namespace MaichessMatchMakerService.Queue;

internal sealed class MatchingService(
    IQueueRepository queue,
    Matches.MatchesClient matchesClient,
    SocketNotifier socketNotifier,
    ILogger<MatchingService> logger)
{
    internal async Task TryMatchAsync(string timeFormatId, CancellationToken ct)
    {
        long count = await queue.GetQueueCountAsync(timeFormatId);
        if (count < 2)
        {
            return;
        }

        string[] tokens = await queue.DequeueOldestPairAsync(timeFormatId);
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
}
