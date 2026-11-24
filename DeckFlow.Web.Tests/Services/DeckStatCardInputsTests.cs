using DeckFlow.Web.Services;

using Xunit;

namespace DeckFlow.Web.Tests.Services;

/// <summary>Tests the oracle text inputs used by deck stat tallies.</summary>
public sealed class DeckStatCardInputsTests
{
    [Fact]
    public void FaceOracleTextsFor_MultiFaceCard_ReturnsOneEntryPerFaceInOrder()
    {
        const string front = "Destroy target creature or planeswalker.";
        const string back = "As this land enters, you may pay 3 life. If you don't, it enters tapped.\n{T}: Add {B}.";
        var card = new ScryfallCard(
            "Fell the Profane // Fell Mire", null, "Instant // Land", null,
            null, null, [], ["B"], null, null, null,
            [
                new ScryfallCardFace("Fell the Profane", "{2}{B}{B}", "Instant", front, null, null),
                new ScryfallCardFace("Fell Mire", "", "Land", back, null, null)
            ],
            Layout: "modal_dfc");

        Assert.Equal([front, back], DeckStatCardInputs.FaceOracleTextsFor(card));
    }

    [Fact]
    public void FaceOracleTextsFor_SingleFaceCard_ReturnsNull()
    {
        var card = new ScryfallCard(
            "Lightning Bolt", "{R}", "Instant", "Lightning Bolt deals 3 damage to any target.",
            null, null, [], ["R"], null, null, null);

        Assert.Null(DeckStatCardInputs.FaceOracleTextsFor(card));
    }
}
