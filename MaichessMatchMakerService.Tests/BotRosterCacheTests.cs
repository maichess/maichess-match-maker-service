using Grpc.Core;
using Maichess.Engine.V1;
using MaichessMatchMakerService.Queue;
using MaichessMatchMakerService.Tests.Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace MaichessMatchMakerService.Tests;

// The engine bot roster is static between deploys, so BotRosterCache caches it for
// 10 minutes under the "engine:bots" key, removing the per-request ListBots roundtrip.
public sealed class BotRosterCacheTests
{
    private const string CacheKey = "engine:bots";

    private readonly Bots.BotsClient engine = Substitute.For<Bots.BotsClient>();
    private readonly IMemoryCache cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
    private readonly BotRosterCache roster;

    public BotRosterCacheTests()
    {
        roster = new BotRosterCache(engine, cache);
        SetupEngine("bot-a", "bot-b");
    }

    private void SetupEngine(params string[] botIds)
    {
        ListBotsResponse response = new();
        response.Bots.AddRange(botIds.Select(id => new Bot { Id = id, Name = id, Elo = 1500 }));
        engine.ListBotsAsync(
                Arg.Any<ListBotsRequest>(),
                Arg.Any<Metadata>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => GrpcHelper.GrpcCall(response));
    }

    [Fact]
    public async Task FirstCall_QueriesEngineOnce_AndReturnsRoster()
    {
        IReadOnlyList<Bot> bots = await roster.GetBotsAsync(CancellationToken.None);

        Assert.Equal(["bot-a", "bot-b"], bots.Select(b => b.Id));
        _ = engine.Received(1).ListBotsAsync(
            Arg.Any<ListBotsRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondCall_ServesFromCache_WithoutQueryingEngineAgain()
    {
        await roster.GetBotsAsync(CancellationToken.None);
        await roster.GetBotsAsync(CancellationToken.None);

        _ = engine.Received(1).ListBotsAsync(
            Arg.Any<ListBotsRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterEntryExpires_RefetchesFromEngine()
    {
        await roster.GetBotsAsync(CancellationToken.None);

        // Evicting the entry stands in for the 10-minute TTL elapsing: the next call
        // sees a miss and re-queries the engine.
        cache.Remove(CacheKey);

        await roster.GetBotsAsync(CancellationToken.None);

        _ = engine.Received(2).ListBotsAsync(
            Arg.Any<ListBotsRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }
}
