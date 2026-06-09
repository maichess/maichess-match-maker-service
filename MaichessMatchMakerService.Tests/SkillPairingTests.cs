using MaichessMatchMakerService.Queue;
using MaichessMatchMakerService.Tests.Support;
using NSubstitute;
using Xunit;

namespace MaichessMatchMakerService.Tests;

// Skill-based pairing: once a time-control pool is large enough, Match Maker pairs the
// closest live ratings read from the KTable store, falling back to FIFO otherwise.
public sealed class SkillPairingTests
{
    private const string Tf = "5+0";

    [Fact]
    public void ClosestRatedPair_FewerThanTwo_ReturnsNull()
    {
        Assert.Null(MatchingService.ClosestRatedPair([]));
        Assert.Null(MatchingService.ClosestRatedPair([new MatchingService.RatedPlayer("t", 1000)]));
    }

    [Fact]
    public void ClosestRatedPair_PicksMinimumRatingGap()
    {
        MatchingService.RatedPlayer[] players =
        [
            new("a", 1000),
            new("b", 1500),
            new("c", 1520),
        ];

        (string White, string Black)? pair = MatchingService.ClosestRatedPair(players);

        Assert.Equal(("b", "c"), pair);
    }

    [Fact]
    public async Task TryMatch_LargePool_PairsClosestRatedAndCreatesMatch()
    {
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 10);
        ctx.SetupWaitingPlayers(("tA", "uA"), ("tB", "uB"), ("tC", "uC"));
        ctx.SetupRating("uA", 1000);
        ctx.SetupRating("uB", 1500);
        ctx.SetupRating("uC", 1520);
        ctx.SetupDequeueSpecificPair("tB", "tC", success: true);
        ctx.SetupEntry("tB", "uB");
        ctx.SetupEntry("tC", "uC");
        ctx.SetupMatchManagerSuccess("m-skill");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        Assert.NotNull(ctx.Creator.LastCall);
        Assert.Equal("uB", ctx.Creator.LastCall!.Value.White.UserId);
        Assert.Equal("uC", ctx.Creator.LastCall.Value.Black.UserId);
        await ctx.Queue.DidNotReceive().DequeueOldestPairAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task TryMatch_LargePool_ExcludesFlaggedAndUnratedPlayers()
    {
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 12);
        ctx.SetupWaitingPlayers(("tA", "uA"), ("tB", "uB"), ("tC", "uC"));
        ctx.SetupRating("uA", 1000);
        ctx.SetupRating("uB", 1490, flagged: true);   // flagged → excluded
        ctx.SetupRating("uC", 1520);                   // uB excluded, so closest is uA/uC
        ctx.SetupDequeueSpecificPair("tA", "tC", success: true);
        ctx.SetupEntry("tA", "uA");
        ctx.SetupEntry("tC", "uC");
        ctx.SetupMatchManagerSuccess("m-skill");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        Assert.Equal("uA", ctx.Creator.LastCall!.Value.White.UserId);
        Assert.Equal("uC", ctx.Creator.LastCall.Value.Black.UserId);
    }

    [Fact]
    public async Task TryMatch_LargePool_TooFewRated_FallsBackToFifo()
    {
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 11);
        ctx.SetupWaitingPlayers(("tA", "uA"), ("tB", "uB"));
        ctx.SetupRating("uA", 1000);   // only one rated → no skill pair
        ctx.SetupDequeueTokens("tX", "tY");
        ctx.SetupEntry("tX", "uX");
        ctx.SetupEntry("tY", "uY");
        ctx.SetupMatchManagerSuccess("m-fifo");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        await ctx.Queue.Received(1).DequeueOldestPairAsync(Tf);
        Assert.Equal("uX", ctx.Creator.LastCall!.Value.White.UserId);
    }

    [Fact]
    public async Task TryMatch_LargePool_SpecificDequeueLost_FallsBackToFifo()
    {
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 10);
        ctx.SetupWaitingPlayers(("tA", "uA"), ("tB", "uB"));
        ctx.SetupRating("uA", 1000);
        ctx.SetupRating("uB", 1010);
        ctx.SetupDequeueSpecificPair("tA", "tB", success: false);   // both raced away
        ctx.SetupDequeueTokens("tX", "tY");
        ctx.SetupEntry("tX", "uX");
        ctx.SetupEntry("tY", "uY");
        ctx.SetupMatchManagerSuccess("m-fifo");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        await ctx.Queue.Received(1).DequeueOldestPairAsync(Tf);
        Assert.Equal("uX", ctx.Creator.LastCall!.Value.White.UserId);
    }
}
