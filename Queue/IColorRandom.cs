namespace MaichessMatchMakerService.Queue;

// Abstraction over the coin flip used when color assignment is ambiguous (two
// players who asked for the same fixed color, or a vs-bot "random" request). The
// indirection keeps assignment deterministic under test.
internal interface IColorRandom
{
    // True assigns White to the primary candidate (the first paired player, or the
    // human in a vs-bot match); false assigns Black.
    bool NextWhite();
}

internal sealed class DefaultColorRandom : IColorRandom
{
    private readonly Random random;

    public DefaultColorRandom()
        : this(Random.Shared)
    {
    }

    internal DefaultColorRandom(Random random)
    {
        this.random = random;
    }

    public bool NextWhite() => random.Next(2) == 0;
}
