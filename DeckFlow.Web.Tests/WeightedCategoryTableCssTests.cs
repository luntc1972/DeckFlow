using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the narrow weighted-category table against numeric-cell wrapping and shared-table regressions.
/// </summary>
public sealed class WeightedCategoryTableCssTests
{
    [Fact]
    public void WeightedCategoryTable_NumericColumnsAreScopedAndUnwrapped()
    {
        var content = ReadSiteCommonCss();

        Assert.Matches(
            new Regex(
                @"\[data-api-panel=""weighted""\][^{}]*\.conflicts-table[^{}]*nth-child\(n \+ 2\)[^{}]*\{[^}]*white-space:\s*nowrap",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                @"\[data-api-panel=""weighted""\][^{}]*\.conflicts-table[^{}]*nth-child\(3\)[^{}]*\{[^}]*min-width:\s*\d+ch",
                RegexOptions.Singleline),
            content);
        Assert.DoesNotMatch(
            new Regex(
                @"(?m)^\.conflicts-table\s+(?:th|td)[^{]*\{[^}]*white-space:\s*nowrap",
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
}
