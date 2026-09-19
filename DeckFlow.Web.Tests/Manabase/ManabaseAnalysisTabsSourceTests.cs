using Xunit;

namespace DeckFlow.Web.Tests.Manabase;

/// <summary>
/// Guards Manabase analysis tab state across history navigation and responsive breakpoints.
/// </summary>
public sealed class ManabaseAnalysisTabsSourceTests
{
    [Fact]
    public void ManabaseAnalysisTabs_UpdatesSelectionWhenHashChanges()
    {
        string content = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "wwwroot",
            "ts",
            "manabase-analysis-tabs.ts"));

        Assert.Contains("window.addEventListener('hashchange', (): void => selectTabForHash(window.location.hash));", content, StringComparison.Ordinal);
    }
}
