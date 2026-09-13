using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards Deck Sync's narrow-viewport workspace treatment without changing desktop styling.
/// </summary>
public sealed class DeckSyncMobileLayoutCssTests
{
    [Fact]
    public void DeckSyncMobileLayout_ProvidesScopedPanelsTouchTargetsAndSingleColumnSyncInputs()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.deck-sync-workspace\\s*\\{[^}]*padding:\\s*0\\s+0\\.75rem",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-sync-workspace\\s*>\\s*\\.cutlab-intake\\s*>\\s*form\\.result-panel\\s*,[^}]*\\{[^}]*margin-top:\\s*1rem[^}]*padding:\\s*1rem",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-sync-workspace\\s+\\.sync-columns\\s*\\{[^}]*grid-template-columns:\\s*minmax\\(0,\\s*1fr\\)",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-sync-workspace\\s+\\.run-button,[^}]*\\.deck-sync-workspace\\s+\\.clear-cache-button,[^}]*\\.deck-sync-workspace\\s+\\.cutlab-intake-summary,[^}]*\\.deck-sync-workspace\\s+\\.mode-picker\\s+\\.df-select__trigger,[^}]*\\.deck-sync-workspace\\s+\\.direction-group\\s+\\.df-select__trigger\\s*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-sync-workspace\\s+textarea\\s*\\{[^}]*overflow-wrap:\\s*anywhere[^}]*overflow-x:\\s*auto",
                RegexOptions.Singleline),
            content);
    }

    private static string ReadSiteMobileCss()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "wwwroot",
            "css",
            "site-mobile.css"));
}
