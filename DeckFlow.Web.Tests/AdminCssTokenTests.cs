using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the Phase 4 admin token system (D-01, D-02, D-03, D-05).
/// </summary>
public sealed class AdminCssTokenTests
{
    [Theory]
    [InlineData("--bg", "#0f172a")]
    [InlineData("--panel", "#1e293b")]
    [InlineData("--text", "#e2e8f0")]
    [InlineData("--muted", "#94a3b8")]
    [InlineData("--accent", "#3b82f6")]
    [InlineData("--border", "#334155")]
    [InlineData("--on-accent", "#fff")]
    [InlineData("--space-xs", "4px")]
    [InlineData("--space-sm", "8px")]
    [InlineData("--space-md", "16px")]
    [InlineData("--space-lg", "24px")]
    [InlineData("--space-xl", "32px")]
    [InlineData("--space-2xl", "48px")]
    [InlineData("--space-3xl", "64px")]
    [InlineData("--radius-sm", "4px")]
    [InlineData("--text-small", "12px")]
    [InlineData("--text-label", "13px")]
    [InlineData("--text-body", "15px")]
    [InlineData("--text-heading", "20px")]
    [InlineData("--text-small-weight", "400")]
    [InlineData("--text-label-weight", "500")]
    [InlineData("--text-body-weight", "400")]
    [InlineData("--text-heading-weight", "600")]
    [InlineData("--text-small-leading", "1.4")]
    [InlineData("--text-label-leading", "1.4")]
    [InlineData("--text-body-leading", "1.5")]
    [InlineData("--text-heading-leading", "1.3")]
    [InlineData("--status-success", "#22c55e")]
    [InlineData("--status-success-bg", "rgba(34, 197, 94, 0.15)")]
    [InlineData("--status-warning", "#eab308")]
    [InlineData("--status-warning-bg", "rgba(234, 179, 8, 0.15)")]
    [InlineData("--status-danger", "#ef4444")]
    [InlineData("--status-danger-bg", "rgba(239, 68, 68, 0.15)")]
    [InlineData("--status-info", "#06b6d4")]
    [InlineData("--status-info-bg", "rgba(6, 182, 212, 0.15)")]
    [InlineData("--accent-subtle", "rgba(59, 130, 246, 0.08)")]
    [InlineData("--status-danger-solid", "#dc2626")]
    [InlineData("--status-danger-solid-hover", "color-mix(in srgb, var(--status-danger-solid) 85%, var(--bg))")]
    [InlineData("--accent-tint", "rgba(59, 130, 246, 0.12)")]
    [InlineData("--hover-tint", "rgba(255, 255, 255, 0.04)")]
    [InlineData("--shadow-color", "rgba(0, 0, 0, 0.35)")]
    [InlineData("--shadow-strong", "rgba(0, 0, 0, 0.5)")]
    [InlineData("--overlay-backdrop", "rgba(15, 23, 42, 0.72)")]
    public void Root_DeclaresToken(string name, string value)
    {
        var declarations = Declarations(RootBlock(ReadCss("admin-common.css")));
        Assert.True(declarations.TryGetValue(name, out var actual));
        Assert.Equal(CollapseWhitespace(value), CollapseWhitespace(actual));
    }

    [Fact]
    public void Root_RetiresLegacyWarningAndDangerTokens()
    {
        foreach (var fileName in new[] { "admin-common.css", "admin-mobile.css" })
        {
            Assert.DoesNotMatch(new Regex(@"(?<![\w-])--(?:warning|danger)(?![\w-])"), StripComments(ReadCss(fileName)));
        }
    }

    [Theory]
    [InlineData("admin-common.css")]
    [InlineData("admin-mobile.css")]
    public void NoColorLiteralOutsideRoot(string fileName)
    {
        var css = StripComments(ReadCss(fileName));
        css = Regex.Replace(css, @":root\s*\{[^{}]*\}", string.Empty, RegexOptions.Singleline);
        var colorPattern = new Regex(@"#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(|(?<![-\w])(?:white|black)(?![-\w])");

        foreach (var (selector, body) in Rules(css))
        {
            foreach (var declaration in Declarations(body))
            {
                Assert.False(colorPattern.IsMatch(declaration.Value), $"{fileName}: {selector.Trim()} contains {declaration.Key}: {declaration.Value}");
            }
        }
    }

    [Theory]
    [InlineData("admin-common.css")]
    [InlineData("admin-mobile.css")]
    public void SpacingDeclarationsUseScaleTokens(string fileName)
    {
        var allowed = new Regex(@"^(?:0|auto|var\(--space-(?:xs|sm|md|lg|xl|2xl|3xl)\))(?:\s+(?:0|auto|var\(--space-(?:xs|sm|md|lg|xl|2xl|3xl)\)))*$");
        foreach (var (selector, body) in Rules(ReadCss(fileName)))
        {
            foreach (var declaration in Declarations(body).Where(item => Regex.IsMatch(item.Key, @"^(?:margin|padding)(?:-.+)?$|^(?:gap|row-gap|column-gap)$")))
            {
                if (selector.Trim() == ".sr-only" && declaration.Key == "margin" && declaration.Value == "-1px") continue;
                var value = declaration.Value.Replace("!important", string.Empty).Trim();
                Assert.True(allowed.IsMatch(value), $"{fileName}: {selector.Trim()} has {declaration.Key}: {declaration.Value}");
            }
        }
    }

    [Theory]
    [InlineData("admin-common.css")]
    [InlineData("admin-mobile.css")]
    public void FontSizesUseTypeScale(string fileName) => AssertDeclarationsMatch(fileName, "font-size", @"^(?:var\(--text-(?:small|label|body|heading)\)|inherit)$");

    [Theory]
    [InlineData("admin-common.css")]
    [InlineData("admin-mobile.css")]
    public void FontWeightsStayInScale(string fileName) => AssertDeclarationsMatch(fileName, "font-weight", @"^(?:400|500|600|var\(--text-(?:small|label|body|heading)-weight\)|inherit)$");

    [Theory]
    [InlineData("admin-common.css")]
    [InlineData("admin-mobile.css")]
    public void FontShorthandOnlyInherit(string fileName) => AssertDeclarationsMatch(fileName, "font", "^inherit$");

    [Theory]
    [InlineData("admin-common.css")]
    [InlineData("admin-mobile.css")]
    public void LineHeightsUseLeadingTokens(string fileName) => AssertDeclarationsMatch(fileName, "line-height", @"^(?:var\(--text-(?:small|label|body|heading)-leading\)|0)$");

    [Fact]
    public void AdminTable_KeepsCompactCellsAndTabularNumerals()
    {
        var rules = Rules(ReadCss("admin-common.css"));
        var cells = rules.Single(rule => Regex.Replace(rule.Selector, @"\s+", " ").Trim() == ".admin-shell .admin-table th, .admin-shell .admin-table td");
        Assert.Equal("var(--space-xs) var(--space-sm)", Declarations(cells.Body)["padding"]);
        Assert.Contains(rules, rule => rule.Selector.Contains(".admin-table td", StringComparison.Ordinal) && Declarations(rule.Body).TryGetValue("font-variant-numeric", out var value) && value == "tabular-nums");
    }

    internal static string ReadCss(string fileName)
    {
        var directory = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(directory, "DeckFlow.Web")))
        {
            directory = Directory.GetParent(directory)?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate DeckFlow.Web.");
        }

        return File.ReadAllText(Path.Combine(directory, "DeckFlow.Web", "wwwroot", "css", fileName));
    }

    internal static string StripComments(string css) => Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    internal static string RootBlock(string css)
    {
        // Why: :root is the only place a color literal may live (D-02).
        var match = Regex.Match(StripComments(css), @":root\s*\{([^{}]*)\}", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : throw new InvalidOperationException("Missing :root block.");
    }

    internal static IReadOnlyList<(string Selector, string Body)> Rules(string css) =>
        Regex.Matches(Regex.Replace(StripComments(css), @":root\s*\{[^{}]*\}", string.Empty, RegexOptions.Singleline), @"([^{}]+)\{([^{}]*)\}")
            .Select(match => (match.Groups[1].Value, match.Groups[2].Value)).ToArray();

    internal static IReadOnlyDictionary<string, string> Declarations(string body) =>
        body.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

    private static string CollapseWhitespace(string value) => Regex.Replace(value, @"\s+", " ").Trim();

    private static void AssertDeclarationsMatch(string fileName, string property, string pattern)
    {
        foreach (var (selector, body) in Rules(ReadCss(fileName)))
        {
            if (Declarations(body).TryGetValue(property, out var value))
            {
                Assert.True(new Regex(pattern).IsMatch(value), $"{fileName}: {selector.Trim()} has {property}: {value}");
            }
        }
    }
}
