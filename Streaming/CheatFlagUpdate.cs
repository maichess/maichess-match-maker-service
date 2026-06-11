namespace MaichessMatchMakerService.Streaming;

// The flag state one cheat.events record sets for one user.
internal sealed record CheatFlagUpdate(string UserId, bool Flagged);
