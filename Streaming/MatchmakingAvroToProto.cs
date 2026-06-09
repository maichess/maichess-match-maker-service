using Avro.Generic;
using Maichess.Events.V1;

namespace MaichessMatchMakerService.Streaming;

// Pure projection of an Avro matchmaking.events.v1 envelope (GenericRecord) onto the
// Protobuf MatchmakingEvent — the Avro arm of the topology's dual-read (Kafka task 02).
// Keeps already-enqueued Avro messages readable after the producer cut over to proto,
// so the cutover stays reversible. An unrecognised/absent payload yields an envelope
// with PayloadCase None (the topology filters it out, same as a non-enqueue).
internal static class MatchmakingAvroToProto
{
    internal static MatchmakingEvent Map(GenericRecord envelope)
    {
        MatchmakingEvent evt = new()
        {
            EventId = Str(envelope, "event_id"),
            EventType = Str(envelope, "event_type"),
            AggregateId = Str(envelope, "aggregate_id"),
            Sequence = Lng(envelope, "sequence"),
            OccurredAt = Lng(envelope, "occurred_at"),
            CorrelationId = Str(envelope, "correlation_id"),
            CausationId = Str(envelope, "causation_id"),
            Producer = Str(envelope, "producer"),
        };

        if (AvroPayload.TryGet(envelope, out GenericRecord payload))
        {
            switch (payload.Schema.Name)
            {
                case "PlayerEnqueued":
                    evt.PlayerEnqueued = new PlayerEnqueued
                    {
                        PlayerId = Str(payload, "player_id"),
                        QueueToken = Str(payload, "queue_token"),
                        TimeFormatId = Str(payload, "time_format_id"),
                    };
                    break;
                case "PlayerDequeued":
                    evt.PlayerDequeued = new PlayerDequeued
                    {
                        PlayerId = Str(payload, "player_id"),
                        QueueToken = Str(payload, "queue_token"),
                    };
                    break;
                case "PlayersMatched":
                    evt.PlayersMatched = new PlayersMatched
                    {
                        WhiteUserId = Str(payload, "white_user_id"),
                        BlackUserId = Str(payload, "black_user_id"),
                        TimeFormatId = Str(payload, "time_format_id"),
                    };
                    break;
                default:
                    break;
            }
        }

        return evt;
    }

    private static string Str(GenericRecord record, string field) =>
        record.TryGetValue(field, out object? v) && v is string s ? s : string.Empty;

    private static long Lng(GenericRecord record, string field) =>
        record.TryGetValue(field, out object? v) && v is long l ? l : 0L;
}
