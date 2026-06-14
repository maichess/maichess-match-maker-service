using MaichessMatchMakerService.Queue;
using MaichessMatchMakerService.Tests.Support;
using NSubstitute;
using Xunit;

namespace MaichessMatchMakerService.Tests;

// Color selection (task 21): the human queue resolves each player's side from their
// preference once paired, and the vs-bot path puts the human on the chosen side.
public sealed class ColorSelectionTests
{
    private const string Tf = "5+0";

    // ── Human queue (MatchingService) ──────────────────────────────────────────

    [Fact]
    public async Task TryMatch_OppositePreferences_AssignsEachTheirColour()
    {
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 2);
        ctx.SetupWaitingPlayers(("t1", "ua"), ("t2", "ub"));
        ctx.SetupDequeueSpecificPair("t1", "t2", success: true);
        ctx.SetupEntry("t1", "ua", ColorPreference.Black);
        ctx.SetupEntry("t2", "ub", ColorPreference.White);
        ctx.SetupMatchManagerSuccess("m1");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        // ua wanted Black, ub wanted White → ub is white.
        Assert.Equal("ub", ctx.Creator.LastCall!.Value.White.UserId);
        Assert.Equal("ua", ctx.Creator.LastCall.Value.Black.UserId);
        await ctx.Queue.Received(1).MarkMatchedAsync("t2", "ub", "m1");
        await ctx.Queue.Received(1).MarkMatchedAsync("t1", "ua", "m1");
    }

    [Fact]
    public async Task TryMatch_FixedVsAny_HonoursTheFixedSide()
    {
        var ctx = new MatchingServiceContext();
        ctx.SetupQueueCount(Tf, 2);
        ctx.SetupWaitingPlayers(("t1", "ua"), ("t2", "ub"));
        ctx.SetupDequeueSpecificPair("t1", "t2", success: true);
        ctx.SetupEntry("t1", "ua", ColorPreference.Any);
        ctx.SetupEntry("t2", "ub", ColorPreference.Black);
        ctx.SetupMatchManagerSuccess("m2");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        // ub wanted Black → ua takes White even though it is first.
        Assert.Equal("ua", ctx.Creator.LastCall!.Value.White.UserId);
        Assert.Equal("ub", ctx.Creator.LastCall.Value.Black.UserId);
    }

    [Theory]
    [InlineData(true, "ua", "ub")]
    [InlineData(false, "ub", "ua")]
    public async Task TryMatch_SameFixedColour_CoinFlipDecidesWhoConcedes(
        bool flipFirstWhite, string expectedWhite, string expectedBlack)
    {
        var ctx = new MatchingServiceContext();
        ctx.ColorRandom.Returns(flipFirstWhite);
        ctx.SetupQueueCount(Tf, 2);
        ctx.SetupWaitingPlayers(("t1", "ua"), ("t2", "ub"));
        ctx.SetupDequeueSpecificPair("t1", "t2", success: true);
        ctx.SetupEntry("t1", "ua", ColorPreference.White);
        ctx.SetupEntry("t2", "ub", ColorPreference.White);
        ctx.SetupMatchManagerSuccess("m3");

        await ctx.MatchingService.TryMatchAsync(Tf, ctx.CancellationSource.Token);

        Assert.Equal(expectedWhite, ctx.Creator.LastCall!.Value.White.UserId);
        Assert.Equal(expectedBlack, ctx.Creator.LastCall.Value.Black.UserId);
    }

    // ── Vs bot (QueueingService) ───────────────────────────────────────────────

    [Fact]
    public async Task Enqueue_BotMatch_HumanChoosesBlack_PutsBotOnWhite()
    {
        var ctx = new QueueingServiceContext();
        ctx.SetupUserNotInQueue("ua");
        ctx.SetupMatchManagerSuccess("m-bot");

        EnqueueResult result = await ctx.Service.EnqueueAsync(
            "ua", Tf, "bot", "bot-1", allowFlagged: false, colorPreference: "black", ctx.CancellationSource.Token);

        Assert.IsType<EnqueueResult.Success>(result);
        Assert.Equal("bot-1", ctx.Creator.LastCall!.Value.White.BotId);
        Assert.Equal("ua", ctx.Creator.LastCall.Value.Black.UserId);
    }

    [Fact]
    public async Task Enqueue_BotMatch_HumanChoosesWhite_PutsHumanOnWhite()
    {
        var ctx = new QueueingServiceContext();
        ctx.SetupUserNotInQueue("ua");
        ctx.SetupMatchManagerSuccess("m-bot");

        await ctx.Service.EnqueueAsync(
            "ua", Tf, "bot", "bot-1", allowFlagged: false, colorPreference: "white", ctx.CancellationSource.Token);

        Assert.Equal("ua", ctx.Creator.LastCall!.Value.White.UserId);
        Assert.Equal("bot-1", ctx.Creator.LastCall.Value.Black.BotId);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Enqueue_BotMatch_Random_FlipsCoinForHumanSide(bool flip, bool humanWhite)
    {
        var ctx = new QueueingServiceContext();
        ctx.ColorRandom.Returns(flip);
        ctx.SetupUserNotInQueue("ua");
        ctx.SetupMatchManagerSuccess("m-bot");

        await ctx.Service.EnqueueAsync(
            "ua", Tf, "bot", "bot-1", allowFlagged: false, colorPreference: "random", ctx.CancellationSource.Token);

        if (humanWhite)
        {
            Assert.Equal("ua", ctx.Creator.LastCall!.Value.White.UserId);
        }
        else
        {
            Assert.Equal("ua", ctx.Creator.LastCall!.Value.Black.UserId);
        }
    }

    [Fact]
    public async Task Enqueue_HumanMatch_ForwardsColorPreferenceToQueue()
    {
        var ctx = new QueueingServiceContext();
        ctx.SetupUserNotInQueue("ua");

        await ctx.Service.EnqueueAsync(
            "ua", Tf, "human", null, allowFlagged: false, colorPreference: "white", ctx.CancellationSource.Token);

        await ctx.Queue.Received(1).EnqueueAsync(Arg.Any<string>(), "ua", Tf, false, ColorPreference.White);
    }

    [Fact]
    public async Task Enqueue_InvalidColorPreference_ReturnsInvalidInput()
    {
        var ctx = new QueueingServiceContext();
        ctx.SetupUserNotInQueue("ua");

        EnqueueResult result = await ctx.Service.EnqueueAsync(
            "ua", Tf, "human", null, allowFlagged: false, colorPreference: "purple", ctx.CancellationSource.Token);

        var invalid = Assert.IsType<EnqueueResult.InvalidInput>(result);
        Assert.Equal("color_preference must be 'white', 'black', 'any', or 'random'", invalid.Message);
    }
}
