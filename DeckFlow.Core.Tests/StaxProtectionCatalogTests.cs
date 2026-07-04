using DeckFlow.Core.Analysis;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Golden tests for <see cref="StaxProtectionCatalog"/> coarse stax and protection membership.
/// </summary>
public sealed class StaxProtectionCatalogTests
{
    [Theory]
    [InlineData("Rule of Law", true)]
    [InlineData("Winter Orb", true)]
    [InlineData("rule of law", true)]
    public void IsStax_TrueCases(string name, bool expected)
    {
        Assert.Equal(expected, StaxProtectionCatalog.IsStax(name));
    }

    [Theory]
    [InlineData("Llanowar Elves", false)]
    [InlineData("", false)]
    public void IsStax_FalseCases(string name, bool expected)
    {
        Assert.Equal(expected, StaxProtectionCatalog.IsStax(name));
    }

    [Theory]
    [InlineData("Heroic Intervention", true)]
    [InlineData("Teferi's Protection", true)]
    public void IsProtection_TrueCases(string name, bool expected)
    {
        Assert.Equal(expected, StaxProtectionCatalog.IsProtection(name));
    }

    [Theory]
    [InlineData("Lightning Bolt", false)]
    [InlineData("", false)]
    public void IsProtection_FalseCases(string name, bool expected)
    {
        Assert.Equal(expected, StaxProtectionCatalog.IsProtection(name));
    }
}
