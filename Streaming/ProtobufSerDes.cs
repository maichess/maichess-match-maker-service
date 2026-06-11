using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Google.Protobuf;
using Streamiz.Kafka.Net.SerDes;

namespace MaichessMatchMakerService.Streaming;

// Raw-Protobuf Streamiz value SerDes for the maichess.events.v1 generated messages.
// Kafka task 09 removed the Confluent Schema Registry: records are the bare Protobuf
// wire bytes (msg.ToByteArray() / Parser.ParseFrom(bytes)) with schemas owned solely by
// maichess-api-contracts. Consume-only in this topology (the join writes its own POCO
// output), so Serialize is exercised only by the changelog if ever needed.
[ExcludeFromCodeCoverage]
internal sealed class ProtobufSerDes<T> : ISerDes<T>
    where T : class, IMessage<T>, new()
{
    private static readonly MessageParser<T> Parser = new(() => new T());

    public void Initialize(SerDesContext context)
    {
        // No registry / external state to wire — raw Protobuf is self-describing via
        // the generated parser.
    }

    public T Deserialize(byte[] data, SerializationContext context) =>
        data is null || data.Length == 0 ? null! : Parser.ParseFrom(data);

    public byte[] Serialize(T data, SerializationContext context) => data.ToByteArray();

    public object? DeserializeObject(byte[] data, SerializationContext context) =>
        Deserialize(data, context);

    public byte[] SerializeObject(object data, SerializationContext context) =>
        Serialize((T)data, context);
}
