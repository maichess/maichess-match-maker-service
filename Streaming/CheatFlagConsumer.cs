using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Maichess.Events.V1;
using MaichessMatchMakerService.Queue;

namespace MaichessMatchMakerService.Streaming;

// Plain Kafka consumer that materialises the compacted cheat.events.v1 topic
// into the in-memory CheatFlagStore. Reads from the beginning on every start
// (a fresh per-run group id, like a Streamiz state-store restore) so each pod
// warms to the full compacted flag state and then follows live updates.
// Protobuf on the wire (the topic is born post-Avro). Excluded from coverage:
// the pure CheatFlagProjection it delegates to is unit-tested; this class is
// the live-Kafka shell, mirroring match-manager's CheatFlagConsumer.
[ExcludeFromCodeCoverage]
internal sealed class CheatFlagConsumer(CheatFlagStore store, ILogger<CheatFlagConsumer> logger)
    : BackgroundService
{
    private const string Topic = "cheat.events.v1";

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

#pragma warning disable CA1031 // Resilient consumer loop: log and continue on per-message failures.
    private void ConsumeLoop(CancellationToken ct)
    {
        string bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "kafka:9092";
        using IConsumer<string, CheatEvent> consumer = new ConsumerBuilder<string, CheatEvent>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,

            // Per-run group: the store is in-memory, so every start replays the
            // compacted topic to rebuild it (no offsets worth resuming from).
            GroupId = $"match-maker-cheat-flags-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
        })
            .SetValueDeserializer(ProtobufEventSerdes.Deserializer<CheatEvent>())
            .Build();

        consumer.Subscribe(Topic);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, CheatEvent> result = consumer.Consume(ct);
                    if (result?.Message?.Value is { } envelope
                        && CheatFlagProjection.Project(envelope) is { } update)
                    {
                        store.Apply(update);
                    }
                }
                catch (ConsumeException ex)
                {
                    logger.LogWarning(ex, "Error consuming {Topic}", Topic);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error applying cheat event to the flag store");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            consumer.Close();
        }
    }
#pragma warning restore CA1031
}
