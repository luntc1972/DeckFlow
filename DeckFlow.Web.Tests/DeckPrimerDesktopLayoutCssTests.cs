using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards Primer's desktop configuration and output workbench split.
/// </summary>
public sealed class DeckPrimerDesktopLayoutCssTests
{
    [Fact]
    public void DeckPrimerDesktopLayout_ExtendsShared900PixelGridRule()
    {
        string css = ReadSiteCommonCss();
        string gridRule = ExtractRuleBody(css, ".card-lookup-grid", 2);
        string view = ReadDeckPrimerView();

        Assert.Contains("grid-template-columns: minmax(0, 55fr) minmax(0, 45fr);", gridRule, StringComparison.Ordinal);
        Assert.Contains("<div class=\"card-lookup-grid primer-workbench-grid\">", view, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckPrimerDesktopLayout_MakesOutputPaneSticky()
    {
        string css = ReadSiteCommonCss();
        string outputRule = ExtractRuleBody(css, ".primer-workbench-grid__output", 1);

        Assert.Contains("position: sticky;", outputRule, StringComparison.Ordinal);
        Assert.Contains("top: 1rem;", outputRule, StringComparison.Ordinal);
        Assert.Contains("max-height: calc(100vh - 2rem);", outputRule, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto;", outputRule, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckPrimerDesktopLayout_KeepsIntakeInConfigAndOutputOutsideIntakeCard()
    {
        string view = ReadDeckPrimerView();
        int gridStart = view.IndexOf("primer-workbench-grid", StringComparison.Ordinal);
        int configStart = view.IndexOf("primer-workbench-grid__config", StringComparison.Ordinal);
        int intakeStart = view.IndexOf("Html.BeginIntakeCard", StringComparison.Ordinal);
        int outputStart = view.IndexOf("primer-workbench-grid__output", StringComparison.Ordinal);
        int stepOne = view.IndexOf("id=\"primer-step-panel-1\"", StringComparison.Ordinal);
        int stepTwo = view.IndexOf("id=\"primer-step-panel-2\"", StringComparison.Ordinal);
        int stepThree = view.IndexOf("id=\"primer-step-panel-3\"", StringComparison.Ordinal);

        Assert.True(gridStart >= 0 && configStart > gridStart && intakeStart > configStart);
        Assert.True(stepOne > intakeStart && stepOne < outputStart);
        Assert.True(stepTwo > intakeStart && stepTwo < outputStart);
        Assert.True(stepThree > outputStart);
    }

    private static string ExtractRuleBody(string css, string selector, int occurrence)
    {
        int selectorIndex = -1;
        for (int index = 0; index < occurrence; index++)
        {
            selectorIndex = css.IndexOf(selector, selectorIndex + 1, StringComparison.Ordinal);
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

    private static string ReadDeckPrimerView()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "Views",
            "Deck",
            "DeckPrimer.cshtml"));
}
