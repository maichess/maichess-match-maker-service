using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Rest;

[ExcludeFromCodeCoverage]
internal sealed record BotMatchRequest(string WhiteBotId, string BlackBotId, string TimeFormatId);
