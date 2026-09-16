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
        string mobileRule = ExtractMobileRule();

        Assert.Contains(".deck-form[data-primer-form]", mobileRule, StringComparison.Ordinal);
        Assert.Contains("padding: 0 0.75rem;", mobileRule, StringComparison.Ordinal);
        Assert.Contains(".deck-form[data-primer-form] .run-button", mobileRule, StringComparison.Ordinal);
        Assert.Contains(".deck-form[data-primer-form] .copy-button", mobileRule, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px;", mobileRule, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckPrimerMobileLayout_ConstrainsLongTextInputsAndOutput()
    {
        string mobileRule = ExtractMobileRule();

        Assert.Contains(".deck-form[data-primer-form] textarea[name=\"DeckText\"]", mobileRule, StringComparison.Ordinal);
        Assert.Contains(".deck-form[data-primer-form] #primer-output", mobileRule, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", mobileRule, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere;", mobileRule, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto;", mobileRule, StringComparison.Ordinal);
    }

    private static string ExtractMobileRule()
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
        for (int index = bodyStart + 1; index < css.Length; index++)
        {
            depth += css[index] == '{' ? 1 : css[index] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return css[(bodyStart + 1)..index];
            }
        }

        throw new InvalidOperationException($"Unclosed media query: {mediaQuery}");
    }
}
