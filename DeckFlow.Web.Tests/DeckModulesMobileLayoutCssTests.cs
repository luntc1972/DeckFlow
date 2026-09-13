using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards Deck Modules' mobile treatment without changing desktop styling.
/// </summary>
public sealed class DeckModulesMobileLayoutCssTests
{
    [Fact]
    public void DeckModulesMobileLayout_IsScopedToDeckModulesAtMobileBreakpoint()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.deck-modules",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void DeckModulesMobileLayout_ProvidesFortyFourPixelTouchTargets()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "\\.deck-modules\\s+\\.run-button[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-modules\\s+\\.copy-button[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-modules\\s+button\\[data-deck-modules-move\\][^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-modules\\s+select\\[data-df-select\\][^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-modules\\s+input:not\\(\\[type=checkbox\\]\\)[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-modules\\s+textarea[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-modules\\s+\\.df-select__trigger[^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.deck-modules\\s+\\[data-deck-modules-alternative\\][^{}]*\\{[^}]*min-height:\\s*44px",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void DeckModulesMobileLayout_ContainsResponsivePathsAssignmentsAndTextareas()
    {
        string content = ReadSiteMobileCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.deck-modules\\s+textarea[^{}]*\\{[^}]*max-width:\\s*100%[^}]*overflow-x:\\s*auto",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.deck-modules\\s+\\.deck-modules__path,\\s+\\.deck-modules\\s+\\.deck-modules__path-header[^{}]*\\{[^}]*grid-template-columns:\\s*minmax\\(0,\\s*1fr\\)",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*900px\\)[^{]*\\{(?:(?!@media).)*\\.deck-modules\\s+\\.deck-modules__assignment\\s*>\\s*button\\[data-deck-modules-move\\][^{}]*\\{[^}]*position:\\s*static[^}]*inline-size:\\s*100%",
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
