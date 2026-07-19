using System.Collections.Generic;
using System.Reflection;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Validates the override-box content rule on <see cref="ManabaseViewModel"/>: pre-populate from
/// detected suggestions when the user supplied nothing, otherwise preserve the user's text.
/// </summary>
public sealed class ManabaseViewModelTests
{
    [Fact]
    public void OverridesBoxText_PrefillsSuggestions_WhenUserTextBlank()
    {
        var vm = new ManabaseViewModel
        {
            Request = new ManabaseRequest { CostOverridesText = "" },
            Suggestions = new List<CostSuggestion>
            {
                new() { Name = "Blasphemous Act", EffectiveCost = "{R}", Reason = "scales down" },
                new() { Name = "Force of Will", EffectiveCost = "0", Reason = "free" },
            },
        };

        Assert.True(vm.HasSuggestions);
        Assert.Equal("Blasphemous Act: {R}\nForce of Will: 0", vm.OverridesBoxText);
    }

    [Fact]
    public void OverridesBoxText_PreservesUserText_WhenProvided()
    {
        var vm = new ManabaseViewModel
        {
            Request = new ManabaseRequest { CostOverridesText = "Force of Will: 0" },
            Suggestions = new List<CostSuggestion>
            {
                new() { Name = "Blasphemous Act", EffectiveCost = "{R}", Reason = "scales down" },
            },
        };

        Assert.Equal("Force of Will: 0", vm.OverridesBoxText);
    }

    [Fact]
    public void OverridesBoxText_Empty_WhenNoUserTextAndNoSuggestions()
    {
        var vm = new ManabaseViewModel { Request = new ManabaseRequest() };

        Assert.False(vm.HasSuggestions);
        Assert.Equal(string.Empty, vm.OverridesBoxText);
    }

    [Fact]
    public void OverridesBoxText_TouchedAndCleared_DoesNotRefillSuggestions()
    {
        // MEDIUM-11: once the user has touched the box, a deliberately empty box stays empty instead
        // of silently re-filling with the suggestions (the reject-by-clear fix).
        var vm = new ManabaseViewModel
        {
            Request = new ManabaseRequest { CostOverridesText = "", OverridesTouched = true },
            Suggestions = new List<CostSuggestion>
            {
                new() { Name = "Blasphemous Act", EffectiveCost = "{R}", Reason = "scales down" },
            },
        };

        Assert.True(vm.HasSuggestions);
        Assert.Equal(string.Empty, vm.OverridesBoxText);
    }

    [Fact]
    public void OverridesBoxText_TouchedWithText_EchoesUserTextVerbatim()
    {
        var vm = new ManabaseViewModel
        {
            Request = new ManabaseRequest { CostOverridesText = "Force of Will: 0", OverridesTouched = true },
            Suggestions = new List<CostSuggestion>
            {
                new() { Name = "Blasphemous Act", EffectiveCost = "{R}", Reason = "scales down" },
            },
        };

        Assert.Equal("Force of Will: 0", vm.OverridesBoxText);
    }

    [Fact]
    public void NotAppliedOverrides_DefaultsEmpty_FlagFalse()
    {
        var vm = new ManabaseViewModel { Request = new ManabaseRequest() };

        Assert.Empty(vm.NotAppliedOverrides);
        Assert.False(vm.HasNotAppliedOverrides);
    }

    [Fact]
    public void NotAppliedOverrides_Populated_FlagTrue()
    {
        var vm = new ManabaseViewModel
        {
            Request = new ManabaseRequest(),
            NotAppliedOverrides = new[] { "Blasphemus Act: {R}", "Fake Card" },
        };

        Assert.True(vm.HasNotAppliedOverrides);
        Assert.Equal(2, vm.NotAppliedOverrides.Count);
    }

    [Fact]
    public void PlainLanguageProperties_DefaultToNullAndFalse()
    {
        var vm = new ManabaseViewModel();

        Assert.Null(GetOptionalProperty<ManabaseVerdict>(vm, "PlainLanguageVerdict"));
        Assert.Null(GetOptionalProperty<ManabaseRampDrawBudget>(vm, "RampDrawBudget"));
        Assert.False(GetBoolProperty(vm, "ShowPlainLanguage"));
    }

    [Fact]
    public void PlainLanguageProperties_RoundTrip()
    {
        var verdict = new ManabaseVerdict
        {
            HasIssues = false,
            Headline = "Reading the deck",
            Lines = new List<string>(),
            NoIssueReason = "Looks fine.",
        };
        var budget = new ManabaseRampDrawBudget
        {
            RampCount = 10,
            DrawCount = 12,
            OverlapCount = 2,
            Threshold = 4,
            ThresholdSource = ManabaseRampDrawThresholdSource.CommanderManaValue,
            TargetRamp = 12,
            TargetDraw = 12,
            IsBalanced = true,
            IsRampLight = false,
            IsRampHeavy = false,
            RampShort = 0,
            IsDrawLight = false,
            DrawShort = 0,
        };

        var vm = new ManabaseViewModel();
        vm = SetProperty(vm, "PlainLanguageVerdict", verdict);
        vm = SetProperty(vm, "RampDrawBudget", budget);
        vm = SetProperty(vm, "ShowPlainLanguage", true);

        Assert.Same(verdict, GetOptionalProperty<ManabaseVerdict>(vm, "PlainLanguageVerdict"));
        Assert.Same(budget, GetOptionalProperty<ManabaseRampDrawBudget>(vm, "RampDrawBudget"));
        Assert.True(GetBoolProperty(vm, "ShowPlainLanguage"));
    }

    [Fact]
    public void ShowTapAnalyzer_DefaultsToFalse()
    {
        var vm = new ManabaseViewModel();

        Assert.False(GetBoolProperty(vm, "ShowTapAnalyzer"));
    }

    [Fact]
    public void ShowTapAnalyzer_RoundTrip()
    {
        var vm = new ManabaseViewModel { ShowTapAnalyzer = true };

        Assert.True(GetBoolProperty(vm, "ShowTapAnalyzer"));
    }

    [Fact]
    public void ShowCastability_CasualWithRows_ReturnsTrue()
    {
        var vm = ViewModel(ManabaseMode.Casual, BuildRows(), showCedhInteractionLens: false);

        Assert.True(vm.ShowCastability);
    }

    [Fact]
    public void ShowCastability_CedhWithRowsAndLensOff_ReturnsFalse()
    {
        var vm = ViewModel(ManabaseMode.Cedh, BuildRows(), showCedhInteractionLens: false);

        Assert.False(vm.ShowCastability);
    }

    [Fact]
    public void ShowCastability_CedhWithRowsAndLensOn_ReturnsTrue()
    {
        var vm = ViewModel(ManabaseMode.Cedh, BuildRows(), showCedhInteractionLens: true);

        Assert.True(vm.ShowCastability);
    }

    [Fact]
    public void ShowCastability_And_HasResult_FalseWhenNoReport()
    {
        // A view model with no report (initial GET or error path) gates both off.
        var empty = new ManabaseViewModel();

        Assert.False(empty.HasResult);
        Assert.False(empty.ShowCastability);
    }

    [Fact]
    public void HasResult_TrueWhenReportPresent()
    {
        var vm = ViewModel(ManabaseMode.Casual, Array.Empty<CardCastability>(), showCedhInteractionLens: false);

        Assert.True(vm.HasResult);
    }

    private static T? GetOptionalProperty<T>(object target, string name)
        where T : class
    {
        PropertyInfo property = target.GetType().GetProperty(name)
            ?? throw new Xunit.Sdk.XunitException($"{target.GetType().Name}.{name} property missing.");
        return property.GetValue(target) as T;
    }

    private static bool GetBoolProperty(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name)
            ?? throw new Xunit.Sdk.XunitException($"{target.GetType().Name}.{name} property missing.");
        return (bool)(property.GetValue(target) ?? false);
    }

    private static ManabaseViewModel SetProperty(ManabaseViewModel vm, string name, object? value)
    {
        PropertyInfo property = typeof(ManabaseViewModel).GetProperty(name)
            ?? throw new Xunit.Sdk.XunitException($"ManabaseViewModel.{name} property missing.");
        property.SetValue(vm, value);
        return vm;
    }

    private static ManabaseViewModel ViewModel(
        ManabaseMode mode,
        IReadOnlyList<CardCastability> rows,
        bool showCedhInteractionLens)
        => new()
        {
            ShowCedhInteractionLens = showCedhInteractionLens,
            Report = new ManabaseReport
            {
                ActualLands = 36,
                TargetLands = 37,
                ColorFindings = Array.Empty<ColorSourceFinding>(),
                Castability = rows,
                Summary = "ok",
                Mode = mode,
            },
        };

    private static CardCastability[] BuildRows()
        =>
        [
            new CardCastability
            {
                Name = "Spell",
                ManaValue = 2,
                OnCurveTurn = 2,
                CastPercent = 50,
                LimitingFactor = "mana",
            },
        ];
}
