# Contract Notes

## GET /queue/{queue_token}/events — SSE not implemented

The REST contract defines this endpoint as a Server-Sent Events stream (`Content-Type: text/event-stream`).

**Current implementation:** `GET /queue/{queue_token}/status` returns a JSON snapshot instead. Clients poll until `status` is `matched`.

```json
{ "status": "waiting", "match_id": null }
{ "status": "matched", "match_id": "a1b2c3d4-..." }
```

**Proposed adjustment:** Replace with SSE in a later iteration once the polling approach is validated end-to-end.
