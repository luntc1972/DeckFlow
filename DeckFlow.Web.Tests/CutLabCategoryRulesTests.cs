using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabCategoryRulesTests
{
    internal static IReadOnlyList<CutLabOriginalEntry> CategoryOverrideMultiRowEntries() =>
    [
        new() { Name = "Sol Ring", Board = "mainboard", Quantity = 1, SetCode = "a", Category = "Ramp" },
        new() { Name = "Sol Ring", Board = "mainboard", Quantity = 97, SetCode = "b", Category = "Draw" },
    ];

    private static CutLabState State() => new()
    {
        Pool = [new() { Name = "Sol Ring" }, new() { Name = "Commander", IsCommander = true }],
        OriginalEntries = CategoryOverrideMultiRowEntries(),
    };

    [Fact]
    public void CategoryOverride_NormalizeInput_CollapsesWhitespace()
    {
        Assert.Equal("Fast Mana", CutLabCategoryRules.NormalizeInput("  Fast   Mana "));
        Assert.Equal("", CutLabCategoryRules.NormalizeInput(null));
    }

    [Fact]
    public void CategoryOverride_Validation_EnforcesExportSyntax()
    {
        Assert.True(CutLabCategoryRules.IsValidCategory(new string('a', 64)));
        Assert.False(CutLabCategoryRules.IsValidCategory(new string('a', 65)));
        Assert.True(CutLabCategoryRules.IsValidCategory("Fast Mana"));
        foreach (char c in ",#[]{}^\u0001")
        {
            Assert.False(CutLabCategoryRules.IsValidCategory("A" + c));
        }
    }

    [Fact]
    public void CategoryOverride_Reserved_ExcludesBoardTokens()
    {
        foreach (string category in new[] { "commander", "SIDEBOARD", "Maybeboard", "Considering", "Uncategorized" })
        {
            Assert.True(CutLabCategoryRules.IsReserved(category));
        }
        Assert.False(CutLabCategoryRules.IsReserved("Ramp"));
    }

    [Fact]
    public void CategoryOverride_SplitTokens_PreservesFirstSpelling()
    {
        Assert.Equal(new[] { "Ramp", "Draw" }, CutLabCategoryRules.SplitTokens("Ramp, Draw ,,ramp"));
        Assert.Empty(CutLabCategoryRules.SplitTokens("Maybeboard"));
        Assert.Empty(CutLabCategoryRules.SplitTokens(null));
    }

    [Fact]
    public void CategoryOverride_ImportedTokens_UnionsAllRows()
    {
        Assert.Equal(new[] { "Ramp", "Draw" }, CutLabCategoryRules.ImportedCategoryTokens([new() { Name = "Sol Ring", Category = "Ramp" }, new() { Name = "Sol Ring", Category = "Draw,Ramp" }], "sol ring"));
        Assert.Empty(CutLabCategoryRules.ImportedCategoryTokens(State().OriginalEntries, "Missing"));
    }

    [Fact]
    public void CategoryOverride_EffectiveTokens_WithoutOverride_KeepsImportedTokens()
    {
        Assert.Equal(new[] { "Ramp", "Draw" }, CutLabCategoryRules.EffectiveCategoryTokens(State().OriginalEntries, [], "Sol Ring"));
    }

    [Theory]
    [InlineData("FastMana", "FastMana,Draw")]
    [InlineData("Draw", "Draw")]
    public void CategoryOverride_EffectiveTokens_ReplacesPrimary(string value, string expected)
    {
        var state = State();
        var effective = CutLabCategoryRules.EffectiveCategoryTokens(state.OriginalEntries, [new() { Name = "Sol Ring", Category = value }], "Sol Ring");
        Assert.Equal(expected, string.Join(",", effective));
        foreach (var row in state.OriginalEntries)
        {
            Assert.Equal(effective, CutLabCategoryRules.SplitTokens(CutLabCategoryRules.ApplyToCategory(row.Category, effective)));
        }
        Assert.Equal(new[] { value }, CutLabCategoryRules.EffectiveCategoryTokens([], [new() { Name = "Other", Category = value }], "Other"));
    }

    [Fact]
    public void CategoryOverride_ApplyToCategory_PreservesUneditedAndBoardTokens()
    {
        string original = new string("Ramp".ToCharArray());
        Assert.Same(original, CutLabCategoryRules.ApplyToCategory(original, null));
        Assert.Equal("Commander,FastMana", CutLabCategoryRules.ApplyToCategory("Commander,Ramp", ["FastMana"]));
        Assert.Equal("FastMana", CutLabCategoryRules.ApplyToCategory(null, ["FastMana"]));
        Assert.Null(CutLabCategoryRules.ApplyToCategory(null, []));
    }

    [Fact]
    public void CategoryOverride_Options_UsesEffectiveSortedUnion()
    {
        var state = State() with { CategoryOverrides = [new() { Name = "Sol Ring", Category = "FastMana" }] };
        Assert.Equal(new[] { "Draw", "FastMana" }, CutLabCategoryRules.CategoryOptions(state));
    }

    [Fact]
    public void CategoryOverride_Sanitize_RejectsTamperingAndKeepsLastPosition()
    {
        CutLabCategoryOverride[] values = [null!, new() { Name = " " }, new() { Name = "Bad", Category = "#bad" }, new() { Name = "Reserved", Category = "Commander" }, new() { Name = "Sol Ring", Category = "Ramp" }, new() { Name = " Gone ", Category = " Draw " }, new() { Name = "sol ring", Category = " Fast   Mana " }, new() { Name = "Commander", Category = "Leader" }];
        var sanitized = CutLabCategoryRules.Sanitize(values, State().Pool);
        Assert.Equal(new[] { "Gone", "Sol Ring", "Commander" }, sanitized.Select(x => x.Name));
        Assert.Equal("Fast Mana", sanitized[1].Category);
        var state = State() with { CategoryOverrides = sanitized };
        Assert.Same(state, CutLabCategoryRules.Sanitize(state));
        var capped = Enumerable.Range(0, 201).Select(i => new CutLabCategoryOverride { Name = "Card " + i, Category = "Ramp" }).ToArray();
        Assert.Equal(200, CutLabCategoryRules.Sanitize(capped, []).Count);
        Assert.Null(CutLabCategoryRules.Find(CutLabCategoryRules.Sanitize(capped, []), "Card 200"));
    }

    [Theory]
    [InlineData("Missing", "Ramp", CutLabCategoryOverrideOperation.Set, "That card isn't in this pool.")]
    [InlineData("Sol Ring", "Ramp", CutLabCategoryOverrideOperation.Unspecified, "That category change couldn't be applied.")]
    [InlineData("Sol Ring", " ", CutLabCategoryOverrideOperation.Set, "Type a category name, or use the imported category.")]
    [InlineData("Sol Ring", "#bad", CutLabCategoryOverrideOperation.Set, "Category names can be up to 64 characters and can't use , # [ ] { } or ^.")]
    [InlineData("Sol Ring", "Commander", CutLabCategoryOverrideOperation.Set, "Commander, Sideboard, Maybeboard, Considering and Uncategorized are reserved and can't be used as categories.")]
    public void CategoryOverride_TryApplyEdit_RefusesInvalidInput(string name, string value, CutLabCategoryOverrideOperation operation, string expected)
    {
        var state = State();
        Assert.False(CutLabCategoryRules.TryApplyEdit(state, name, value, operation, out var updated, out var error));
        Assert.Same(state, updated);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void CategoryOverride_TryApplyEdit_SetsResetsAndCanonicalizes()
    {
        var state = State() with { BaselineSnapshot = new(), BaselineActualLands = 30, BaselineTargetLands = 32 };
        Assert.True(CutLabCategoryRules.TryApplyEdit(state, "Sol Ring", "FastMana", CutLabCategoryOverrideOperation.Set, out var edited, out var error));
        Assert.Null(error);
        Assert.Same(state.BaselineSnapshot, edited.BaselineSnapshot);
        Assert.Equal(state.BaselineActualLands, edited.BaselineActualLands);
        Assert.Equal(state.BaselineTargetLands, edited.BaselineTargetLands);
        Assert.Equal("FastMana", Assert.Single(edited.CategoryOverrides).Category);
        Assert.True(CutLabCategoryRules.TryApplyEdit(edited, "Sol Ring", "fastmana", CutLabCategoryOverrideOperation.Set, out var unchanged, out _));
        Assert.Same(edited, unchanged);
        Assert.True(CutLabCategoryRules.TryApplyEdit(edited, "Sol Ring", "ramp", CutLabCategoryOverrideOperation.Set, out var imported, out _));
        Assert.Empty(imported.CategoryOverrides);
        Assert.True(CutLabCategoryRules.TryApplyEdit(edited, "Sol Ring", null, CutLabCategoryOverrideOperation.Reset, out var reset, out _));
        Assert.Empty(reset.CategoryOverrides);
        Assert.True(CutLabCategoryRules.TryApplyEdit(state, "Commander", "Leader", CutLabCategoryOverrideOperation.Set, out var commander, out _));
        Assert.Single(commander.CategoryOverrides);
    }

    [Fact]
    public void CategoryOverride_TryApplyEdit_CapAllowsExistingAndImportedPrimary()
    {
        var state = State() with { CategoryOverrides = Enumerable.Range(0, 200).Select(i => new CutLabCategoryOverride { Name = "Card " + i, Category = "Ramp" }).ToArray() };
        Assert.False(CutLabCategoryRules.TryApplyEdit(state, "Sol Ring", "FastMana", CutLabCategoryOverrideOperation.Set, out _, out var error));
        Assert.Equal(CutLabMessages.CategoryOverrideLimitMessage, error);
        Assert.True(CutLabCategoryRules.TryApplyEdit(state, "Sol Ring", "Ramp", CutLabCategoryOverrideOperation.Set, out _, out _));
        var existing = state with { Pool = [new() { Name = "Card 0" }] };
        Assert.True(CutLabCategoryRules.TryApplyEdit(existing, "Card 0", "Draw", CutLabCategoryOverrideOperation.Set, out var edited, out _));
        Assert.Equal("Card 0", edited.CategoryOverrides[^1].Name);
    }
}
