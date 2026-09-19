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
                "\\.cutlab-workspace\\s+label\\.kb-chip|\\.cutlab-workspace\\s+\\.cutlab-anchor-nav\\s+a",
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
    public void CutLabMobileLayout_UsesTrailingFadeForScrollableAnchorNav()
    {
        string content = ReadSiteCommonCss();

        int selectorStart = content.LastIndexOf(
            "nav.cutlab-anchor-nav .cutlab-anchor-nav-list",
            content.Length - 1,
            StringComparison.Ordinal);
        int mobileBlockStart = content.LastIndexOf(
            "@media (max-width: 640px)",
            selectorStart,
            StringComparison.Ordinal);
        Match mobileBlock = Regex.Match(
            content[mobileBlockStart..],
            @"nav\.cutlab-anchor-nav \.cutlab-anchor-nav-list\s*\{(?<properties>[^}]*)\}");

        Assert.True(mobileBlock.Success);
        Assert.Contains("mask-image: linear-gradient(to right, rgba(0, 0, 0, 1) 0 calc(100% - 24px), transparent 100%);", mobileBlock.Groups["properties"].Value);
        Assert.Contains("-webkit-mask-image: linear-gradient(to right, rgba(0, 0, 0, 1) 0 calc(100% - 24px), transparent 100%);", mobileBlock.Groups["properties"].Value);
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
