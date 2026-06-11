# Match Maker Service

Handles session initialisation. Queues players looking for human opponents and pairs them by time control (and skill when the pool is large enough). Bot matches resolve immediately. Once an opponent is found, a match is created through the `IMatchCreator` seam (`Queue/`) and the resulting `match_id` is returned to the client.

## Contracts

- **REST:** `maichess-api-contracts/rest/match-maker.md`
- **gRPC client only** — no gRPC server; see `maichess-api-contracts/protos/match-maker-service/v1/matchmaker.proto`
- **Match Manager gRPC stubs:** reference `Maichess.PlatformProtos` (see `maichess-api-contracts/dotnet/`)

Implement against these contracts exactly. If a contract cannot be implemented as specified, document the blocker in `CONTRACT_NOTES.md` — do not silently deviate.

## Stack

- **Runtime:** ASP.NET (net10.0), C#, nullable enabled, AOT-compatible (`PublishAot = true`)
- **Queue storage:** Redis — use `StackExchange.Redis`
- **gRPC client:** `Grpc.Net.Client` with stubs from `Maichess.PlatformProtos`
- **JSON:** Use `[JsonSerializable]` source-generation; no runtime reflection


## Structure

```
MaichessMatchMakerService/
  Queue/           # Redis-backed queue: enqueue, dequeue, match, status
  Streaming/       # Streamiz user-ratings KTable + co-partitioned join (skill pairing)
  Rest/            # REST endpoint handlers (POST /queue, GET /queue/{token}/status, DELETE /queue/{token})
  Program.cs       # Startup: DI wiring, middleware, route registration
```

## Redis Data Model

Two keys per queued player:

| Key | Type | Contents |
|---|---|---|
| `queue:{time_control}` | Sorted set | Members are `queue_token`s, score is Unix timestamp at join time |
| `queue_entry:{queue_token}` | Hash | `user_id`, `time_control`, `status` (`waiting`/`matched`), `match_id` (once matched) |

**Lifecycle:**
1. `POST /queue` — add entry hash, push token into sorted set, return `queue_token`.
2. Matching loop — pop two tokens from sorted set, call `Matches.CreateMatch`, update both entry hashes.
3. `GET /queue/{token}/status` — read entry hash, return current `status` and `match_id` if matched. Client polls this until `status` is `matched`.
4. `DELETE /queue/{token}` — remove from sorted set and delete entry hash. No-op if already matched.

## Matching

- Match players with the same `time_control` only.
- When the queue for a given time control has ≥ 10 waiting players, pair by closest ELO; otherwise pair by longest wait (FIFO). Live ratings come from the **Streamiz user-ratings KTable** (`Streaming/`, fed by `user.events.v1`), read locally via interactive queries (`IUserRatingStore`) — no `GetUser` RPC. `MatchingService.ClosestRatedPair` picks the minimum-gap pair; flagged/unrated players are excluded and a lost race (`DequeueSpecificPairAsync` returns false) falls back to FIFO. See `maichess-knowledge-base/caching-and-read-models.md` (Stage 3) and `README.md`.
- Bot matches skip the queue entirely: create the match immediately and return `match_id` directly in the `POST /queue` response.

## Match creation transport (`IMatchCreator`)

Human-vs-human and human-vs-bot creation goes through the `Queue/IMatchCreator` seam, whose sole
implementation is `KafkaMatchCreator`: it mints the `match_id`, publishes a `CreateMatchCommand` to
`match.commands.v1` (raw Protobuf, fire-and-forget), and returns the minted id immediately. Match
Manager's command consumer materialises the document with that caller-minted id — so an immediate
read of a brand-new match can briefly 404 (accepted; the client uses optimistic UI + socket/poll
confirmation). The legacy synchronous `GrpcMatchCreator` (`Matches.CreateMatch`) and the
`Socket:Transport` flag were removed in Kafka task 09 — creation/notification are always Kafka.
Real-time notification likewise always goes through `KafkaMatchmakingNotifier`
(`socket.outbound.v1` + `matchmaking.events.v1`).

**Bot-vs-bot is not routed through the seam** — `QueueingService.CreateBotVsBotMatchAsync` stays on synchronous gRPC `Matches.CreateMatch` because it needs synchronous `start_fen` validation to return `invalid start_fen` to the caller. The `Matches.MatchesClient` gRPC client therefore remains wired.

## Code Style

- Prefer direct, readable code over clever abstractions
- One concern per class; keep classes small
- No dead code, no commented-out blocks, no TODOs left in merged code
- Use C# records for request/response models
- Use `[JsonSerializable]` source-generation for all types passed through `System.Text.Json`
- Validate inputs at REST boundaries; trust internal data after that
- No comments unless explaining a non-obvious algorithm — names carry intent

## Mutation Testing

Stryker.NET is wired up as a local dotnet tool. Config lives in
`MaichessMatchMakerService.Tests/stryker-config.json`. Run via
`dotnet tool restore` then `dotnet stryker` inside the test project directory.
See `README.md` for details.
