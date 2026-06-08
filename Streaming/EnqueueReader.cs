using Avro.Generic;

namespace MaichessMatchMakerService.Streaming;

// Pure reads over a matchmaking.events.v1 envelope and the value-joiner that tags a
// PlayerEnqueued event with the joined KTable rating.
internal static class EnqueueReader
{
    internal static bool IsPlayerEnqueued(GenericRecord envelope) =>
        AvroPayload.Name(envelope) == "PlayerEnqueued";

    // Only invoked on a schema-valid PlayerEnqueued (the topology filters first), so the
    // payload and its string fields are always present.
    internal static SkillEnrichedEnqueue Enrich(GenericRecord enqueueEnvelope, UserRatingState rating)
    {
        var payload = (GenericRecord)enqueueEnvelope["payload"];
        return new SkillEnrichedEnqueue(
            (string)payload["player_id"],
            (string)payload["queue_token"],
            (string)payload["time_format_id"],
            rating.Rating,
            rating.Flagged);
    }
}
