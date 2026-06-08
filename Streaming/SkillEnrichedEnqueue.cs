namespace MaichessMatchMakerService.Streaming;

// Output of the co-partitioned stream-table join: a PlayerEnqueued event tagged with
// the player's live rating from the KTable. Only emitted for players the KTable knows
// (inner join), so a queued player with no materialised rating is excluded from the
// enriched stream — the join's natural "exclude appropriately" behaviour.
internal sealed record SkillEnrichedEnqueue(
    string PlayerId,
    string QueueToken,
    string TimeFormatId,
    double Rating,
    bool Flagged);
