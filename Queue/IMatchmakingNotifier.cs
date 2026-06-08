namespace MaichessMatchMakerService.Queue;

// Abstraction over matchmaking-related real-time delivery and event emission.
// Implemented by SocketNotifier (legacy gRPC transport) and
// KafkaMatchmakingNotifier (socket.outbound.v1 + matchmaking.events.v1).
// Selected at startup via the Socket:Transport setting.
internal interface IMatchmakingNotifier
{
    // Pushes a `matched` event carrying the match_id to a single user.
    void NotifyMatched(string userId, string matchId);

    // Records that two players were paired (matchmaking event trail).
    void PlayersMatched(string whiteUserId, string blackUserId, string timeFormatId);
}
