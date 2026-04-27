using Maichess.MatchManager.V1;

namespace MaichessMatchMakerService.Queue;

internal sealed class QueueingService(IQueueRepository queue, Matches.MatchesClient matchesClient)
{
    private static readonly HashSet<string> ValidTimeControls = ["bullet", "blitz", "rapid", "classical"];

    internal async Task<EnqueueResult> EnqueueAsync(
        string userId, string timeControl, string opponentType, string? botId, CancellationToken ct)
    {
        if (!ValidTimeControls.Contains(timeControl))
        {
            return new EnqueueResult.InvalidInput("invalid time_control");
        }

        if (opponentType is not "human" and not "bot")
        {
            return new EnqueueResult.InvalidInput("opponent.type must be 'human' or 'bot'");
        }

        if (opponentType == "bot" && string.IsNullOrWhiteSpace(botId))
        {
            return new EnqueueResult.InvalidInput("opponent.bot_id is required for bot matches");
        }

        string? existingToken = await queue.GetUserQueueTokenAsync(userId);
        if (existingToken is not null)
        {
            return new EnqueueResult.AlreadyQueued();
        }

        string queueToken = Guid.NewGuid().ToString();

        if (opponentType == "bot")
        {
            var request = new CreateMatchRequest
            {
                White = new Player { UserId = userId },
                Black = new Player { BotId = botId },
                TimeControl = MatchingService.MapTimeControl(timeControl),
            };

            CreateMatchResponse response = await matchesClient.CreateMatchAsync(request, cancellationToken: ct);
            await queue.EnqueueBotMatchAsync(queueToken, userId, timeControl, response.Match.Id);
        }
        else
        {
            await queue.EnqueueAsync(queueToken, userId, timeControl);
        }

        return new EnqueueResult.Success(queueToken);
    }

    internal async Task<GetStatusResult> GetStatusAsync(string queueToken, string userId)
    {
        QueueEntry? entry = await queue.GetEntryAsync(queueToken);

        return entry is null || entry.UserId != userId
            ? new GetStatusResult.NotFound()
            : new GetStatusResult.Found(
                entry.Status == QueueStatus.Matched ? "matched" : "waiting",
                entry.MatchId);
    }

    internal async Task<DequeueResult> DequeueAsync(string queueToken, string userId)
    {
        QueueEntry? entry = await queue.GetEntryAsync(queueToken);

        if (entry is null || entry.UserId != userId)
        {
            return new DequeueResult.NotFound();
        }

        await queue.RemoveAsync(queueToken, userId, entry.TimeControl);
        return new DequeueResult.Success();
    }
}
