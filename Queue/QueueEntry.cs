namespace MaichessMatchMakerService.Queue;

internal enum QueueStatus
{
    Waiting,
    Matched,
}

internal sealed record QueueEntry(
    string QueueToken,
    string UserId,
    string TimeFormatId,
    QueueStatus Status,
    string? MatchId);
