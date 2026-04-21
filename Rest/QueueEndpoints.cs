using System.Security.Claims;
using Maichess.MatchManager.V1;
using MaichessMatchMakerService.Queue;
using Microsoft.AspNetCore.Mvc;
using MatchManagerTimeControl = Maichess.MatchManager.V1.TimeControl;

namespace MaichessMatchMakerService.Rest;

internal static class QueueEndpoints
{
    private static readonly HashSet<string> ValidTimeControls = ["bullet", "blitz", "rapid", "classical"];

    internal static IEndpointRouteBuilder MapQueueEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/queue").RequireAuthorization();
        group.MapPost("/", Enqueue);
        group.MapGet("/{queueToken}/status", GetStatus);
        group.MapDelete("/{queueToken}", Dequeue);
        return routes;
    }

    private static async Task<IResult> Enqueue(
        [FromBody] QueueRequest body,
        ClaimsPrincipal principal,
        QueueRepository queue,
        Matches.MatchesClient matchesClient,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out string userId))
        {
            return Results.Unauthorized();
        }

        if (!ValidTimeControls.Contains(body.TimeControl))
        {
            return Results.BadRequest(new ErrorResponse("invalid time_control"));
        }

        if (body.Opponent.Type is not "human" and not "bot")
        {
            return Results.BadRequest(new ErrorResponse("opponent.type must be 'human' or 'bot'"));
        }

        if (body.Opponent.Type == "bot" && string.IsNullOrWhiteSpace(body.Opponent.BotId))
        {
            return Results.BadRequest(new ErrorResponse("opponent.bot_id is required for bot matches"));
        }

        string? existingToken = await queue.GetUserQueueTokenAsync(userId);
        if (existingToken is not null)
        {
            return Results.Conflict(new ErrorResponse("already in queue"));
        }

        string queueToken = Guid.NewGuid().ToString();

        if (body.Opponent.Type == "bot")
        {
            var request = new CreateMatchRequest
            {
                White = new Player { UserId = userId },
                Black = new Player { BotId = body.Opponent.BotId },
                TimeControl = MapTimeControl(body.TimeControl),
            };

            CreateMatchResponse response = await matchesClient.CreateMatchAsync(request, cancellationToken: ct);
            await queue.EnqueueBotMatchAsync(queueToken, userId, body.TimeControl, response.Match.Id);
        }
        else
        {
            await queue.EnqueueAsync(queueToken, userId, body.TimeControl);
        }

        return Results.Created($"/queue/{queueToken}", new QueueResponse(queueToken));
    }

    private static async Task<IResult> GetStatus(
        string queueToken,
        ClaimsPrincipal principal,
        QueueRepository queue)
    {
        if (!TryGetUserId(principal, out string userId))
        {
            return Results.Unauthorized();
        }

        QueueEntry? entry = await queue.GetEntryAsync(queueToken);

        return entry is null || entry.UserId != userId
            ? Results.NotFound()
            : Results.Ok(new QueueStatusResponse(
                entry.Status == QueueStatus.Matched ? "matched" : "waiting",
                entry.MatchId));
    }

    private static async Task<IResult> Dequeue(
        string queueToken,
        ClaimsPrincipal principal,
        QueueRepository queue)
    {
        if (!TryGetUserId(principal, out string userId))
        {
            return Results.Unauthorized();
        }

        QueueEntry? entry = await queue.GetEntryAsync(queueToken);

        if (entry is null || entry.UserId != userId)
        {
            return Results.NotFound();
        }

        await queue.RemoveAsync(queueToken, userId, entry.TimeControl);
        return Results.NoContent();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out string userId)
    {
        string? value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        userId = value ?? string.Empty;
        return value is not null;
    }

    private static MatchManagerTimeControl MapTimeControl(string value) => value switch
    {
        "bullet" => MatchManagerTimeControl.Bullet,
        "blitz" => MatchManagerTimeControl.Blitz,
        "rapid" => MatchManagerTimeControl.Rapid,
        "classical" => MatchManagerTimeControl.Classical,
        _ => MatchManagerTimeControl.Unspecified,
    };
}
