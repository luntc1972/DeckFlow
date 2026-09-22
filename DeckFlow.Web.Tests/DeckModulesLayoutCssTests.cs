using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckModulesLayoutCssTests
{
    [Fact]
    public void DeckModulesHybridLayout_DefinesTopLevelRegions()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex(@"\.deck-modules__commander\s*\{[^}]*display:\s*flex;[^}]*flex-wrap:\s*wrap;", RegexOptions.Singleline), content);
        Assert.Matches(new Regex(@"\.deck-modules__overview\s*\{", RegexOptions.Singleline), content);
        Assert.Matches(new Regex(@"\.deck-modules__unassigned\s*\{", RegexOptions.Singleline), content);
        Assert.Matches(new Regex(@"\.deck-modules__howto,\s*\.deck-modules__empty,\s*\.deck-modules__configuration,\s*\.deck-modules__unassigned,\s*\.deck-modules__report\s*\{[^}]*grid-column:\s*1 / -1;", RegexOptions.Singleline), content);
    }

    [Fact]
    public void DeckModulesConfiguration_UsesFourColumnDesktopGrid()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex(@"@media \(min-width: 1024px\)\s*\{[^@]*\.deck-modules__configuration\s*\{[^}]*grid-template-columns:\s*minmax\(0, 1fr\) minmax\(0, 1fr\) minmax\(0, 2fr\) auto;", RegexOptions.Singleline), content);
    }

    [Fact]
    public void DeckModulesWorkspace_PlacesCoreBeforeActiveAlternativeAtDesktop()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex(@"\.deck-modules__workspace\s*\{[^}]*grid-column:\s*1 / -1", RegexOptions.Singleline), content);
        Assert.Matches(new Regex(@"@media \(min-width: 1024px\)\s*\{[^@]*\.deck-modules__workspace\s*\{[^}]*display:\s*grid;[^}]*grid-template-columns:\s*minmax\(0, 1fr\) minmax\(0, 2fr\)", RegexOptions.Singleline), content);
    }

    [Fact]
    public void DeckModulesDesktopLayout_FlattensUnassignedAndAlignsConfiguration()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex(@"\.deck-modules__unassigned \.deck-modules__assignment\s*\{[^}]*padding:\s*0;[^}]*border:\s*0;[^}]*background:\s*transparent;", RegexOptions.Singleline), content);
        Assert.Matches(new Regex(@"@media \(min-width: 1024px\)\s*\{[^@]*\.deck-modules__configuration\s*\{[^}]*align-items:\s*start;", RegexOptions.Singleline), content);
    }

    [Fact]
    public void DeckModulesActions_StyleSecondaryActionsSeparately()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex(@"\.deck-modules__actions \[data-deck-modules-analyze\],\s*\.deck-modules__actions \[data-deck-modules-export\],\s*\.deck-modules__actions \[data-deck-modules-copy\]\s*\{[^}]*border:", RegexOptions.Singleline), content);
    }

    private static string ReadSiteCommonCss()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DeckFlow.Web", "wwwroot", "css", "site-common.css"));
}
