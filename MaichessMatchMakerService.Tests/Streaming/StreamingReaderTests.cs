using Avro;
using Avro.Generic;
using MaichessMatchMakerService.Streaming;
using MaichessMatchMakerService.Tests.Support;
using Xunit;

namespace MaichessMatchMakerService.Tests.Streaming;

// Pure reads over the event envelopes feeding the KTable topology: the AvroPayload guard,
// the rating aggregator, and the enqueue value-joiner.
public sealed class StreamingReaderTests
{
    [Fact]
    public void Apply_RatingUpdated_SetsRatingAndMarksPresent()
    {
        UserRatingState result = UserEventReader.Apply(UserRatingState.Empty, AvroTestData.RatingUpdated("u1", 1480, rd: 95));

        Assert.Equal(1480, result.Rating);
        Assert.Equal(95, result.RatingDeviation);
        Assert.True(result.HasRating);
    }

    [Fact]
    public void Apply_NonRatingEvent_LeavesStateUntouched()
    {
        var current = new UserRatingState(1500, 80, false, true);

        UserRatingState result = UserEventReader.Apply(current, AvroTestData.UserRegistered("u1", "alice"));

        Assert.Equal(current, result);
    }

    [Fact]
    public void Apply_EnvelopeWithoutPayload_LeavesStateUntouched()
    {
        var current = new UserRatingState(1500, 80, false, true);

        Assert.Equal(current, UserEventReader.Apply(current, NoPayload()));
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
        var rating = new UserRatingState(1320, 70, true, true);

        SkillEnrichedEnqueue enriched = EnqueueReader.Enrich(ProtoTestData.PlayerEnqueued("u9", "tok-9", "10+0"), rating);

        Assert.Equal("u9", enriched.PlayerId);
        Assert.Equal("tok-9", enriched.QueueToken);
        Assert.Equal("10+0", enriched.TimeFormatId);
        Assert.Equal(1320, enriched.Rating);
        Assert.True(enriched.Flagged);
    }

    [Theory]
    [InlineData(true)]   // payload field present but not a record
    [InlineData(false)]  // payload field absent from the record
    public void AvroPayloadName_GuardsMalformedEnvelopes(bool payloadFieldPresentButWrongType)
    {
        GenericRecord envelope = payloadFieldPresentButWrongType ? PayloadIsNotARecord() : NoPayloadField();

        Assert.Equal(string.Empty, AvroPayload.Name(envelope));
    }

    // An envelope built on the real schema but with `payload` left unset.
    private static GenericRecord NoPayload()
    {
        GenericRecord env = new(AvroTestData.UserEvents);
        env.Add("event_id", "e");
        env.Add("event_type", "user.UserRegistered");
        env.Add("aggregate_id", "u1");
        env.Add("sequence", 0L);
        env.Add("occurred_at", 0L);
        env.Add("producer", "test");
        return env;
    }

    // payload present in the dictionary but holding a non-record value.
    private static GenericRecord PayloadIsNotARecord()
    {
        GenericRecord env = NoPayload();
        env.Add("payload", "not-a-record");
        return env;
    }

    // A record whose schema has no payload field at all.
    private static GenericRecord NoPayloadField()
    {
        var schema = (RecordSchema)Schema.Parse(
            """{ "type": "record", "name": "Bare", "fields": [ { "name": "x", "type": "string" } ] }""");
        GenericRecord r = new(schema);
        r.Add("x", "y");
        return r;
    }
}
