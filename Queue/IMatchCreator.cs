namespace MaichessMatchMakerService.Queue;

// Creates a match and yields its id, abstracting the transport. The gRPC implementation
// returns the server-assigned id (synchronous Matches.CreateMatch). The Kafka implementation
// mints the id, publishes a CreateMatchCommand to match.commands.v1, and returns the minted id
// immediately (the match materialises asynchronously via Match Manager's command consumer).
// Selected at startup via the Socket:Transport setting, mirroring IMatchmakingNotifier.
// Bot-vs-bot creation is not routed here — it stays on synchronous gRPC for start_fen validation.
internal interface IMatchCreator
{
    Task<string> CreateMatchAsync(CommandPlayer white, CommandPlayer black, string timeFormatId, CancellationToken ct);
}

// A match participant identity for a create-match request: a human (UserId) or a bot (BotId).
internal readonly record struct CommandPlayer(string? UserId, string? BotId);
