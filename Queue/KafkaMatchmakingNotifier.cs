using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Confluent.Kafka;
using Maichess.Events.V1;

namespace MaichessMatchMakerService.Queue;

// Publishes the `matched` push to socket.outbound.v1 (user-targeted) and pairing
// facts to matchmaking.events.v1, replacing the direct Socket.EmitEvent gRPC call.
// Both topics carry raw Protobuf bytes (Kafka task 09 removed the Schema Registry).
[ExcludeFromCodeCoverage]
internal sealed class KafkaMatchmakingNotifier : IMatchmakingNotifier, IDisposable
{
    private const string SocketTopic = "socket.outbound.v1";
    private const string EventsTopic = "matchmaking.events.v1";
    private const string ProducerName = "match-maker-service";

    private readonly IProducer<string, OutboundEvent> socketProducer;
    private readonly IProducer<string, MatchmakingEvent> eventsProducer;
    private readonly ILogger<KafkaMatchmakingNotifier> logger;

    public KafkaMatchmakingNotifier(ILogger<KafkaMatchmakingNotifier> logger)
    {
        this.logger = logger;

        string bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "kafka:9092";

        var producerConfig = new ProducerConfig { BootstrapServers = bootstrap };
        socketProducer = new ProducerBuilder<string, OutboundEvent>(producerConfig)
            .SetValueSerializer(ProtobufEventSerdes.Serializer<OutboundEvent>())
            .Build();
        eventsProducer = new ProducerBuilder<string, MatchmakingEvent>(producerConfig)
            .SetValueSerializer(ProtobufEventSerdes.Serializer<MatchmakingEvent>())
            .Build();
    }

    public void NotifyMatched(string userId, string matchId)
    {
        string payloadJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["match_id"] = matchId });

        OutboundEvent envelope = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "socket.matched",
            AggregateId = userId,
            Sequence = 0L,
            OccurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Producer = ProducerName,
            Push = new SocketPush
            {
                TargetUserId = userId,
                EventName = "matched",
                PayloadJson = payloadJson,
            },
        };

        Message<string, OutboundEvent> message = new() { Key = userId, Value = envelope };
        _ = Task.Run(() => ProduceSocket(message));
    }

    public void PlayersMatched(string whiteUserId, string blackUserId, string timeFormatId)
    {
        MatchmakingEvent envelope = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "matchmaking.PlayersMatched",
            AggregateId = whiteUserId,
            Sequence = 0L,
            OccurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Producer = ProducerName,
            PlayersMatched = new PlayersMatched
            {
                WhiteUserId = whiteUserId,
                BlackUserId = blackUserId,
                TimeFormatId = timeFormatId,
            },
        };

        Message<string, MatchmakingEvent> message = new() { Key = whiteUserId, Value = envelope };
        _ = Task.Run(() => ProduceEvents(message));
    }

    public void Dispose()
    {
        socketProducer.Flush(TimeSpan.FromSeconds(5));
        eventsProducer.Flush(TimeSpan.FromSeconds(5));
        socketProducer.Dispose();
        eventsProducer.Dispose();
    }

#pragma warning disable CA1031 // Fire-and-forget background publish: log and swallow all failures.
    private async Task ProduceSocket(Message<string, OutboundEvent> message)
    {
        try
        {
            await socketProducer.ProduceAsync(SocketTopic, message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish matched to {Topic}", SocketTopic);
        }
    }

    private async Task ProduceEvents(Message<string, MatchmakingEvent> message)
    {
        try
        {
            await eventsProducer.ProduceAsync(EventsTopic, message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish PlayersMatched to {Topic}", EventsTopic);
        }
    }
#pragma warning restore CA1031
}
