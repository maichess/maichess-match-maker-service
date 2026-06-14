using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Rest;

// allow_flagged is the per-search anti-cheat toggle (default false = disallow
// being matched with previously-flagged players). Ignored for bot opponents.
// color_preference is the requested side: "white" | "black" | "any" (default).
// For a vs-bot match "random" is accepted as a synonym for "any".
[ExcludeFromCodeCoverage]
internal sealed record QueueRequest(
    string TimeFormatId,
    OpponentRequest Opponent,
    bool? AllowFlagged = null,
    string? ColorPreference = null);
