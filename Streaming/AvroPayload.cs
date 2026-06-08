using Avro.Generic;

namespace MaichessMatchMakerService.Streaming;

// Shared, guarded access to the `payload` union of an event envelope GenericRecord.
// Centralised so the readers that build on it carry no duplicated defensive branches.
internal static class AvroPayload
{
    internal static bool TryGet(GenericRecord envelope, out GenericRecord payload)
    {
        if (envelope.TryGetValue("payload", out object? p) && p is GenericRecord record)
        {
            payload = record;
            return true;
        }

        payload = null!;
        return false;
    }

    internal static string Name(GenericRecord envelope) =>
        TryGet(envelope, out GenericRecord payload) ? payload.Schema.Name : string.Empty;
}
