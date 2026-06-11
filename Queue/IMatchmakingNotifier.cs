namespace MaichessMatchMakerService.Queue;

// Abstraction over matchmaking-related real-time delivery and event emission.
// Implemented by KafkaMatchmakingNotifier (socket.outbound.v1 +
// matchmaking.events.v1). The legacy gRPC transport (SocketNotifier →
// Socket.EmitEvent) was removed in Kafka task 09.
internal interface IMatchmakingNotifier
{
    // Pushes a `matched` event carrying the match_id to a single user.
    void NotifyMatched(string userId, string matchId);

    // Records that two players were paired (matchmaking event trail).
    void PlayersMatched(string whiteUserId, string blackUserId, string timeFormatId);
}
