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
        Assert.True(definition.IsLand);
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
}
