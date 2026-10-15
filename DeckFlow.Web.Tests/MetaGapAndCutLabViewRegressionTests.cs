using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class MetaGapAndCutLabViewRegressionTests
{
    [Fact]
    public void CedhMetaGapRecommendedEdits_ShowPriorityForStaplesAndCuts()
    {
        string content = ReadWebFile("Views", "Deck", "CedhMetaGap.cshtml");

        Assert.Contains("<span class=\"manabase-chip manabase-chip--good\">Add staple</span> <strong>#@item.Priority @item.Card</strong>", content, StringComparison.Ordinal);
        Assert.Contains("<span class=\"manabase-chip manabase-chip--low\">Potential cut</span> <strong>#@item.Priority @item.Card</strong>", content, StringComparison.Ordinal);
        Assert.Contains("names the card it replaces", content, StringComparison.Ordinal);
        Assert.Contains("low-impact", content, StringComparison.Ordinal);
        Assert.Contains("2 or more reference decks", content, StringComparison.Ordinal);
        Assert.Contains("none of the reference decks", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CedhMetaGapMirror_UsesBoundedDesktopStatColumn()
    {
        string css = ReadWebFile("wwwroot", "css", "site-common.css");

        Assert.Contains("grid-template-columns: minmax(8rem, 11rem) minmax(0, 1fr) auto minmax(0, 1fr);", css, StringComparison.Ordinal);
        Assert.DoesNotContain("grid-template-columns: minmax(8rem, 0.8fr) minmax(0, 1fr) auto minmax(0, 1fr);", css, StringComparison.Ordinal);
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
    public void CedhMetaGapResults_BreaksOutOnlyOnWideScreens()
    {
        string view = ReadWebFile("Views", "Deck", "CedhMetaGap.cshtml");
        string css = ReadWebFile("wwwroot", "css", "site-common.css");

        Assert.Contains("class=\"result-panel nested-panel summary-panel meta-gap-results\"", view, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 1280px)", css, StringComparison.Ordinal);
        Assert.Contains(".meta-gap-results", css, StringComparison.Ordinal);
        Assert.Contains("width: min(1440px, 100vw - 4rem);", css, StringComparison.Ordinal);
        Assert.Contains("margin-inline: calc(50% - min(1440px, 100vw - 4rem) / 2);", css, StringComparison.Ordinal);
        Assert.Contains("--shell-max-width: 1120px;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void CedhMetaGapEditDetails_SeparatesRoleAndReferenceCount()
    {
        string view = ReadWebFile("Views", "Deck", "CedhMetaGap.cshtml");

        Assert.Contains("<text>@item.Role</text> } @if (item.RefCount > 0)", view, StringComparison.Ordinal);
        Assert.Contains("<text> · </text>", view, StringComparison.Ordinal);
        Assert.Contains("<text>seen in @item.RefCount reference deck(s)</text>", view, StringComparison.Ordinal);
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
