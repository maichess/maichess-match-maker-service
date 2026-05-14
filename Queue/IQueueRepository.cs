namespace MaichessMatchMakerService.Queue;

internal interface IQueueRepository
{
    Task EnqueueAsync(string queueToken, string userId, string timeFormatId);

    Task EnqueueBotMatchAsync(string queueToken, string userId, string timeFormatId, string matchId);

    Task<QueueEntry?> GetEntryAsync(string queueToken);

    Task RemoveAsync(string queueToken, string userId, string timeFormatId);

    Task MarkMatchedAsync(string queueToken, string userId, string matchId);

    Task<string?> GetUserQueueTokenAsync(string userId);

    Task<long> GetQueueCountAsync(string timeFormatId);

    Task<string[]> DequeueOldestPairAsync(string timeFormatId);
}
