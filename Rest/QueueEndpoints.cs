using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Maichess.Engine.V1;
using MaichessMatchMakerService.Queue;
using Microsoft.AspNetCore.Mvc;

namespace MaichessMatchMakerService.Rest;

[ExcludeFromCodeCoverage]
internal static class QueueEndpoints
{
    internal static IEndpointRouteBuilder MapQueueEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots", GetBots);

        RouteGroupBuilder group = routes.MapGroup("/queue").RequireAuthorization();
        group.MapPost("/", Enqueue);
        group.MapGet("/{queueToken}/status", GetStatus);
        group.MapDelete("/{queueToken}", Dequeue);
        return routes;
    }

    private static async Task<IResult> GetBots(Bots.BotsClient botsClient, CancellationToken ct)
    {
        ListBotsResponse response = await botsClient.ListBotsAsync(new ListBotsRequest(), cancellationToken: ct);
        IReadOnlyList<BotResponse> bots = [.. response.Bots.Select(b => new BotResponse(b.Id, b.Name, b.Elo))];
        return Results.Ok(new BotsListResponse(bots));
    }

    private static async Task<IResult> Enqueue(
        [FromBody] QueueRequest body,
        ClaimsPrincipal principal,
        QueueingService service,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out string userId))
        {
            return Results.Unauthorized();
        }

        EnqueueResult result = await service.EnqueueAsync(
            userId, body.TimeControl, body.Opponent.Type, body.Opponent.BotId, ct);

        return result switch
        {
            EnqueueResult.Success ok => Results.Created($"/queue/{ok.QueueToken}", new QueueResponse(ok.QueueToken)),
            EnqueueResult.InvalidInput err => Results.BadRequest(new ErrorResponse(err.Message)),
            EnqueueResult.AlreadyQueued => Results.Conflict(new ErrorResponse("already in queue")),
            _ => Results.Problem(),
        };
    }

    private static async Task<IResult> GetStatus(
        string queueToken,
        ClaimsPrincipal principal,
        QueueingService service)
    {
        if (!TryGetUserId(principal, out string userId))
        {
            return Results.Unauthorized();
        }

        GetStatusResult result = await service.GetStatusAsync(queueToken, userId);

        return result switch
        {
            GetStatusResult.Found found => Results.Ok(new QueueStatusResponse(found.Status, found.MatchId)),
            GetStatusResult.NotFound => Results.NotFound(),
            _ => Results.Problem(),
        };
    }

    private static async Task<IResult> Dequeue(
        string queueToken,
        ClaimsPrincipal principal,
        QueueingService service)
    {
        if (!TryGetUserId(principal, out string userId))
        {
            return Results.Unauthorized();
        }

        DequeueResult result = await service.DequeueAsync(queueToken, userId);

        return result switch
        {
            DequeueResult.Success => Results.NoContent(),
            DequeueResult.NotFound => Results.NotFound(),
            _ => Results.Problem(),
        };
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out string userId)
    {
        string? value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        userId = value ?? string.Empty;
        return value is not null;
    }
}
