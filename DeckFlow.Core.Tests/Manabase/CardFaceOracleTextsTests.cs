using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

public sealed class CardFaceOracleTextsTests
{
    [Fact]
    public void From_MultiFaceCard_ReturnsOneEntryPerFaceInOrder()
    {
        const string front = "Draw a card.";
        const string back = "Search your library for a card, put it into your hand, then shuffle.";
        ScryfallCardData card = new()
        {
            Name = "Front // Back",
            CardFaces = [new() { OracleText = front }, new() { OracleText = back }]
        };

        Assert.Equal([front, back], CardFaceOracleTexts.From(card));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData(" ", " ")]
    public void From_FaceWithBlankText_KeepsFacePosition(string? faceText, string expected)
    {
        const string back = "Draw a card.";
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
    public void From_NullCard_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("card", () => CardFaceOracleTexts.From(null!));
    }
}
