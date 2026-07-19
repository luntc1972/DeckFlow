using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for the shared Cut Lab land-type predicate used by the client lock surface.</summary>
public sealed class CutLabRoleGroupLockTests
{
    [Theory]
    [InlineData("Basic Land — Forest", true)]
    [InlineData("Land", true)]
    [InlineData("Instant // Land", false)]
    [InlineData("Creature — Elf", false)]
    public void IsLand_TypeLineRow_ReturnsExpectedResult(string typeLine, bool expected)
    {
        var result = CutLabLockRules.IsLand(typeLine);

        Assert.Equal(expected, result);
    }
}
