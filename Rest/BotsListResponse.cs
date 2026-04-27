namespace MaichessMatchMakerService.Rest;

internal sealed record BotsListResponse(IReadOnlyList<BotResponse> Bots);
