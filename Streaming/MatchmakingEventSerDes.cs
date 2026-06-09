using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Maichess.Events.V1;
using Streamiz.Kafka.Net.SerDes;

namespace MaichessMatchMakerService.Streaming;

// Dual-read Streamiz value SerDes for matchmaking.events.v1 (Kafka task 02). The topic
// is mid-migration from Avro to Protobuf, so each record is decoded with the arm its
// Confluent schema id resolves to in the registry and projected onto the Protobuf
// MatchmakingEvent the topology works in. Consume-only: the topology reads this topic
// and writes its join output elsewhere, so Serialize is never exercised.
//
// I/O glue (registry + Confluent deserializers) — excluded from coverage like the other
// Kafka serde wrappers. The pure pieces it composes (ConfluentFraming.TryReadSchemaId,
// MatchmakingAvroToProto.Map) are unit-tested.
[ExcludeFromCodeCoverage]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Owned for the process lifetime by the long-running KafkaStream, like SchemaAvroSerDes.")]
internal sealed class MatchmakingEventSerDes : ISerDes<MatchmakingEvent>
{
    private readonly ConcurrentDictionary<int, bool> isProtobuf = new();
    private CachedSchemaRegistryClient? registry;
    private IDeserializer<GenericRecord>? avro;
    private IDeserializer<MatchmakingEvent>? proto;

    public void Initialize(SerDesContext context)
    {
        string registryUrl = Environment.GetEnvironmentVariable("SCHEMA_REGISTRY_URL")
            ?? "http://schema-registry:8081";
        registry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = registryUrl });
        avro = new AvroDeserializer<GenericRecord>(registry).AsSyncOverAsync();
        proto = new ProtobufDeserializer<MatchmakingEvent>().AsSyncOverAsync();
    }

    public MatchmakingEvent Deserialize(byte[] data, SerializationContext context)
    {
        if (data is null || data.Length == 0)
        {
            return null!;
        }

        int? schemaId = ConfluentFraming.TryReadSchemaId(data);
        return schemaId is int id && IsProtobuf(id)
            ? proto!.Deserialize(data, false, context)
            : MatchmakingAvroToProto.Map(avro!.Deserialize(data, false, context));
    }

    public byte[] Serialize(MatchmakingEvent data, SerializationContext context) =>
        throw new NotSupportedException("matchmaking.events.v1 is consume-only in this topology");

    public object? DeserializeObject(byte[] data, SerializationContext context) =>
        Deserialize(data, context);

    public byte[] SerializeObject(object data, SerializationContext context) =>
        Serialize((MatchmakingEvent)data, context);

    private bool IsProtobuf(int schemaId)
    {
        if (isProtobuf.TryGetValue(schemaId, out bool cached))
        {
            return cached;
        }

        Schema schema = registry!.GetSchemaAsync(schemaId).GetAwaiter().GetResult();
        bool result = schema.SchemaType == SchemaType.Protobuf;
        isProtobuf[schemaId] = result;
        return result;
    }
}
