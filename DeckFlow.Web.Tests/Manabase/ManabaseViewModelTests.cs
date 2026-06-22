using System.Collections.Generic;
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
}
