using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests.Manabase;

/// <summary>
/// Guards Manabase Deep-dive's mobile workspace treatment without changing desktop styling.
/// </summary>
public sealed class ManabaseMobileLayoutCssTests
{
    [Fact]
    public void ManabaseMobileLayout_IsScopedToWorkspaceAtMobileBreakpoint()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.manabase-workspace",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void ManabaseMobileLayout_ProvidesFortyFourPixelTouchTargets()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "\\.manabase-workspace\\s+button[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.manabase-workspace\\s+details[^{}]*>\\s*summary[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.manabase-workspace\\s+\\.manabase-pill\\s*[,{][^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.manabase-workspace\\s+\\.manabase-anchor-nav-list\\s+a\\s*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void ManabaseAnchorNavigation_IsNotStickyBelowNineHundredPixels()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*899\\.98px\\)\\s*\\{(?:(?!@media).)*\\.manabase-anchor-nav\\s*\\{[^}]*position:\\s*static",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void ManabaseMobileLayout_RevealsHiddenAnalysisPanelsWithoutImportant()
    {
        string content = ReadSiteCommonCss();

        Match rule = Regex.Match(
            content,
            "@media\\s*\\(max-width:\\s*1023\\.98px\\)[^{]*\\{(?:(?!@media).)*\\.manabase-analysis-stage\\s+\\[hidden\\]\\s*\\{[^}]*display:\\s*block\\s*;[^}]*\\}",
            RegexOptions.Singleline);

        Assert.True(rule.Success);
        Assert.DoesNotContain("!important", rule.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ManabaseMobileLayout_ContainsWideTablesAndTextareas()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.manabase-workspace\\s+textarea[^{}]*\\{[^}]*max-width:\\s*100%[^}]*overflow-x:\\s*auto",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.manabase-workspace\\s+table[^{}]*\\{[^}]*max-width:\\s*100%[^}]*overflow-x:\\s*auto",
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
