using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Google.Protobuf;

namespace MaichessMatchMakerService.Queue;

// Confluent Protobuf serde factory for the maichess.events.v1 generated messages
// (OutboundEvent, MatchmakingEvent, MatchCommand). Sits next to the Avro serde
// used by KafkaMatchmakingNotifier / KafkaMatchCreator during the per-topic
// migration: nothing is switched to it yet (the IMatchmakingNotifier /
// IMatchCreator producers keep their Avro serde). Task 02 onwards swaps it in
// once each topic dual-reads then cuts over.
//
// Reuses the existing protobuf tooling: the generated types ship in the same
// Maichess.PlatformProtos package as the gRPC stubs (Google.Protobuf runtime),
// so the only new dependency is Confluent.SchemaRegistry.Serdes.Protobuf.
[ExcludeFromCodeCoverage]
internal static class ProtobufEventSerdes
{
    // Value serializer for a generated proto envelope; pass to
    // ProducerBuilder.SetValueSerializer (it accepts IAsyncSerializer<T>, exactly
    // as the Avro path passes AvroSerializer<GenericRecord>).
    public static IAsyncSerializer<T> Serializer<T>(ISchemaRegistryClient registry)
        where T : class, IMessage<T>, new()
        => new ProtobufSerializer<T>(registry);

    // Sync value deserializer for use with a synchronous consumer loop
    // (mirrors AvroDeserializer<GenericRecord>(registry).AsSyncOverAsync()).
    public static IDeserializer<T> Deserializer<T>()
        where T : class, IMessage<T>, new()
        => new ProtobufDeserializer<T>().AsSyncOverAsync();
}
