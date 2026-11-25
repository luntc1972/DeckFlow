using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

public sealed class CardFaceOracleTextsTests
{
    [Theory]
    [InlineData("Draw a card.", "Draw a card.")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData(" ", " ")]
    public void From_MultiFaceCard_KeepsFacePositions(string? faceText, string expected)
    {
        const string back = "Search your library for a card, put it into your hand, then shuffle.";
        ScryfallCardData card = new()
        {
            Name = "Front // Back",
            CardFaces = [new() { OracleText = faceText }, new() { OracleText = back }]
        };

        Assert.Equal([expected, back], CardFaceOracleTexts.From(card));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void From_CardWithoutFaces_ReturnsEmptyList(bool hasEmptyFaceList)
    {
        ScryfallCardData card = new()
        {
            Name = "Single Face",
            OracleText = "Draw a card.",
            CardFaces = hasEmptyFaceList ? [] : null
        };

        Assert.Empty(CardFaceOracleTexts.From(card));
    }

    [Fact]
    public void From_NullFaceTexts_ReturnsEmptyList()
    {
        IEnumerable<string?>? faceTexts = null;

        Assert.Empty(CardFaceOracleTexts.From(faceTexts));
    }

    [Fact]
    public void From_NullCard_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("card", () => CardFaceOracleTexts.From((ScryfallCardData)null!));
    }
}
