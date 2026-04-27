using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Queue;

[ExcludeFromCodeCoverage]
internal abstract record GetStatusResult
{
    internal sealed record Found(string Status, string? MatchId) : GetStatusResult;

    internal sealed record NotFound : GetStatusResult;
}
