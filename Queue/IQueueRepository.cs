namespace MaichessMatchMakerService.Queue;

internal interface IQueueRepository
{
    Task EnqueueAsync(string queueToken, string userId, string timeControl);

    Task EnqueueBotMatchAsync(string queueToken, string userId, string timeControl, string matchId);

    Task<QueueEntry?> GetEntryAsync(string queueToken);

    Task RemoveAsync(string queueToken, string userId, string timeControl);

    Task MarkMatchedAsync(string queueToken, string userId, string matchId);

    Task<string?> GetUserQueueTokenAsync(string userId);

    Task<long> GetQueueCountAsync(string timeControl);

    Task<string[]> DequeueOldestPairAsync(string timeControl);
}
