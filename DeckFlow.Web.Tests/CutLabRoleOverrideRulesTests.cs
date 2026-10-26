using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabRoleOverrideRulesTests
{
    [Fact]
    public void RoleOverrideApplyToRoles_RemovingOnlyRole_ReturnsOther()
    {
        var roles = CutLabRoleOverrideRules.ApplyToRoles(
            ["ramp"],
            new CutLabRoleOverride { Removed = ["ramp"] });

        Assert.Equal(["other"], roles);
    }

    [Fact]
    public void RoleOverrideSanitize_RampOnLand_DropsRamp()
    {
        var overrides = CutLabRoleOverrideRules.Sanitize(
            [new CutLabRoleOverride { Name = "Forest", Added = ["ramp"] }],
            [new CutLabPoolCard { Name = "Forest", TypeLine = "Basic Land — Forest" }],
            []);

        Assert.Empty(overrides);
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_Commander_Refuses()
    {
        var state = new CutLabState { Pool = [new CutLabPoolCard { Name = "Commander", IsCommander = true }] };

        bool changed = CutLabRoleOverrideRules.TryApplyEdit(state, "Commander", "draw", CutLabRoleOverrideOperation.Add, out CutLabState updated, out string? error);

        Assert.False(changed);
        Assert.Same(state, updated);
        Assert.Equal(CutLabMessages.RoleOverrideCommanderMessage, error);
    }
}
