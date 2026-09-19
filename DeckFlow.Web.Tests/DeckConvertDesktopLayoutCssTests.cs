using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards Convert Deck's desktop input/output split without changing Card Lookup's layout.
/// </summary>
public sealed class DeckConvertDesktopLayoutCssTests
{
    [Fact]
    public void DeckConvertDesktopLayout_Uses55_45GridForConvertPanes()
    {
        string content = ReadSiteCommonCss();

        Assert.Contains(".deck-convert-grid", content, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: minmax(0, 55fr) minmax(0, 45fr);", ExtractRuleBody(content, ".deck-convert-grid", 2), StringComparison.Ordinal);
    }

    [Fact]
    public void DeckConvertDesktopLayout_SizesResultRailToContentWithinItsGridColumn()
    {
        string content = ReadSiteCommonCss();

        string railRule = ExtractRuleBody(content, ".deck-convert-grid__rail", 1);

        Assert.Contains("justify-self: start;", railRule, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", railRule, StringComparison.Ordinal);
        Assert.DoesNotContain("align-self: start;", railRule, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckConvertDesktopLayout_KeepsCachePillOutsideGridItems()
    {
        string content = ReadDeckConvertView();
        int formStart = content.IndexOf("<form", StringComparison.Ordinal);
        int formEnd = content.IndexOf('>', formStart);

        Assert.True(formStart >= 0 && formEnd > formStart);
        Assert.DoesNotContain("card-lookup-grid", content[formStart..formEnd], StringComparison.Ordinal);
        Assert.Contains("<div class=\"card-lookup-grid deck-convert-grid\">", content, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckConvertDesktopLayout_PlacesConversionControlsInInputPane()
    {
        string content = ReadDeckConvertView();
        int inputStart = content.IndexOf("deck-convert-grid__input", StringComparison.Ordinal);
        int railStart = content.IndexOf("deck-convert-grid__rail", StringComparison.Ordinal);

        Assert.True(inputStart >= 0 && railStart > inputStart);

        string inputPane = content[inputStart..railStart];
        Assert.Contains("name=\"SourceFormat\"", inputPane, StringComparison.Ordinal);
        Assert.Contains("name=\"InputSource\"", inputPane, StringComparison.Ordinal);
        Assert.Contains("name=\"DeckUrl\"", inputPane, StringComparison.Ordinal);
        Assert.Contains("name=\"DeckText\"", inputPane, StringComparison.Ordinal);
        Assert.Contains("name=\"CommanderOverride\"", inputPane, StringComparison.Ordinal);
        Assert.Contains("name=\"TargetFormat\"", inputPane, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckConvertDesktopLayout_RendersResultOnlyInsideConditionalRightPane()
    {
        string content = ReadDeckConvertView();
        int railStart = content.IndexOf("deck-convert-grid__rail", StringComparison.Ordinal);
        int conditionalStart = content.IndexOf("@if (!string.IsNullOrWhiteSpace(Model.ConvertedText))", railStart, StringComparison.Ordinal);
        int resultStart = content.IndexOf("<section class=\"result-panel\">", conditionalStart, StringComparison.Ordinal);

        Assert.True(railStart >= 0 && conditionalStart > railStart && resultStart > conditionalStart);
        Assert.DoesNotContain("<section class=\"result-panel\">", content[railStart..conditionalStart], StringComparison.Ordinal);
        Assert.Contains("data-copy-target=\"deck-convert-output\"", content[resultStart..], StringComparison.Ordinal);
    }

    private static string ExtractRuleBody(string css, string selector, int occurrence)
    {
        int selectorIndex = -1;
        for (int index = 0; index < occurrence; index++)
        {
            do
            {
                selectorIndex = css.IndexOf(selector, selectorIndex + 1, StringComparison.Ordinal);
            }
            while (selectorIndex >= 0
                && selectorIndex + selector.Length < css.Length
                && !char.IsWhiteSpace(css[selectorIndex + selector.Length])
                && css[selectorIndex + selector.Length] != ',');
        }

        Assert.True(selectorIndex >= 0, $"Missing CSS selector: {selector}");
        int bodyStart = css.IndexOf('{', selectorIndex);
        Assert.True(bodyStart >= 0, $"Missing CSS rule body: {selector}");

        int depth = 1;
        for (int index = bodyStart + 1; index < css.Length; index++)
        {
            depth += css[index] == '{' ? 1 : css[index] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return css[(bodyStart + 1)..index];
            }
        }

        throw new InvalidOperationException($"Unbalanced CSS rule: {selector}");
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

    private static string ReadDeckConvertView()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "Views",
            "Deck",
            "DeckConvert.cshtml"));
}
