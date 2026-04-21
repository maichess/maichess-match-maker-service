# Match Maker Service

Handles session initialisation. Queues players looking for human opponents and pairs them by time control (and skill when the pool is large enough). Bot matches resolve immediately. Once an opponent is found, calls `Matches.CreateMatch` on Match Manager via gRPC and returns the resulting `match_id` to the client.

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

## Contract Deviation

The REST contract specifies `GET /queue/{queue_token}/events` as a Server-Sent Events stream. SSE is **not implemented yet**. Instead, implement `GET /queue/{queue_token}/status` as a simple polling endpoint that returns the current queue entry state. Document this deviation in `CONTRACT_NOTES.md`.

## Structure

```
MaichessMatchMakerService/
  Queue/           # Redis-backed queue: enqueue, dequeue, match, status
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
- When the queue for a given time control has ≥ 10 waiting players, prefer pairing by closest ELO; otherwise pair by longest wait (FIFO).
- Bot matches skip the queue entirely: call `Matches.CreateMatch` immediately and return `match_id` directly in the `POST /queue` response.

## Code Style

- Prefer direct, readable code over clever abstractions
- One concern per class; keep classes small
- No dead code, no commented-out blocks, no TODOs left in merged code
- Use C# records for request/response models
- Use `[JsonSerializable]` source-generation for all types passed through `System.Text.Json`
- Validate inputs at REST boundaries; trust internal data after that
- No comments unless explaining a non-obvious algorithm — names carry intent
