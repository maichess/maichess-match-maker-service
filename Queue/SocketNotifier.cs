using System.Diagnostics.CodeAnalysis;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Socket.V1;

using SocketSvc = Socket.V1.Socket;

namespace MaichessMatchMakerService.Queue;

[ExcludeFromCodeCoverage]
internal sealed class SocketNotifier(SocketSvc.SocketClient client, ILogger<SocketNotifier> logger)
{
    internal void NotifyMatched(string userId, string matchId)
    {
        Struct payload = new();
        payload.Fields["match_id"] = Value.ForString(matchId);
        _ = Task.Run(() => EmitAsync(userId, "matched", payload));
    }

    private async Task EmitAsync(string userId, string @event, Struct payload)
    {
        try
        {
            await client.EmitEventAsync(new EmitEventRequest
            {
                UserId = userId,
                Event = @event,
                Payload = payload,
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "Failed to emit '{Event}' to user {UserId}", @event, userId);
        }
    }
}
