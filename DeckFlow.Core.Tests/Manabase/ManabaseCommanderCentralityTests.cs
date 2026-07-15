using System.Collections.Generic;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ManabaseCommanderCentralityTests
{
    [Fact]
    public void Cedh_WinDirectedCommander_WithStrongOnCurveCastability_IsCentral()
    {
        ManabaseDeck deck = DeckWithCommander(PlanRole.Payoff | PlanRole.Engine);
        CardCastability[] castability =
        [
            CommanderRow(88),
            NonCommanderRow("Support")
        ];

        bool central = ManabaseAnalyzer.IsCommanderCentralForTest(
            deck, castability, CommanderImportance.Central, ManabaseMode.Cedh);

        Assert.True(central);
    }

    [Theory]
    [InlineData(88, true)]
    [InlineData(87, false)]
    public void Cedh_CommanderCentrality_UsesCedhSupportThresholdBoundary(int castPercent, bool expected)
    {
        ManabaseDeck deck = DeckWithCommander(PlanRole.Engine);
        CardCastability[] castability =
        [
            CommanderRow(castPercent),
            NonCommanderRow("Support")
        ];

        bool central = ManabaseAnalyzer.IsCommanderCentralForTest(
            deck, castability, CommanderImportance.Standard, ManabaseMode.Cedh);

        Assert.Equal(expected, central);
    }

    [Fact]
    public void CasualMode_IsAlwaysNonCentral()
    {
        ManabaseDeck deck = DeckWithCommander(PlanRole.Payoff);
        CardCastability[] castability = [CommanderRow(100)];

        bool central = ManabaseAnalyzer.IsCommanderCentralForTest(
            deck, castability, CommanderImportance.Central, ManabaseMode.Casual);

        Assert.False(central);
    }

    [Fact]
    public void Cedh_LowImportanceOrLowCastabilityCommander_IsNotCentral()
    {
        ManabaseDeck deck = DeckWithCommander(PlanRole.Payoff);

        bool lowImportance = ManabaseAnalyzer.IsCommanderCentralForTest(
            deck,
            [CommanderRow(100)],
            CommanderImportance.Low,
            ManabaseMode.Cedh);

        bool lowCastability = ManabaseAnalyzer.IsCommanderCentralForTest(
            deck,
            [CommanderRow(60)],
            CommanderImportance.Central,
            ManabaseMode.Cedh);

        Assert.False(lowImportance);
        Assert.False(lowCastability);
    }

    [Fact]
    public void NonCentral_ValueCommander_NoWinRole_IsNotCentral()
    {
        ManabaseDeck deck = DeckWithCommander(PlanRole.None, supportRoles: PlanRole.Payoff);
        CardCastability[] castability = [CommanderRow(100)];

        bool central = ManabaseAnalyzer.IsCommanderCentralForTest(
            deck, castability, CommanderImportance.Standard, ManabaseMode.Cedh);

        Assert.False(central);
    }

    private static ManabaseDeck DeckWithCommander(PlanRole commanderRoles, PlanRole supportRoles = PlanRole.None) => new()
    {
        TotalCards = 99,
        CommanderCount = 1,
        Sources = new List<ManaSource>(),
        Spells = new List<SpellRequirement>
        {
            new()
            {
                Name = "Commander",
                ManaValue = 4,
                Pips = new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
                IsCommander = true,
                PlanRoles = commanderRoles,
            },
            new()
            {
                Name = "Support",
                ManaValue = 2,
                Pips = new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
                PlanRoles = supportRoles,
            },
        },
        AverageManaValue = 2.5,
        IsSingleton = true,
    };

    private static CardCastability CommanderRow(int castPercent) => new()
    {
        Name = "Commander",
        ManaValue = 4,
        OnCurveTurn = 4,
        CastPercent = castPercent,
        LimitingFactor = "mana",
        IsCommander = true,
    };

    private static CardCastability NonCommanderRow(string name) => new()
    {
        Name = name,
        ManaValue = 2,
        OnCurveTurn = 2,
        CastPercent = 90,
        LimitingFactor = "mana",
    };
}
