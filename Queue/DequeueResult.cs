using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Queue;

[ExcludeFromCodeCoverage]
internal abstract record DequeueResult
{
    internal sealed record Success : DequeueResult;

    internal sealed record NotFound : DequeueResult;
}
