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
        Assert.Null(data.ColorIdentity);
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

    [Fact]
    public void ToCardData_CopiesColorIdentity()
    {
        var card = new ScryfallCard(
            Name: "Grand Arbiter Augustin IV", ManaCost: "{2}{W}{U}", TypeLine: "Legendary Creature — Human Advisor", OracleText: "White spells you cast cost {1} less to cast.",
            Power: "2", Toughness: "3", Keywords: null, ColorIdentity: new[] { "W", "U" },
            SetCode: "dis", SetName: "Dissension", CollectorNumber: "112", CardFaces: null, Id: null,
            Layout: "normal", Cmc: 4, ProducedMana: null, Rarity: "rare");

        var data = ScryfallCardDataMapper.ToCardData(card);

        Assert.Equal(new[] { "W", "U" }, data.ColorIdentity);
    }

    [Fact]
    public void ToCardData_CopiesCardLevelPowerAndToughness()
    {
        var card = new ScryfallCard(
            Name: "Watchwolf", ManaCost: "{G}{W}", TypeLine: "Creature — Wolf", OracleText: null,
            Power: "3", Toughness: "3", Keywords: null, ColorIdentity: new[] { "G", "W" },
            SetCode: "rav", SetName: "Ravnica: City of Guilds", CollectorNumber: "233", CardFaces: null, Id: null,
            Layout: "normal", Cmc: 2, ProducedMana: null, Rarity: "uncommon");

        var data = ScryfallCardDataMapper.ToCardData(card);

        Assert.Equal("3", data.Power);
        Assert.Equal("3", data.Toughness);
    }

    [Fact]
    public void ToCardData_CopiesFaceLevelPowerAndToughness()
    {
        var card = new ScryfallCard(
            Name: "Delver of Secrets // Insectile Aberration", ManaCost: "{U}", TypeLine: "Creature — Human Wizard", OracleText: null,
            Power: "1", Toughness: "1", Keywords: null, ColorIdentity: new[] { "U" },
            SetCode: "isd", SetName: "Innistrad", CollectorNumber: "51",
            CardFaces: new[]
            {
                new ScryfallCardFace("Delver of Secrets", "{U}", "Creature — Human Wizard", "At the beginning of your upkeep, look at the top card of your library.", "1", "1"),
                new ScryfallCardFace("Insectile Aberration", null, "Creature — Human Insect", "Flying", "3", "2"),
            },
            Id: null, Layout: "transform", Cmc: 1, ProducedMana: null, Rarity: "common");

        var data = ScryfallCardDataMapper.ToCardData(card);

        Assert.NotNull(data.CardFaces);
        Assert.Equal("1", data.CardFaces![0].Power);
        Assert.Equal("1", data.CardFaces[0].Toughness);
        Assert.Equal("3", data.CardFaces[1].Power);
        Assert.Equal("2", data.CardFaces[1].Toughness);
    }
}
