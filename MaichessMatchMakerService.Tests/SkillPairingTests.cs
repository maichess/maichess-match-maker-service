using MaichessMatchMakerService.Queue;
using MaichessMatchMakerService.Tests.Support;
using NSubstitute;
using Xunit;

using WaitingPlayer = MaichessMatchMakerService.Queue.MatchingService.WaitingPlayer;
using RatedPlayer = MaichessMatchMakerService.Queue.MatchingService.RatedPlayer;

namespace MaichessMatchMakerService.Tests;

// Skill-based pairing: once a time-control pool is large enough, Match Maker pairs the
// closest live ratings read from the KTable store, falling back to FIFO otherwise. Both
// paths exclude anti-cheat-inadmissible pairs (the matchmaking toggle).
public sealed class SkillPairingTests
{
    private const string Tf = "5+0";

    private static WaitingPlayer Wp(string token, string userId, bool allowFlagged = false, bool flagged = false) =>
        new(token, userId, allowFlagged, flagged);

    [Fact]
    public void ClosestRatedPair_FewerThanTwo_ReturnsNull()
    {
        Assert.Null(MatchingService.ClosestRatedPair([]));
        Assert.Null(MatchingService.ClosestRatedPair([new RatedPlayer(Wp("t", "u"), 1000)]));
    }

    [Fact]
    public void ClosestRatedPair_PicksMinimumRatingGap()
    {
        RatedPlayer[] players =
        [
            new(Wp("a", "ua"), 1000),
            new(Wp("b", "ub"), 1500),
            new(Wp("c", "uc"), 1520),
        ];

        (string White, string Black)? pair = MatchingService.ClosestRatedPair(players);

        Assert.Equal(("b", "c"), pair);
    }

    [Fact]
    public void ClosestRatedPair_SkipsInadmissiblePairs()
    {
        // ub is flagged; the rating-closest neighbour uc disallows flagged players, so
        // the pair is skipped and the next-closest admissible pair wins.
        RatedPlayer[] players =
        [
            new(Wp("a", "ua"), 1000),
            new(Wp("b", "ub", flagged: true), 1500),
            new(Wp("c", "uc"), 1520),
        ];

        Assert.Equal(("a", "c"), MatchingService.ClosestRatedPair(players));
    }

    [Fact]
    public void ClosestRatedPair_PairsTwoFlaggedPlayersWhoBothAllowFlagged()
    {
        RatedPlayer[] players =
        [
            new(Wp("a", "ua", allowFlagged: true, flagged: true), 1000),
            new(Wp("b", "ub", allowFlagged: true, flagged: true), 1010),
        ];

        Assert.Equal(("a", "b"), MatchingService.ClosestRatedPair(players));
    }

    [Fact]
    public void OldestAdmissiblePair_ReturnsFirstAdmissiblePairInOrder()
    {
        WaitingPlayer[] players = [Wp("a", "ua", flagged: true), Wp("b", "ub"), Wp("c", "uc")];

        // a is flagged and b disallows flagged → first admissible pair is (b, c).
        Assert.Equal(("b", "c"), MatchingService.OldestAdmissiblePair(players));
    }

    [Fact]
    public void OldestAdmissiblePair_NoAdmissiblePair_ReturnsNull()
    {
        WaitingPlayer[] players = [Wp("a", "ua", flagged: true), Wp("b", "ub")];

        Assert.Null(MatchingService.OldestAdmissiblePair(players));
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
    }

    [Fact]
    public async Task TryMatch_LargePool_ExcludesFlaggedFromDisallowingOpponents()
    {
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 12);
        ctx.SetupWaitingPlayers(("tA", "uA"), ("tB", "uB"), ("tC", "uC"));
        ctx.SetupRating("uA", 1000);
        ctx.SetupRating("uB", 1490, flagged: true);   // flagged; others disallow → excluded
        ctx.SetupRating("uC", 1520);
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
        ctx.SetupDequeueSpecificPair("tA", "tB", success: true);
        ctx.SetupEntry("tA", "uA");
        ctx.SetupEntry("tB", "uB");
        ctx.SetupMatchManagerSuccess("m-fifo");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        await ctx.Queue.Received(1).DequeueSpecificPairAsync(Tf, "tA", "tB");
        Assert.Equal("uA", ctx.Creator.LastCall!.Value.White.UserId);
    }

    [Fact]
    public async Task TryMatch_LargePool_SpecificDequeueLost_FallsBackToFifoFreshRead()
    {
        // The skill pair (tA,tB) is closest but loses the race; the FIFO re-read shows
        // those gone, leaving (tC,tD) to pair.
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 10);
        ctx.SetupRating("uA", 1000);
        ctx.SetupRating("uB", 1010);
        ctx.SetupRating("uC", 2000);
        ctx.SetupRating("uD", 2010);
        ctx.SetupWaitingPlayersSequence(
            [("tA", "uA"), ("tB", "uB"), ("tC", "uC"), ("tD", "uD")],
            [("tC", "uC"), ("tD", "uD")]);
        ctx.SetupDequeueSpecificPair("tA", "tB", success: false);   // skill pair raced away
        ctx.SetupDequeueSpecificPair("tC", "tD", success: true);
        ctx.SetupEntry("tC", "uC");
        ctx.SetupEntry("tD", "uD");
        ctx.SetupMatchManagerSuccess("m-fifo");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        Assert.Equal("uC", ctx.Creator.LastCall!.Value.White.UserId);
        Assert.Equal("uD", ctx.Creator.LastCall.Value.Black.UserId);
    }

    [Fact]
    public async Task TryMatch_FlaggedSearcherAllowed_PairsWithDisallowingOpponentIsBlocked()
    {
        // Small pool (FIFO path): a flagged player and a default (disallow) opponent
        // cannot pair, so no match is created even though two players wait.
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 2);
        ctx.SetupWaitingPlayers(("tA", "uA"), ("tB", "uB"));
        ctx.SetupFlagged("uA");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        Assert.Null(ctx.Creator.LastCall);
        await ctx.Queue.DidNotReceive().DequeueSpecificPairAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task TryMatch_FlaggedPlayerPairsWithAllowingOpponent()
    {
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 2);
        ctx.SetupWaitingPlayersWithToggle(("tA", "uA", false), ("tB", "uB", true));
        ctx.SetupFlagged("uA");   // uA flagged; uB allows flagged → admissible
        ctx.SetupDequeueSpecificPair("tA", "tB", success: true);
        ctx.SetupEntry("tA", "uA");
        ctx.SetupEntry("tB", "uB");
        ctx.SetupMatchManagerSuccess("m-allowed");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        Assert.Equal("uA", ctx.Creator.LastCall!.Value.White.UserId);
        Assert.Equal("uB", ctx.Creator.LastCall.Value.Black.UserId);
    }
}
