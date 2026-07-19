using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for the Cut Lab pool-length and non-commander card-count validation guards.</summary>
public sealed class CutLabPoolValidatorTests
{
    [Theory]
    [InlineData(100)] // 101 total with commander -> reject
    [InlineData(50)]
    public void ValidateCardCount_CountAtOrBelowMaximumDeckSize_ThrowsAlreadyAtOrBelow100Message(int nonCommanderCardCount)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CutLabPoolValidator.ValidateCardCount(nonCommanderCardCount));

        Assert.Equal(
            "This pool already has 100 cards or fewer — Cut Lab is for trimming an oversized pool down to 100. Try Deck Sync or Deck Analysis instead.",
            exception.Message);
    }

    [Theory]
    [InlineData(101)] // 102 total with commander -> valid lower bound
    [InlineData(150)] // 151 total = 150 pool + commander -> VALID
    public void ValidateCardCount_CountWithin101To150Inclusive_DoesNotThrow(int nonCommanderCardCount)
    {
        var exception = Record.Exception(() => CutLabPoolValidator.ValidateCardCount(nonCommanderCardCount));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(151)] // 152 total with commander -> reject
    [InlineData(300)]
    public void ValidateCardCount_CountAboveSupportedRange_ThrowsExceedsCapMessage(int nonCommanderCardCount)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CutLabPoolValidator.ValidateCardCount(nonCommanderCardCount));

        Assert.Equal(
            "This pool has too many cards for Cut Lab (limit 150 plus commander). Trim it closer to 150 before importing.",
            exception.Message);
    }

    [Fact]
    public void ValidateCardCount_OutOfRangeBranches_UseDistinctMessages()
    {
        var tooSmall = Assert.Throws<InvalidOperationException>(() => CutLabPoolValidator.ValidateCardCount(100));
        var tooLarge = Assert.Throws<InvalidOperationException>(() => CutLabPoolValidator.ValidateCardCount(151));

        Assert.NotEqual(tooSmall.Message, tooLarge.Message);
    }

    [Fact]
    public void ValidateSourceLength_LengthAboveMaximum_ThrowsOversizedInputMessage()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CutLabPoolValidator.ValidateSourceLength(CutLabPoolValidator.MaxDeckSourceChars + 1));

        Assert.Contains("That deck input is too large to import.", exception.Message, StringComparison.Ordinal);
    }
}
