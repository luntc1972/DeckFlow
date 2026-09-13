using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards Deck History's narrow-viewport workspace treatment without changing desktop styling.
/// </summary>
public sealed class DeckHistoryMobileLayoutCssTests
{
    [Fact]
    public void DeckHistoryMobileLayout_ProvidesScopedPanelsTouchTargetsStackedResultsAndResponsiveTimeline()
    {
        string content = ExtractMediaBlock(ReadSiteMobileCss());

        Assert.Matches(
            new Regex(
                "\\.deck-history-workspace\\s*\\{[^}]*padding:\\s*0\\s+0\\.75rem",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-history-workspace\\s*>\\s*\\.cutlab-intake\\s*>\\s*\\.deck-history-form\\s*,\\s*\\.deck-history-workspace\\s*>\\s*\\.deck-history-results\\s*>\\s*\\.result-panel\\s*\\{[^}]*margin-top:\\s*1rem[^}]*padding:\\s*1rem",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-history-workspace\\s*>\\s*\\.deck-history-results\\s*>\\s*\\.result-panel:nth-of-type\\(3\\)\\s*,\\s*\\.deck-history-workspace\\s*>\\s*\\.deck-history-results\\s*>\\s*\\.result-panel:nth-of-type\\(4\\)\\s*\\{[^}]*grid-column:\\s*1\\s*/\\s*-1",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-history-workspace\\s*>\\s*\\.deck-history-results\\s*\\{[^}]*grid-template-columns:\\s*minmax\\(0,\\s*1fr\\)",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-history-workspace\\s+\\.run-button\\s*,\\s*\\.deck-history-workspace\\s+\\.clear-cache-button\\s*,\\s*\\.deck-history-workspace\\s+\\.deck-history-form\\s+\\.df-select__trigger\\s*,\\s*\\.deck-history-workspace\\s+\\.history-compare-controls\\s+\\.df-select__trigger\\s*,\\s*\\.deck-history-workspace\\s+\\.copy-button\\s*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-history-workspace\\s+input\\[type=file\\],\\s*\\.deck-history-workspace\\s+input\\[type=url\\],\\s*\\.deck-history-workspace\\s+input\\[type=text\\],\\s*\\.deck-history-workspace\\s+textarea\\s*\\{[^}]*box-sizing:\\s*border-box[^}]*max-width:\\s*100%",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-history-workspace\\s+textarea\\s*\\{[^}]*overflow-wrap:\\s*anywhere[^}]*overflow-x:\\s*auto",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-history-workspace\\s+\\.history-timeline\\s*\\{[^}]*display:\\s*block[^}]*max-width:\\s*100%[^}]*overflow-x:\\s*auto",
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

    private static string ExtractMediaBlock(string content)
    {
        foreach (Match match in Regex.Matches(content, @"@media\s*\(max-width:\s*900px\)"))
        {
            int openingBrace = content.IndexOf('{', match.Index);
            int depth = 0;
            for (int index = openingBrace; index < content.Length; index++)
            {
                if (content[index] == '{')
                {
                    depth++;
                }
                else if (content[index] == '}' && --depth == 0)
                {
                    string block = content[match.Index..(index + 1)];
                    if (block.Contains(".deck-history-workspace", StringComparison.Ordinal))
                    {
                        return block;
                    }

                    break;
                }
            }
        }

        throw new InvalidOperationException("Unbalanced 900px media block.");
    }
}
