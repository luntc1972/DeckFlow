using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabRoleOverrideApplierTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_ConditionalEnablersAddition_PreservesOtherOverrideKeys(bool active)
    {
        CutLabAnalysisContext context = CreateContext([new CutLabAnalyzedCard("Card", 1, false, ["ramp"], [])])
            with
        {
            ActiveTriggerFamilies = active ? ["lifegain"] : []
        };
        CutLabRoleOverride item = new() { Name = "Card", Added = ["enablers", "stax"] };

        CutLabAnalysisContext result = CutLabRoleOverrideApplier.Apply(context, [item]);

        Assert.Equal(active, result.RolesByCardName["Card"].Contains("enablers"));
        Assert.Contains("stax", result.RolesByCardName["Card"]);
        Assert.Contains("enablers", item.Added);
        Assert.Equal(active ? 1 : 0, result.RoleCounts.GetValueOrDefault("enablers"));
    }

    [Fact]
    public void Apply_ActiveFamilyRemoval_RemovesAssignedEnablers()
    {
        CutLabAnalysisContext context = CreateContext([new CutLabAnalyzedCard("Card", 1, false, ["ramp", "enablers"], [])])
            with
        {
            ActiveTriggerFamilies = ["lifegain"]
        };

        CutLabAnalysisContext result = CutLabRoleOverrideApplier.Apply(context, [new CutLabRoleOverride { Name = "Card", Removed = ["enablers"] }]);

        Assert.DoesNotContain("enablers", result.RolesByCardName["Card"]);
        Assert.False(result.RoleCounts.ContainsKey("enablers"));
    }

    [Fact]
    public void RoleOverrideApplier_EmptyOverrides_UsesExistingContext()
    {
        CutLabAnalysisContext context = CreateContext([new CutLabAnalyzedCard("Card", 1, false, ["ramp"], [])]);

        Assert.Same(context, CutLabRoleOverrideApplier.Apply(context, []));
    }

    [Fact]
    public void RoleOverrideApplier_CommanderAndUnresolvedCards_AreUntouched()
    {
        CutLabAnalysisContext context = CreateContext([new CutLabAnalyzedCard("Commander", 1, false, ["ramp"], []) { IsCommander = true }, new CutLabAnalyzedCard("Unknown", 1, false, [], [])]);

        CutLabAnalysisContext result = CutLabRoleOverrideApplier.Apply(context, [new CutLabRoleOverride { Name = "Commander", Added = ["draw"] }, new CutLabRoleOverride { Name = "Unknown", Added = ["draw"] }]);

        Assert.Equal(["ramp"], result.AnalyzedCards[0].Roles);
        Assert.Empty(result.AnalyzedCards[1].Roles);
    }

    [Fact]
    public void RoleOverrideApplier_UnmatchedOverride_PreservesRoleCounts()
    {
        CutLabAnalysisContext context = CreateContext([new CutLabAnalyzedCard("Card", 1, false, ["ramp"], [])]);

        CutLabAnalysisContext result = CutLabRoleOverrideApplier.Apply(context, [new CutLabRoleOverride { Name = "Missing", Added = ["draw"] }]);

        Assert.Equal(context.RoleCounts, result.RoleCounts);
    }

    [Fact]
    public void RoleOverrideApplier_QuantityTwo_RecountsAndUsesCaseInsensitiveName()
    {
        CutLabAnalysisContext context = CreateContext([new CutLabAnalyzedCard("Card", 1, false, ["ramp"], []) { Quantity = 2 }]);

        CutLabAnalysisContext result = CutLabRoleOverrideApplier.Apply(context, [new CutLabRoleOverride { Name = "card", Added = ["draw"], Removed = ["ramp"] }]);

        Assert.Equal(["draw"], result.RolesByCardName["CARD"]);
        Assert.Equal(2, result.RoleCounts["draw"]);
        Assert.False(result.RoleCounts.ContainsKey("ramp"));
    }

    private static CutLabAnalysisContext CreateContext(IReadOnlyList<CutLabAnalyzedCard> cards)
    {
        return new CutLabAnalysisContext(
            cards,
            cards.ToDictionary(card => card.Name, card => card.Roles, StringComparer.OrdinalIgnoreCase),
            cards.SelectMany(card => card.Roles).ToDictionary(role => role, _ => 1, StringComparer.OrdinalIgnoreCase),
            0,
            ManabaseMode.Focused,
            new CutLabClassificationContext([], true, true, new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            []);
    }
}
