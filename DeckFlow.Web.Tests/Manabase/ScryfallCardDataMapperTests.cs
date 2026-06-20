using System.Linq;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Manabase;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Validates <see cref="ScryfallCardDataMapper"/>: field carry-over including the mana-base
/// fields and multi-faced card faces.
/// </summary>
public sealed class ScryfallCardDataMapperTests
{
    [Fact]
    public void ToCardData_CarriesManabaseFields()
    {
        var card = new ScryfallCard(
            Name: "Breeding Pool", ManaCost: null, TypeLine: "Land — Forest Island", OracleText: "({T}: Add {G} or {U}.)",
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: "grn", SetName: "Guilds of Ravnica", CollectorNumber: "246", CardFaces: null, Id: null,
            Layout: "normal", Cmc: 0, ProducedMana: new[] { "G", "U" }, Rarity: "rare");

        var data = ScryfallCardDataMapper.ToCardData(card);

        Assert.Equal("Breeding Pool", data.Name);
        Assert.Equal(0, data.Cmc);
        Assert.Equal(new[] { "G", "U" }, data.ProducedMana);
        Assert.Equal("rare", data.Rarity);
        Assert.Equal("grn", data.Set);
        Assert.Equal("246", data.CollectorNumber);
        Assert.Equal("normal", data.Layout);
        Assert.Null(data.CardFaces);
    }

    [Fact]
    public void ToCardData_MapsMultiFacedCardFaces()
    {
        var card = new ScryfallCard(
            Name: "Fire // Ice", ManaCost: null, TypeLine: "Instant // Instant", OracleText: null,
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: "apc", SetName: "Apocalypse", CollectorNumber: "128",
            CardFaces: new[]
            {
                new ScryfallCardFace("Fire", "{1}{R}", "Instant", "Deal 2 damage.", null, null),
                new ScryfallCardFace("Ice", "{1}{U}", "Instant", "Tap target permanent.", null, null),
            },
            Id: null, Layout: "split", Cmc: 4, ProducedMana: null, Rarity: "uncommon");

        var data = ScryfallCardDataMapper.ToCardData(card);

        Assert.NotNull(data.CardFaces);
        Assert.Equal(2, data.CardFaces!.Count);
        Assert.Equal("Fire", data.CardFaces[0].Name);
        Assert.Equal("{1}{R}", data.CardFaces[0].ManaCost);
        Assert.Equal("Ice", data.CardFaces[1].Name);
    }
}
