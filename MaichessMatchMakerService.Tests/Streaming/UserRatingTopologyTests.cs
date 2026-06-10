using Maichess.Events.V1;
using MaichessMatchMakerService.Streaming;
using MaichessMatchMakerService.Tests.Support;
using Streamiz.Kafka.Net;
using Streamiz.Kafka.Net.Mock;
using Streamiz.Kafka.Net.SerDes;
using Streamiz.Kafka.Net.State;
using Xunit;

namespace MaichessMatchMakerService.Tests.Streaming;

// The single Streamiz KTable + co-partitioned stream-table join, driven through
// Streamiz's TopologyTestDriver. Asserts the join enriches an enqueue with the KTable's
// live rating, materialises the rating store, and excludes enqueues for users the KTable
// does not know (inner-join semantics). See feature-prompts/11 + caching-and-read-models.md.
public sealed class UserRatingTopologyTests
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    private static TopologyTestDriver BuildDriver()
    {
        var config = new StreamConfig<StringSerDes, StringSerDes>
        {
            ApplicationId = "match-maker-user-ratings-test",
        };
        config.StateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var builder = new StreamBuilder();
        UserRatingTopology.Build(
            builder,
            new AvroTestSerDes(AvroTestData.UserEvents),
            new ProtoTestSerDes<MatchmakingEvent>(),
            inMemoryStore: true);

        return new TopologyTestDriver(builder.Build(), config);
    }

    [Fact]
    public void BuildDefault_BuildsTopologyWithRocksDbStore()
    {
        // Exercises the production wiring (RocksDb store + Schema Registry serdes); the
        // graph is built but not run, so no broker/registry is needed.
        var builder = new StreamBuilder();
        UserRatingTopology.BuildDefault(builder);

        Assert.NotNull(builder.Build());
    }

    [Fact]
    public void Join_EnrichesEnqueueWithKTableRating()
    {
        using TopologyTestDriver driver = BuildDriver();
        var users = driver.CreateInputTopic(
            UserRatingTopology.UserEventsTopic, new StringSerDes(), new AvroTestSerDes(AvroTestData.UserEvents));
        var enqueues = driver.CreateInputTopic(
            UserRatingTopology.MatchmakingEventsTopic, new StringSerDes(), new ProtoTestSerDes<MatchmakingEvent>());
        var enriched = driver.CreateOutputTopic(
            UserRatingTopology.EnrichedTopic, ReadTimeout, new StringSerDes(), new PocoJsonSerDes<SkillEnrichedEnqueue>());

        users.PipeInput("u1", AvroTestData.RatingUpdated("u1", 1500));
        enqueues.PipeInput("u1", ProtoTestData.PlayerEnqueued("u1", "tok-1", "5+0"));

        var record = enriched.ReadKeyValue();
        Assert.Equal("u1", record.Message.Key);
        Assert.Equal("tok-1", record.Message.Value.QueueToken);
        Assert.Equal(1500, record.Message.Value.Rating);
    }

    [Fact]
    public void KTable_MaterialisesLatestRatingPerUser()
    {
        using TopologyTestDriver driver = BuildDriver();
        var users = driver.CreateInputTopic(
            UserRatingTopology.UserEventsTopic, new StringSerDes(), new AvroTestSerDes(AvroTestData.UserEvents));

        // A profile-only snapshot then a rating: the aggregate must keep the rating, and a
        // later rating update must win.
        users.PipeInput("u1", AvroTestData.UserRegistered("u1", "alice"));
        users.PipeInput("u1", AvroTestData.RatingUpdated("u1", 1500));
        users.PipeInput("u1", AvroTestData.RatingUpdated("u1", 1612));

        IReadOnlyKeyValueStore<string, UserRatingState> store =
            driver.GetKeyValueStore<string, UserRatingState>(UserRatingTopology.StoreName);
        UserRatingState state = store.Get("u1");

        Assert.Equal(1612, state.Rating);
        Assert.True(state.HasRating);
    }

    [Fact]
    public void Join_ExcludesEnqueueForUnknownUser()
    {
        using TopologyTestDriver driver = BuildDriver();
        var enqueues = driver.CreateInputTopic(
            UserRatingTopology.MatchmakingEventsTopic, new StringSerDes(), new ProtoTestSerDes<MatchmakingEvent>());
        var enriched = driver.CreateOutputTopic(
            UserRatingTopology.EnrichedTopic, ReadTimeout, new StringSerDes(), new PocoJsonSerDes<SkillEnrichedEnqueue>());

        // No user.events for u2 → inner join drops the enqueue.
        enqueues.PipeInput("u2", ProtoTestData.PlayerEnqueued("u2", "tok-2", "5+0"));

        Assert.Empty(enriched.ReadKeyValueList());
    }

    [Fact]
    public void Join_IgnoresNonEnqueueMatchmakingEvents()
    {
        using TopologyTestDriver driver = BuildDriver();
        var users = driver.CreateInputTopic(
            UserRatingTopology.UserEventsTopic, new StringSerDes(), new AvroTestSerDes(AvroTestData.UserEvents));
        var enqueues = driver.CreateInputTopic(
            UserRatingTopology.MatchmakingEventsTopic, new StringSerDes(), new ProtoTestSerDes<MatchmakingEvent>());
        var enriched = driver.CreateOutputTopic(
            UserRatingTopology.EnrichedTopic, ReadTimeout, new StringSerDes(), new PocoJsonSerDes<SkillEnrichedEnqueue>());

        users.PipeInput("u1", AvroTestData.RatingUpdated("u1", 1500));
        enqueues.PipeInput("u1", ProtoTestData.PlayerDequeued("u1", "tok-1"));

        Assert.Empty(enriched.ReadKeyValueList());
    }
}
