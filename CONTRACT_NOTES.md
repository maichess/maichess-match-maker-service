# Contract Notes

## GET /bots — description field not covered by tests

The `GET /bots` response shape (including the `description` field added in the bot-description feature) is not exercised by any automated test. The `QueueEndpoints` class is marked `[ExcludeFromCodeCoverage]` because it is a thin HTTP adapter; the `BotResponse` record is excluded for the same reason. A contract-level integration test that calls the real endpoint would be the appropriate place to assert `description` is present and non-empty, but that requires a running gRPC engine stub and is out of scope for the current unit test infrastructure.
