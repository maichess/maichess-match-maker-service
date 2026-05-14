using Maichess.MatchManager.V1;

namespace MaichessMatchMakerService.Queue;

// Mirror of the canonical preset list. Single source of truth lives in
// maichess-api-contracts/rest/match-maker.md — keep these in sync.
internal static class TimeFormatRegistry
{
    internal static readonly IReadOnlyList<TimeFormat> Presets =
    [
        new() { Id = "1+0", BaseMs = 60_000, IncrementMs = 0, Category = "bullet" },
        new() { Id = "2+1", BaseMs = 120_000, IncrementMs = 1_000, Category = "bullet" },
        new() { Id = "3+0", BaseMs = 180_000, IncrementMs = 0, Category = "blitz" },
        new() { Id = "3+2", BaseMs = 180_000, IncrementMs = 2_000, Category = "blitz" },
        new() { Id = "5+0", BaseMs = 300_000, IncrementMs = 0, Category = "blitz" },
        new() { Id = "5+3", BaseMs = 300_000, IncrementMs = 3_000, Category = "blitz" },
        new() { Id = "10+0", BaseMs = 600_000, IncrementMs = 0, Category = "rapid" },
        new() { Id = "10+5", BaseMs = 600_000, IncrementMs = 5_000, Category = "rapid" },
        new() { Id = "15+10", BaseMs = 900_000, IncrementMs = 10_000, Category = "rapid" },
        new() { Id = "30+0", BaseMs = 1_800_000, IncrementMs = 0, Category = "classical" },
        new() { Id = "30+20", BaseMs = 1_800_000, IncrementMs = 20_000, Category = "classical" },
    ];

    internal static bool IsKnown(string id) => Presets.Any(p => p.Id == id);

    internal static TimeFormat Resolve(string id) =>
        Presets.FirstOrDefault(p => p.Id == id)
            ?? throw new ArgumentException($"unknown time_format_id '{id}'", nameof(id));
}
