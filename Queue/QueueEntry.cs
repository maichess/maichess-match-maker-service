namespace MaichessMatchMakerService.Queue;

internal enum QueueStatus
{
    Waiting,
    Matched,
}

// AllowFlagged is the per-search anti-cheat toggle: whether this player
// accepts being matched with previously-flagged players (default false).
// ColorPreference is the requested side (default Any — let the matcher decide).
internal sealed record QueueEntry(
    string QueueToken,
    string UserId,
    string TimeFormatId,
    QueueStatus Status,
    string? MatchId,
    bool AllowFlagged = false,
    ColorPreference ColorPreference = ColorPreference.Any);
