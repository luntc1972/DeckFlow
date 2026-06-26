using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Grace-window seam tests for the castability simulator. The default path keeps the current
/// uniform +1 tolerance; the strict-P1 path removes the extra turn for 1-drops only.
/// </summary>
public sealed class GraceWindowTests
{
    [Theory]
    [InlineData(1, false, 1)]
    [InlineData(1, true, 0)]
    [InlineData(2, true, 1)]
    [InlineData(3, true, 1)]
    [InlineData(0, true, 0)]
    [InlineData(6, false, 1)]
    public void GraceWindowForTest_TurnAndFlag_ReturnsExpectedWindow(int turn, bool strictP1Grace, int expected)
    {
        Assert.Equal(expected, CastabilitySimulator.GraceWindowForTest(turn, strictP1Grace));
    }
}
