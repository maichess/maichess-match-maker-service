using Grpc.Core;
using Maichess.MatchManager.V1;
using MaichessMatchMakerService.Queue;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

using SocketSvc = Socket.V1.Socket;

namespace MaichessMatchMakerService.Tests.Support;

internal sealed class MatchingServiceContext
{
    internal IQueueRepository Queue { get; } = Substitute.For<IQueueRepository>();

    internal Matches.MatchesClient MatchesClient { get; } = Substitute.For<Matches.MatchesClient>();

    internal SocketNotifier SocketNotifier { get; } =
        new SocketNotifier(Substitute.For<SocketSvc.SocketClient>(), NullLogger<SocketNotifier>.Instance);

    internal FakeLogger<MatchingService> Logger { get; } = new FakeLogger<MatchingService>();

    internal MatchingService MatchingService { get; }

    internal string? CurrentTimeFormatId { get; set; }

    internal CreateMatchRequest? LastCreateMatchRequest { get; set; }

    internal Exception? LastException { get; set; }

    internal CancellationTokenSource CancellationSource { get; } = new CancellationTokenSource();

    internal MatchingServiceContext()
    {
        MatchingService = new MatchingService(Queue, MatchesClient, SocketNotifier, Logger);

        Queue.DequeueOldestPairAsync(Arg.Any<string>())
            .Returns(Task.FromResult(Array.Empty<string>()));
    }

    internal void SetupQueueCount(string timeFormatId, long count)
    {
        CurrentTimeFormatId = timeFormatId;
        Queue.GetQueueCountAsync(timeFormatId).Returns(Task.FromResult(count));
    }

    internal void SetupDequeueTokens(string token1, string token2)
    {
        Queue.DequeueOldestPairAsync(CurrentTimeFormatId!)
            .Returns(Task.FromResult(new[] { token1, token2 }));
    }

    internal void SetupDequeueEmpty()
    {
        Queue.DequeueOldestPairAsync(CurrentTimeFormatId!)
            .Returns(Task.FromResult(Array.Empty<string>()));
    }

    internal void SetupEntry(string token, string userId)
    {
        Queue.GetEntryAsync(token).Returns(Task.FromResult<QueueEntry?>(
            new QueueEntry(token, userId, CurrentTimeFormatId ?? "5+0", QueueStatus.Waiting, null)));
    }

    internal void SetupEntryMissing(string token)
    {
        Queue.GetEntryAsync(token).Returns(Task.FromResult<QueueEntry?>(null));
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

    internal void SetupMatchManagerThrows()
    {
        MatchesClient
            .CreateMatchAsync(
                Arg.Any<CreateMatchRequest>(),
                Arg.Any<Metadata>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(GrpcHelper.GrpcCallFailed<CreateMatchResponse>());
    }
}
