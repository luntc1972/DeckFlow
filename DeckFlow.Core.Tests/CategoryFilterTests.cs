using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Tests;

public sealed class CategoryFilterTests
{
    [Theory]
    [InlineData("3")]
    [InlineData("01")]
    [InlineData("3-Drop")]
    [InlineData("Turn 1")]
    [InlineData("x")]
    [InlineData("cards that also draw more cards")]
    [InlineData("This category label is definitely longer than forty chars")]
    [InlineData("Ramp, Fixing")]
    [InlineData("Card Draw; Advantage")]
    [InlineData("Board Wipe.")]
    [InlineData("Reanimate!")]
    [InlineData("WTF?")]
    [InlineData("Value...")]
    [InlineData("PUMP✊")]
    [InlineData("комбо")]
    public void IsJunk_JunkCategory_ReturnsTrue(string category)
    {
        Assert.True(CategoryFilter.IsJunk(category));
    }

    [Theory]
    [InlineData("Draw Two Or More")]
    [InlineData("Board Wipe")]
    [InlineData("Card Advantage")]
    [InlineData("Extra Combat Step")]
    [InlineData("Aristocrat's Payoff")]
    [InlineData("Enters-The-Battlefield")]
    public void IsJunk_UsefulCategory_ReturnsFalse(string category)
    {
        Assert.False(CategoryFilter.IsJunk(category));
    }

    [Theory]
    [InlineData("Maybeboard")]
    [InlineData("Mainboard")]
    public void IsIncluded_ExcludedBoardCategory_ReturnsFalse(string category)
    {
        Assert.False(CategoryFilter.IsIncluded(category));
    }

    [Fact]
    public void IncludedOrFallback_AllCategoriesAreJunk_ReturnsEmpty()
    {
        var result = CategoryFilter.IncludedOrFallback(["3", "WTF?", "PUMP✊"]);

        Assert.Empty(result);
    }

    [Fact]
    public void IncludedOrFallback_OnlyGenericTypeSurvives_ReturnsFallbackCategory()
    {
        var result = CategoryFilter.IncludedOrFallback(["Creature"]);

        Assert.Equal(["Creature"], result);
    }
}
