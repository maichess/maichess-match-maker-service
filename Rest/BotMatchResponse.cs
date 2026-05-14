using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Rest;

[ExcludeFromCodeCoverage]
internal sealed record BotMatchResponse(string MatchId);
