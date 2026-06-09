using Grpc.Core;
using Maichess.MatchManager.V1;
using MaichessMatchMakerService.Queue;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

using SocketSvc = Socket.V1.Socket;

namespace MaichessMatchMakerService.Tests.Support;

internal sealed class QueueingServiceContext
{
    internal IQueueRepository Queue { get; } = Substitute.For<IQueueRepository>();

    // Human-vs-bot creation goes through the IMatchCreator seam; bot-vs-bot stays on the gRPC client.
    internal FakeMatchCreator Creator { get; } = new FakeMatchCreator();

    internal Matches.MatchesClient MatchesClient { get; } = Substitute.For<Matches.MatchesClient>();

    internal SocketNotifier SocketNotifier { get; } =
        new SocketNotifier(Substitute.For<SocketSvc.SocketClient>(), NullLogger<SocketNotifier>.Instance);

    internal QueueingService Service { get; }

    internal EnqueueResult? EnqueueResult { get; set; }

    internal GetStatusResult? GetStatusResult { get; set; }

    internal DequeueResult? DequeueResult { get; set; }

    internal CreateMatchRequest? LastCreateMatchRequest { get; set; }

    internal CancellationTokenSource CancellationSource { get; } = new CancellationTokenSource();

    internal QueueingServiceContext()
    {
        Service = new QueueingService(Queue, Creator, MatchesClient, SocketNotifier);
    }

    internal void SetupUserNotInQueue(string userId)
    {
        Queue.GetUserQueueTokenAsync(userId).Returns(Task.FromResult<string?>(null));
    }

    internal void SetupUserAlreadyInQueue(string userId)
    {
        Queue.GetUserQueueTokenAsync(userId).Returns(Task.FromResult<string?>("existing-token"));
    }

    internal void SetupEntry(string queueToken, string userId, QueueStatus status = QueueStatus.Waiting, string? matchId = null)
    {
        Queue.GetEntryAsync(queueToken).Returns(Task.FromResult<QueueEntry?>(
            new QueueEntry(queueToken, userId, "5+0", status, matchId)));
    }

    internal void SetupEntryMissing(string queueToken)
    {
        Queue.GetEntryAsync(queueToken).Returns(Task.FromResult<QueueEntry?>(null));
    }

    internal void SetupMatchManagerSuccess(string matchId)
    {
        // Human-vs-bot resolves through the creator; bot-vs-bot through the gRPC client. Configure
        // both so a single Given step serves whichever path the scenario exercises.
        Creator.ReturnsMatch(matchId);

        var response = new CreateMatchResponse { Match = new Match { Id = matchId } };
        MatchesClient
            .CreateMatchAsync(
                Arg.Do<CreateMatchRequest>(r => LastCreateMatchRequest = r),
                Arg.Any<Metadata>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(GrpcHelper.GrpcCall(response));
    }

    internal void SetupMatchManagerRejectsStartPosition()
    {
        MatchesClient
            .CreateMatchAsync(
                Arg.Do<CreateMatchRequest>(r => LastCreateMatchRequest = r),
                Arg.Any<Metadata>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new RpcException(new Status(StatusCode.InvalidArgument, "invalid start_fen")));
    }
}
