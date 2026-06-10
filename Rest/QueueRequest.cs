using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Rest;

// allow_flagged is the per-search anti-cheat toggle (default false = disallow
// being matched with previously-flagged players). Ignored for bot opponents.
[ExcludeFromCodeCoverage]
internal sealed record QueueRequest(string TimeFormatId, OpponentRequest Opponent, bool? AllowFlagged = null);
