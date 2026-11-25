using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

public sealed class LandSearchClassifierTests
{
    [Theory]
    [InlineData("Polluted Delta", "Land",
        "{T}, Pay 1 life, Sacrifice this land: Search your library for an Island or Swamp card, put it onto the battlefield, then shuffle.")]
    [InlineData("Krosan Verge", "Land",
        "This land enters tapped.\n{T}: Add {C}.\n{2}, {T}, Sacrifice this land: Search your library for a Forest card and a Plains card, put them onto the battlefield tapped, then shuffle.")]
    [InlineData("Evolving Wilds", "Land",
        "{T}, Sacrifice this land: Search your library for a basic land card, put it onto the battlefield tapped, then shuffle.")]
    [InlineData("Myriad Landscape", "Land",
        "This land enters tapped.\n{T}: Add {C}.\n{2}, {T}, Sacrifice this land: Search your library for up to two basic land cards that share a land type, put them onto the battlefield tapped, then shuffle.")]
    [InlineData("Prismatic Vista", "Land",
        "{T}, Pay 1 life, Sacrifice this land: Search your library for a basic land card, put it onto the battlefield, then shuffle.")]
    [InlineData("Flooded Strand", "Land",
        "{T}, Pay 1 life, Sacrifice this land: Search your library for a Plains or Island card, put it onto the battlefield, then shuffle.")]
    [InlineData("Misty Rainforest", "Land",
        "{T}, Pay 1 life, Sacrifice this land: Search your library for a Forest or Island card, put it onto the battlefield, then shuffle.")]
    public void IsLandOnlyFetchLand_LandWithOnlyLandSearches_ReturnsTrue(string name, string typeLine, string oracleText)
    {
        Assert.True(LandSearchClassifier.IsLandOnlyFetchLand(typeLine, oracleText), $"{name} ({typeLine})");
    }

    [Theory]
    [InlineData("Farseek", "Sorcery",
        "Search your library for a Plains, Island, Swamp, or Mountain card, put it onto the battlefield tapped, then shuffle.")]
    [InlineData("Troll of Khazad-dûm", "Creature — Troll",
        "This creature can't be blocked except by three or more creatures.\nSwampcycling {1} ({1}, Discard this card: Search your library for a Swamp card, reveal it, put it into your hand, then shuffle.)")]
    [InlineData("Lórien Revealed", "Sorcery",
        "Draw three cards.\nIslandcycling {1} ({1}, Discard this card: Search your library for an Island card, reveal it, put it into your hand, then shuffle.)")]
    [InlineData("Demonic Tutor", "Sorcery",
        "Search your library for a card, put that card into your hand, then shuffle.")]
    [InlineData("Urza's Saga", "Enchantment Land — Urza's Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — This Saga gains \"{T}: Add {C}.\"\nII — This Saga gains \"{2}, {T}: Create a 0/0 colorless Construct artifact creature token with 'This token gets +1/+1 for each artifact you control.'\"\nIII — Search your library for an artifact card with mana cost {0} or {1}, put it onto the battlefield, then shuffle.")]
    [InlineData("Tolaria West", "Land",
        "This land enters tapped.\n{T}: Add {U}.\nTransmute {1}{U}{U} ({1}{U}{U}, Discard this card: Search your library for a card with mana value 0, reveal it, put it into your hand, then shuffle. Transmute only as a sorcery.)")]
    [InlineData("Inventors' Fair", "Legendary Land",
        "At the beginning of your upkeep, if you control three or more artifacts, you gain 1 life.\n{T}: Add {C}.\n{4}, {T}, Sacrifice Inventors' Fair: Search your library for an artifact card, reveal it, put it into your hand, then shuffle. Activate only if you control three or more artifacts.")]
    [InlineData("Waterlogged Teachings", "Instant // Land",
        "Search your library for an instant card or a card with flash, reveal it, put it into your hand, then shuffle.")]
    [InlineData("Polluted Delta with unknown type", null,
        "{T}, Pay 1 life, Sacrifice this land: Search your library for an Island or Swamp card, put it onto the battlefield, then shuffle.")]
    [InlineData("Land without a search", "Land",
        "{T}: Add {C}.")]
    [InlineData("Land with empty oracle text", "Land",
        "")]
    public void IsLandOnlyFetchLand_NoQualifyingLandSearch_ReturnsFalse(string name, string? typeLine, string oracleText)
    {
        Assert.False(LandSearchClassifier.IsLandOnlyFetchLand(typeLine, oracleText), $"{name} ({typeLine})");
    }

    [Fact]
    public void IsLandOnlyFetchLand_NullOracleText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("oracleText", () => LandSearchClassifier.IsLandOnlyFetchLand("Land", null!));
    }
}
