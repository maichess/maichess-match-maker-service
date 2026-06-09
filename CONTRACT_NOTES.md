# Contract Notes

## Event-driven migration (Kafka)

Per [event-driven-architecture.md](../../maichess-knowledge-base/event-driven-architecture.md).
Schemas are Avro in `maichess-api-contracts/events/v1/`.

### Phase 2 — shipped

- `matched` is published to `socket.outbound.v1` (user-targeted) instead of `Socket.EmitEvent`
  gRPC. `PlayersMatched` is published to `matchmaking.events.v1` on each human pairing.
- Transport is selected by the `Socket:Transport` setting (`kafka` default, `grpc` fallback)
  via `IMatchmakingNotifier` (`KafkaMatchmakingNotifier` / legacy `SocketNotifier`).
- **Dropped:** `Socket.EmitEvent` gRPC (in kafka mode).

### Blocked — `CreateMatch` over Kafka (deferred to Phase 3)

Moving match creation off `Matches.CreateMatch` gRPC onto a `CreateMatchCommand`
(`match.commands.v1`) is **blocked by a contract issue**: event sourcing requires the match
aggregate id to be **client-mintable** (it is the partition key for `match.commands`/
`match.events`), but `DatabaseService.Insert` ignores any supplied `id` and assigns its own
(`protos/database-service/v1/database.proto`). With server-assigned ids, the producer cannot put
the match id on the command, and the queue paths cannot route/correlate by it.

See [maichess-database-service `CONTRACT_NOTES.md`](../maichess-database-service/CONTRACT_NOTES.md)
for the proposed minimal change. Until it is approved and implemented, `Matches.CreateMatch`
stays gRPC for all three paths (human pairing, bot-from-queue, bot-vs-bot). `bot-vs-bot`
additionally needs the synchronous `INVALID_ARGUMENT` response to validate `start_fen`, so it is
a natural candidate to remain request/response regardless.

**Keeps (synchronous):** REST reads (`GET /time-formats`, `GET /bots`), `Matches.CreateMatch`.

### Protobuf event serde — implemented (Kafka task `01`)

The event/command schemas are now **Protobuf**, not Avro: `maichess-api-contracts/protos/events/v1/`
(`match_commands.proto`, `matchmaking_events.proto`, `socket_outbound.proto`, all package
`maichess.events.v1`). They mirror the `events/v1/*.avsc` field-for-field; the `.avsc` files stay in
place until each topic cuts over (task `02`).

Contracts **v0.6.0** is published; `Maichess.PlatformProtos` is pinned at `0.6.0` in
`MaichessMatchMakerService.csproj` (and `Confluent.SchemaRegistry.Serdes.Protobuf` is referenced).
Done:

1. `Queue/ProtobufEventSerdes.cs` — `Serializer<T>` / `Deserializer<T>` factory over the Confluent
   Protobuf serde + the generated `Maichess.Events.V1` types, alongside the Avro path. Serde
   plumbing only; **no producer/consumer is switched in task `01`** — the `IMatchCreator` /
   `IMatchmakingNotifier` seams are untouched here.
2. `MaichessMatchMakerService.Tests/ProtobufEventRoundTripTests.cs` — round-trips the envelope +
   every payload variant on the topics Match Maker produces (socket.outbound, matchmaking.events,
   match.commands).

**Local verify pending (auth handoff):** a fresh agent shell has no `GITHUB_TOKEN`, so
`dotnet restore` cannot pull `Maichess.PlatformProtos@0.6.0` from GitHub Packages (401). Run
`dotnet test -p:CollectCoverage=true` where the token is available to confirm.

---

## GET /bots — description field not covered by tests

The `GET /bots` response shape (including the `description` field added in the bot-description feature) is not exercised by any automated test. The `QueueEndpoints` class is marked `[ExcludeFromCodeCoverage]` because it is a thin HTTP adapter; the `BotResponse` record is excluded for the same reason. A contract-level integration test that calls the real endpoint would be the appropriate place to assert `description` is present and non-empty, but that requires a running gRPC engine stub and is out of scope for the current unit test infrastructure.
