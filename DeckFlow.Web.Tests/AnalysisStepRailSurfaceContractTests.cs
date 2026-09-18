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
        Assert.DoesNotContain("? Model.InputSummary", content);
        Assert.Contains("Main deck cards:", content);
    }

    [Fact]
    public void AnalysisStepRail_CssIsScopedAndMarksOptionalBranch()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "DeckFlow.Web", "wwwroot", "css", "site-common.css"));
        const string mediaQuery = "@media (min-width: 900px)";
        var desktopRailStart = content.LastIndexOf(mediaQuery, StringComparison.Ordinal);
        var desktopRailEnd = content.IndexOf("\n}\n\n.prompt-step-heading", desktopRailStart, StringComparison.Ordinal);

        Assert.NotEqual(-1, desktopRailStart);
        Assert.NotEqual(-1, desktopRailEnd);
        var desktopRail = content[desktopRailStart..desktopRailEnd];

        Assert.Contains(".prompt-analysis-workflow .prompt-step-nav--analysis-rail .prompt-step-rail__node", desktopRail);
        Assert.Contains(".prompt-analysis-workflow .prompt-step-nav--analysis-rail .prompt-step-tab:not(:last-of-type)::after", desktopRail);
        Assert.Contains(".prompt-analysis-workflow .prompt-step-nav--analysis-rail .prompt-step-tab.is-complete .prompt-step-rail__node", desktopRail);
        Assert.Contains(".prompt-analysis-workflow .prompt-step-nav--analysis-rail .prompt-step-tab.is-active:not(.is-complete) .prompt-step-rail__node", desktopRail);
        Assert.Contains(".prompt-analysis-workflow .prompt-step-nav--analysis-rail .prompt-step-tab--optional", desktopRail);
        Assert.Contains(".prompt-analysis-workflow .prompt-step-nav--analysis-rail .prompt-step-rail__copy", desktopRail);
        Assert.Contains(".prompt-analysis-workflow .prompt-step-nav--analysis-rail .prompt-step-rail__summary", desktopRail);
        Assert.Contains(".prompt-analysis-workflow .prompt-step-nav--analysis-rail .prompt-step-rail__desktop-title", desktopRail);
        Assert.Contains(".prompt-analysis-workflow .prompt-step-nav--analysis-rail .prompt-step-rail__optional-caption", desktopRail);
        Assert.Contains("var(--success,", desktopRail);
        Assert.Contains("var(--accent-contrast,", desktopRail);
    }

    [Fact]
    public void AnalysisStepRail_IncludesDesktopOptionalCaptionUnlockCopyAndEnabledStepWiring()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "DeckFlow.Web", "Views", "Shared", "_AnalysisStepRail.cshtml"));

        Assert.Contains("Optional, once Step 3 has results", content);
        Assert.Contains("Unlocks after Step 3", content);
        Assert.Contains("Unlocks after Step 4", content);
        Assert.Contains("role=\"presentation\"", content);
        Assert.Contains("IsEnabled: Model.AnalysisResponse is not null || Model.IsSetUpgradePromptStepComplete || Model.Request.WorkflowStep >= 4", content);
        Assert.Contains("IsEnabled: Model.IsSetUpgradePromptStepComplete || Model.Request.WorkflowStep >= 5", content);
    }

    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
