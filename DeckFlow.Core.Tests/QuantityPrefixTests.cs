using DeckFlow.Core.Parsing;

namespace DeckFlow.Core.Tests;

/// <summary>Tests the quantity prefix shared by pasted-line parsers.</summary>
public sealed class QuantityPrefixTests
{
    [Theory]
    [InlineData("1 Sol Ring", "1", "Sol Ring")]
    [InlineData("1x Sol Ring", "1", "Sol Ring")]
    [InlineData("1X Sol Ring", "1", "Sol Ring")]
    [InlineData("12 Island", "12", "Island")]
    public void Pattern_WithQuantity_CapturesQuantityAndRest(string line, string quantity, string rest)
    {
        var match = QuantityPrefix.Pattern().Match(line);

        Assert.True(match.Success);
        Assert.Equal(quantity, match.Groups["quantity"].Value);
        Assert.Equal(rest, match.Groups["rest"].Value);
    }

    [Theory]
    [InlineData("Sol Ring")]
    [InlineData("x1 Sol Ring")]
    public void Pattern_WithoutValidQuantity_DoesNotMatch(string line)
    {
        Assert.DoesNotMatch(QuantityPrefix.Pattern(), line);
    }

    [Theory]
    [InlineData("１ Sol Ring")]
    [InlineData("١ Sol Ring")]
    public void Pattern_WithNonAsciiDigits_DoesNotMatch(string line)
    {
        Assert.DoesNotMatch(QuantityPrefix.Pattern(), line);
    }

    [Theory]
    [InlineData("1", true, 1)]
    [InlineData("999", true, 999)]
    [InlineData("0", false, 0)]
    [InlineData("1000", false, 1000)]
    [InlineData("99999999999", false, 0)]
    public void TryParseQuantity_EnforcesBounds(string value, bool expected, int expectedQuantity)
    {
        var success = QuantityPrefix.TryParseQuantity(value, out var quantity);

        Assert.Equal(expected, success);
        Assert.Equal(expectedQuantity, quantity);
    }
}
