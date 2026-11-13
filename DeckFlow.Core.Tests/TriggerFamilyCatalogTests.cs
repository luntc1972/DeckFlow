using DeckFlow.Core.Analysis;

namespace DeckFlow.Core.Tests;

/// <summary>Verifies the trigger-family vocabulary independently of deck classification.</summary>
public sealed class TriggerFamilyCatalogTests
{
    [Theory]
    [InlineData("Flying\nWhenever you gain life, you may pay that much life. If you do, draw that many cards.\n{T}: Each opponent loses 1 life and you gain 1 life.", true)]
    [InlineData("Whenever you gain life, put two +1/+1 counters on Karlov of the Ghost Council.", true)]
    [InlineData("Whenever you gain life, you may pay {1}. If you do, draw a card and each opponent loses 1 life.", true)]
    [InlineData("Lifelink\nAt the beginning of your end step, if you gained life this turn, distribute up to that many +1/+1 counters among any number of other target creatures.", true)]
    [InlineData("Whenever you gain or lose life during your turn, ...", true)]
    [InlineData("Lifelink", false)]
    [InlineData("When this creature enters, you gain 3 life.", false)]
    [InlineData("Whenever an opponent gains life, ...", false)]
    [InlineData("Flying\nWhenever Zur the Enchanter attacks, you may search your library for an enchantment card with mana value 3 or less, put it onto the battlefield, then shuffle.", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("WHENEVER YOU GAIN LIFE, draw a card.", true)]
    public void GetActiveFamilies_OracleExamples_MatchesContract(string? oracleText, bool expected)
    {
        var actual = TriggerFamilyCatalog.GetActiveFamilies([oracleText]);
        Assert.Equal(expected ? new[] { TriggerFamilyCatalog.Lifegain } : Array.Empty<string>(), actual);
    }

    [Theory]
    [InlineData("Whenever another creature enters, you gain 1 life.", true)]
    [InlineData("{1}, Sacrifice a land: You gain 2 life.", true)]
    [InlineData("Each opponent loses X life. You gain life equal to the life lost this way.", true)]
    [InlineData("{T}: Each opponent loses 1 life and you gain 1 life.", true)]
    [InlineData("First strike, lifelink", true)]
    [InlineData("Target creature gains lifelink until end of turn.", true)]
    [InlineData("Lifelink (Damage dealt by this creature also causes you to gain life.)", true)]
    [InlineData("Exile target creature. Its controller gains life equal to its power.", false)]
    [InlineData("Target opponent gains 5 life.", false)]
    [InlineData("Target player gains 4 life.", false)]
    [InlineData("You lose 2 life.", false)]
    [InlineData("Pay 2 life: ...", false)]
    [InlineData("Whenever you gain life, put two +1/+1 counters on this creature.", false)]
    [InlineData("At the beginning of your end step, if you gained life this turn, draw a card.", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Each time you gain life, draw a card.", false)]
    [InlineData("Creatures your opponents control lose lifelink.", false)]
    [InlineData("Target creature loses lifelink until end of turn.", false)]
    [InlineData("You gain control of target creature if its owner has less life than you.", false)]
    [InlineData("Creatures you control have lifelink.", true)]
    [InlineData("Target creature you control gains lifelink until end of turn.", true)]
    [InlineData("If you would gain life, you gain twice that much life instead.", true)]
    [InlineData("Each player gains 5 life.", false)]
    [InlineData("You and target opponent each gain 3 life.", false)]
    [InlineData("YOU GAIN 2 LIFE.", true)]
    [InlineData("Whenever you gain or lose life during your turn, draw a card.", false)]
    [InlineData("Whenever another creature enters, you may gain 1 life.", true)]
    [InlineData("Whenever an artifact enters, you may gain 1 life.", true)]
    [InlineData("Whenever a player discards a card, you may gain 1 life.", true)]
    [InlineData("Whenever a creature dies, you may gain 1 life.", true)]
    [InlineData("Whenever you gain life, you may pay {1}. If you do, draw a card.", false)]
    [InlineData("You may pay 2 life.", false)]
    public void IsEnabler_OracleExamples_MatchesContract(string? oracleText, bool expected)
    {
        Assert.Equal(expected, TriggerFamilyCatalog.IsEnabler([TriggerFamilyCatalog.Lifegain], oracleText));
    }

    [Fact]
    public void GetActiveFamilies_OnlySecondCommanderActivates_ReturnsLifegain()
    {
        Assert.Equal(new[] { TriggerFamilyCatalog.Lifegain },
            TriggerFamilyCatalog.GetActiveFamilies(["Lifelink", "Whenever you gain life, draw a card."]));
    }

    [Fact]
    public void GetActiveFamilies_BothCommandersActivate_ReturnsOneKey()
    {
        Assert.Equal(new[] { TriggerFamilyCatalog.Lifegain },
            TriggerFamilyCatalog.GetActiveFamilies(["Whenever you gain life, draw a card.", "If you gained life this turn, draw a card."]));
    }

    [Fact]
    public void GetActiveFamilies_NoCommanders_ReturnsEmpty()
    {
        Assert.Empty(TriggerFamilyCatalog.GetActiveFamilies([]));
        Assert.Empty(TriggerFamilyCatalog.GetActiveFamilies(null));
    }

    [Fact]
    public void IsEnabler_NoActiveFamilies_ReturnsFalse()
    {
        Assert.False(TriggerFamilyCatalog.IsEnabler([], "You gain 2 life."));
        Assert.False(TriggerFamilyCatalog.IsEnabler(null, "Lifelink"));
    }

    [Fact]
    public void IsEnabler_UnknownFamily_IgnoresUnknownKey()
    {
        Assert.False(TriggerFamilyCatalog.IsEnabler(["unknown"], "You gain 2 life."));
        Assert.True(TriggerFamilyCatalog.IsEnabler(["unknown", TriggerFamilyCatalog.Lifegain], "You gain 2 life."));
    }
}
