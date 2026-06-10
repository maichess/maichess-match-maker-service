namespace MaichessMatchMakerService.Streaming;

// The per-user value held in the Match Maker KTable (materialised from user.events.v1).
// Carries the live Glicko-2 rating used for skill-based pairing. HasRating
// distinguishes "rating not yet materialised" (e.g. only a UserRegistered snapshot
// seen) from a genuine value, so pairing never pairs against a default. The anti-cheat
// flag is NOT part of this state: it rides cheat.events.v1 into the separate
// CheatFlagStore (same local-read-model idea, different topic and key source).
internal sealed record UserRatingState(double Rating, double RatingDeviation, bool HasRating)
{
    public static UserRatingState Empty { get; } = new(0, 0, false);
}
