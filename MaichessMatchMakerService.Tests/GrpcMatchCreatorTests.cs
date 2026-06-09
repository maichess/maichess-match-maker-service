using Grpc.Core;
using Maichess.MatchManager.V1;
using MaichessMatchMakerService.Queue;
using MaichessMatchMakerService.Tests.Support;
using NSubstitute;
using Xunit;

namespace MaichessMatchMakerService.Tests;

// The gRPC IMatchCreator builds the legacy CreateMatchRequest and returns the server-assigned id.
// These assertions (player mapping, time-format resolution) previously lived in the service tests;
// they belong here now that the services depend on the transport-agnostic seam.
public sealed class GrpcMatchCreatorTests
{
    private readonly Matches.MatchesClient client = Substitute.For<Matches.MatchesClient>();
    private CreateMatchRequest? captured;

    [Fact]
    public async Task CreateMatchAsync_returns_the_server_assigned_id()
    {
        var sut = CreateSut("match-99");

        string id = await sut.CreateMatchAsync(
            new CommandPlayer("user-a", null), new CommandPlayer("user-b", null), "5+0", CancellationToken.None);

        Assert.Equal("match-99", id);
    }

    [Fact]
    public async Task CreateMatchAsync_maps_human_vs_human_players()
    {
        var sut = CreateSut();

        await sut.CreateMatchAsync(
            new CommandPlayer("user-a", null), new CommandPlayer("user-b", null), "5+0", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("user-a", captured.White.UserId);
        Assert.Equal("user-b", captured.Black.UserId);
    }

    [Fact]
    public async Task CreateMatchAsync_maps_human_vs_bot_players()
    {
        var sut = CreateSut();

        await sut.CreateMatchAsync(
            new CommandPlayer("user-a", null), new CommandPlayer(null, "bot-1"), "5+0", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("user-a", captured.White.UserId);
        Assert.Equal("bot-1", captured.Black.BotId);
    }

    [Theory]
    [InlineData("1+0", 60_000L, 0L)]
    [InlineData("3+2", 180_000L, 2_000L)]
    [InlineData("5+0", 300_000L, 0L)]
    [InlineData("10+5", 600_000L, 5_000L)]
    [InlineData("30+20", 1_800_000L, 20_000L)]
    public async Task CreateMatchAsync_resolves_the_time_format_from_the_registry(
        string id, long baseMs, long incrementMs)
    {
        var sut = CreateSut();

        await sut.CreateMatchAsync(
            new CommandPlayer("user-a", null), new CommandPlayer("user-b", null), id, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(id, captured.TimeFormat.Id);
        Assert.Equal(baseMs, captured.TimeFormat.BaseMs);
        Assert.Equal(incrementMs, captured.TimeFormat.IncrementMs);
    }

    private GrpcMatchCreator CreateSut(string matchId = "m-1")
    {
        var response = new CreateMatchResponse { Match = new Match { Id = matchId } };
        client
            .CreateMatchAsync(
                Arg.Do<CreateMatchRequest>(r => captured = r),
                Arg.Any<Metadata>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(GrpcHelper.GrpcCall(response));
        return new GrpcMatchCreator(client);
    }
}
