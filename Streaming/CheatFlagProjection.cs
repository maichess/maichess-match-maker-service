using Maichess.Events.V1;

namespace MaichessMatchMakerService.Streaming;

// Pure transform: a cheat.events.v1 envelope -> the flag update it implies.
// Only the durable verdicts count: PlayerFlagged -> true, PlayerUnflagged ->
// false. LiveSuspicionRaised is an advisory in-game signal and MUST NOT set
// the persistent flag (anticheat contract); it and unknown payloads project to
// nothing. The consumer that drives this is the only impure part.
internal static class CheatFlagProjection
{
    internal static CheatFlagUpdate? Project(CheatEvent envelope) =>
        envelope.AggregateId.Length == 0
            ? null
            : envelope.PayloadCase switch
            {
                CheatEvent.PayloadOneofCase.PlayerFlagged => new CheatFlagUpdate(envelope.AggregateId, true),
                CheatEvent.PayloadOneofCase.PlayerUnflagged => new CheatFlagUpdate(envelope.AggregateId, false),
                _ => null,
            };
}
