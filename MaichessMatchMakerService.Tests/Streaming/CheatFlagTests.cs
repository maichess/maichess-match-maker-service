using Maichess.Events.V1;
using MaichessMatchMakerService.Streaming;
using Xunit;

namespace MaichessMatchMakerService.Tests.Streaming;

// The cheat.events.v1 read model feeding the matchmaking toggle: the pure projection
// and the in-memory flag store.
public sealed class CheatFlagTests
{
    private static CheatEvent Envelope(string userId = "u1") =>
        new() { EventId = "e1", EventType = "cheat", AggregateId = userId, Sequence = 1, OccurredAt = 1 };

    [Fact]
    public void Project_PlayerFlagged_SetsFlaggedTrue()
    {
        CheatEvent evt = Envelope();
        evt.PlayerFlagged = new PlayerFlagged { UserId = "u1", CaseId = "c", Score = 0.9 };

        CheatFlagUpdate? update = CheatFlagProjection.Project(evt);

        Assert.NotNull(update);
        Assert.Equal("u1", update.UserId);
        Assert.True(update.Flagged);
    }

    [Fact]
    public void Project_PlayerUnflagged_SetsFlaggedFalse()
    {
        CheatEvent evt = Envelope();
        evt.PlayerUnflagged = new PlayerUnflagged { UserId = "u1", CaseId = "c", UnflaggedBy = "dev" };

        CheatFlagUpdate? update = CheatFlagProjection.Project(evt);

        Assert.NotNull(update);
        Assert.False(update.Flagged);
    }

    [Fact]
    public void Project_LiveSuspicion_ProjectsToNothing()
    {
        CheatEvent evt = Envelope();
        evt.LiveSuspicionRaised = new LiveSuspicionRaised { UserId = "u1", MatchId = "m", Ply = 9, Score = 0.8 };

        Assert.Null(CheatFlagProjection.Project(evt));
    }

    [Fact]
    public void Project_PayloadlessOrKeyless_ProjectsToNothing()
    {
        Assert.Null(CheatFlagProjection.Project(Envelope()));

        CheatEvent keyless = Envelope(userId: string.Empty);
        keyless.PlayerFlagged = new PlayerFlagged { UserId = "u1" };
        Assert.Null(CheatFlagProjection.Project(keyless));
    }

    [Fact]
    public void Store_UnknownUserIsNotFlagged()
    {
        Assert.False(new CheatFlagStore().IsFlagged("nobody"));
    }

    [Fact]
    public void Store_AppliesAndOverwritesFlagState()
    {
        CheatFlagStore store = new();

        store.Apply(new CheatFlagUpdate("u1", true));
        Assert.True(store.IsFlagged("u1"));

        store.Apply(new CheatFlagUpdate("u1", false));
        Assert.False(store.IsFlagged("u1"));
    }
}
