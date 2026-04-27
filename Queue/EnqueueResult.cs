using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Queue;

[ExcludeFromCodeCoverage]
internal abstract record EnqueueResult
{
    internal sealed record Success(string QueueToken) : EnqueueResult;

    internal sealed record InvalidInput(string Message) : EnqueueResult;

    internal sealed record AlreadyQueued : EnqueueResult;
}
