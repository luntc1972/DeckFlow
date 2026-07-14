using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Tests;

public sealed class CategoryCanonicalizerTests
{
    [Theory]
    [InlineData("card draw", "Draw")]
    [InlineData("Lands", "Land")]
    [InlineData("Tokens & Extras", "Tokens")]
    public void Canonicalize_MappedVariant_ReturnsCanonicalLabel(string category, string expected)
    {
        Assert.Equal(expected, CategoryCanonicalizer.Canonicalize(category));
    }

    [Fact]
    public void CanonicalKey_CaseVariants_ReturnSameKey()
    {
        Assert.Equal(
            CategoryCanonicalizer.CanonicalKey("ramp"),
            CategoryCanonicalizer.CanonicalKey("Ramp"));
    }

    [Fact]
    public void Canonicalize_WhitespaceRuns_CollapsesWhitespace()
    {
        Assert.Equal("Big Mana", CategoryCanonicalizer.Canonicalize("  Big   Mana  "));
    }

    [Fact]
    public void CanonicalKey_AntiCollisionCategories_RemainDistinct()
    {
        Assert.NotEqual(
            CategoryCanonicalizer.CanonicalKey("Counter"),
            CategoryCanonicalizer.CanonicalKey("Counterspell"));
        Assert.NotEqual(
            CategoryCanonicalizer.CanonicalKey("Land"),
            CategoryCanonicalizer.CanonicalKey("Landfall"));
    }
}
