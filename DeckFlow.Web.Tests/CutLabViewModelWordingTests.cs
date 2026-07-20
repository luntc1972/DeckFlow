using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabViewModelWordingTests
{
    [Theory]
    [InlineData(0, "0 cards")]
    [InlineData(1, "1 card")]
    [InlineData(2, "2 cards")]
    public void FormatCutsMadeCount_ReturnsExpectedCardWording(int count, string expected)
    {
        Assert.Equal(expected, CutLabViewModel.FormatCutsMadeCount(count));
    }

    [Theory]
    [InlineData(0, "0 cuts so far")]
    [InlineData(1, "1 cut so far")]
    [InlineData(2, "2 cuts so far")]
    public void FormatCutsAcceptedSoFar_ReturnsExpectedCutWording(int count, string expected)
    {
        Assert.Equal(expected, CutLabViewModel.FormatCutsAcceptedSoFar(count));
    }
}
