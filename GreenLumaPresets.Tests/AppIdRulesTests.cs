using GreenLumaPresets.Models;

namespace GreenLumaPresets.Tests;

public class AppIdRulesTests
{
    [Theory]
    [InlineData("620", "620")]
    [InlineData("  620\t", "620")]
    [InlineData("00620", "620")]
    [InlineData("4294967295", "4294967295")]
    public void TryNormalize_AcceptsPositiveIntegers(string input, string expected)
    {
        Assert.True(AppIdRules.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("12a")]
    [InlineData("1.5")]
    [InlineData("4294967296")]
    public void TryNormalize_RejectsInvalidValues(string? input)
    {
        Assert.False(AppIdRules.TryNormalize(input, out _));
    }
}
