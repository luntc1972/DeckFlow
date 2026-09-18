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
        string desktopRail = ExtractMediaBlock(content, mediaQuery);
        const string rail = ".prompt-analysis-workflow .prompt-step-nav--analysis-rail";

        AssertRule(desktopRail, rail + " {", "display: flex;", "flex-direction: column;", "border: 1px solid var(--line);");
        AssertRule(desktopRail, rail + " .prompt-step-tab {", "display: grid;", "grid-template-columns: 1.5rem minmax(0, 1fr);", "border: 0;");
        AssertRule(desktopRail, rail + " .prompt-step-tab .prompt-step-tab__num {", "display: none;");
        AssertRule(desktopRail, rail + " .prompt-step-tab.is-active,", "background: transparent;", "color: var(--ink);");
        AssertRule(desktopRail, rail + " .prompt-step-tab.is-complete::before {", "display: none;");
        AssertRule(desktopRail, rail + " .prompt-step-rail__node {", "border: 2px solid var(--muted);", "border-radius: 50%;", "background: var(--panel);");
        AssertRule(desktopRail, rail + " .prompt-step-tab:not(:last-of-type)::after {", "background: var(--line);", "content: \"\";");
        AssertRule(desktopRail, rail + " .prompt-step-tab.is-complete .prompt-step-rail__node {", "border-color: var(--accent-strong);", "background: var(--accent-strong);", "color: var(--accent-contrast, #fff);");
        AssertRule(desktopRail, rail + " .prompt-step-tab.is-complete .prompt-step-rail__node::before {", "content: \"\\2713\";");
        AssertRule(desktopRail, rail + " .prompt-step-tab.is-active:not(.is-complete) .prompt-step-rail__node,", "border-color: var(--accent);", "background: var(--accent);");
        AssertRule(desktopRail, rail + " .prompt-step-tab[aria-disabled=\"true\"] .prompt-step-rail__node {", "border-color: var(--muted);", "background: var(--panel);");
        AssertRule(desktopRail, rail + " .prompt-step-tab[aria-disabled=\"true\"] {", "color: var(--muted);");
        AssertRule(desktopRail, rail + " .prompt-step-tab--optional {", "margin-left: 0;");
        AssertRule(desktopRail, rail + " .prompt-step-rail__copy {", "display: grid;", "gap: 0.15rem;");
        AssertRule(desktopRail, rail + " .prompt-step-rail__summary {", "display: block;", "color: var(--muted);");
        AssertRule(desktopRail, rail + " .prompt-step-rail__mobile-label {", "display: none;");
        AssertRule(desktopRail, rail + " .prompt-step-rail__desktop-title {", "display: inline;", "font-weight: 700;");
        AssertRule(desktopRail, rail + " .prompt-step-rail__optional-caption {", "display: block;", "text-transform: uppercase;");
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

    private static void AssertRule(string css, string selector, params string[] declarations)
    {
        string rule = ExtractRule(css, selector);
        foreach (string declaration in declarations)
        {
            Assert.Contains(declaration, rule, StringComparison.Ordinal);
        }
    }

    private static string ExtractMediaBlock(string css, string mediaQuery)
    {
        int mediaStart = css.LastIndexOf(mediaQuery, StringComparison.Ordinal);
        Assert.True(mediaStart >= 0, $"Missing media query: {mediaQuery}");
        int bodyStart = css.IndexOf('{', mediaStart);
        return ExtractBalancedBlock(css, bodyStart, mediaQuery);
    }

    private static string ExtractRule(string css, string selector)
    {
        int selectorStart = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(selectorStart >= 0, $"Missing desktop rail rule: {selector}");
        int bodyStart = css.IndexOf('{', selectorStart);
        return ExtractBalancedBlock(css, bodyStart, selector);
    }

    private static string ExtractBalancedBlock(string css, int bodyStart, string description)
    {
        Assert.True(bodyStart >= 0, $"Missing rule body: {description}");
        int depth = 0;
        for (int index = bodyStart; index < css.Length; index++)
        {
            depth += css[index] == '{' ? 1 : css[index] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return css[bodyStart..(index + 1)];
            }
        }

        throw new Xunit.Sdk.XunitException($"Unclosed rule: {description}");
    }
}
