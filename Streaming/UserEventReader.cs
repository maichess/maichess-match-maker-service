using Maichess.Events.V1;

namespace MaichessMatchMakerService.Streaming;

// The KTable aggregator that folds each user.events.v1 UserEvent into the running
// UserRatingState. Only RatingUpdated carries a rating, so other event types leave the
// accumulated rating untouched (a plain compacted MapValues of the latest event would
// otherwise clobber a rating with a profile-only update). The anti-cheat flag is not a
// user.events fact — it lives in the CheatFlagStore fed by cheat.events.v1.
internal static class UserEventReader
{
    internal static UserRatingState Apply(UserRatingState current, UserEvent envelope) =>
        envelope.PayloadCase == UserEvent.PayloadOneofCase.RatingUpdated
            ? current with
            {
                Rating = envelope.RatingUpdated.Rating,
                RatingDeviation = envelope.RatingUpdated.RatingDeviation,
                HasRating = true,
            }
            : current;
}
