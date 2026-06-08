namespace MaichessMatchMakerService.Streaming;

// The per-user value held in the Match Maker KTable (materialised from user.events.v1).
// Carries the live Glicko-2 rating used for skill-based pairing and the anti-cheat
// `flagged` bit (populated by a later stage — cheat.events; defaults false here).
// HasRating distinguishes "rating not yet materialised" (e.g. only a UserRegistered
// snapshot seen) from a genuine value, so pairing never pairs against a default.
internal sealed record UserRatingState(double Rating, double RatingDeviation, bool Flagged, bool HasRating)
{
    public static UserRatingState Empty { get; } = new(0, 0, false, false);
}
