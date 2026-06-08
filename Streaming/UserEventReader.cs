using System.Globalization;
using Avro.Generic;

namespace MaichessMatchMakerService.Streaming;

// The KTable aggregator that folds each user.events.v1 envelope into the running
// UserRatingState. Only RatingUpdated carries a rating, so other event types leave the
// accumulated rating untouched (a plain compacted MapValues of the latest event would
// otherwise clobber a rating with a profile-only update). flagged has no source on
// user.events yet (arrives via cheat.events in a later stage), so it is carried through.
internal static class UserEventReader
{
    internal static UserRatingState Apply(UserRatingState current, GenericRecord envelope) =>
        AvroPayload.TryGet(envelope, out GenericRecord payload) && payload.Schema.Name == "RatingUpdated"
            ? current with
            {
                Rating = Dbl(payload, "rating"),
                RatingDeviation = Dbl(payload, "rating_deviation"),
                HasRating = true,
            }
            : current;

    // RatingUpdated is schema-validated upstream, so the numeric fields are always present.
    private static double Dbl(GenericRecord r, string field) =>
        Convert.ToDouble(r[field], CultureInfo.InvariantCulture);
}
