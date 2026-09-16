using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards Primer's narrow-viewport workspace refinements.
/// </summary>
public sealed class DeckPrimerMobileLayoutCssTests
{
    [Fact]
    public void DeckPrimerMobileLayout_ProvidesWorkspacePaddingAndTouchTargets()
    {
        string css = ReadMobileCss();
        string formRule = ExtractRule(css, ".deck-form[data-primer-form] {");
        string touchTargetRule = ExtractRule(css, ".deck-form[data-primer-form] .run-button,");
        string summaryRule = ExtractRule(css, ".deck-form[data-primer-form] .prompt-resume > summary,", 2);

        Assert.Contains("padding: 0 0.75rem;", formRule, StringComparison.Ordinal);
        Assert.Contains(".deck-form[data-primer-form] .copy-button", touchTargetRule, StringComparison.Ordinal);
        Assert.Contains(".deck-form[data-primer-form] .prompt-resume > summary", touchTargetRule, StringComparison.Ordinal);
        Assert.Contains(".deck-form[data-primer-form] .primer-section__help > summary", touchTargetRule, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px;", touchTargetRule, StringComparison.Ordinal);
        Assert.Contains("display: list-item;", summaryRule, StringComparison.Ordinal);
        Assert.Contains("line-height: 1.5;", summaryRule, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckPrimerMobileLayout_ConstrainsLongTextInputsAndOutput()
    {
        string css = ReadMobileCss();
        string inputSizingRule = ExtractRule(css, ".deck-form[data-primer-form] textarea[name=\"DeckText\"],");
        string inputOverflowRule = ExtractRule(css, ".deck-form[data-primer-form] textarea[name=\"DeckText\"],", 2);

        Assert.Contains(".deck-form[data-primer-form] #primer-output", inputSizingRule, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", inputSizingRule, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere;", inputOverflowRule, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto;", inputOverflowRule, StringComparison.Ordinal);
    }

    private static string ReadMobileCss()
    {
        string css = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "wwwroot",
            "css",
            "site-mobile.css"));
        const string mediaQuery = "@media (max-width: 900px)";
        int selectorIndex = css.LastIndexOf(mediaQuery, StringComparison.Ordinal);

        Assert.True(selectorIndex >= 0, $"Missing mobile media query: {mediaQuery}");
        int bodyStart = css.IndexOf('{', selectorIndex);
        Assert.True(bodyStart >= 0, $"Missing media query body: {mediaQuery}");

        int depth = 1;
        int bodyEnd = -1;
        for (int index = bodyStart + 1; index < css.Length; index++)
        {
            depth += css[index] == '{' ? 1 : css[index] == '}' ? -1 : 0;
            if (depth == 0)
            {
                bodyEnd = index;
                break;
            }
        }

        Assert.True(bodyEnd >= 0, $"Unclosed media query: {mediaQuery}");
        return css[bodyStart..bodyEnd];
    }

    private static string ExtractRule(string css, string selector, int occurrence = 1)
    {
        int selectorIndex = -1;
        for (int index = 0; index < occurrence; index++)
        {
            selectorIndex = css.IndexOf(selector, selectorIndex + 1, StringComparison.Ordinal);
        }
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
