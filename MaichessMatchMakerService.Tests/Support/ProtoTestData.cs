using Confluent.Kafka;
using Google.Protobuf;
using Maichess.Events.V1;
using Streamiz.Kafka.Net.SerDes;

namespace MaichessMatchMakerService.Tests.Support;

// Registry-free Protobuf SerDes + MatchmakingEvent builders so the Streamiz topology can
// be driven under TopologyTestDriver without a Schema Registry. Encodes/decodes bare
// protobuf bytes (no Confluent framing); the production path uses MatchmakingEventSerDes.
internal sealed class ProtoTestSerDes<T> : ISerDes<T>
    where T : class, IMessage<T>, new()
{
    private static readonly MessageParser<T> Parser = new(() => new T());

    public byte[] Serialize(T data, SerializationContext context) =>
        data is null ? [] : data.ToByteArray();

    public T Deserialize(byte[] data, SerializationContext context) =>
        data is null || data.Length == 0 ? null! : Parser.ParseFrom(data);

    public byte[] SerializeObject(object data, SerializationContext context) =>
        Serialize((T)data, context);

    public object DeserializeObject(byte[] data, SerializationContext context) =>
        Deserialize(data, context)!;

    public void Initialize(SerDesContext context)
    {
    }
}

internal static class ProtoTestData
{
    internal static MatchmakingEvent PlayerEnqueued(string playerId, string queueToken, string timeFormatId) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        EventType = "matchmaking.PlayerEnqueued",
        AggregateId = playerId,
        OccurredAt = 0,
        Producer = "test",
        PlayerEnqueued = new PlayerEnqueued
        {
            PlayerId = playerId,
            QueueToken = queueToken,
            TimeFormatId = timeFormatId,
        },
    };

    internal static MatchmakingEvent PlayerDequeued(string playerId, string queueToken) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        EventType = "matchmaking.PlayerDequeued",
        AggregateId = playerId,
        OccurredAt = 0,
        Producer = "test",
        PlayerDequeued = new PlayerDequeued { PlayerId = playerId, QueueToken = queueToken },
    };

    internal static UserEvent RatingUpdated(string userId, double rating, double rd = 200) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        EventType = "user.RatingUpdated",
        AggregateId = userId,
        OccurredAt = 0,
        Producer = "test",
        RatingUpdated = new RatingUpdated
        {
            UserId = userId,
            Rating = rating,
            RatingDeviation = rd,
            Volatility = 0.06,
            Elo = (int)rating,
        },
    };

    internal static UserEvent UserRegistered(string userId, string username) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        EventType = "user.UserRegistered",
        AggregateId = userId,
        OccurredAt = 0,
        Producer = "test",
        UserRegistered = new UserRegistered { UserId = userId, Username = username },
    };

    // A user.events envelope with no payload set (PayloadCase = None).
    internal static UserEvent NoPayloadUserEvent() => new()
    {
        EventId = "e",
        EventType = "user.UserRegistered",
        AggregateId = "u1",
        OccurredAt = 0,
        Producer = "test",
    };
}
