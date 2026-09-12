using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests.Bracket;

/// <summary>
/// Guards Bracket Check's mobile workspace treatment without changing desktop styling.
/// </summary>
public sealed class BracketMobileLayoutCssTests
{
    [Fact]
    public void BracketMobileLayout_IsScopedToWorkspaceAtMobileBreakpoint()
    {
        string content = ReadSiteMobileCss();

        string mobileWorkspaceBlock = ExtractMobileWorkspaceBlock(content);

        Assert.Contains(".bracket-workspace", mobileWorkspaceBlock);
        Assert.Matches(
            new Regex(
                "\\.bracket-workspace\\s*\\{[^}]*padding:\\s*0\\s+0\\.75rem",
                RegexOptions.Singleline),
            mobileWorkspaceBlock);
    }

    [Fact]
    public void BracketMobileLayout_ProvidesFortyFourPixelTouchTargets()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "\\.bracket-workspace\\s+button[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.bracket-workspace\\s+details[^{}]*>\\s*summary[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.bracket-workspace\\s+\\.manabase-pill\\s*[,{][^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void BracketMobileLayout_ContainsPanelAndTextareaTreatment()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "\\.bracket-workspace\\s*>\\s*\\.result-panel[^{}]*\\{[^}]*margin-top:\\s*1rem[^}]*padding:\\s*1rem",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.bracket-workspace\\s+\\.manabase-pill\\s*\\{[^}]*display:\\s*inline-flex[^}]*align-items:\\s*center",
                RegexOptions.Singleline),
            content);
        string mobileWorkspaceBlock = ExtractMobileWorkspaceBlock(content);

        Assert.Matches(
            new Regex(
                "\\.bracket-workspace\\s+textarea[^{}]*\\{[^}]*max-width:\\s*100%[^}]*overflow-x:\\s*auto",
                RegexOptions.Singleline),
            mobileWorkspaceBlock);
    }

    private static string ExtractMobileWorkspaceBlock(string content)
    {
        foreach (Match mediaMatch in Regex.Matches(
                     content,
                     "@media\\s*\\(max-width:\\s*900px\\)",
                     RegexOptions.Singleline))
        {
            int openBrace = content.IndexOf('{', mediaMatch.Index + mediaMatch.Length);
            if (openBrace < 0)
            {
                continue;
            }

            int depth = 0;
            for (int index = openBrace; index < content.Length; index++)
            {
                depth += content[index] switch
                {
                    '{' => 1,
                    '}' => -1,
                    _ => 0
                };

                if (depth == 0)
                {
                    string block = content[mediaMatch.Index..(index + 1)];
                    if (block.Contains(".bracket-workspace", StringComparison.Ordinal))
                    {
                        return block;
                    }

                    break;
                }
            }
        }

        return string.Empty;
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
