using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;

namespace MaichessMatchMakerService.Queue;

[ExcludeFromCodeCoverage]
internal sealed class QueueRepository(IConnectionMultiplexer redis) : IQueueRepository
{
    private IDatabase Db => redis.GetDatabase();

    public async Task EnqueueAsync(string queueToken, string userId, string timeFormatId)
    {
        double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ITransaction tx = Db.CreateTransaction();
        _ = tx.HashSetAsync(
            EntryKey(queueToken),
            [
                new HashEntry("user_id", userId),
                new HashEntry("time_format_id", timeFormatId),
                new HashEntry("status", "waiting"),
            ]);
        _ = tx.SortedSetAddAsync(QueueKey(timeFormatId), queueToken, score);
        _ = tx.StringSetAsync(UserKey(userId), queueToken);
        await tx.ExecuteAsync();
    }

    // Bot matches are immediately resolved — no sorted-set entry needed.
    public async Task EnqueueBotMatchAsync(string queueToken, string userId, string timeFormatId, string matchId)
    {
        string key = EntryKey(queueToken);
        await Db.HashSetAsync(
            key,
            [
                new HashEntry("user_id", userId),
                new HashEntry("time_format_id", timeFormatId),
                new HashEntry("status", "matched"),
                new HashEntry("match_id", matchId),
            ]);
        await Db.KeyExpireAsync(key, TimeSpan.FromMinutes(10));
    }

    public async Task<QueueEntry?> GetEntryAsync(string queueToken)
    {
        HashEntry[] fields = await Db.HashGetAllAsync(EntryKey(queueToken));
        return fields.Length == 0 ? null : ParseEntry(queueToken, fields);
    }

    public async Task RemoveAsync(string queueToken, string userId, string timeFormatId)
    {
        // No-op if already matched — client may still need to read the match_id.
        RedisValue status = await Db.HashGetAsync(EntryKey(queueToken), "status");
        if (status == "matched")
        {
            return;
        }

        ITransaction tx = Db.CreateTransaction();
        _ = tx.KeyDeleteAsync(EntryKey(queueToken));
        _ = tx.SortedSetRemoveAsync(QueueKey(timeFormatId), queueToken);
        _ = tx.KeyDeleteAsync(UserKey(userId));
        await tx.ExecuteAsync();
    }

    public async Task MarkMatchedAsync(string queueToken, string userId, string matchId)
    {
        ITransaction tx = Db.CreateTransaction();
        _ = tx.HashSetAsync(
            EntryKey(queueToken),
            [
                new HashEntry("status", "matched"),
                new HashEntry("match_id", matchId),
            ]);
        _ = tx.KeyExpireAsync(EntryKey(queueToken), TimeSpan.FromMinutes(10));
        _ = tx.KeyDeleteAsync(UserKey(userId));
        await tx.ExecuteAsync();
    }

    public async Task<string?> GetUserQueueTokenAsync(string userId)
    {
        RedisValue value = await Db.StringGetAsync(UserKey(userId));
        return value.HasValue ? (string?)value : null;
    }

    public async Task<long> GetQueueCountAsync(string timeFormatId) =>
        await Db.SortedSetLengthAsync(QueueKey(timeFormatId));

    public async Task<string[]> DequeueOldestPairAsync(string timeFormatId)
    {
        SortedSetEntry[] entries = await Db.SortedSetPopAsync(QueueKey(timeFormatId), count: 2);
        return entries.Select(e => (string)e.Element!).ToArray();
    }

    private static string EntryKey(string queueToken) => $"queue_entry:{queueToken}";

    private static string QueueKey(string timeFormatId) => $"queue:{timeFormatId}";

    private static string UserKey(string userId) => $"queue_user:{userId}";

    private static QueueEntry ParseEntry(string queueToken, HashEntry[] fields)
    {
        Dictionary<string, string?> dict = fields.ToDictionary(
            f => (string)f.Name!,
            f => (string?)f.Value);

        return new QueueEntry(
            queueToken,
            dict.GetValueOrDefault("user_id") ?? string.Empty,
            dict.GetValueOrDefault("time_format_id") ?? string.Empty,
            dict.GetValueOrDefault("status") == "matched" ? QueueStatus.Matched : QueueStatus.Waiting,
            dict.GetValueOrDefault("match_id"));
    }
}
