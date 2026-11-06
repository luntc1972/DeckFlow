using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards Cut Lab's mobile workspace treatment without changing shared tool-page panels.
/// </summary>
public sealed class CutLabMobileLayoutCssTests
{
    [Fact]
    public void CutLabMobileLayout_IsScopedToWorkspaceAtMobileBreakpoint()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.cutlab-workspace",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.cutlab-workspace\\s+button:not\\(\\.card-picker__add\\):not\\(\\.card-picker__remove\\)[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.cutlab-workspace\\s+label\\.kb-chip[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.DoesNotMatch(
            new Regex(
                "\\.cutlab-workspace\\s+\\.kb-chip[^{}]*\\{[^}]*display:\\s*inline-flex",
                RegexOptions.Singleline),
            content);
        Assert.DoesNotMatch(
            new Regex(
                "\\.cutlab-workspace\\s+\\.cutlab-collapsible__summary[^{}]*\\{[^}]*display:\\s*inline-flex",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.cutlab-workspace\\s+label\\.kb-chip",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void CutLabMobileLayout_KeepsCardPickerControlsAtLeast44Pixels()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*600px\\)[^{]*\\{(?:(?!@media).)*?\\.card-picker__add\\s*,\\s*\\.card-picker__remove\\s*\\{[^}]*min-width:\\s*44px[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void CutLabMobileLayout_ContainsWideContentWithinWorkspace()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.cutlab-workspace\\s+textarea[^{}]*\\{[^}]*overflow-x:\\s*auto",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.cutlab-workspace\\s+table[^{}]*\\{[^}]*overflow-x:\\s*auto",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void CutLabMobileLayout_TreatsIntakeFormAsResultPanel()
    {
        string content = ReadSiteMobileCss();

        Assert.Equal(2, Regex.Matches(
            content,
            @"\.cutlab-workspace > \.cutlab-intake > form\.result-panel").Count);
    }

    [Fact]
    public void CutLabMobileLayout_ReservesSpaceForFixedBottomStepRail()
    {
        string content = ReadSiteCommonCss();
        int mobileBlockStart = content.LastIndexOf("@media (max-width: 640px)", StringComparison.Ordinal);
        Assert.True(mobileBlockStart >= 0);
        string mobileBlock = content[mobileBlockStart..];
        Match workspaceRule = Regex.Match(
            mobileBlock,
            @"body:has\(\.cutlab-workspace \.cutlab-step-rail\)\s*\{(?<properties>[^}]*)\}");
        Assert.True(workspaceRule.Success);
        Assert.Contains("padding-bottom: calc(7rem + env(safe-area-inset-bottom));", workspaceRule.Groups["properties"].Value);
        Match railRule = Regex.Match(
            mobileBlock,
            @"\.cutlab-workspace \.cutlab-step-rail\s*\{(?<properties>[^}]*)\}");
        Assert.True(railRule.Success);
        Assert.Contains("position: fixed;", railRule.Groups["properties"].Value);
        Assert.Contains("bottom: 0;", railRule.Groups["properties"].Value);
    }

    [Fact]
    public void CutLabDesktopLayout_DoesNotAddGapsBetweenEmptyRailRows()
    {
        string content = ReadSiteCommonCss();
        int desktopBlockStart = content.LastIndexOf("@media (min-width: 1024px)", StringComparison.Ordinal);
        string desktopBlock = content[desktopBlockStart..];
        Match gridRule = Regex.Match(desktopBlock,
            @"\.cutlab-workspace \.deck-history-results\s*\{(?<properties>[^}]*)\}");
        Assert.True(gridRule.Success);
        Assert.Contains("row-gap: 0;", gridRule.Groups["properties"].Value);
        Assert.Contains("margin-bottom: 1.25rem;", desktopBlock);
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

    private static string ReadSiteCommonCss()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "wwwroot",
            "css",
            "site-common.css"));
}
