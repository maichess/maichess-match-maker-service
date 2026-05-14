using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Rest;

[ExcludeFromCodeCoverage]
internal sealed record TimeFormatResponse(string Id, long BaseMs, long IncrementMs, string Category);
