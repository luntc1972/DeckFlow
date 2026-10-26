using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabRoleOverrideRulesTests
{
    [Fact]
    public void RoleOverrideSanitize_ExcessInput_IgnoresEntriesAfterCap()
    {
        var overrides = Enumerable.Range(0, 5000)
            .Select(index => new CutLabRoleOverride { Name = "Card", Added = [index < CutLabRoleOverrideRules.MaxRoleOverrides ? "draw" : "tutor"] })
            .ToArray();

        var result = CutLabRoleOverrideRules.Sanitize(overrides, Pool(), []);

        Assert.True(result.Count <= CutLabRoleOverrideRules.MaxRoleOverrides);
        Assert.Equal(["draw"], Assert.Single(result).Added);
    }

    [Fact]
    public void RoleOverrideSanitize_DuplicatePoolNames_UsesLastCard()
    {
        var result = CutLabRoleOverrideRules.Sanitize(
            [new CutLabRoleOverride { Name = "Card", Added = ["draw"] }],
            [new() { Name = "CARD", IsCommander = true }, new() { Name = "Card" }], []);

        Assert.Equal("Card", Assert.Single(result).Name);
    }

    [Fact]
    public void RoleOverrideSanitize_NullList_ReturnsEmpty()
    {
        Assert.Empty(CutLabRoleOverrideRules.Sanitize(null, Pool(), []));
    }

    [Fact]
    public void RoleOverrideSanitize_BlankAndUnknownNames_DropsEntries()
    {
        IReadOnlyList<CutLabRoleOverride> result = CutLabRoleOverrideRules.Sanitize(
            [new CutLabRoleOverride { Name = " " }, new CutLabRoleOverride { Name = "Missing", Added = ["draw"] }], Pool(), []);

        Assert.Empty(result);
    }

    [Fact]
    public void RoleOverrideSanitize_Commander_DropsEntry()
    {
        Assert.Empty(CutLabRoleOverrideRules.Sanitize([new CutLabRoleOverride { Name = "Commander", Added = ["draw"] }], Pool(), []));
    }

    [Fact]
    public void RoleOverrideSanitize_InvalidAndDuplicateKeys_Canonicalizes()
    {
        IReadOnlyList<CutLabRoleOverride> result = CutLabRoleOverrideRules.Sanitize(
            [new CutLabRoleOverride { Name = "Card", Added = ["DRAW", "draw", "lands", "other", "unknown"] }], Pool(), []);

        Assert.Equal(["draw"], Assert.Single(result).Added);
    }

    [Fact]
    public void RoleOverrideSanitize_KeyInAddedAndRemoved_DropsFromBoth()
    {
        IReadOnlyList<CutLabRoleOverride> result = CutLabRoleOverrideRules.Sanitize(
            [new CutLabRoleOverride { Name = "Card", Added = ["draw"], Removed = ["draw"] }], Pool(), []);

        Assert.Empty(result);
    }

    [Fact]
    public void RoleOverrideSanitize_BothInteractionKeys_DropsBoth()
    {
        IReadOnlyList<CutLabRoleOverride> result = CutLabRoleOverrideRules.Sanitize(
            [new CutLabRoleOverride { Name = "Card", Added = ["interaction-targeted", "interaction-mass"] }], Pool(), []);

        Assert.Empty(result);
    }

    [Fact]
    public void RoleOverrideSanitize_RampOnLand_DropsRamp()
    {
        Assert.Empty(CutLabRoleOverrideRules.Sanitize(
            [new CutLabRoleOverride { Name = "Land", Added = ["ramp"] }], Pool(), []));
    }

    [Fact]
    public void RoleOverrideSanitize_EmptyEntry_DropsEntry()
    {
        Assert.Empty(CutLabRoleOverrideRules.Sanitize([new CutLabRoleOverride { Name = "Card" }], Pool(), []));
    }

    [Fact]
    public void RoleOverrideSanitize_DuplicateName_KeepsLastOccurrencePosition()
    {
        IReadOnlyList<CutLabRoleOverride> result = CutLabRoleOverrideRules.Sanitize(
            [new CutLabRoleOverride { Name = "Card", Added = ["draw"] }, new CutLabRoleOverride { Name = "Other", Added = ["ramp"] }, new CutLabRoleOverride { Name = "Card", Added = ["tutor"] }],
            [new CutLabPoolCard { Name = "Card" }, new CutLabPoolCard { Name = "Other" }], []);

        Assert.Equal(["Other", "Card"], result.Select(item => item.Name));
        Assert.Equal(["tutor"], result[1].Added);
    }

    [Fact]
    public void RoleOverrideApplyToRoles_NullOverride_ReturnsSameInstance()
    {
        IReadOnlyList<string> roles = ["draw"];

        Assert.Same(roles, CutLabRoleOverrideRules.ApplyToRoles(roles, null));
    }

    [Fact]
    public void RoleOverrideApplyToRoles_RemovingOnlyRole_ReturnsOther()
    {
        Assert.Equal(["other"], CutLabRoleOverrideRules.ApplyToRoles(["ramp"], new CutLabRoleOverride { Removed = ["ramp"] }));
    }

    [Fact]
    public void RoleOverrideApplyToRoles_LandCannotBeChanged()
    {
        Assert.Equal(["lands"], CutLabRoleOverrideRules.ApplyToRoles(["lands"], new CutLabRoleOverride { Added = ["ramp", "lands"], Removed = ["lands"] }));
    }

    [Fact]
    public void RoleOverrideApplyToRoles_AddedInteractionWins()
    {
        Assert.Equal(["interaction-mass"], CutLabRoleOverrideRules.ApplyToRoles(
            ["interaction-targeted"], new CutLabRoleOverride { Added = ["interaction-mass"] }));
    }

    [Fact]
    public void RoleOverrideApplyToRoles_UsesAssignRolesOrder()
    {
        Assert.Equal(["ramp", "draw", "tutor", "wincons"], CutLabRoleOverrideRules.ApplyToRoles(
            ["wincons", "tutor", "draw"], new CutLabRoleOverride { Added = ["ramp"] }));
    }

    [Theory]
    [InlineData(CutLabRoleOverrideOperation.Unspecified, "draw", CutLabMessages.RoleOverrideRefusedMessage)]
    [InlineData(CutLabRoleOverrideOperation.Add, "lands", CutLabMessages.RoleOverrideLandsLockedMessage)]
    [InlineData(CutLabRoleOverrideOperation.Add, "other", CutLabMessages.RoleOverrideRefusedMessage)]
    public void RoleOverrideTryApplyEdit_InvalidOperationOrRole_Refuses(CutLabRoleOverrideOperation operation, string role, string expected)
    {
        bool changed = CutLabRoleOverrideRules.TryApplyEdit(new CutLabState { Pool = Pool() }, "Card", role, operation, out CutLabState updated, out string? error);

        Assert.False(changed);
        Assert.Equal(expected, error);
        Assert.Empty(updated.RoleOverrides);
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_UnknownCard_Refuses()
    {
        AssertRefusal(new CutLabState { Pool = Pool() }, "Missing", "draw", CutLabRoleOverrideOperation.Add, CutLabMessages.RoleOverrideCardNotInPoolMessage);
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_Commander_Refuses()
    {
        AssertRefusal(new CutLabState { Pool = Pool() }, "Commander", "draw", CutLabRoleOverrideOperation.Add, CutLabMessages.RoleOverrideCommanderMessage);
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_RampOnLand_Refuses()
    {
        AssertRefusal(new CutLabState { Pool = Pool() }, "Land", "ramp", CutLabRoleOverrideOperation.Add, CutLabMessages.RoleOverrideRampOnLandMessage);
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_AddInteraction_ExchangesCounterpart()
    {
        CutLabState state = new() { Pool = Pool(), RoleOverrides = [new CutLabRoleOverride { Name = "Card", Added = ["interaction-targeted"] }] };

        bool changed = CutLabRoleOverrideRules.TryApplyEdit(state, "Card", "interaction-mass", CutLabRoleOverrideOperation.Add, out CutLabState updated, out _);

        CutLabRoleOverride roleOverride = Assert.Single(updated.RoleOverrides);
        Assert.True(changed);
        Assert.Equal(["interaction-mass"], roleOverride.Added);
        Assert.Equal(["interaction-targeted"], roleOverride.Removed);
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_Reset_RemovesEntry()
    {
        CutLabState state = new() { Pool = Pool(), RoleOverrides = [new CutLabRoleOverride { Name = "Card", Added = ["draw"] }] };

        Assert.True(CutLabRoleOverrideRules.TryApplyEdit(state, "Card", "", CutLabRoleOverrideOperation.Reset, out CutLabState updated, out _));
        Assert.Empty(updated.RoleOverrides);
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_ChangedEdit_NullsBaselines()
    {
        CutLabState state = new() { Pool = Pool(), BaselineActualLands = 1, BaselineTargetLands = 2 };

        Assert.True(CutLabRoleOverrideRules.TryApplyEdit(state, "Card", "draw", CutLabRoleOverrideOperation.Add, out CutLabState updated, out _));
        Assert.Null(updated.BaselineSnapshot);
        Assert.Null(updated.BaselineActualLands);
        Assert.Null(updated.BaselineTargetLands);
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_NoChange_ReturnsSameState()
    {
        CutLabState state = new() { Pool = Pool(), RoleOverrides = [new CutLabRoleOverride { Name = "Card", Added = ["draw"] }] };

        Assert.True(CutLabRoleOverrideRules.TryApplyEdit(state, "Card", "draw", CutLabRoleOverrideOperation.Add, out CutLabState updated, out _));
        Assert.Same(state, updated);
    }

    [Fact]
    public void RoleOverrideFingerprint_PermutedInputs_AreEqual()
    {
        Assert.Equal(CutLabRoleOverrideRules.Fingerprint([new CutLabRoleOverride { Name = "Card", Added = ["draw", "ramp"] }]), CutLabRoleOverrideRules.Fingerprint([new CutLabRoleOverride { Name = "card", Added = ["ramp", "draw"] }]));
    }

    [Theory]
    [InlineData(CutLabRoleOverrideOperation.Add)]
    [InlineData(CutLabRoleOverrideOperation.Remove)]
    public void RoleOverrideTryApplyEdit_AtCapNewCard_RefusesWithLimitMessage(CutLabRoleOverrideOperation operation)
    {
        CutLabState state = AtCapState();

        AssertRefusal(state, "Card 201", "draw", operation, "Role edits are limited to 200 cards.");
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_AtCapExistingCard_Applies()
    {
        CutLabState state = AtCapState();

        Assert.True(CutLabRoleOverrideRules.TryApplyEdit(state, "Card 1", "tutor", CutLabRoleOverrideOperation.Add, out CutLabState updated, out string? error));
        Assert.Null(error);
        Assert.Contains("tutor", CutLabRoleOverrideRules.Find(updated.RoleOverrides, "Card 1")!.Added);
        Assert.Equal(200, updated.RoleOverrides.Count);
        Assert.True(CutLabRoleOverrideRules.TryApplyEdit(updated, "Card 1", "", CutLabRoleOverrideOperation.Reset, out CutLabState reset, out error));
        Assert.Null(error);
        Assert.Equal(199, reset.RoleOverrides.Count);
        Assert.Null(CutLabRoleOverrideRules.Find(reset.RoleOverrides, "Card 1"));
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_RepeatedAdd_KeepsStateAndBaseline()
    {
        CutLabState state = new()
        {
            Pool = [new CutLabPoolCard { Name = "A" }, new CutLabPoolCard { Name = "B" }],
            RoleOverrides = [new CutLabRoleOverride { Name = "A", Added = ["draw"] }, new CutLabRoleOverride { Name = "B", Added = ["ramp"] }],
            BaselineSnapshot = new CutLabMetricSnapshot(),
            BaselineActualLands = 1,
            BaselineTargetLands = 2
        };

        Assert.True(CutLabRoleOverrideRules.TryApplyEdit(state, "A", "draw", CutLabRoleOverrideOperation.Add, out CutLabState updated, out string? error));
        Assert.Null(error);
        Assert.Same(state, updated);
        Assert.Same(state.BaselineSnapshot, updated.BaselineSnapshot);
        Assert.Equal(state.BaselineActualLands, updated.BaselineActualLands);
        Assert.Equal(state.BaselineTargetLands, updated.BaselineTargetLands);
    }

    [Fact]
    public void RoleOverrideTryApplyEdit_RepeatedRemove_KeepsStateAndBaseline()
    {
        CutLabState state = new()
        {
            Pool = [new CutLabPoolCard { Name = "A" }, new CutLabPoolCard { Name = "B" }],
            RoleOverrides = [new CutLabRoleOverride { Name = "A", Removed = ["tutor"] }, new CutLabRoleOverride { Name = "B", Added = ["ramp"] }],
            BaselineSnapshot = new CutLabMetricSnapshot(),
            BaselineActualLands = 1,
            BaselineTargetLands = 2
        };

        Assert.True(CutLabRoleOverrideRules.TryApplyEdit(state, "A", "tutor", CutLabRoleOverrideOperation.Remove, out CutLabState updated, out string? error));
        Assert.Null(error);
        Assert.Same(state, updated);
        Assert.Same(state.BaselineSnapshot, updated.BaselineSnapshot);
        Assert.Equal(state.BaselineActualLands, updated.BaselineActualLands);
        Assert.Equal(state.BaselineTargetLands, updated.BaselineTargetLands);
    }

    private static CutLabState AtCapState()
        => new()
        {
            Pool = Enumerable.Range(1, 201).Select(index => new CutLabPoolCard { Name = $"Card {index}" }).ToArray(),
            RoleOverrides = Enumerable.Range(1, 200).Select(index => new CutLabRoleOverride { Name = $"Card {index}", Added = ["draw"] }).ToArray()
        };

    private static IReadOnlyList<CutLabPoolCard> Pool()
        => [new CutLabPoolCard { Name = "Card" }, new CutLabPoolCard { Name = "Land", TypeLine = "Basic Land — Forest" }, new CutLabPoolCard { Name = "Commander", IsCommander = true }];

    private static void AssertRefusal(CutLabState state, string cardName, string role, CutLabRoleOverrideOperation operation, string expected)
    {
        Assert.False(CutLabRoleOverrideRules.TryApplyEdit(state, cardName, role, operation, out CutLabState updated, out string? error));
        Assert.Same(state, updated);
        Assert.Equal(expected, error);
    }
}
