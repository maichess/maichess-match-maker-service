using System.Diagnostics.CodeAnalysis;
using Streamiz.Kafka.Net;
using Streamiz.Kafka.Net.SerDes;
using Streamiz.Kafka.Net.State;

namespace MaichessMatchMakerService.Streaming;

// Hosts the Streamiz topology: builds the user-ratings KTable + co-partitioned join,
// runs the KafkaStream, and serves skill-based pairing's rating lookups via interactive
// queries against the RocksDb store. Excluded from coverage — the topology wiring is
// unit-tested with TopologyTestDriver (UserRatingTopologyTests) and the read logic it
// feeds (UserEventReader/EnqueueReader) is pure; this class is the live-Kafka shell.
[ExcludeFromCodeCoverage]
internal sealed class MatchMakerStreams : BackgroundService, IUserRatingStore
{
    private readonly KafkaStream stream;
    private IReadOnlyKeyValueStore<string, UserRatingState>? store;

    public MatchMakerStreams(IConfiguration configuration)
    {
        string bootstrap = configuration["Kafka:BootstrapServers"]
            ?? Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "kafka:9092";
        string stateDir = configuration["Kafka:StateDir"]
            ?? Environment.GetEnvironmentVariable("STREAMIZ_STATE_DIR") ?? "/var/lib/match-maker/streamiz";

        var config = new StreamConfig<StringSerDes, StringSerDes>
        {
            ApplicationId = "match-maker-user-ratings",
            BootstrapServers = bootstrap,
            StateDir = stateDir,
        };

        var builder = new StreamBuilder();
        UserRatingTopology.BuildDefault(builder);
        stream = new KafkaStream(builder.Build(), config);
    }

    public UserRatingState? TryGet(string userId)
    {
        try
        {
            store ??= stream.Store(StoreQueryParameters.FromNameAndType(
                UserRatingTopology.StoreName,
                QueryableStoreTypes.KeyValueStore<string, UserRatingState>()));
            return store.Get(userId);
        }
#pragma warning disable CA1031 // A query failure (store not ready) degrades to FIFO pairing.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    public override void Dispose()
    {
        stream.Dispose();
        base.Dispose();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        stream.StartAsync(stoppingToken);
}
