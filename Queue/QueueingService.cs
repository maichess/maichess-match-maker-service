using Maichess.MatchManager.V1;

namespace MaichessMatchMakerService.Queue;

internal sealed class QueueingService(IQueueRepository queue, Matches.MatchesClient matchesClient, SocketNotifier socketNotifier)
{
    internal async Task<EnqueueResult> EnqueueAsync(
        string userId, string timeFormatId, string opponentType, string? botId, CancellationToken ct)
    {
        if (!TimeFormatRegistry.IsKnown(timeFormatId))
        {
            return new EnqueueResult.InvalidInput("invalid time_format_id");
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
        TimeFormat timeFormat = TimeFormatRegistry.Resolve(timeFormatId);

        if (opponentType == "bot")
        {
            var request = new CreateMatchRequest
            {
                White = new Player { UserId = userId },
                Black = new Player { BotId = botId },
                TimeFormat = timeFormat,
            };

            CreateMatchResponse response = await matchesClient.CreateMatchAsync(request, cancellationToken: ct);
            string matchId = response.Match.Id;
            await queue.EnqueueBotMatchAsync(queueToken, userId, timeFormatId, matchId);
            socketNotifier.NotifyMatched(userId, matchId);
            return new EnqueueResult.Success(queueToken, matchId);
        }

        await queue.EnqueueAsync(queueToken, userId, timeFormatId);
        return new EnqueueResult.Success(queueToken);
    }

    internal async Task<EnqueueResult> CreateBotVsBotMatchAsync(
        string whiteBotId, string blackBotId, string timeFormatId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(whiteBotId) || string.IsNullOrWhiteSpace(blackBotId))
        {
            return new EnqueueResult.InvalidInput("bot ids are required");
        }

        if (!TimeFormatRegistry.IsKnown(timeFormatId))
        {
            return new EnqueueResult.InvalidInput("invalid time_format_id");
        }

        TimeFormat timeFormat = TimeFormatRegistry.Resolve(timeFormatId);
        var request = new CreateMatchRequest
        {
            White = new Player { BotId = whiteBotId },
            Black = new Player { BotId = blackBotId },
            TimeFormat = timeFormat,
        };

        CreateMatchResponse response = await matchesClient.CreateMatchAsync(request, cancellationToken: ct);
        return new EnqueueResult.Success(string.Empty, response.Match.Id);
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

        await queue.RemoveAsync(queueToken, userId, entry.TimeFormatId);
        return new DequeueResult.Success();
    }
}
