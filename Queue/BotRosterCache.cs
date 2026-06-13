using Maichess.Engine.V1;
using Microsoft.Extensions.Caching.Memory;

namespace MaichessMatchMakerService.Queue;

// Caches the engine's bot roster behind a long TTL. The list is static between deploys
// (no runtime bot registration), so a hit lets the bot-list and bot-vs-bot validation
// paths skip the per-request ListBots gRPC roundtrip. Same cache key as match-manager's
// roster cache; a separate process with its own IMemoryCache. See
// caching-and-read-models.md (ListBots cache).
internal sealed class BotRosterCache(Bots.BotsClient bots, IMemoryCache cache)
{
    private const string CacheKey = "engine:bots";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    internal async Task<IReadOnlyList<Bot>> GetBotsAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<Bot>? cached) && cached is not null)
        {
            return cached;
        }

        ListBotsResponse response = await bots.ListBotsAsync(new ListBotsRequest(), cancellationToken: ct);
        IReadOnlyList<Bot> roster = [.. response.Bots];
        cache.Set(CacheKey, roster, Ttl);
        return roster;
    }
}
