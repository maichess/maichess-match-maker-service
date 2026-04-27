using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Rest;

[ExcludeFromCodeCoverage]
internal sealed record OpponentRequest(string Type, string? BotId);
