using Grpc.Core;

namespace MaichessMatchMakerService.Tests.Support;

internal static class GrpcHelper
{
    internal static AsyncUnaryCall<T> GrpcCall<T>(T response) =>
        new(
            Task.FromResult(response),
            Task.FromResult(Metadata.Empty),
            () => Status.DefaultSuccess,
            () => Metadata.Empty,
            () => { });

    internal static AsyncUnaryCall<T> GrpcCallFailed<T>() =>
        new(
            Task.FromException<T>(new RpcException(new Status(StatusCode.Internal, "upstream error"))),
            Task.FromResult(Metadata.Empty),
            () => Status.DefaultSuccess,
            () => Metadata.Empty,
            () => { });
}
