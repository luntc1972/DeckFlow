using System.Collections.Generic;
using System.Text.Json;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Phase 1 (plan-presence): the additive <see cref="PlanRole"/> flags enum and
/// <see cref="SpellRequirement.PlanRoles"/> field. Verifies the default is None (so existing
/// construction is unchanged), flags compose, and the get/init property survives a JSON round-trip
/// (the .editorconfig carve-out keeps it { get; init; } precisely so System.Text.Json does not skip it).
/// </summary>
public sealed class PlanRoleTests
{
    private static SpellRequirement Spell(PlanRole roles = PlanRole.None) => new()
    {
        Name = "Test",
        ManaValue = 2,
        Pips = new Dictionary<ManaColor, int>(),
        PlanRoles = roles,
    };

    [Fact]
    public void PlanRoles_DefaultsToNone_WhenUnset()
    {
        var spell = new SpellRequirement
        {
            Name = "Sol Ring",
            ManaValue = 1,
            Pips = new Dictionary<ManaColor, int>(),
        };

        Assert.Equal(PlanRole.None, spell.PlanRoles);
    }

    [Fact]
    public void PlanRoles_ComposeAsFlags()
    {
        PlanRole roles = PlanRole.Payoff | PlanRole.Interaction;

        Assert.True(roles.HasFlag(PlanRole.Payoff));
        Assert.True(roles.HasFlag(PlanRole.Interaction));
        Assert.False(roles.HasFlag(PlanRole.Engine));
        Assert.NotEqual(PlanRole.None, roles);
    }

    [Fact]
    public void PlanRoles_SurvivesJsonRoundTrip()
    {
        SpellRequirement original = Spell(PlanRole.Payoff | PlanRole.TutorCombo);

        string json = JsonSerializer.Serialize(original);
        SpellRequirement? restored = JsonSerializer.Deserialize<SpellRequirement>(json);

        Assert.NotNull(restored);
        Assert.Equal(PlanRole.Payoff | PlanRole.TutorCombo, restored!.PlanRoles);
    }
}
