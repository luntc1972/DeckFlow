using System;
using DeckFlow.Core.Normalization;
using Xunit;

namespace DeckFlow.Core.Tests.Normalization;

public sealed class NormalizedCardNameSetTests
{
    [Theory]
    [InlineData("thalia guardian of thraben")]
    [InlineData("Thalia, Guardian of Thraben")]
    [InlineData("Boom")]
    [InlineData("Boom / Bust")]
    [InlineData("BOOM // BUST")]
    public void Contains_CasePunctuationOrFaceVariant_ReturnsTrue(string name)
    {
        var names = new NormalizedCardNameSet(["Thalia, Guardian of Thraben", "Boom // Bust"]);

        Assert.True(names.Contains(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("★")]
    public void Contains_NullBlankOrPunctuationOnly_ReturnsFalse(string? name)
    {
        var names = new NormalizedCardNameSet(["Plains"]);

        Assert.False(names.Contains(name));
    }

    [Fact]
    public void Contains_UnlistedName_ReturnsFalse()
    {
        var names = new NormalizedCardNameSet(["Plains"]);

        Assert.False(names.Contains("Sol Ring"));
    }

    [Theory]
    [InlineData("plains", "Plains")]
    [InlineData("SNOW COVERED PLAINS", "Snow-Covered Plains")]
    [InlineData("Snow-Covered Plains", "Snow-Covered Plains")]
    public void TryGetCanonicalName_Variant_ReturnsListedSpelling(string name, string expected)
    {
        var names = new NormalizedCardNameSet(["Plains", "Snow-Covered Plains"]);

        Assert.True(names.TryGetCanonicalName(name, out var canonicalName));
        Assert.Equal(expected, canonicalName, StringComparer.Ordinal);
    }

    [Fact]
    public void TryGetCanonicalName_UnlistedOrNull_ReturnsFalseAndNull()
    {
        var names = new NormalizedCardNameSet(["Plains"]);

        Assert.False(names.TryGetCanonicalName("Island", out var canonicalName));
        Assert.Null(canonicalName);
        Assert.False(names.TryGetCanonicalName(null, out canonicalName));
        Assert.Null(canonicalName);
    }

    [Fact]
    public void Constructor_DuplicateNormalizedKey_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new NormalizedCardNameSet(["Plains", "plains"]));

        Assert.Contains("'Plains'", exception.Message);
        Assert.Contains("'plains'", exception.Message);
    }

    [Fact]
    public void Constructor_InvalidInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new NormalizedCardNameSet(null!));
        Assert.Throws<ArgumentException>(() => new NormalizedCardNameSet([null!]));
        Assert.Throws<ArgumentException>(() => new NormalizedCardNameSet([" "]));
        Assert.Throws<ArgumentException>(() => new NormalizedCardNameSet(["★"]));
    }
}
