# maichess-match-maker-service

See `CLAUDE.md` for architecture, contracts, and design notes.

## Streamiz user-ratings KTable (skill-based pairing)

This service hosts the platform's single **Streamiz** (`Streamiz.Kafka.Net`) topology —
the deliberate one-KTable exception to the Redis-replica default (see
`maichess-knowledge-base/caching-and-read-models.md`). It materialises the compacted
`user.events.v1` into a RocksDb-backed KTable of live ratings and joins it onto
`matchmaking.events.v1` (`PlayerEnqueued`), co-partitioned on player/user id, so
skill-based pairing reads ratings locally instead of calling `GetUser`.

- **Code:** `Streaming/` (`UserRatingTopology`, `MatchMakerStreams`, the pure readers).
- **State store:** RocksDb at `STREAMIZ_STATE_DIR` (default `/var/lib/match-maker/streamiz`;
  the chart mounts a volume there). Rebuildable from its changelog topic
  `match-maker-user-ratings-user-ratings-store-changelog`.
- **Co-partition requirement:** `matchmaking.events.v1` and `user.events.v1` must share
  partition count (both 3 in the chart) for the join.
- **Config:** `KAFKA_BOOTSTRAP`, `STREAMIZ_STATE_DIR`. (Kafka task 09 removed the Confluent Schema
  Registry — events are raw Protobuf bytes, so there is no `SCHEMA_REGISTRY_URL`.)
- **Tests:** the topology is unit-tested with Streamiz's `TopologyTestDriver`
  (`MaichessMatchMakerService.Tests/Streaming`).

## Mutation Testing (Stryker.NET)

Stryker is installed as a local .NET tool. Configuration lives in
`MaichessMatchMakerService.Tests/stryker-config.json`.

```powershell
# First time on a clean checkout — restore the local tool
dotnet tool restore

# Run mutation tests (from the test project directory)
cd MaichessMatchMakerService.Tests
dotnet stryker
```

After the run, open `StrykerOutput/<timestamp>/reports/mutation-report.html`
in a browser to inspect surviving mutants.

To bump the Stryker version: `dotnet tool update dotnet-stryker`.
