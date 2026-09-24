using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckModulesSpacingCssTests
{
    [Fact]
    public void DeckModulesSpacing_UsesCompactFlowAndActionBar()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex("\\.deck-modules\\s*\\{[^}]*row-gap:\\s*0;[^}]*column-gap:\\s*0\\.75rem", RegexOptions.Singleline), content);
        Assert.Matches(new Regex("\\.deck-modules > :not\\(:empty\\)\\s*\\{[^}]*margin-block-end:\\s*0\\.75rem", RegexOptions.Singleline), content);
        Assert.Matches(new Regex("\\.deck-modules__actions\\s*\\{[^}]*display:\\s*flex[^}]*flex-wrap:\\s*wrap[^}]*gap:\\s*0\\.5rem[^}]*grid-column:\\s*1 / -1", RegexOptions.Singleline), content);
    }

    [Fact]
    public void DeckModulesEmptyStatusSlots_KeepZeroFlowWithoutDisplayNone()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex("\\.deck-modules > \\[data-deck-modules-live\\]:empty[^}]*min-block-size:\\s*0[^}]*margin:\\s*0[^}]*padding:\\s*0", RegexOptions.Singleline), content);
        Assert.DoesNotMatch(new Regex("\\.deck-modules > \\[data-deck-modules-(?:live|error|notice|reconciliation)\\]:empty[^}]*display:\\s*none", RegexOptions.Singleline), content);
    }

    [Fact]
    public void ConfigurationTextarea_DefaultSize_IsAtMostFiveRem()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex(@"\.deck-modules \.deck-modules__configuration \.deck-modules__field > textarea\s*\{[^}]*min-height:\s*0;[^}]*height:\s*5rem;", RegexOptions.Singleline), content);
    }

    [Fact]
    public void ConfigurationHeadingAndSummaries_Margins_AreZero()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex(@"\.deck-modules__configuration > h2,\s*\.deck-modules__configuration-summaries p\s*\{[^}]*margin:\s*0;", RegexOptions.Singleline), content);
    }

    [Fact]
    public void EmptyStatusSlots_DoNotUseNegativeMarginOrGridRowGap()
    {
        string content = ReadSiteCommonCss();

        foreach (string slot in new[] { "live", "error", "notice", "reconciliation" })
        {
            Match rule = Regex.Match(content, @"\.deck-modules > \[data-deck-modules-" + slot + @"\]:empty[^{}]*\{([^}]*)\}");
            Assert.True(rule.Success);
            Assert.DoesNotContain("margin-block-end", rule.Groups[1].Value);
            Assert.DoesNotMatch(@"(?:display:\s*none|visibility:\s*hidden)", rule.Groups[1].Value);
        }

        Assert.Matches(@"\.deck-modules\s*\{[^}]*row-gap:\s*0;[^}]*column-gap:\s*0\.75rem;", content);
        Assert.Matches(@"@media \(max-width: 760px\)\s*\{\s*\.deck-modules\s*\{[^}]*row-gap:\s*0;[^}]*column-gap:\s*0\.8rem;", content);
        Assert.DoesNotMatch(@"\.deck-modules > \[data-deck-modules-(?:live|error|notice|reconciliation)\]:empty[^}]*margin-block-end:\s*calc\(-1", content);
    }

    private static string ReadSiteCommonCss()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DeckFlow.Web", "wwwroot", "css", "site-common.css"));
}
