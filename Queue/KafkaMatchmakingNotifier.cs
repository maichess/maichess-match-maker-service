using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;

namespace MaichessMatchMakerService.Queue;

// Publishes the `matched` push to socket.outbound.v1 (user-targeted) and pairing
// facts to matchmaking.events.v1, replacing the direct Socket.EmitEvent gRPC call.
[ExcludeFromCodeCoverage]
internal sealed class KafkaMatchmakingNotifier : IMatchmakingNotifier, IDisposable
{
    private const string SocketTopic = "socket.outbound.v1";
    private const string EventsTopic = "matchmaking.events.v1";
    private const string ProducerName = "match-maker-service";

    private readonly IProducer<string, GenericRecord> producer;
    private readonly CachedSchemaRegistryClient registry;
    private readonly RecordSchema socketEnvelopeSchema;
    private readonly RecordSchema socketPushSchema;
    private readonly RecordSchema eventsEnvelopeSchema;
    private readonly RecordSchema playersMatchedSchema;
    private readonly ILogger<KafkaMatchmakingNotifier> logger;

    public KafkaMatchmakingNotifier(ILogger<KafkaMatchmakingNotifier> logger)
    {
        this.logger = logger;

        string bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "kafka:9092";
        string registryUrl = Environment.GetEnvironmentVariable("SCHEMA_REGISTRY_URL")
            ?? "http://schema-registry:8081";

        socketEnvelopeSchema = (RecordSchema)Avro.Schema.Parse(LoadSchema("socket.outbound.v1.avsc"));
        socketPushSchema = (RecordSchema)socketEnvelopeSchema.Fields.Single(f => f.Name == "payload").Schema;

        eventsEnvelopeSchema = (RecordSchema)Avro.Schema.Parse(LoadSchema("matchmaking.events.v1.avsc"));
        var eventsUnion = (UnionSchema)eventsEnvelopeSchema.Fields.Single(f => f.Name == "payload").Schema;
        playersMatchedSchema = (RecordSchema)eventsUnion.Schemas.Single(s => s.Name == "PlayersMatched");

        registry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = registryUrl });
        producer = new ProducerBuilder<string, GenericRecord>(
                new ProducerConfig { BootstrapServers = bootstrap })
            .SetValueSerializer(new AvroSerializer<GenericRecord>(registry))
            .Build();
    }

    public void NotifyMatched(string userId, string matchId)
    {
        string payloadJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["match_id"] = matchId });

        GenericRecord push = new(socketPushSchema);
        push.Add("target_user_id", userId);
        push.Add("target_match_id", null);
        push.Add("event_name", "matched");
        push.Add("payload_json", payloadJson);

        GenericRecord envelope = NewEnvelope(socketEnvelopeSchema, userId, "socket.matched");
        envelope.Add("payload", push);
        Publish(SocketTopic, userId, envelope, "matched");
    }

    public void PlayersMatched(string whiteUserId, string blackUserId, string timeFormatId)
    {
        GenericRecord paired = new(playersMatchedSchema);
        paired.Add("white_user_id", whiteUserId);
        paired.Add("black_user_id", blackUserId);
        paired.Add("time_format_id", timeFormatId);

        GenericRecord envelope = NewEnvelope(eventsEnvelopeSchema, whiteUserId, "matchmaking.PlayersMatched");
        envelope.Add("payload", paired);
        Publish(EventsTopic, whiteUserId, envelope, "PlayersMatched");
    }

    public void Dispose()
    {
        producer.Flush(TimeSpan.FromSeconds(5));
        producer.Dispose();
        registry.Dispose();
    }

    private static GenericRecord NewEnvelope(RecordSchema schema, string aggregateId, string eventType)
    {
        GenericRecord envelope = new(schema);
        envelope.Add("event_id", Guid.NewGuid().ToString());
        envelope.Add("event_type", eventType);
        envelope.Add("aggregate_id", aggregateId);
        envelope.Add("sequence", 0L);
        envelope.Add("occurred_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        envelope.Add("correlation_id", string.Empty);
        envelope.Add("causation_id", string.Empty);
        envelope.Add("producer", ProducerName);
        return envelope;
    }

    private static string LoadSchema(string suffix)
    {
        Assembly asm = typeof(KafkaMatchmakingNotifier).Assembly;
        string name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(suffix, StringComparison.Ordinal));
        using Stream stream = asm.GetManifestResourceStream(name)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private void Publish(string topic, string key, GenericRecord value, string label)
    {
        Message<string, GenericRecord> message = new() { Key = key, Value = value };
        _ = Task.Run(() => ProduceAsync(topic, message, label));
    }

#pragma warning disable CA1031 // Fire-and-forget background publish: log and swallow all failures.
    private async Task ProduceAsync(string topic, Message<string, GenericRecord> message, string label)
    {
        try
        {
            await producer.ProduceAsync(topic, message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish {Label} to {Topic}", label, topic);
        }
    }
#pragma warning restore CA1031
}
