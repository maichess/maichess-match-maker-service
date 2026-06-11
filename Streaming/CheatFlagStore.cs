using System.Collections.Concurrent;

namespace MaichessMatchMakerService.Streaming;

// In-memory materialisation of the compacted cheat.events.v1 topic (userId ->
// flagged). Per-pod like the Streamiz state store; warmed from the beginning
// of the topic on startup by CheatFlagConsumer, so a restart rebuilds the full
// flag state. Matchmaking reads it locally — no RPC on the pairing path.
internal sealed class CheatFlagStore : ICheatFlagStore
{
    private readonly ConcurrentDictionary<string, bool> flags = new();

    public bool IsFlagged(string userId) => flags.TryGetValue(userId, out bool flagged) && flagged;

    public void Apply(CheatFlagUpdate update) => flags[update.UserId] = update.Flagged;
}
