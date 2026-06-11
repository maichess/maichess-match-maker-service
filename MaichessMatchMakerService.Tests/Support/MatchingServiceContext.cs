using Grpc.Core;
using MaichessMatchMakerService.Queue;
using MaichessMatchMakerService.Streaming;
using NSubstitute;

namespace MaichessMatchMakerService.Tests.Support;

internal sealed class MatchingServiceContext
{
    internal IQueueRepository Queue { get; } = Substitute.For<IQueueRepository>();

    internal FakeMatchCreator Creator { get; } = new FakeMatchCreator();

    internal IUserRatingStore RatingStore { get; } = Substitute.For<IUserRatingStore>();

    internal IMatchmakingNotifier Notifier { get; } = Substitute.For<IMatchmakingNotifier>();

    internal CheatFlagStore CheatFlags { get; } = new CheatFlagStore();

    internal FakeLogger<MatchingService> Logger { get; } = new FakeLogger<MatchingService>();

    internal MatchingService MatchingService { get; }

    internal string? CurrentTimeFormatId { get; set; }

    internal Exception? LastException { get; set; }

    internal CancellationTokenSource CancellationSource { get; } = new CancellationTokenSource();

    internal MatchingServiceContext()
    {
        MatchingService = new MatchingService(Queue, Creator, Notifier, RatingStore, CheatFlags, Logger);

        Queue.GetWaitingPlayersAsync(Arg.Any<string>())
            .Returns(Task.FromResult<IReadOnlyList<(string Token, string UserId, bool AllowFlagged)>>([]));
    }

    // Configures the live rating the KTable store reports for a user (skill pairing path).
    // flagged routes to the local cheat-flag store (its own read model), not the rating.
    internal void SetupRating(string userId, double rating, bool flagged = false, bool hasRating = true)
    {
        RatingStore.TryGet(userId).Returns(new UserRatingState(rating, 0, hasRating));
        if (flagged)
        {
            CheatFlags.Apply(new CheatFlagUpdate(userId, true));
        }
    }

    // Marks a user anti-cheat-flagged in the local store.
    internal void SetupFlagged(string userId, bool flagged = true)
    {
        CheatFlags.Apply(new CheatFlagUpdate(userId, flagged));
    }

    // The waiting players (oldest-first) a pairing pass reads. AllowFlagged defaults
    // to false (the disallow default); use SetupWaitingPlayersWithToggle to vary it.
    internal void SetupWaitingPlayers(params (string Token, string UserId)[] players)
    {
        SetupWaitingPlayersWithToggle([.. players.Select(p => (p.Token, p.UserId, false))]);
    }

    // The waiting players with explicit per-player allow_flagged toggles.
    internal void SetupWaitingPlayersWithToggle(params (string Token, string UserId, bool AllowFlagged)[] players)
    {
        Queue.GetWaitingPlayersAsync(CurrentTimeFormatId!)
            .Returns(Task.FromResult<IReadOnlyList<(string Token, string UserId, bool AllowFlagged)>>(players));
    }

    // Distinct waiting lists for the skill read then the FIFO re-read (the two
    // GetWaitingPlayersAsync calls in one TryMatch when the skill pair races away).
    internal void SetupWaitingPlayersSequence(
        (string Token, string UserId)[] first,
        (string Token, string UserId)[] second)
    {
        Queue.GetWaitingPlayersAsync(CurrentTimeFormatId!).Returns(
            _ => Task.FromResult<IReadOnlyList<(string Token, string UserId, bool AllowFlagged)>>(
                [.. first.Select(p => (p.Token, p.UserId, false))]),
            _ => Task.FromResult<IReadOnlyList<(string Token, string UserId, bool AllowFlagged)>>(
                [.. second.Select(p => (p.Token, p.UserId, false))]));
    }

    internal void SetupDequeueSpecificPair(string tokenA, string tokenB, bool success)
    {
        Queue.DequeueSpecificPairAsync(CurrentTimeFormatId!, tokenA, tokenB)
            .Returns(Task.FromResult(success));
    }

    internal void SetupQueueCount(string timeFormatId, long count)
    {
        CurrentTimeFormatId = timeFormatId;
        Queue.GetQueueCountAsync(timeFormatId).Returns(Task.FromResult(count));
    }

    // The FIFO fallback now reads the waiting list and atomically removes the chosen
    // pair: seed two oldest waiting players and let their specific dequeue succeed.
    internal void SetupDequeueTokens(string token1, string token2)
    {
        SetupWaitingPlayers((token1, $"u-{token1}"), (token2, $"u-{token2}"));
        SetupDequeueSpecificPair(token1, token2, success: true);
    }

    internal void SetupDequeueEmpty()
    {
        SetupWaitingPlayers();
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
        Creator.ReturnsMatch(matchId);
    }

    internal void SetupMatchManagerThrows()
    {
        Creator.Throws(new RpcException(new Status(StatusCode.Internal, "upstream error")));
    }
}
