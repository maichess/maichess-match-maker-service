using MaichessMatchMakerService.Queue;
using Xunit;

namespace MaichessMatchMakerService.Tests;

public sealed class TimeFormatRegistryTests
{
    public static IEnumerable<object[]> ExpectedPresets()
    {
        yield return ["1+0", 60_000L, 0L, "bullet"];
        yield return ["2+1", 120_000L, 1_000L, "bullet"];
        yield return ["3+0", 180_000L, 0L, "blitz"];
        yield return ["3+2", 180_000L, 2_000L, "blitz"];
        yield return ["5+0", 300_000L, 0L, "blitz"];
        yield return ["5+3", 300_000L, 3_000L, "blitz"];
        yield return ["10+0", 600_000L, 0L, "rapid"];
        yield return ["10+5", 600_000L, 5_000L, "rapid"];
        yield return ["15+10", 900_000L, 10_000L, "rapid"];
        yield return ["30+0", 1_800_000L, 0L, "classical"];
        yield return ["30+20", 1_800_000L, 20_000L, "classical"];
    }

    [Theory]
    [MemberData(nameof(ExpectedPresets))]
    public void Resolve_ReturnsPresetMatchingId(string id, long baseMs, long incrementMs, string category)
    {
        var preset = TimeFormatRegistry.Resolve(id);

        Assert.Equal(id, preset.Id);
        Assert.Equal(baseMs, preset.BaseMs);
        Assert.Equal(incrementMs, preset.IncrementMs);
        Assert.Equal(category, preset.Category);
    }

    [Fact]
    public void Presets_ContainsExactlyTheExpectedFormats()
    {
        var expectedIds = ExpectedPresets().Select(p => (string)p[0]).ToArray();
        var actualIds = TimeFormatRegistry.Presets.Select(p => p.Id).ToArray();

        Assert.Equal(expectedIds, actualIds);
    }

    [Theory]
    [InlineData("1+0")]
    [InlineData("2+1")]
    [InlineData("3+0")]
    [InlineData("3+2")]
    [InlineData("5+0")]
    [InlineData("5+3")]
    [InlineData("10+0")]
    [InlineData("10+5")]
    [InlineData("15+10")]
    [InlineData("30+0")]
    [InlineData("30+20")]
    public void IsKnown_ReturnsTrueForExpectedIds(string id)
    {
        Assert.True(TimeFormatRegistry.IsKnown(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("99+99")]
    public void IsKnown_ReturnsFalseForUnknownIds(string id)
    {
        Assert.False(TimeFormatRegistry.IsKnown(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("99+99")]
    public void Resolve_ThrowsArgumentExceptionForUnknownId(string id)
    {
        var ex = Assert.Throws<ArgumentException>(() => TimeFormatRegistry.Resolve(id));

        Assert.Equal("id", ex.ParamName);
        Assert.Contains(id, ex.Message, StringComparison.Ordinal);
    }
}
