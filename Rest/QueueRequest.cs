namespace MaichessMatchMakerService.Rest;

internal sealed record QueueRequest(string TimeControl, OpponentRequest Opponent);
