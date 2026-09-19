using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class MetaGapAndCutLabViewRegressionTests
{
    [Fact]
    public void CedhMetaGapRecommendedEdits_ShowPriorityForStaplesAndCuts()
    {
        string content = ReadWebFile("Views", "Deck", "CedhMetaGap.cshtml");

        Assert.Contains("<strong>#@item.Priority Add staple: @item.Card</strong>", content, StringComparison.Ordinal);
        Assert.Contains("<strong>#@item.Priority Potential cut: @item.Card</strong>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CedhMetaGapSpeedMismatch_UsesNeutralNonDirectionalChip()
    {
        string content = ReadWebFile("Views", "Deck", "CedhMetaGap.cshtml");

        Assert.Contains("speedMatchesField ? \"↔ 0\" : \"differs\"", content, StringComparison.Ordinal);
        Assert.Contains("class=\"manabase-chip @(speedMatchesField ? \"manabase-chip--ok\" : \"\")\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("speedMatchesField ? \"manabase-chip--ok\" : \"manabase-chip--ok\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("speedMatchesField ? \"↔ 0\" : \"↓ n/a\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("speedMatchesField ? \"manabase-chip--ok\" : \"manabase-chip--low\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CedhMetaGapMirror_LabelsValuesBelowDesktopBreakpoint()
    {
        string view = ReadWebFile("Views", "Deck", "CedhMetaGap.cshtml");
        string css = ReadWebFile("wwwroot", "css", "site-common.css");

        Assert.True(Regex.Matches(view, "class=\"meta-gap-mirror__value\" data-label=\"Your deck\"").Count >= 1);
        Assert.True(Regex.Matches(view, "class=\"meta-gap-mirror__value\" data-label=\"The field\"").Count >= 1);
        Assert.Matches(new Regex("@media\\s*\\(max-width:\\s*1023\\.98px\\)[^{]*\\{(?:(?!@media).)*\\.meta-gap-mirror__header\\s*\\{[^}]*display:\\s*none", RegexOptions.Singleline), css);
        Assert.Matches(new Regex("@media\\s*\\(max-width:\\s*1023\\.98px\\)[^{]*\\{(?:(?!@media).)*\\.meta-gap-mirror__value\\[data-label\\]::before\\s*\\{[^}]*content:\\s*attr\\(data-label\\)", RegexOptions.Singleline), css);
    }

    [Fact]
    public void CutLabConstraintsSummary_HasPoliteAtomicLiveRegion()
    {
        string content = ReadWebFile("Views", "Deck", "CutLab.cshtml");

        Assert.Contains("<div class=\"cutlab-constraints-summary\" aria-live=\"polite\" aria-atomic=\"true\">", content, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulIntakeCollapse_MovesFocusOnlyFromTheCollapsedIntake()
    {
        string categorySource = ReadWebFile("wwwroot", "ts", "category-suggestions.ts");
        string judgeSource = ReadWebFile("wwwroot", "ts", "judge-questions.ts");

        Assert.Contains("document.activeElement !== null && intake.contains(document.activeElement)", categorySource, StringComparison.Ordinal);
        Assert.Contains("focus({ preventScroll: true })", categorySource, StringComparison.Ordinal);
        Assert.Contains("document.activeElement !== null && intake.contains(document.activeElement)", judgeSource, StringComparison.Ordinal);
        Assert.Contains("focus({ preventScroll: true })", judgeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckConvertRail_CollapsesGridWhenRailIsHidden()
    {
        string css = ReadWebFile("wwwroot", "css", "site-common.css");

        Assert.Contains(".deck-convert-grid:has(.deck-convert-grid__rail.hidden)", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 1fr;", css, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] pathSegments)
        => File.ReadAllText(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "DeckFlow.Web", .. pathSegments]));
}
