namespace MaichessMatchMakerService.Streaming;

// Local lookup of a user's live rating from the Match Maker KTable's state store.
// Returns null when the user is unknown or the store is not yet queryable (stream
// warming / rebalancing), in which case skill-based pairing falls back to FIFO.
internal interface IUserRatingStore
{
    UserRatingState? TryGet(string userId);
}
