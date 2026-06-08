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

    // Waiting (token, userId) pairs for skill-based pairing — read without dequeuing so
    // ratings can be compared before two are chosen.
    Task<IReadOnlyList<(string Token, string UserId)>> GetWaitingPlayersAsync(string timeFormatId);

    // Atomically removes exactly the two named tokens from the queue. Returns false if
    // either was already taken (a concurrent match), so the caller can fall back.
    Task<bool> DequeueSpecificPairAsync(string timeFormatId, string tokenA, string tokenB);
}
