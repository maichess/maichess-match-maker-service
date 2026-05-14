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
        routes.MapGet("/time-formats", GetTimeFormats);

        RouteGroupBuilder group = routes.MapGroup("/queue").RequireAuthorization();
        group.MapPost("/", Enqueue);
        group.MapDelete("/{queueToken}", Dequeue);

        routes.MapPost("/matches/bot-vs-bot", CreateBotVsBotMatch).RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> GetBots(Bots.BotsClient botsClient, CancellationToken ct)
    {
        ListBotsResponse response = await botsClient.ListBotsAsync(new ListBotsRequest(), cancellationToken: ct);
        IReadOnlyList<BotResponse> bots = [.. response.Bots.Select(b => new BotResponse(b.Id, b.Name, b.Elo, b.Description))];
        return Results.Ok(new BotsListResponse(bots));
    }

    private static IResult GetTimeFormats()
    {
        IReadOnlyList<TimeFormatResponse> formats = [.. TimeFormatRegistry.Presets.Select(
            p => new TimeFormatResponse(p.Id, p.BaseMs, p.IncrementMs, p.Category))];
        return Results.Ok(new TimeFormatsResponse(formats));
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
            userId, body.TimeFormatId, body.Opponent.Type, body.Opponent.BotId, ct);

        return result switch
        {
            EnqueueResult.Success ok => Results.Created($"/queue/{ok.QueueToken}", new QueueResponse(ok.QueueToken, ok.MatchId)),
            EnqueueResult.InvalidInput err => Results.BadRequest(new ErrorResponse(err.Message)),
            EnqueueResult.AlreadyQueued => Results.Conflict(new ErrorResponse("already in queue")),
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

    private static async Task<IResult> CreateBotVsBotMatch(
        [FromBody] BotMatchRequest body,
        ClaimsPrincipal principal,
        QueueingService service,
        Bots.BotsClient botsClient,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out _))
        {
            return Results.Unauthorized();
        }

        ListBotsResponse bots = await botsClient.ListBotsAsync(new ListBotsRequest(), cancellationToken: ct);
        HashSet<string> known = [.. bots.Bots.Select(b => b.Id)];
        if (!known.Contains(body.WhiteBotId) || !known.Contains(body.BlackBotId))
        {
            return Results.BadRequest(new ErrorResponse("unknown bot_id"));
        }

        EnqueueResult result = await service.CreateBotVsBotMatchAsync(
            body.WhiteBotId, body.BlackBotId, body.TimeFormatId, ct);

        return result switch
        {
            EnqueueResult.Success ok => Results.Created(
                $"/matches/{ok.MatchId}", new BotMatchResponse(ok.MatchId ?? string.Empty)),
            EnqueueResult.InvalidInput err => Results.BadRequest(new ErrorResponse(err.Message)),
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
