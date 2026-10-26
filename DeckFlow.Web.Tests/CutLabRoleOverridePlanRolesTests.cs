using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabRoleOverridePlanRolesTests
{
    [Theory]
    [InlineData("Creature", "", PlanRole.None, false, "payoffs", "", "", PlanRole.Payoff, false)]
    [InlineData("Instant", "", PlanRole.None, false, "interaction-targeted", "", "", PlanRole.None, true)]
    [InlineData("Enchantment", "Whenever you cast a spell, draw a card.", PlanRole.Engine, false, "", "engines", "draw", PlanRole.None, false)]
    [InlineData("Creature", "", PlanRole.Payoff | PlanRole.Engine, true, "ramp", "", "", PlanRole.Payoff | PlanRole.Engine, true)]
    [InlineData("Creature", "", PlanRole.TutorCombo, false, "engines", "", "", PlanRole.TutorCombo | PlanRole.Engine, false)]
    public void Apply_RoleOverride_UpdatesOnlyTouchedFlags(string typeLine, string oracleText, PlanRole initial, bool interaction,
        string added, string removed, string removedAlso, PlanRole expected, bool expectedInteraction)
    {
        var fact = new CardFact { Name = "Test card", Quantity = 1, TypeLine = typeLine, OracleText = oracleText };
        var spell = new SpellRequirement { Name = fact.Name, ManaValue = 2, Pips = new Dictionary<ManaColor, int>(), PlanRoles = initial, IsInteractionSpell = interaction };
        var roleOverride = new CutLabRoleOverride
        {
            Name = fact.Name,
            Added = string.IsNullOrEmpty(added) ? [] : [added],
            Removed = new[] { removed, removedAlso }.Where(role => role.Length > 0).ToArray(),
        };

        SpellRequirement result = CutLabRoleOverridePlanRoles.Apply(spell, fact, ManabaseMode.Focused, roleOverride);

        Assert.Equal(expected, result.PlanRoles);
        Assert.Equal(expectedInteraction, result.IsInteractionSpell);
        Assert.Equal(spell.Name, result.Name);
        Assert.Equal(spell.ManaValue, result.ManaValue);
        Assert.Same(spell.Pips, result.Pips);
    }
}
