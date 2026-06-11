using Maichess.Events.V1;

namespace MaichessMatchMakerService.Streaming;

// Pure reads over a matchmaking.events.v1 envelope and the value-joiner that tags a
// PlayerEnqueued event with the joined KTable rating. The topology's in-memory value
// type is the Protobuf MatchmakingEvent, read as raw Protobuf bytes (Kafka task 09).
internal static class EnqueueReader
{
    internal static bool IsPlayerEnqueued(MatchmakingEvent envelope) =>
        envelope.PayloadCase == MatchmakingEvent.PayloadOneofCase.PlayerEnqueued;

    // Only invoked on a PlayerEnqueued envelope (the topology filters first), so the
    // payload and its string fields are always present.
    internal static SkillEnrichedEnqueue Enrich(MatchmakingEvent enqueueEnvelope, UserRatingState rating)
    {
        PlayerEnqueued payload = enqueueEnvelope.PlayerEnqueued;
        return new SkillEnrichedEnqueue(
            payload.PlayerId,
            payload.QueueToken,
            payload.TimeFormatId,
            rating.Rating);
    }
}
