using Avro;
using Avro.Generic;
using Avro.IO;
using Confluent.Kafka;
using Streamiz.Kafka.Net.SerDes;

namespace MaichessMatchMakerService.Tests.Support;

// Registry-free Avro GenericRecord SerDes + record builders so the Streamiz topology can
// be driven under TopologyTestDriver without a Schema Registry. Encodes/decodes plain
// Avro binary against a fixed schema (the production path uses SchemaAvroSerDes).
internal sealed class AvroTestSerDes(RecordSchema schema) : ISerDes<GenericRecord>
{
    public byte[] Serialize(GenericRecord data, SerializationContext context)
    {
        if (data is null)
        {
            return [];
        }

        using var ms = new MemoryStream();
        var encoder = new BinaryEncoder(ms);
        new GenericDatumWriter<GenericRecord>(schema).Write(data, encoder);
        encoder.Flush();
        return ms.ToArray();
    }

    public GenericRecord Deserialize(byte[] data, SerializationContext context)
    {
        if (data is null || data.Length == 0)
        {
            return null!;
        }

        var decoder = new BinaryDecoder(new MemoryStream(data));
        return new GenericDatumReader<GenericRecord>(schema, schema).Read(null!, decoder);
    }

    public byte[] SerializeObject(object data, SerializationContext context) =>
        Serialize((GenericRecord)data, context);

    public object DeserializeObject(byte[] data, SerializationContext context) =>
        Deserialize(data, context);

    public void Initialize(SerDesContext context)
    {
    }
}

internal static class AvroTestData
{
    internal const string UserEventsAvsc = """
    {
      "type": "record", "name": "UserEvent", "namespace": "maichess.events.user",
      "fields": [
        { "name": "event_id", "type": "string" },
        { "name": "event_type", "type": "string" },
        { "name": "aggregate_id", "type": "string" },
        { "name": "sequence", "type": "long", "default": 0 },
        { "name": "occurred_at", "type": "long" },
        { "name": "producer", "type": "string", "default": "" },
        {
          "name": "payload",
          "type": [
            { "type": "record", "name": "UserRegistered",
              "fields": [ { "name": "user_id", "type": "string" }, { "name": "username", "type": "string" } ] },
            { "type": "record", "name": "RatingUpdated",
              "fields": [
                { "name": "user_id", "type": "string" },
                { "name": "rating", "type": "double" },
                { "name": "rating_deviation", "type": "double" },
                { "name": "volatility", "type": "double" },
                { "name": "elo", "type": "int" }
              ] }
          ]
        }
      ]
    }
    """;

    internal const string MatchmakingEventsAvsc = """
    {
      "type": "record", "name": "MatchmakingEvent", "namespace": "maichess.events.matchmaking",
      "fields": [
        { "name": "event_id", "type": "string" },
        { "name": "event_type", "type": "string" },
        { "name": "aggregate_id", "type": "string" },
        { "name": "sequence", "type": "long", "default": 0 },
        { "name": "occurred_at", "type": "long" },
        { "name": "producer", "type": "string", "default": "" },
        {
          "name": "payload",
          "type": [
            { "type": "record", "name": "PlayerEnqueued",
              "fields": [
                { "name": "player_id", "type": "string" },
                { "name": "queue_token", "type": "string" },
                { "name": "time_format_id", "type": "string" }
              ] },
            { "type": "record", "name": "PlayerDequeued",
              "fields": [ { "name": "player_id", "type": "string" }, { "name": "queue_token", "type": "string" } ] }
          ]
        }
      ]
    }
    """;

    internal static RecordSchema UserEvents { get; } = (RecordSchema)Schema.Parse(UserEventsAvsc);

    internal static RecordSchema Matchmaking { get; } = (RecordSchema)Schema.Parse(MatchmakingEventsAvsc);

    internal static GenericRecord RatingUpdated(string userId, double rating, double rd = 200)
    {
        GenericRecord payload = new(PayloadSchema(UserEvents, "RatingUpdated"));
        payload.Add("user_id", userId);
        payload.Add("rating", rating);
        payload.Add("rating_deviation", rd);
        payload.Add("volatility", 0.06);
        payload.Add("elo", (int)rating);
        return Envelope(UserEvents, userId, "user.RatingUpdated", payload);
    }

    internal static GenericRecord UserRegistered(string userId, string username)
    {
        GenericRecord payload = new(PayloadSchema(UserEvents, "UserRegistered"));
        payload.Add("user_id", userId);
        payload.Add("username", username);
        return Envelope(UserEvents, userId, "user.UserRegistered", payload);
    }

    internal static GenericRecord PlayerEnqueued(string playerId, string queueToken, string timeFormatId)
    {
        GenericRecord payload = new(PayloadSchema(Matchmaking, "PlayerEnqueued"));
        payload.Add("player_id", playerId);
        payload.Add("queue_token", queueToken);
        payload.Add("time_format_id", timeFormatId);
        return Envelope(Matchmaking, playerId, "matchmaking.PlayerEnqueued", payload);
    }

    internal static GenericRecord PlayerDequeued(string playerId, string queueToken)
    {
        GenericRecord payload = new(PayloadSchema(Matchmaking, "PlayerDequeued"));
        payload.Add("player_id", playerId);
        payload.Add("queue_token", queueToken);
        return Envelope(Matchmaking, playerId, "matchmaking.PlayerDequeued", payload);
    }

    internal static RecordSchema PayloadSchema(RecordSchema envelope, string name)
    {
        var union = (UnionSchema)envelope.Fields.Single(f => f.Name == "payload").Schema;
        return (RecordSchema)union.Schemas.Single(s => s.Name == name);
    }

    private static GenericRecord Envelope(RecordSchema schema, string aggregateId, string eventType, GenericRecord payload)
    {
        GenericRecord env = new(schema);
        env.Add("event_id", Guid.NewGuid().ToString());
        env.Add("event_type", eventType);
        env.Add("aggregate_id", aggregateId);
        env.Add("sequence", 0L);
        env.Add("occurred_at", 0L);
        env.Add("producer", "test");
        env.Add("payload", payload);
        return env;
    }
}
