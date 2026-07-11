using DeckFlow.Core.Loading;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers commander-eligibility checks that run on type/oracle text only.
/// </summary>
public sealed class CommanderEligibilityTests
{
    [Theory]
    [InlineData("Legendary Creature — Angel", null)]
    [InlineData("Legendary Vehicle", null)]
    [InlineData("Legendary Planeswalker — Chandra", "+1: Draw a card.\nThis planeswalker can be your commander.")]
    [InlineData("Legendary Enchantment — Background", null)]
    public void IsEligible_RecognizesCommanderEligibleTypePatterns(string typeLine, string? oracleText)
    {
        Assert.True(CommanderEligibility.IsEligible(typeLine, oracleText));
    }

    [Theory]
    [InlineData("Creature — Human", null)]
    [InlineData("Land", null)]
    [InlineData("Instant", null)]
    [InlineData("Legendary Enchantment — Aura", "Background Story")]
    [InlineData("Enchantment — Background", null)]
    public void IsEligible_RejectsNonCommanderEligibleTypePatterns(string typeLine, string? oracleText)
    {
        Assert.False(CommanderEligibility.IsEligible(typeLine, oracleText));
    }
}
