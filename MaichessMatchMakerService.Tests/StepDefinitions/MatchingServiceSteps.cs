using Grpc.Core;
using Maichess.MatchManager.V1;
using MaichessMatchMakerService.Tests.Support;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Reqnroll;
using Xunit;

namespace MaichessMatchMakerService.Tests.StepDefinitions;

[Binding]
[Scope(Feature = "MatchingService — background match-making logic")]
internal sealed class MatchingServiceSteps(MatchingServiceContext context)
{
    // ── Given ────────────────────────────────────────────────────────────────

    [Given(@"the ""([^""]*)"" queue has fewer than 2 players")]
    public void GivenQueueHasFewerThan2Players(string timeFormatId)
    {
        context.SetupQueueCount(timeFormatId, 1L);
    }

    [Given(@"the ""([^""]*)"" queue reports 2 or more players")]
    public void GivenQueueReports2OrMorePlayers(string timeFormatId)
    {
        context.SetupQueueCount(timeFormatId, 2L);
    }

    [Given(@"the dequeue returns tokens ""([^""]*)"" and ""([^""]*)""")]
    public void GivenDequeueReturnsTokens(string token1, string token2)
    {
        context.SetupDequeueTokens(token1, token2);
    }

    [Given(@"the dequeue returns 0 tokens")]
    public void GivenDequeueReturns0Tokens()
    {
        context.SetupDequeueEmpty();
    }

    [Given(@"the entry for ""([^""]*)"" belongs to user ""([^""]*)""")]
    public void GivenEntryBelongsToUser(string token, string userId)
    {
        context.SetupEntry(token, userId);
    }

    [Given(@"the entry for ""([^""]*)"" is missing")]
    public void GivenEntryIsMissing(string token)
    {
        context.SetupEntryMissing(token);
    }

    [Given(@"the match manager creates match ""([^""]*)""")]
    public void GivenMatchManagerCreatesMatch(string matchId)
    {
        context.SetupMatchManagerSuccess(matchId);
    }

    [Given(@"the match manager throws a gRPC exception")]
    public void GivenMatchManagerThrows()
    {
        context.SetupMatchManagerThrows();
    }

    [Given(@"the cancellation token is cancelled")]
    public void GivenCancellationTokenIsCancelled()
    {
        context.CancellationSource.Cancel();
    }

    // ── When ─────────────────────────────────────────────────────────────────

    [When(@"the matching service processes the ""([^""]*)"" queue")]
    public async Task WhenMatchingServiceProcessesQueue(string timeFormatId)
    {
        context.LastException = null;
        try
        {
            await context.MatchingService.TryMatchAsync(timeFormatId, context.CancellationSource.Token);
        }
        catch (Exception ex)
        {
            context.LastException = ex;
        }
    }

    // ── Then ─────────────────────────────────────────────────────────────────

    [Then(@"no CreateMatch gRPC call is made")]
    public void ThenNoCreateMatchCallMade()
    {
        context.MatchesClient.DidNotReceive().CreateMatchAsync(
            Arg.Any<CreateMatchRequest>(),
            Arg.Any<Metadata>(),
            Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>());
    }

    [Then(@"a CreateMatch gRPC call is made with white ""([^""]*)"" and black ""([^""]*)""")]
    public void ThenCreateMatchCallMadeWithPlayers(string white, string black)
    {
        Assert.NotNull(context.LastCreateMatchRequest);
        Assert.Equal(white, context.LastCreateMatchRequest.White.UserId);
        Assert.Equal(black, context.LastCreateMatchRequest.Black.UserId);
    }

    [Then(@"""([^""]*)"" is marked matched with user ""([^""]*)"" and match ""([^""]*)""")]
    public async Task ThenTokenIsMarkedMatched(string token, string userId, string matchId)
    {
        await context.Queue.Received(1).MarkMatchedAsync(token, userId, matchId);
    }

    [Then(@"no tokens are marked matched")]
    public async Task ThenNoTokensMarkedMatched()
    {
        await context.Queue.DidNotReceive().MarkMatchedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Then(@"a warning is logged")]
    public void ThenWarningIsLogged()
    {
        Assert.Contains(context.Logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Then(@"an error is logged")]
    public void ThenErrorIsLogged()
    {
        Assert.Contains(context.Logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Then(@"the exception propagates")]
    public void ThenExceptionPropagates()
    {
        Assert.NotNull(context.LastException);
        Assert.IsType<RpcException>(context.LastException);
    }

    [Then(@"the CreateMatch request uses time format id ""([^""]*)"" with base (\d+) and increment (\d+)")]
    public void ThenCreateMatchRequestUsesTimeFormat(string id, long baseMs, long incrementMs)
    {
        Assert.NotNull(context.LastCreateMatchRequest);
        TimeFormat tf = context.LastCreateMatchRequest.TimeFormat;
        Assert.Equal(id, tf.Id);
        Assert.Equal(baseMs, tf.BaseMs);
        Assert.Equal(incrementMs, tf.IncrementMs);
    }
}
