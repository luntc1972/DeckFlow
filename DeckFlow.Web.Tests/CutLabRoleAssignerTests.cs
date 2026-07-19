using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services.CutLab;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for pure Cut Lab role assignment across the eight fixed slot keys.</summary>
public sealed class CutLabRoleAssignerTests
{
    [Fact]
    public void AssignRoles_Forest_MapsToExactlyLands()
    {
        CardFact fact = Fact(
            "Forest",
            "Basic Land — Forest");

        IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(
            fact,
            Array.Empty<string>(),
            isComboPiece: false,
            ManabaseMode.Casual);

        Assert.Equal(["lands"], roles);
    }

    [Fact]
    public void AssignRoles_Cultivate_MapsToRampOnly()
    {
        CardFact fact = Fact(
            "Cultivate",
            "Sorcery",
            oracle: "Search your library for up to two basic land cards, reveal those cards, put one onto the battlefield tapped and the other into your hand, then shuffle.");

        IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(
            fact,
            Array.Empty<string>(),
            isComboPiece: false,
            ManabaseMode.Casual);

        Assert.Equal(["ramp"], roles);
    }

    [Fact]
    public void AssignRoles_ModalDfcLandFront_MapsToLandsAndNotRamp()
    {
        CardFact fact = Fact(
            "Bala Ged Sanctuary // Bala Ged Recovery",
            "Land // Sorcery",
            oracle: "{T}: Add {G}. // Return target card from your graveyard to your hand.");

        IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(
            fact,
            Array.Empty<string>(),
            isComboPiece: false,
            ManabaseMode.Casual);

        Assert.Equal(["lands"], roles);
    }

    [Fact]
    public void AssignRoles_SwordsToPlowshares_IsInteractionInCasualViaPreGateSignal()
    {
        CardFact fact = Fact(
            "Swords to Plowshares",
            "Instant",
            oracle: "Exile target creature. Its controller gains life equal to its power.");

        IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(
            fact,
            Array.Empty<string>(),
            isComboPiece: false,
            ManabaseMode.Casual);

        Assert.Equal(["interaction"], roles);
    }

    [Theory]
    [InlineData(ManabaseMode.Cedh, new[] { "interaction" })]
    [InlineData(ManabaseMode.Casual, new string[0])]
    public void AssignRoles_Counterspell_RespectsModeGate(ManabaseMode mode, string[] expected)
    {
        CardFact fact = Fact(
            "Counterspell",
            "Instant",
            oracle: "Counter target spell.");

        IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(
            fact,
            Array.Empty<string>(),
            isComboPiece: false,
            mode);

        Assert.Equal(expected, roles);
    }

    [Fact]
    public void AssignRoles_RhysticStudy_CanHoldDrawAndEngineRoles()
    {
        CardFact fact = Fact(
            "Rhystic Study",
            "Enchantment",
            oracle: "Whenever an opponent casts a spell, you may draw a card unless that player pays {1}.");

        IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(
            fact,
            new[] { "Card Draw" },
            isComboPiece: false,
            ManabaseMode.Casual);

        Assert.Equal(["draw", "engines"], roles);
    }

    [Fact]
    public void AssignRoles_ComboPiece_IsWinconEvenWithoutClosingPower()
    {
        CardFact fact = Fact(
            "Isochron Scepter",
            "Artifact",
            oracle: "Imprint — When Isochron Scepter enters, you may exile an instant card with mana value 2 or less from your hand.");

        IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(
            fact,
            Array.Empty<string>(),
            isComboPiece: true,
            ManabaseMode.Casual);

        Assert.Equal(["wincons"], roles);
    }

    [Fact]
    public void AssignRoles_TormentOfHailfire_IsWinconDespitePlanRolePermanentGate()
    {
        CardFact fact = Fact(
            "Torment of Hailfire",
            "Sorcery",
            oracle: "Repeat the following process X times. Each opponent loses 3 life unless that player sacrifices a nonland permanent or discards a card.");

        IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(
            fact,
            new[] { "Win Condition" },
            isComboPiece: false,
            ManabaseMode.Casual);

        Assert.Equal(["wincons"], roles);
    }

    [Theory]
    [InlineData("cEDH", ManabaseMode.Cedh)]
    [InlineData("CeDh", ManabaseMode.Cedh)]
    [InlineData("Focused", ManabaseMode.Focused)]
    [InlineData("Casual", ManabaseMode.Casual)]
    [InlineData("", ManabaseMode.Casual)]
    [InlineData("unknown", ManabaseMode.Casual)]
    public void ResolveMode_MapsPlayExperienceLabels(string? playExperience, ManabaseMode expected)
    {
        Assert.Equal(expected, CutLabRoleAssigner.ResolveMode(playExperience));
    }

    [Fact]
    public void ResolveMode_Null_FallsBackToCasual()
    {
        Assert.Equal(ManabaseMode.Casual, CutLabRoleAssigner.ResolveMode(null));
    }

    private static CardFact Fact(string name, string typeLine, string? oracle = null) => new()
    {
        Name = name,
        Quantity = 1,
        TypeLine = typeLine,
        OracleText = oracle,
        ManaValue = 0,
    };
}
