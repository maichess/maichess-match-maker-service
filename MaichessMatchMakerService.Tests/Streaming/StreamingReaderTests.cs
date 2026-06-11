using MaichessMatchMakerService.Streaming;
using MaichessMatchMakerService.Tests.Support;
using Xunit;

namespace MaichessMatchMakerService.Tests.Streaming;

// Pure reads over the event envelopes feeding the KTable topology: the rating aggregator
// and the enqueue value-joiner, both over raw-Protobuf events (Kafka task 09).
public sealed class StreamingReaderTests
{
    [Fact]
    public void Apply_RatingUpdated_SetsRatingAndMarksPresent()
    {
        UserRatingState result = UserEventReader.Apply(UserRatingState.Empty, ProtoTestData.RatingUpdated("u1", 1480, rd: 95));

        Assert.Equal(1480, result.Rating);
        Assert.Equal(95, result.RatingDeviation);
        Assert.True(result.HasRating);
    }

    [Fact]
    public void Apply_NonRatingEvent_LeavesStateUntouched()
    {
        var current = new UserRatingState(1500, 80, true);

        UserRatingState result = UserEventReader.Apply(current, ProtoTestData.UserRegistered("u1", "alice"));

        Assert.Equal(current, result);
    }

    [Fact]
    public void Apply_EnvelopeWithoutPayload_LeavesStateUntouched()
    {
        var current = new UserRatingState(1500, 80, true);

        Assert.Equal(current, UserEventReader.Apply(current, ProtoTestData.NoPayloadUserEvent()));
    }

    [Fact]
    public void IsPlayerEnqueued_TrueForEnqueueFalseOtherwise()
    {
        Assert.True(EnqueueReader.IsPlayerEnqueued(ProtoTestData.PlayerEnqueued("u1", "tok", "5+0")));
        Assert.False(EnqueueReader.IsPlayerEnqueued(ProtoTestData.PlayerDequeued("u1", "tok")));
    }

    [Fact]
    public void Enrich_CopiesEnqueueFieldsAndRating()
    {
        var rating = new UserRatingState(1320, 70, true);

        SkillEnrichedEnqueue enriched = EnqueueReader.Enrich(ProtoTestData.PlayerEnqueued("u9", "tok-9", "10+0"), rating);

        Assert.Equal("u9", enriched.PlayerId);
        Assert.Equal("tok-9", enriched.QueueToken);
        Assert.Equal("10+0", enriched.TimeFormatId);
        Assert.Equal(1320, enriched.Rating);
    }
}
