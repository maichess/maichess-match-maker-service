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
        Service = new QueueingService(Queue, MatchesClient, SocketNotifier);
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
        var response = new CreateMatchResponse { Match = new Match { Id = matchId } };
        MatchesClient
            .CreateMatchAsync(
                Arg.Do<CreateMatchRequest>(r => LastCreateMatchRequest = r),
                Arg.Any<Metadata>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(GrpcHelper.GrpcCall(response));
    }
}
