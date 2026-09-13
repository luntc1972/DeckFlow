using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards Convert Deck's narrow-viewport workspace treatment without changing desktop styling.
/// </summary>
public sealed class DeckConvertMobileLayoutCssTests
{
    [Fact]
    public void DeckConvertMobileLayout_ProvidesScopedPanelsTouchTargetsAndOverflowProtection()
    {
        string content = ExtractMediaBlock(ReadSiteMobileCss());

        Assert.Matches(
            new Regex(
                "\\.deck-convert-workspace\\s*\\{[^}]*padding:\\s*0\\s+0\\.75rem",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-convert-workspace\\s*>\\s*\\.deck-convert-grid\\s*>\\s*\\.deck-convert-grid__input\\s*,\\s*\\.deck-convert-workspace\\s*>\\s*\\.deck-convert-grid\\s*>\\s*\\.deck-convert-grid__rail\\s*\\{[^}]*margin-top:\\s*1rem[^}]*padding:\\s*1rem",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-convert-workspace\\s+\\.run-button\\s*,\\s*\\.deck-convert-workspace\\s+\\.clear-cache-button\\s*,\\s*\\.deck-convert-workspace\\s+\\.copy-button\\s*,\\s*\\.deck-convert-workspace\\s+\\.df-select__trigger\\s*,\\s*\\.deck-convert-workspace\\s+\\.cache-pill__reset\\s*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-convert-workspace\\s+\\.df-select__trigger\\s*,\\s*\\.deck-convert-workspace\\s+\\.cache-pill__reset\\s*\\{[^}]*min-width:\\s*44px[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Contains("class=\"deck-form deck-convert-workspace\"", ReadDeckConvertView(), StringComparison.Ordinal);
        Assert.Matches(
            new Regex(
                "\\.deck-convert-workspace\\s+input\\[name=\\\"DeckUrl\\\"\\]\\s*,\\s*\\.deck-convert-workspace\\s+input\\[name=\\\"CommanderOverride\\\"\\]\\s*,\\s*\\.deck-convert-workspace\\s+textarea\\[name=\\\"DeckText\\\"\\]\\s*\\{[^}]*box-sizing:\\s*border-box[^}]*max-width:\\s*100%",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-convert-workspace\\s+textarea\\[name=\\\"DeckText\\\"\\]\\s*,\\s*\\.deck-convert-workspace\\s+#deck-convert-output\\s*\\{[^}]*overflow-wrap:\\s*anywhere[^}]*overflow-x:\\s*auto",
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

    private static string ReadDeckConvertView()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "Views",
            "Deck",
            "DeckConvert.cshtml"));

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
                    if (block.Contains(".deck-convert-workspace .run-button", StringComparison.Ordinal))
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
