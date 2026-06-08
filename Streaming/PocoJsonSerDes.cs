using System.Text.Json;
using Confluent.Kafka;
using Streamiz.Kafka.Net.SerDes;

namespace MaichessMatchMakerService.Streaming;

// Minimal System.Text.Json SerDes for the topology's internal POCO values
// (UserRatingState in the KTable / changelog, SkillEnrichedEnqueue on the join output).
// Streamiz requires a parameterless constructor and null-tolerant (de)serialisation.
internal sealed class PocoJsonSerDes<T> : ISerDes<T>
{
    public byte[] Serialize(T data, SerializationContext context) =>
        data is null ? [] : JsonSerializer.SerializeToUtf8Bytes(data);

    public T Deserialize(byte[] data, SerializationContext context) =>
        data is null || data.Length == 0 ? default! : JsonSerializer.Deserialize<T>(data)!;

    public byte[] SerializeObject(object data, SerializationContext context) =>
        Serialize((T)data, context);

    public object? DeserializeObject(byte[] data, SerializationContext context) =>
        Deserialize(data, context);

    public void Initialize(SerDesContext context)
    {
        // No registry or external resource to initialise.
    }
}
