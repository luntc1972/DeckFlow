using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Tests;

public sealed class CategoryFilterTests
{
    [Theory]
    [InlineData("3")]
    [InlineData("01")]
    [InlineData("x")]
    [InlineData("This category label is definitely longer than forty chars")]
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
