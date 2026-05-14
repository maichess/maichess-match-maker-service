using System.Diagnostics.CodeAnalysis;

namespace MaichessMatchMakerService.Rest;

[ExcludeFromCodeCoverage]
internal sealed record TimeFormatsResponse(IReadOnlyList<TimeFormatResponse> Formats);
