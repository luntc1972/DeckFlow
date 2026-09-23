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
        AssertFullWidthRegions(content);
    }

    [Fact]
    public void FullWidthRegions_ReorderedSelectorsPassButMissingRequirementsFail()
    {
        const string unrelatedRule = "@media (min-width: 1px) { .unrelated { grid-column: 1 / -1; } }";
        string[] selectors =
        [
            ".deck-modules__report",
            ".deck-modules__unassigned",
            ".deck-modules__configuration",
            ".deck-modules__empty",
            ".deck-modules__howto",
        ];
        const string declaration = " { grid-column: 1 / -1; }";

        AssertFullWidthRegions(unrelatedRule + string.Join(", ", selectors) + declaration);
        foreach (var missingSelector in selectors)
        {
            var incompleteRule = string.Join(", ", selectors.Where(selector => selector != missingSelector)) + declaration;
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFullWidthRegions(unrelatedRule + incompleteRule));
        }

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertFullWidthRegions(unrelatedRule + string.Join(", ", selectors) + " { color: red; }"));
    }

    private static void AssertFullWidthRegions(string content)
    {
        var uncommentedContent = Regex.Replace(content, @"/\*[\s\S]*?\*/", string.Empty);
        var fullWidthRule = Regex.Matches(uncommentedContent,
                @"(?:\A|(?<=[{}]))\s*(?<selectors>[^{};@]+)\{(?<declarations>[^{}]*)\}", RegexOptions.Singleline)
            .Cast<Match>()
            .FirstOrDefault(match => match.Groups["selectors"].Value
                .Split(',', StringSplitOptions.TrimEntries).Contains(".deck-modules__howto", StringComparer.Ordinal));

        Assert.NotNull(fullWidthRule);
        Assert.Matches(@"(?:\A|;)\s*grid-column:\s*1\s*/\s*-1\s*;", fullWidthRule.Groups["declarations"].Value);
        var ruleSelectors = fullWidthRule.Groups["selectors"].Value.Split(',', StringSplitOptions.TrimEntries);
        foreach (var selector in new[]
        {
            ".deck-modules__howto",
            ".deck-modules__empty",
            ".deck-modules__configuration",
            ".deck-modules__unassigned",
            ".deck-modules__report",
        })
        {
            Assert.Contains(selector, ruleSelectors);
        }
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
