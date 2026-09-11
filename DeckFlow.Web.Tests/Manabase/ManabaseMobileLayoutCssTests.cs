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
}
