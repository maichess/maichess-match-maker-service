namespace MaichessMatchMakerService.Queue;

internal enum QueueStatus
{
    Waiting,
    Matched,
}

internal sealed record QueueEntry(
    string QueueToken,
    string UserId,
    string TimeControl,
    QueueStatus Status,
    string? MatchId);
