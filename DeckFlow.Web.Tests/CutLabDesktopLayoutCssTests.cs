using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards Cut Lab's desktop workspace treatment without changing shared tool-page panels.
/// </summary>
public sealed class CutLabDesktopLayoutCssTests
{
    [Fact]
    public void CutLabDesktopLayout_IsScopedToWorkspaceAtDesktopBreakpoint()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(min-width:\\s*1024px\\)[^{]*\\{(?:(?!@media).)*\\.cutlab-workspace",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "\\.cutlab-workspace\\s+>\\s+\\.result-panel[^{}]*\\{[^}]*border:\\s*1px\\s+solid\\s+var\\(--line\\)",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void CutLabDesktopLayout_UsesWorkspaceWrapperWithoutReplacingWorkflowHooks()
    {
        string content = ReadCutLabView();

        Assert.Contains("<div class=\"cutlab-workspace\">", content, StringComparison.Ordinal);
        Assert.Contains("Html.BeginIntakeCard(new IntakeCardOptions(Model.HasResult, Model.IntakeSummaryText))", content, StringComparison.Ordinal);
        Assert.Contains("data-cut-lab-decide-action", content, StringComparison.Ordinal);
        Assert.Contains("id=\"cut-lab-step-panel-1\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CutLabDesktopLayout_ScopesConstraintsSummaryAndCollapsesItAt720Pixels()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(
            new Regex(
                "\\.cutlab-constraints-summary__block[^{}]*\\{[^}]*border:\\s*1px\\s+solid\\s+var\\(--line\\)[^}]*background:\\s*var\\(--bg\\)",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*720px\\)[^{]*\\{(?:(?!@media).)*\\.cutlab-constraints-summary\\s*\\{[^}]*grid-template-columns:\\s*1fr",
                RegexOptions.Singleline),
            content);
    }

    [Fact]
    public void CutLabDesktopLayout_UsesThemeSecondaryFallbackForRolesCount()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(
            new Regex(
                "\\.cutlab-constraints-summary__block--roles\\s+\\.cutlab-constraints-summary__count\\s*\\{[^}]*color:\\s*var\\(--mythic-gold,\\s*var\\(--theme-secondary\\)\\)",
                RegexOptions.Singleline),
            content);
    }

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

    private static string ReadCutLabView()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "Views",
            "Deck",
            "CutLab.cshtml"));
}
