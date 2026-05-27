using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class MdfcDetectionTests
{
    [Theory]
    [InlineData("Land")]
    [InlineData("Land — Desert")]
    public void IsModalDfcLand_ReturnsTrue_WhenModalDfcBackFaceIsLand(string backFaceTypeLine)
    {
        var card = CreateCard(
            "modal_dfc",
            [
                new ScryfallCardFace("Spikefield Hazard", "{R}", "Instant", "Spikefield Hazard deals 1 damage to any target.", null, null),
                new ScryfallCardFace("Spikefield Cave", null, backFaceTypeLine, "Spikefield Cave enters tapped.", null, null)
            ]);

        Assert.True(DeckAnalysisPacketService.IsModalDfcLand(card));
    }

    [Fact]
    public void IsModalDfcLand_ReturnsFalse_WhenModalDfcFacesAreNonLands()
    {
        var card = CreateCard(
            "modal_dfc",
            [
                new ScryfallCardFace("Test Front", "{1}{U}", "Instant", "Draw a card.", null, null),
                new ScryfallCardFace("Test Back", "{2}{R}", "Sorcery", "Deal 2 damage to any target.", null, null)
            ]);

        Assert.False(DeckAnalysisPacketService.IsModalDfcLand(card));
    }

    [Fact]
    public void IsModalDfcLand_ReturnsFalse_WhenTransformBackFaceIsLand()
    {
        var card = CreateCard(
            "transform",
            [
                new ScryfallCardFace("Test Front", "{1}{G}", "Creature", "When this creature enters, draw a card.", "2", "2"),
                new ScryfallCardFace("Test Back", null, "Land", "{T}: Add {G}.", null, null)
            ]);

        Assert.False(DeckAnalysisPacketService.IsModalDfcLand(card));
    }

    [Fact]
    public void IsModalDfcLand_ReturnsFalse_WhenCardFacesAreNull()
    {
        var card = CreateCard("modal_dfc", null);

        Assert.False(DeckAnalysisPacketService.IsModalDfcLand(card));
    }

    private static ScryfallCard CreateCard(string layout, IReadOnlyList<ScryfallCardFace>? faces)
        => new(
            "Test Card",
            null,
            "Instant // Land",
            null,
            null,
            null,
            [],
            [],
            null,
            null,
            null,
            faces,
            Layout: layout);
}
