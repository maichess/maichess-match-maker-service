using Google.Protobuf;
using Maichess.Events.V1;
using Xunit;

namespace MaichessMatchMakerService.Tests;

// Round-trips the maichess.events.v1 generated proto types (encode -> decode) for
// the envelopes and every payload variant on the topics Match Maker produces:
// socket.outbound (matched push), matchmaking.events, match.commands. Proves the
// proto schemas carry the same field set the Avro .avsc did, before the
// IMatchmakingNotifier / IMatchCreator producers switch to the Protobuf serde.
public sealed class ProtobufEventRoundTripTests
{
    private static void AssertRoundTrips<T>(T original, MessageParser<T> parser)
        where T : IMessage<T>
    {
        byte[] bytes = original.ToByteArray();
        T parsed = parser.ParseFrom(bytes);
        Assert.Equal(original, parsed);
    }

    private static TimeFormat SampleTimeFormat() => new()
    {
        Id = "3+2",
        BaseMs = 180_000,
        IncrementMs = 2_000,
        Category = "blitz",
    };

    [Fact]
    public void SocketOutbound_MatchedPush_RoundTrips()
    {
        OutboundEvent matched = new()
        {
            EventId = "e1",
            EventType = "socket.matched",
            AggregateId = "user-1",
            OccurredAt = 1_700_000_000_000,
            Producer = "match-maker-service",
            Push = new SocketPush
            {
                TargetUserId = "user-1",
                EventName = "matched",
                PayloadJson = "{\"match_id\":\"m1\"}",
            },
        };

        AssertRoundTrips(matched, OutboundEvent.Parser);
        Assert.Equal(SocketPush.TargetOneofCase.TargetUserId, matched.Push.TargetCase);
    }

    public static IEnumerable<object[]> MatchmakingPayloads()
    {
        yield return [new MatchmakingEvent
        {
            PlayerEnqueued = new PlayerEnqueued { PlayerId = "p1", QueueToken = "t1", TimeFormatId = "3+2" },
        }];
        yield return [new MatchmakingEvent
        {
            PlayerDequeued = new PlayerDequeued { PlayerId = "p1", QueueToken = "t1" },
        }];
        yield return [new MatchmakingEvent
        {
            PlayersMatched = new PlayersMatched { WhiteUserId = "w", BlackUserId = "b", TimeFormatId = "3+2" },
        }];
    }

    [Theory]
    [MemberData(nameof(MatchmakingPayloads))]
    public void MatchmakingEvent_EveryPayload_RoundTrips(MatchmakingEvent payload)
    {
        MatchmakingEvent envelope = new(payload)
        {
            EventId = "mm1",
            EventType = "matchmaking.event",
            AggregateId = "p1",
            OccurredAt = 1_700_000_000_000,
            Producer = "match-maker-service",
        };

        AssertRoundTrips(envelope, MatchmakingEvent.Parser);
        Assert.NotEqual(MatchmakingEvent.PayloadOneofCase.None, envelope.PayloadCase);
    }

    [Fact]
    public void MatchCommand_CreateMatch_RoundTrips()
    {
        MatchCommand command = new()
        {
            EventId = "c1",
            EventType = "match.command",
            AggregateId = "m1",
            OccurredAt = 1_700_000_000_000,
            Producer = "match-maker-service",
            CreateMatch = new CreateMatchCommand
            {
                White = new Player { UserId = "w" },
                Black = new Player { BotId = "bot-1" },
                TimeFormat = SampleTimeFormat(),
                CreatedBy = new Player { UserId = "w" },
                Source = MatchSource.Native,
            },
        };

        AssertRoundTrips(command, MatchCommand.Parser);
        Assert.Equal(MatchCommand.PayloadOneofCase.CreateMatch, command.PayloadCase);
        Assert.Equal(Player.IdentityOneofCase.BotId, command.CreateMatch.Black.IdentityCase);
    }
}
