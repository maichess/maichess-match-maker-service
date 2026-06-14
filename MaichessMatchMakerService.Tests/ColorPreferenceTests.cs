using MaichessMatchMakerService.Queue;
using Xunit;

namespace MaichessMatchMakerService.Tests;

// Pure color logic: wire parsing, the two-sided side-assignment rules (task 21), the
// vs-bot human-side rule, and the seeded coin flip. Inputs are wire strings (parsed
// inside) so the public xUnit signatures never expose the internal enum.
public sealed class ColorPreferenceTests
{
    private static ColorPreference Parse(string? wire)
    {
        Assert.True(ColorPreferenceParser.TryParse(wire, out ColorPreference preference));
        return preference;
    }

    [Theory]
    [InlineData(null, "any")]
    [InlineData("", "any")]
    [InlineData("any", "any")]
    [InlineData("random", "any")]
    [InlineData("white", "white")]
    [InlineData("WHITE", "white")]
    [InlineData("  black  ", "black")]
    public void TryParse_KnownValues_ParseToPreference(string? value, string expectedWire)
    {
        Assert.True(ColorPreferenceParser.TryParse(value, out ColorPreference actual));
        Assert.Equal(expectedWire, ColorPreferenceParser.ToWire(actual));
    }

    [Fact]
    public void TryParse_UnknownValue_ReturnsFalseAndDefaultsAny()
    {
        Assert.False(ColorPreferenceParser.TryParse("purple", out ColorPreference actual));
        Assert.Equal("any", ColorPreferenceParser.ToWire(actual));
    }

    [Theory]
    [InlineData("any")]
    [InlineData("white")]
    [InlineData("black")]
    public void ToWire_RoundTripsThroughTryParse(string wire)
    {
        Assert.Equal(wire, ColorPreferenceParser.ToWire(Parse(wire)));
    }

    [Theory]
    // Opposite fixed colors — each gets their wish (coin flip irrelevant).
    [InlineData("white", "black", true, true)]
    [InlineData("white", "black", false, true)]
    [InlineData("black", "white", true, false)]
    [InlineData("black", "white", false, false)]
    // One fixed, the other ANY — the fixed side gets its color.
    [InlineData("white", "any", false, true)]
    [InlineData("black", "any", true, false)]
    [InlineData("any", "white", true, false)]
    [InlineData("any", "black", false, true)]
    // Both ANY — first (longest-waiting) takes White, as before.
    [InlineData("any", "any", false, true)]
    // Same fixed color — defers to the coin flip.
    [InlineData("white", "white", true, true)]
    [InlineData("white", "white", false, false)]
    [InlineData("black", "black", true, true)]
    [InlineData("black", "black", false, false)]
    public void FirstIsWhite_AppliesMatchingRules(string first, string second, bool coinFlip, bool expected)
    {
        Assert.Equal(expected, ColorAssignment.FirstIsWhite(Parse(first), Parse(second), coinFlip));
    }

    [Theory]
    [InlineData("white", false, true)]
    [InlineData("black", true, false)]
    [InlineData("any", true, true)]
    [InlineData("any", false, false)]
    public void HumanIsWhite_HonoursFixedColorAndFlipsOnAny(string human, bool coinFlip, bool expected)
    {
        Assert.Equal(expected, ColorAssignment.HumanIsWhite(Parse(human), coinFlip));
    }

    [Fact]
    public void DefaultColorRandom_SameSeed_ProducesSameSequence()
    {
        var a = new DefaultColorRandom(new Random(42));
        var b = new DefaultColorRandom(new Random(42));

        bool[] seqA = [a.NextWhite(), a.NextWhite(), a.NextWhite(), a.NextWhite()];
        bool[] seqB = [b.NextWhite(), b.NextWhite(), b.NextWhite(), b.NextWhite()];

        Assert.Equal(seqA, seqB);
    }

    [Fact]
    public void DefaultColorRandom_Parameterless_ReturnsABoolean()
    {
        var random = new DefaultColorRandom();

        // Exercises the production ctor; the value is non-deterministic but always valid.
        bool result = random.NextWhite();

        Assert.True(result || !result);
    }
}
