using System.IO;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the Deck Analysis-only vertical workflow rail's ARIA and CSS surface.
/// </summary>
public sealed class AnalysisStepRailSurfaceContractTests
{
    [Fact]
    public void AnalysisStepRail_PreservesPromptStepContractAndVerticalTablist()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "DeckFlow.Web", "Views", "Shared", "_AnalysisStepRail.cshtml"));

        Assert.Contains("role=\"tablist\"", content);
        Assert.Contains("aria-orientation=\"vertical\"", content);
        Assert.Contains("data-prompt-show-step=\"@step.Step\"", content);
        Assert.Contains("id=\"@($\"prompt-step-tab-{step.Step}\")\"", content);
        Assert.Contains("aria-controls=\"@($\"prompt-step-panel-{step.Step}\")\"", content);
        Assert.Contains("aria-selected=\"@(step.Step == currentStep ? \"true\" : \"false\")\"", content);
        Assert.Contains("tabindex=\"@(step.Step == currentStep ? \"0\" : \"-1\")\"", content);
        Assert.Contains("aria-disabled=\"@(!step.IsEnabled ? \"true\" : \"false\")\"", content);
    }

    [Fact]
    public void AnalysisStepRail_CssIsScopedAndMarksOptionalBranch()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "DeckFlow.Web", "wwwroot", "css", "site-common.css"));

        Assert.Contains(".prompt-step-nav--analysis-rail", content);
        Assert.Contains(".prompt-step-tab--optional", content);
    }

    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
