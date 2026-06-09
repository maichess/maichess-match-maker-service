using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Maichess.MatchManager.V1;

namespace MaichessMatchMakerService.Queue;

// Mints a matchId and publishes a CreateMatchCommand to match.commands.v1, replacing the
// synchronous Matches.CreateMatch gRPC call for human matches. Match Manager's command consumer
// materialises the match document with this caller-minted id. Fire-and-forget: the minted id is
// returned immediately so the caller (queue token / matched push) does not block on the broker.
[ExcludeFromCodeCoverage]
internal sealed class KafkaMatchCreator : IMatchCreator, IDisposable
{
    private const string Topic = "match.commands.v1";
    private const string ProducerName = "match-maker-service";

    private readonly IProducer<string, GenericRecord> producer;
    private readonly CachedSchemaRegistryClient registry;
    private readonly RecordSchema envelopeSchema;
    private readonly RecordSchema commandSchema;
    private readonly RecordSchema playerSchema;
    private readonly RecordSchema timeFormatSchema;
    private readonly EnumSchema sourceSchema;
    private readonly ILogger<KafkaMatchCreator> logger;

    public KafkaMatchCreator(ILogger<KafkaMatchCreator> logger)
    {
        this.logger = logger;

        string bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "kafka:9092";
        string registryUrl = Environment.GetEnvironmentVariable("SCHEMA_REGISTRY_URL")
            ?? "http://schema-registry:8081";

        envelopeSchema = (RecordSchema)Avro.Schema.Parse(LoadSchema("match.commands.v1.avsc"));
        var union = (UnionSchema)envelopeSchema.Fields.Single(f => f.Name == "payload").Schema;
        commandSchema = (RecordSchema)union.Schemas.Single(s => s.Name == "CreateMatchCommand");
        playerSchema = (RecordSchema)commandSchema.Fields.Single(f => f.Name == "white").Schema;
        timeFormatSchema = (RecordSchema)commandSchema.Fields.Single(f => f.Name == "time_format").Schema;
        sourceSchema = (EnumSchema)commandSchema.Fields.Single(f => f.Name == "source").Schema;

        registry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = registryUrl });
        producer = new ProducerBuilder<string, GenericRecord>(
                new ProducerConfig { BootstrapServers = bootstrap })
            .SetValueSerializer(new AvroSerializer<GenericRecord>(registry))
            .Build();
    }

    public Task<string> CreateMatchAsync(
        CommandPlayer white, CommandPlayer black, string timeFormatId, CancellationToken ct)
    {
        string matchId = Guid.NewGuid().ToString();

        TimeFormat tf = TimeFormatRegistry.Resolve(timeFormatId);
        GenericRecord timeFormat = new(timeFormatSchema);
        timeFormat.Add("id", tf.Id);
        timeFormat.Add("base_ms", tf.BaseMs);
        timeFormat.Add("increment_ms", tf.IncrementMs);
        timeFormat.Add("category", tf.Category);

        GenericRecord command = new(commandSchema);
        command.Add("white", ToPlayer(white));
        command.Add("black", ToPlayer(black));
        command.Add("time_format", timeFormat);
        command.Add("created_by", null);
        command.Add("start_fen", string.Empty);
        command.Add("source", new GenericEnum(sourceSchema, "NATIVE"));
        command.Add("external_provider", string.Empty);
        command.Add("external_ref", string.Empty);

        GenericRecord envelope = new(envelopeSchema);
        envelope.Add("event_id", Guid.NewGuid().ToString());
        envelope.Add("event_type", "match.CreateMatch");
        envelope.Add("aggregate_id", matchId);
        envelope.Add("sequence", 0L);
        envelope.Add("occurred_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        envelope.Add("correlation_id", string.Empty);
        envelope.Add("causation_id", string.Empty);
        envelope.Add("producer", ProducerName);
        envelope.Add("payload", command);

        Message<string, GenericRecord> message = new() { Key = matchId, Value = envelope };

        // Detached from the request token on purpose: the publish must outlive the caller, which
        // has already been handed the minted id.
        _ = Task.Run(() => ProduceAsync(message), CancellationToken.None);
        return Task.FromResult(matchId);
    }

    public void Dispose()
    {
        producer.Flush(TimeSpan.FromSeconds(5));
        producer.Dispose();
        registry.Dispose();
    }

    private static string LoadSchema(string suffix)
    {
        Assembly asm = typeof(KafkaMatchCreator).Assembly;
        string name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(suffix, StringComparison.Ordinal));
        using Stream stream = asm.GetManifestResourceStream(name)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private GenericRecord ToPlayer(CommandPlayer player)
    {
        GenericRecord record = new(playerSchema);
        record.Add("user_id", player.UserId);
        record.Add("bot_id", player.BotId);
        record.Add("external_name", null);
        return record;
    }

#pragma warning disable CA1031 // Fire-and-forget background publish: log and swallow all failures.
    private async Task ProduceAsync(Message<string, GenericRecord> message)
    {
        try
        {
            await producer.ProduceAsync(Topic, message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish CreateMatchCommand to {Topic}", Topic);
        }
    }
#pragma warning restore CA1031
}
