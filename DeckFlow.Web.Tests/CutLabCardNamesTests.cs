using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Verifies display names preserve canonical card identity outside display text.</summary>
public sealed class CutLabCardNamesTests
{
    [Fact]
    public void ToDisplayName_ModalDoubleFacedName_ReplacesSeparatorWithSingleSlash()
    {
        Assert.Equal("Witch Enchanter / Witch-Blessed Meadow", CutLabCardNames.ToDisplayName("Witch Enchanter // Witch-Blessed Meadow"));
    }

    [Fact]
    public void ToDisplayName_SingleFacedName_ReturnsInputUnchanged()
    {
        Assert.Equal("Sol Ring", CutLabCardNames.ToDisplayName("Sol Ring"));
    }

    [Fact]
    public void ToDisplayName_Null_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, CutLabCardNames.ToDisplayName(null));
    }

    [Theory]
    [InlineData("A//B")]
    [InlineData("https://scryfall.com/card/x")]
    public void ToDisplayName_SlashesWithoutSurroundingSpaces_ReturnsInputUnchanged(string text)
    {
        Assert.Equal(text, CutLabCardNames.ToDisplayName(text));
    }

    [Fact]
    public void ToDisplayName_SentenceWithTwoMdfcNames_ReplacesEveryOccurrence()
    {
        Assert.Equal("cutting A / B and C / D.", CutLabCardNames.ToDisplayName("cutting A // B and C // D."));
    }
}
