using Avro.Generic;
using Maichess.Events.V1;
using MaichessMatchMakerService.Streaming;
using MaichessMatchMakerService.Tests.Support;
using Xunit;

namespace MaichessMatchMakerService.Tests.Streaming;

// The Avro arm of the matchmaking.events.v1 dual-read (Kafka task 02): an already-on-
// topic Avro envelope is mapped into the Protobuf MatchmakingEvent the topology works
// in, so the cutover stays reversible. Builds Avro GenericRecords with the same fixed
// schema AvroTestData uses, then maps and asserts the projection.
public sealed class MatchmakingAvroToProtoTests
{
    [Fact]
    public void Map_PlayerEnqueued_ProjectsEnvelopeAndPayload()
    {
        GenericRecord avro = AvroTestData.PlayerEnqueued("p1", "tok-1", "5+0");

        MatchmakingEvent evt = MatchmakingAvroToProto.Map(avro);

        Assert.Equal(MatchmakingEvent.PayloadOneofCase.PlayerEnqueued, evt.PayloadCase);
        Assert.Equal("p1", evt.PlayerEnqueued.PlayerId);
        Assert.Equal("tok-1", evt.PlayerEnqueued.QueueToken);
        Assert.Equal("5+0", evt.PlayerEnqueued.TimeFormatId);
        Assert.Equal("p1", evt.AggregateId);
        Assert.Equal("matchmaking.PlayerEnqueued", evt.EventType);
        Assert.Equal("test", evt.Producer);
    }

    [Fact]
    public void Map_PlayerDequeued_ProjectsPayload()
    {
        MatchmakingEvent evt = MatchmakingAvroToProto.Map(AvroTestData.PlayerDequeued("p2", "tok-2"));

        Assert.Equal(MatchmakingEvent.PayloadOneofCase.PlayerDequeued, evt.PayloadCase);
        Assert.Equal("p2", evt.PlayerDequeued.PlayerId);
        Assert.Equal("tok-2", evt.PlayerDequeued.QueueToken);
    }

    [Fact]
    public void Map_EnqueuedAvro_IsRecognisedByEnqueueReader()
    {
        // End-to-end of the Avro arm: a mapped Avro enqueue is filtered + enriched
        // exactly like a native proto enqueue.
        MatchmakingEvent evt = MatchmakingAvroToProto.Map(AvroTestData.PlayerEnqueued("p3", "tok-3", "10+0"));

        Assert.True(EnqueueReader.IsPlayerEnqueued(evt));
        SkillEnrichedEnqueue enriched = EnqueueReader.Enrich(evt, new UserRatingState(1400, 60, true));
        Assert.Equal("p3", enriched.PlayerId);
        Assert.Equal("10+0", enriched.TimeFormatId);
        Assert.Equal(1400, enriched.Rating);
    }

    [Fact]
    public void Map_EnvelopeWithoutPayload_YieldsNonePayloadCase()
    {
        GenericRecord bare = new(AvroTestData.Matchmaking);
        bare.Add("event_id", "e");
        bare.Add("event_type", "matchmaking.Unknown");
        bare.Add("aggregate_id", "p9");
        bare.Add("sequence", 0L);
        bare.Add("occurred_at", 0L);
        bare.Add("producer", "test");
        // payload left unset

        MatchmakingEvent evt = MatchmakingAvroToProto.Map(bare);

        Assert.Equal(MatchmakingEvent.PayloadOneofCase.None, evt.PayloadCase);
        Assert.False(EnqueueReader.IsPlayerEnqueued(evt));
    }

    [Fact]
    public void TryReadSchemaId_ReadsBigEndianAndRejectsMalformed()
    {
        byte[] framed = [0x00, 0x00, 0x00, 0x01, 0x2C];
        Assert.Equal(300, ConfluentFraming.TryReadSchemaId(framed));
        Assert.Null(ConfluentFraming.TryReadSchemaId([0x01, 0x00, 0x00, 0x00, 0x05]));
        Assert.Null(ConfluentFraming.TryReadSchemaId([0x00, 0x00]));
    }
}
