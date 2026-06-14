using MaichessMatchMakerService.Queue;

namespace MaichessMatchMakerService.Tests.Support;

// Deterministic IColorRandom: returns a configured coin-flip result and records how
// many times it was consulted, so tests can assert when the flip was (not) needed.
internal sealed class FakeColorRandom : IColorRandom
{
    private bool nextWhite = true;

    internal int Calls { get; private set; }

    internal void Returns(bool white) => nextWhite = white;

    public bool NextWhite()
    {
        Calls++;
        return nextWhite;
    }
}
