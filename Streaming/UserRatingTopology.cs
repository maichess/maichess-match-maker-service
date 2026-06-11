using Maichess.Events.V1;
using Streamiz.Kafka.Net;
using Streamiz.Kafka.Net.Crosscutting;
using Streamiz.Kafka.Net.SerDes;
using Streamiz.Kafka.Net.State;
using Streamiz.Kafka.Net.Table;

namespace MaichessMatchMakerService.Streaming;

// The single Streamiz KTable in the platform (see caching-and-read-models.md). Builds a
// co-partitioned stream-table join:
//   * user.events.v1 (keyed by userId) is folded into a KTable of live ratings, backed
//     by the RocksDb store `user-ratings-store` (+ its changelog) — the read model that
//     skill-based pairing queries locally instead of calling GetUser.
//   * matchmaking.events.v1 (PlayerEnqueued, keyed by playerId) co-partitions with it,
//     so an inner KStream-KTable join tags each enqueue with the player's live rating.
//     The inner join excludes enqueues for users the KTable does not yet know.
// matchmaking.events.v1 and user.events.v1 must share partition count for the join to
// co-partition (both 3 in the chart).
internal static class UserRatingTopology
{
    internal const string StoreName = "user-ratings-store";
    internal const string UserEventsTopic = "user.events.v1";
    internal const string MatchmakingEventsTopic = "matchmaking.events.v1";
    internal const string EnrichedTopic = "matchmaking.skill.v1";

    // Both topics carry raw Protobuf bytes (Kafka task 09 removed the Schema Registry),
    // so user.events folds UserEvent and matchmaking.events folds MatchmakingEvent via
    // ProtobufSerDes; see BuildDefault. The serdes are injectable so the join can run
    // under TopologyTestDriver.
    internal static void Build(
        StreamBuilder builder,
        ISerDes<UserEvent> userEventSerdes,
        ISerDes<MatchmakingEvent> matchmakingSerdes,
        bool inMemoryStore = false)
    {
        Materialized<string, UserRatingState, IKeyValueStore<Bytes, byte[]>> store =
            (inMemoryStore
                ? InMemory.As<string, UserRatingState>(StoreName)
                : RocksDb.As<string, UserRatingState>(StoreName))
            .WithKeySerdes(new StringSerDes())
            .WithValueSerdes(new PocoJsonSerDes<UserRatingState>());

        IKTable<string, UserRatingState> userRatings = builder
            .Stream(UserEventsTopic, new StringSerDes(), userEventSerdes)
            .GroupByKey()
            .Aggregate(
                () => UserRatingState.Empty,
                (_, evt, agg) => UserEventReader.Apply(agg, evt),
                store);

        builder
            .Stream(MatchmakingEventsTopic, new StringSerDes(), matchmakingSerdes)
            .Filter((_, evt, _) => EnqueueReader.IsPlayerEnqueued(evt))
            .Join<UserRatingState, SkillEnrichedEnqueue>(userRatings, EnqueueReader.Enrich)
            .To(EnrichedTopic, new StringSerDes(), new PocoJsonSerDes<SkillEnrichedEnqueue>());
    }

    internal static void BuildDefault(StreamBuilder builder) =>
        Build(builder, new ProtobufSerDes<UserEvent>(), new ProtobufSerDes<MatchmakingEvent>());
}
