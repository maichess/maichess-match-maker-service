using Maichess.MatchManager.V1;

namespace MaichessMatchMakerService.Queue;

// Creates matches via the legacy synchronous Matches.CreateMatch gRPC call and returns the
// server-assigned id. Used when Socket:Transport is "grpc" (and wherever Kafka is disabled).
internal sealed class GrpcMatchCreator(Matches.MatchesClient matchesClient) : IMatchCreator
{
    public async Task<string> CreateMatchAsync(
        CommandPlayer white, CommandPlayer black, string timeFormatId, CancellationToken ct)
    {
        var request = new CreateMatchRequest
        {
            White = ToPlayer(white),
            Black = ToPlayer(black),
            TimeFormat = TimeFormatRegistry.Resolve(timeFormatId),
        };

        CreateMatchResponse response = await matchesClient.CreateMatchAsync(request, cancellationToken: ct);
        return response.Match.Id;
    }

    private static Player ToPlayer(CommandPlayer player) =>
        player.UserId is not null
            ? new Player { UserId = player.UserId }
            : new Player { BotId = player.BotId };
}
