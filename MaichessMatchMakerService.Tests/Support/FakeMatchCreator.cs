using MaichessMatchMakerService.Queue;

namespace MaichessMatchMakerService.Tests.Support;

// Transport-agnostic IMatchCreator double: records each create-match call and returns a
// configured match id (or throws a configured exception). Stands in for both the gRPC and Kafka
// implementations in the service-layer tests.
internal sealed class FakeMatchCreator : IMatchCreator
{
    private readonly List<(CommandPlayer White, CommandPlayer Black, string TimeFormatId)> calls = [];
    private string matchId = string.Empty;
    private Exception? throwOnCall;

    internal IReadOnlyList<(CommandPlayer White, CommandPlayer Black, string TimeFormatId)> Calls => calls;

    internal (CommandPlayer White, CommandPlayer Black, string TimeFormatId)? LastCall =>
        calls.Count > 0 ? calls[^1] : null;

    internal void ReturnsMatch(string id) => matchId = id;

    internal void Throws(Exception ex) => throwOnCall = ex;

    public Task<string> CreateMatchAsync(
        CommandPlayer white, CommandPlayer black, string timeFormatId, CancellationToken ct)
    {
        calls.Add((white, black, timeFormatId));
        return throwOnCall is not null
            ? Task.FromException<string>(throwOnCall)
            : Task.FromResult(matchId);
    }
}
