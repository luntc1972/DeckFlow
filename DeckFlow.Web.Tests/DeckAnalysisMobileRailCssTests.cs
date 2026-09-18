using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the mobile-only Deck Analysis progress rail refinements.
/// </summary>
public sealed class DeckAnalysisMobileRailCssTests
{
    private const string RailScope = ".prompt-analysis-workflow .prompt-step-nav--analysis-rail";

    [Fact]
    public void DeckAnalysisMobileRail_DrawsConnectorBehindOpaqueTabs()
    {
        string css = ReadMobileCss();
        string navRule = ExtractRule(css, RailScope + " {");
        string connectorRule = ExtractRule(css, RailScope + "::before {");
        string tabRule = ExtractRule(css, RailScope + " .prompt-step-tab {");
        string opaqueTabRule = ExtractRule(css, RailScope + " .prompt-step-tab:not(.is-active):not([aria-selected=true]):not(.is-complete) {");
        string activeRule = ExtractRule(css, RailScope + " .prompt-step-tab.is-active,");

        Assert.Contains("position: relative;", navRule, StringComparison.Ordinal);
        Assert.Contains("content: \"\";", connectorRule, StringComparison.Ordinal);
        Assert.Contains("background: var(--line);", connectorRule, StringComparison.Ordinal);
        Assert.Contains("z-index: 0;", connectorRule, StringComparison.Ordinal);
        Assert.Contains("z-index: 1;", tabRule, StringComparison.Ordinal);
        Assert.Contains("background: var(--panel);", opaqueTabRule, StringComparison.Ordinal);
        Assert.Contains("background: var(--accent);", activeRule, StringComparison.Ordinal);
        Assert.Contains("color: var(--accent-contrast, #fff);", activeRule, StringComparison.Ordinal);
        Assert.Contains("box-shadow:", activeRule, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckAnalysisMobileRail_ReplacesCompleteNumbersWithCheckmark()
    {
        string css = ReadMobileCss();
        string numberRule = ExtractRule(css, RailScope + " .prompt-step-tab.is-complete .prompt-step-tab__num {");
        string checkRule = ExtractRule(css, RailScope + " .prompt-step-tab.is-complete::before {");

        Assert.Contains("display: none;", numberRule, StringComparison.Ordinal);
        Assert.Contains("content: \"✓\";", checkRule, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", checkRule, StringComparison.Ordinal);
        Assert.Contains("letter-spacing: 0;", checkRule, StringComparison.Ordinal);
        Assert.DoesNotContain(".prompt-step-tab.is-complete::after", css, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckAnalysisMobileRail_StylesLockedAndAvailableOptionalSteps()
    {
        string css = ReadMobileCss();
        string lockedRule = ExtractRule(css, RailScope + " .prompt-step-tab[aria-disabled=true] {");
        string optionalRule = ExtractRule(css, RailScope + " .prompt-step-tab--optional:not(.is-complete):not([aria-disabled=true]) {");

        Assert.Contains("border-style: dashed;", lockedRule, StringComparison.Ordinal);
        Assert.Contains("color: var(--muted);", lockedRule, StringComparison.Ordinal);
        Assert.Contains("cursor: not-allowed;", lockedRule, StringComparison.Ordinal);
        Assert.Contains("border-style: dashed;", optionalRule, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckAnalysisMobileRail_PinsActiveTitleCaptionBelowRow()
    {
        string css = ReadMobileCss();
        string navRule = ExtractRule(css, RailScope + " {");
        string captionRule = ExtractRule(css, RailScope + " .prompt-step-tab.is-active .prompt-step-rail__desktop-title,");
        string copyRule = ExtractRule(css, RailScope + " .prompt-step-tab.is-active .prompt-step-rail__copy,");
        string mobileLabelRule = ExtractRule(css, RailScope + " .prompt-step-tab.is-active .prompt-step-rail__mobile-label,");

        Assert.Contains("padding-bottom:", navRule, StringComparison.Ordinal);
        Assert.Contains(RailScope + " .prompt-step-tab[aria-selected=true] .prompt-step-rail__desktop-title", captionRule, StringComparison.Ordinal);
        Assert.Contains("position: absolute;", captionRule, StringComparison.Ordinal);
        Assert.Contains("text-overflow: ellipsis;", captionRule, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", captionRule, StringComparison.Ordinal);
        Assert.Contains("display: block;", copyRule, StringComparison.Ordinal);
        Assert.Contains("position: static;", copyRule, StringComparison.Ordinal);
        Assert.Contains("overflow: visible;", copyRule, StringComparison.Ordinal);
        Assert.Contains("transform: none;", copyRule, StringComparison.Ordinal);
        Assert.Contains("display: none;", mobileLabelRule, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckAnalysisMobileRail_ScopesEveryAddedSelector()
    {
        string css = ReadMobileCss();
        const string startMarker = "/* The mobile rail needs a shared track without changing other compact steppers. */";
        const string endMarker = "/* mobile UI phase 2: ai selector compact */";
        int start = css.IndexOf(startMarker, StringComparison.Ordinal);
        int end = css.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Missing mobile rail start marker: {startMarker}");
        Assert.True(end > start, $"Missing mobile rail end marker: {endMarker}");
        foreach (string rule in css[start..end].Split('}'))
        {
            int bodyStart = rule.IndexOf('{');
            if (bodyStart >= 0)
            {
                Assert.Contains(RailScope, rule[..bodyStart], StringComparison.Ordinal);
            }
        }
    }

    private static string ReadMobileCss()
    {
        return File.ReadAllText(Path.Combine(
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

    private static string ExtractRule(string css, string selector)
    {
        int selectorIndex = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(selectorIndex >= 0, $"Missing mobile rule: {selector}");
        int bodyStart = css.IndexOf('{', selectorIndex);
        Assert.True(bodyStart >= 0, $"Missing rule body: {selector}");
        int depth = 1;
        for (int index = bodyStart + 1; index < css.Length; index++)
        {
            depth += css[index] == '{' ? 1 : css[index] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return css[selectorIndex..index];
            }
        }

        throw new Xunit.Sdk.XunitException($"Unclosed mobile rule: {selector}");
    }
}
