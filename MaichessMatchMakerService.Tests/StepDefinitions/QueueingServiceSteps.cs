using Grpc.Core;
using Maichess.MatchManager.V1;
using MaichessMatchMakerService.Queue;
using MaichessMatchMakerService.Tests.Support;
using NSubstitute;
using Reqnroll;
using Xunit;
using MatchManagerTimeControl = Maichess.MatchManager.V1.TimeControl;

namespace MaichessMatchMakerService.Tests.StepDefinitions;

[Binding]
[Scope(Feature = "QueueingService — queue entry, status, and dequeue logic")]
internal sealed class QueueingServiceSteps(QueueingServiceContext context)
{
    // ── Given ────────────────────────────────────────────────────────────────

    [Given(@"the user ""([^""]*)"" is not in any queue")]
    public void GivenUserNotInQueue(string userId)
    {
        context.SetupUserNotInQueue(userId);
    }

    [Given(@"the user ""([^""]*)"" is already in a queue")]
    public void GivenUserAlreadyInQueue(string userId)
    {
        context.SetupUserAlreadyInQueue(userId);
    }

    [Given(@"the match manager creates match ""([^""]*)""")]
    public void GivenMatchManagerCreatesMatch(string matchId)
    {
        context.SetupMatchManagerSuccess(matchId);
    }

    [Given(@"the queue entry ""([^""]*)"" does not exist")]
    public void GivenQueueEntryDoesNotExist(string queueToken)
    {
        context.SetupEntryMissing(queueToken);
    }

    [Given(@"the queue entry ""([^""]*)"" belongs to user ""([^""]*)"" and is waiting")]
    public void GivenQueueEntryWaiting(string queueToken, string userId)
    {
        context.SetupEntry(queueToken, userId, QueueStatus.Waiting);
    }

    [Given(@"the queue entry ""([^""]*)"" belongs to user ""([^""]*)"" and is matched with match ""([^""]*)""")]
    public void GivenQueueEntryMatched(string queueToken, string userId, string matchId)
    {
        context.SetupEntry(queueToken, userId, QueueStatus.Matched, matchId);
    }

    // ── When ─────────────────────────────────────────────────────────────────

    [When(@"enqueue is called with userId ""([^""]*)"" timeControl ""([^""]*)"" opponentType ""([^""]*)"" and botId ""([^""]*)""")]
    public async Task WhenEnqueueIsCalled(string userId, string timeControl, string opponentType, string botId)
    {
        string? nullableBotId = string.IsNullOrEmpty(botId) ? null : botId;
        context.EnqueueResult = await context.Service.EnqueueAsync(
            userId, timeControl, opponentType, nullableBotId, context.CancellationSource.Token);
    }

    [When(@"get status is called for token ""([^""]*)"" by user ""([^""]*)""")]
    public async Task WhenGetStatusIsCalled(string queueToken, string userId)
    {
        context.GetStatusResult = await context.Service.GetStatusAsync(queueToken, userId);
    }

    [When(@"dequeue is called for token ""([^""]*)"" by user ""([^""]*)""")]
    public async Task WhenDequeueIsCalled(string queueToken, string userId)
    {
        context.DequeueResult = await context.Service.DequeueAsync(queueToken, userId);
    }

    // ── Then ─────────────────────────────────────────────────────────────────

    [Then(@"the enqueue result is invalid input ""([^""]*)""")]
    public void ThenEnqueueResultIsInvalidInput(string message)
    {
        var result = Assert.IsType<EnqueueResult.InvalidInput>(context.EnqueueResult);
        Assert.Equal(message, result.Message);
    }

    [Then(@"the enqueue result is already queued")]
    public void ThenEnqueueResultIsAlreadyQueued()
    {
        Assert.IsType<EnqueueResult.AlreadyQueued>(context.EnqueueResult);
    }

    [Then(@"the enqueue result is success with a queue token")]
    public void ThenEnqueueResultIsSuccess()
    {
        var result = Assert.IsType<EnqueueResult.Success>(context.EnqueueResult);
        Assert.NotEmpty(result.QueueToken);
    }

    [Then(@"EnqueueAsync is called for user ""([^""]*)"" with time control ""([^""]*)""")]
    public async Task ThenEnqueueAsyncIsCalled(string userId, string timeControl)
    {
        await context.Queue.Received(1).EnqueueAsync(
            Arg.Any<string>(), userId, timeControl);
    }

    [Then(@"the CreateMatch gRPC request has white userId ""([^""]*)"" and black botId ""([^""]*)""")]
    public void ThenGrpcRequestHasCorrectPlayers(string userId, string botId)
    {
        Assert.NotNull(context.LastCreateMatchRequest);
        Assert.Equal(userId, context.LastCreateMatchRequest.White.UserId);
        Assert.Equal(botId, context.LastCreateMatchRequest.Black.BotId);
    }

    [Then(@"EnqueueBotMatchAsync is called for user ""([^""]*)"" with time control ""([^""]*)"" and match ""([^""]*)""")]
    public async Task ThenEnqueueBotMatchAsyncIsCalled(string userId, string timeControl, string matchId)
    {
        await context.Queue.Received(1).EnqueueBotMatchAsync(
            Arg.Any<string>(), userId, timeControl, matchId);
    }

    [Then(@"the CreateMatch gRPC request uses time control (.+)")]
    public void ThenGrpcRequestUsesTimeControl(string expectedEnum)
    {
        MatchManagerTimeControl expected = Enum.Parse<MatchManagerTimeControl>(expectedEnum);
        Assert.NotNull(context.LastCreateMatchRequest);
        Assert.Equal(expected, context.LastCreateMatchRequest.TimeControl);
    }

    [Then(@"the get status result is not found")]
    public void ThenGetStatusResultIsNotFound()
    {
        Assert.IsType<GetStatusResult.NotFound>(context.GetStatusResult);
    }

    [Then(@"the get status result is found with status ""([^""]*)"" and no match id")]
    public void ThenGetStatusResultIsFoundWithNoMatchId(string status)
    {
        var result = Assert.IsType<GetStatusResult.Found>(context.GetStatusResult);
        Assert.Equal(status, result.Status);
        Assert.Null(result.MatchId);
    }

    [Then(@"the get status result is found with status ""([^""]*)"" and match id ""([^""]*)""")]
    public void ThenGetStatusResultIsFoundWithMatchId(string status, string matchId)
    {
        var result = Assert.IsType<GetStatusResult.Found>(context.GetStatusResult);
        Assert.Equal(status, result.Status);
        Assert.Equal(matchId, result.MatchId);
    }

    [Then(@"the dequeue result is not found")]
    public void ThenDequeueResultIsNotFound()
    {
        Assert.IsType<DequeueResult.NotFound>(context.DequeueResult);
    }

    [Then(@"the dequeue result is success")]
    public void ThenDequeueResultIsSuccess()
    {
        Assert.IsType<DequeueResult.Success>(context.DequeueResult);
    }

    [Then(@"RemoveAsync is called for token ""([^""]*)"" user ""([^""]*)""")]
    public async Task ThenRemoveAsyncIsCalled(string queueToken, string userId)
    {
        await context.Queue.Received(1).RemoveAsync(queueToken, userId, Arg.Any<string>());
    }
}
