using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards print-only result-region selectors against layout wrapper regressions.
/// </summary>
public sealed class PrintRegionCssTests
{
    [Fact]
    public void PrintRegion_ManabaseWorkspacePreservesResultsAndRevealsTabPanels()
    {
        string css = ReadCommonCss();

        Assert.Contains(".content-shell:has([data-print-region]) > *:not([data-print-region]):not(.manabase-workspace) {", css, StringComparison.Ordinal);
        Assert.Contains(".manabase-workspace > *:not([data-print-region]) {", css, StringComparison.Ordinal);
        Assert.Contains(".manabase-workspace [data-print-region] [role=\"tabpanel\"][hidden] {", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintRegion_WorkflowWrapperPreservesPanelsAndHidesItsChrome()
    {
        string css = ReadCommonCss();

        Assert.Contains("[data-print-region=\"workflow\"] > *:not(.prompt-step-panel):not(.prompt-analysis-workflow) {", css, StringComparison.Ordinal);
        Assert.Contains("[data-print-region=\"workflow\"] .prompt-analysis-workflow > *:not(.prompt-step-panel) {", css, StringComparison.Ordinal);
    }

    private static string ReadCommonCss()
    {
        return File.ReadAllText(Path.Combine(
            RepoPaths.Root(),
            "DeckFlow.Web",
            "wwwroot",
            "css",
            "site-common.css"));
    }
}
