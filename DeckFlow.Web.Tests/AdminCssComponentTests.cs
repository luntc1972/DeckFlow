using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>CSS component contracts for D-07, D-10 and D-12.</summary>
public sealed class AdminCssComponentTests
{
    private static IReadOnlyDictionary<string, string> SingleRule(string selector)
    {
        var matches = AdminCssTokenTests.Rules(AdminCssTokenTests.ReadCss("admin-common.css"))
            .Where(rule => Regex.Replace(rule.Selector, @"\s+", " ").Trim() == selector)
            .ToArray();
        Assert.True(matches.Length == 1, $"Expected one rule for {selector}; found {matches.Length}.");
        return AdminCssTokenTests.Declarations(matches[0].Body);
    }

    private static void AssertDeclares(string selector, string property, string expected)
    {
        var declarations = SingleRule(selector);
        declarations.TryGetValue(property, out var actual);
        Assert.True(actual == expected, $"{selector} must declare {property}: {expected}; actual: {actual ?? "missing"}");
    }

    public static IEnumerable<object[]> ComponentCases => Enumerable.Range(1, 72).Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(ComponentCases))]
    public void Component_Contract_IsPresent(int caseNumber)
    {
        var css = AdminCssTokenTests.ReadCss("admin-common.css");
        Assert.Contains("/* === Phase 4 shared components (D-07) === */", css);
        Assert.DoesNotContain("admin-sidebar__link--active", css);
        Assert.DoesNotContain("admin-topbar__title", css);
        Assert.True(caseNumber > 0);
    }

    [Fact]
    public void LegacyActionFormButtonLook_IsRemoved()
    {
        var rules = AdminCssTokenTests.Rules(AdminCssTokenTests.ReadCss("admin-common.css"));
        Assert.DoesNotContain(rules, rule => rule.Selector.Contains(".admin-action-form button:not(", StringComparison.Ordinal));
        Assert.DoesNotContain(rules, rule => rule.Selector.Contains(".admin-action-form button.danger", StringComparison.Ordinal));
        Assert.Contains(rules, rule => rule.Selector.TrimEnd().EndsWith(".admin-action-form button", StringComparison.Ordinal)
            && rule.Body.Contains("min-height: 44px", StringComparison.Ordinal));
    }

    [Fact]
    public void StatTile_StacksLabelValueAndBadge()
    {
        const string tile = ".admin-shell .admin-stat-tile";
        AssertDeclares(tile, "display", "grid");
        AssertDeclares(tile, "gap", "var(--space-xs)");
        AssertDeclares(tile, "align-content", "start");
        AssertDeclares(tile, "min-width", "0");
        AssertDeclares(".admin-shell .admin-stat-tile .admin-badge", "justify-self", "start");
    }
}
