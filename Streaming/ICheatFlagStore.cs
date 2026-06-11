namespace MaichessMatchMakerService.Streaming;

// Local lookup of a user's anti-cheat flag, materialised from the compacted
// cheat.events.v1 topic. Unknown users are not flagged (the topic only carries
// users anti-cheat has ever ruled on).
internal interface ICheatFlagStore
{
    bool IsFlagged(string userId);
}
