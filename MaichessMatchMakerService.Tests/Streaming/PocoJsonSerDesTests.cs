using Confluent.Kafka;
using MaichessMatchMakerService.Streaming;
using Xunit;

namespace MaichessMatchMakerService.Tests.Streaming;

// The internal JSON SerDes that backs the KTable store and the join output topic.
public sealed class PocoJsonSerDesTests
{
    private static readonly SerializationContext Ctx = new();

    private readonly PocoJsonSerDes<UserRatingState> serdes = new();

    [Fact]
    public void RoundTripsValue()
    {
        var value = new UserRatingState(1530.5, 88.0, true);

        byte[] bytes = serdes.Serialize(value, Ctx);
        UserRatingState back = serdes.Deserialize(bytes, Ctx);

        Assert.Equal(value, back);
    }

    [Fact]
    public void SerializeNull_ReturnsEmpty()
    {
        Assert.Empty(serdes.Serialize(null!, Ctx));
    }

    [Theory]
    [InlineData(0)]   // empty
    public void DeserializeEmpty_ReturnsDefault(int length)
    {
        Assert.Null(serdes.Deserialize(new byte[length], Ctx));
    }

    [Fact]
    public void DeserializeNull_ReturnsDefault()
    {
        Assert.Null(serdes.Deserialize(null!, Ctx));
    }

    [Fact]
    public void ObjectMethods_DelegateToTyped()
    {
        var value = new UserRatingState(1200, 60, true);

        byte[] bytes = serdes.SerializeObject(value, Ctx);

        Assert.Equal(value, serdes.DeserializeObject(bytes, Ctx));
    }
}
