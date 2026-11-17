using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabBasicLands"/> covering constant lookup and synthetic card data.</summary>
public sealed class CutLabBasicLandsTests
{
    [Theory]
    [InlineData("Plains", "Basic Land — Plains", new[] { "W" }, new[] { "W" })]
    [InlineData("Island", "Basic Land — Island", new[] { "U" }, new[] { "U" })]
    [InlineData("Swamp", "Basic Land — Swamp", new[] { "B" }, new[] { "B" })]
    [InlineData("Mountain", "Basic Land — Mountain", new[] { "R" }, new[] { "R" })]
    [InlineData("Forest", "Basic Land — Forest", new[] { "G" }, new[] { "G" })]
    [InlineData("Snow-Covered Plains", "Basic Snow Land — Plains", new[] { "W" }, new[] { "W" })]
    [InlineData("Snow-Covered Island", "Basic Snow Land — Island", new[] { "U" }, new[] { "U" })]
    [InlineData("Snow-Covered Swamp", "Basic Snow Land — Swamp", new[] { "B" }, new[] { "B" })]
    [InlineData("Snow-Covered Mountain", "Basic Snow Land — Mountain", new[] { "R" }, new[] { "R" })]
    [InlineData("Snow-Covered Forest", "Basic Snow Land — Forest", new[] { "G" }, new[] { "G" })]
    [InlineData("Wastes", "Basic Land", new string[0], new[] { "C" })]
    public void TryResolve_KnownBasic_ReturnsExpectedMetadata(
        string name,
        string expectedTypeLine,
        string[] expectedColorIdentity,
        string[] expectedProducedMana)
    {
        bool resolved = CutLabBasicLands.TryResolve(name, out var definition);

        Assert.True(resolved);
        Assert.NotNull(definition);
        Assert.Equal(expectedTypeLine, definition.TypeLine);
        Assert.Equal(expectedColorIdentity, definition.ColorIdentity);
        Assert.Equal(expectedProducedMana, definition.ProducedMana);
        Assert.True(CutLabBasicLands.Contains(name));
        Assert.Contains(name, CutLabBasicLands.Names);
        Assert.True(CutLabLockRules.IsLand(definition.TypeLine));
    }

    [Theory]
    [InlineData("Island", "Basic Land — Island", new[] { "U" }, new[] { "U" })]
    [InlineData("Wastes", "Basic Land", new string[0], new[] { "C" })]
    public void SyntheticCardData_KnownBasic_ReturnsLandScryfallCard(
        string name,
        string expectedTypeLine,
        string[] expectedColorIdentity,
        string[] expectedProducedMana)
    {
        ScryfallCardData card = CutLabBasicLands.SyntheticCardData(name);

        Assert.Equal(name, card.Name);
        Assert.Equal(expectedTypeLine, card.TypeLine);
        Assert.Equal(expectedColorIdentity, card.ColorIdentity ?? []);
        Assert.Equal(expectedProducedMana, card.ProducedMana ?? []);
        Assert.Equal(0, card.Cmc);
        Assert.Equal("normal", card.Layout);
        Assert.True(CutLabLockRules.IsLand(card.TypeLine));
        Assert.NotNull(card.OracleText);
    }

    // Why: Case and punctuation variants must resolve canonical basic-land metadata.
    [Theory]
    [InlineData("plains", "Basic Land — Plains")]
    [InlineData("SNOW-COVERED FOREST", "Basic Snow Land — Forest")]
    [InlineData("Snow Covered Forest", "Basic Snow Land — Forest")]
    [InlineData("wastes", "Basic Land")]
    public void TryResolve_CaseOrPunctuationVariant_ReturnsCanonicalDefinition(string name, string expectedTypeLine)
    {
        Assert.True(CutLabBasicLands.TryResolve(name, out CutLabBasicLands.Definition? definition));
        Assert.NotNull(definition);
        Assert.Equal(expectedTypeLine, definition.TypeLine);
    }

    // Why: Lowercase typed basics must be recognized by the membership lookup.
    [Theory]
    [InlineData("island")]
    [InlineData("snow-covered swamp")]
    public void Contains_CaseVariant_ReturnsTrue(string name)
    {
        Assert.True(CutLabBasicLands.Contains(name));
    }

    // Why: Synthetic basics must preserve typed names while resolving canonical metadata.
    [Fact]
    public void SyntheticCardData_LowercaseBasic_KeepsTypedNameAndResolvesMetadata()
    {
        ScryfallCardData card = CutLabBasicLands.SyntheticCardData("island");
        Assert.Equal("island", card.Name);
        Assert.Equal("Basic Land — Island", card.TypeLine);
    }

    // Why: Normalized lookups must not change canonical display spellings or their order.
    [Fact]
    public void Names_KeepCanonicalSpellingsInOrder()
    {
        Assert.Equal(new[]
        {
            "Plains", "Island", "Swamp", "Mountain", "Forest", "Snow-Covered Plains",
            "Snow-Covered Island", "Snow-Covered Swamp", "Snow-Covered Mountain",
            "Snow-Covered Forest", "Wastes",
        }, CutLabBasicLands.Names);
    }

    // Why: Normalized membership must not accept nonbasics or blank pool names.
    [Theory]
    [InlineData("Sol Ring")]
    [InlineData("  ")]
    public void Contains_NonBasicOrBlank_ReturnsFalse(string name)
    {
        Assert.False(CutLabBasicLands.Contains(name));
    }
}
