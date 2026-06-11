using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Maichess.Events.V1;
using GrpcTimeFormat = Maichess.MatchManager.V1.TimeFormat;

namespace MaichessMatchMakerService.Queue;

// Mints a matchId and publishes a CreateMatchCommand to match.commands.v1, replacing the
// synchronous Matches.CreateMatch gRPC call for human matches. Match Manager's command consumer
// materialises the match document with this caller-minted id. Fire-and-forget: the minted id is
// returned immediately so the caller (queue token / matched push) does not block on the broker.
//
// Serialized as raw Protobuf bytes (Kafka task 09 removed the Schema Registry).
[ExcludeFromCodeCoverage]
internal sealed class KafkaMatchCreator : IMatchCreator, IDisposable
{
    private const string Topic = "match.commands.v1";
    private const string ProducerName = "match-maker-service";

    private readonly IProducer<string, MatchCommand> producer;
    private readonly ILogger<KafkaMatchCreator> logger;

    public KafkaMatchCreator(ILogger<KafkaMatchCreator> logger)
    {
        this.logger = logger;

        string bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "kafka:9092";

        producer = new ProducerBuilder<string, MatchCommand>(
                new ProducerConfig { BootstrapServers = bootstrap })
            .SetValueSerializer(ProtobufEventSerdes.Serializer<MatchCommand>())
            .Build();
    }

    public Task<string> CreateMatchAsync(
        CommandPlayer white, CommandPlayer black, string timeFormatId, CancellationToken ct)
    {
        string matchId = Guid.NewGuid().ToString();
        GrpcTimeFormat tf = TimeFormatRegistry.Resolve(timeFormatId);

        MatchCommand command = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "match.CreateMatch",
            AggregateId = matchId,
            Sequence = 0L,
            OccurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Producer = ProducerName,
            CreateMatch = new CreateMatchCommand
            {
                White = ToPlayer(white),
                Black = ToPlayer(black),
                TimeFormat = new TimeFormat
                {
                    Id = tf.Id,
                    BaseMs = tf.BaseMs,
                    IncrementMs = tf.IncrementMs,
                    Category = tf.Category,
                },
                Source = MatchSource.Native,
            },
        };

        Message<string, MatchCommand> message = new() { Key = matchId, Value = command };

        // Detached from the request token on purpose: the publish must outlive the caller, which
        // has already been handed the minted id.
        _ = Task.Run(() => ProduceAsync(message), CancellationToken.None);
        return Task.FromResult(matchId);
    }

    public void Dispose()
    {
        producer.Flush(TimeSpan.FromSeconds(5));
        producer.Dispose();
    }

    private static Player ToPlayer(CommandPlayer player) =>
        !string.IsNullOrEmpty(player.UserId) ? new Player { UserId = player.UserId }
        : !string.IsNullOrEmpty(player.BotId) ? new Player { BotId = player.BotId }
        : new Player();

#pragma warning disable CA1031 // Fire-and-forget background publish: log and swallow all failures.
    private async Task ProduceAsync(Message<string, MatchCommand> message)
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
