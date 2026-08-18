using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards every selectable theme against browser-default link colours and inaccessible link contrast.
/// </summary>
public sealed class ThemeLinkColorTests
{
    private const double MinimumContrastRatio = 4.5;
    private const int MaximumVariableDepth = 8;
    private static readonly Regex ThemeOptionRegex = new("\\(\\s*\"(?<file>site-[^\"]+\\.css)\"\\s*,", RegexOptions.Compiled);
    private static readonly Regex DefaultThemeRegex = new("const\\s+string\\s+defaultThemeFileName\\s*=\\s*\"(?<file>[^\"]+\\.css)\"", RegexOptions.Compiled);
    private static readonly Regex CssRuleRegex = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex ColorDeclarationRegex = new(@"(?<![-\w])color\s*:\s*(?<value>[^;}]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CustomPropertyRegex = new(@"(?<name>--[\w-]+)\s*:\s*(?<value>[^;}]+)", RegexOptions.Compiled);
    private static readonly Regex HexColorRegex = new(@"^#(?<hex>[0-9a-f]{3}|[0-9a-f]{6})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex VarRegex = new(@"^var\(\s*(?<name>--[\w-]+)\s*(?:,\s*(?<fallback>.+))?\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Ensures the layout remains the authoritative, non-empty source of themes under test.
    /// </summary>
    [Fact]
    public void DiscoveredThemes_ContainsExpectedNonEmptyThemeSet()
    {
        var themes = DiscoverThemeFiles();

        // A theory whose data source is empty passes silently, leaving this regression guard worthless.
        Assert.True(themes.Count >= 20, "Expected at least 20 themes discovered from _Layout.cshtml.");
        Assert.Contains("site.css", themes);
        Assert.Contains("site-nyx.css", themes);
    }

    /// <summary>
    /// Ensures every selectable theme explicitly resolves a usable bare-link colour.
    /// </summary>
    [Theory]
    [MemberData(nameof(ThemeFiles))]
    public void Theme_BareLinkColorIsDeclaredAndNotBrowserDefault(string themeFileName)
    {
        var linkColor = ResolveEffectiveColor(themeFileName, "a");

        Assert.True(linkColor is not null, $"Theme '{themeFileName}' does not resolve a bare-link colour.");
        Assert.NotEqual("#0000ee", linkColor);
    }

    /// <summary>
    /// Ensures every selectable theme's bare-link colour clears AA contrast on its primary surfaces.
    /// </summary>
    [Theory]
    [MemberData(nameof(ThemeFiles))]
    public void Theme_BareLinkColorMeetsAaContrastAgainstPanelAndBackground(string themeFileName)
    {
        var linkColor = ResolveEffectiveColor(themeFileName, "a");

        Assert.NotNull(linkColor);
        AssertContrast(themeFileName, "--panel", linkColor!, ResolveEffectiveColor(themeFileName, "--panel"));
        AssertContrast(themeFileName, "--bg", linkColor!, ResolveEffectiveColor(themeFileName, "--bg"));
    }

    /// <summary>
    /// Pins link literals required because the shared token is unsuitable for these two themes.
    /// </summary>
    [Fact]
    public void Theme_DeliberateLiteralLinkExceptionsRemainPinned()
    {
        // Jund's --link is #a6613f at 2.89:1 and other consumers require that token; Orzhov's --accent-strong is #0f0d0c at 1.06:1 against body text.
        Assert.Equal("#c7896b", ResolveEffectiveColor("site-jund.css", "a"));
        Assert.Equal("#6c5624", ResolveEffectiveColor("site-orzhov.css", "a"));
    }

    /// <summary>
    /// Provides the layout-declared themes to the theory tests.
    /// </summary>
    public static IEnumerable<object[]> ThemeFiles()
        => DiscoverThemeFiles().Select(fileName => new object[] { fileName });

    private static IReadOnlyList<string> DiscoverThemeFiles()
    {
        var layout = File.ReadAllText(LayoutPath);
        var themes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ThemeOptionRegex.Matches(layout))
        {
            themes.Add(match.Groups["file"].Value);
        }

        var defaultTheme = DefaultThemeRegex.Match(layout);
        Assert.True(defaultTheme.Success, "Could not find defaultThemeFileName in _Layout.cshtml.");
        themes.Add(defaultTheme.Groups["file"].Value);
        return themes.OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? ResolveEffectiveColor(string themeFileName, string target)
    {
        var themeCss = ReadThemeCss(themeFileName);
        var importsBase = themeFileName != "site.css" && Regex.IsMatch(themeCss, "@import\\s+url\\(\\s*['\"]site\\.css['\"]\\s*\\)", RegexOptions.IgnoreCase);
        var baseCss = importsBase ? ReadThemeCss("site.css") : null;
        var value = target == "a"
            ? FindBareLinkColor(themeCss) ?? (baseCss is null ? null : FindBareLinkColor(baseCss))
            : FindCustomProperty(themeCss, target) ?? (baseCss is null ? null : FindCustomProperty(baseCss, target));

        if (value is null)
        {
            return null;
        }

        var variables = ReadCustomProperties(baseCss);
        foreach (var pair in ReadCustomProperties(themeCss))
        {
            variables[pair.Key] = pair.Value;
        }

        return ResolveColorValue(value, variables, 0);
    }

    private static string? FindBareLinkColor(string css)
    {
        string? result = null;
        foreach (Match rule in CssRuleRegex.Matches(RemoveComments(css)))
        {
            if (!rule.Groups["selector"].Value.Split(',').Any(selector => string.Equals(selector.Trim(), "a", StringComparison.Ordinal)))
            {
                continue;
            }

            var declaration = ColorDeclarationRegex.Matches(rule.Groups["body"].Value).LastOrDefault();
            if (declaration is not null)
            {
                result = declaration.Groups["value"].Value.Trim();
            }
        }

        return result;
    }

    private static string? FindCustomProperty(string css, string propertyName)
        => ReadCustomProperties(css).TryGetValue(propertyName, out var value) ? value : null;

    private static Dictionary<string, string> ReadCustomProperties(string? css)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (css is null)
        {
            return properties;
        }

        foreach (Match rule in CssRuleRegex.Matches(RemoveComments(css)))
        {
            if (!rule.Groups["selector"].Value.Split(',').Any(selector => selector.Trim().EndsWith(":root", StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (Match declaration in CustomPropertyRegex.Matches(rule.Groups["body"].Value))
            {
                properties[declaration.Groups["name"].Value] = declaration.Groups["value"].Value.Trim();
            }
        }

        return properties;
    }

    private static string? ResolveColorValue(string value, IReadOnlyDictionary<string, string> variables, int depth)
    {
        if (depth >= MaximumVariableDepth)
        {
            return null;
        }

        var normalized = value.Trim().Replace("!important", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var hex = HexColorRegex.Match(normalized);
        if (hex.Success)
        {
            var valueHex = hex.Groups["hex"].Value.ToLowerInvariant();
            return valueHex.Length == 3
                ? $"#{valueHex[0]}{valueHex[0]}{valueHex[1]}{valueHex[1]}{valueHex[2]}{valueHex[2]}"
                : $"#{valueHex}";
        }

        var variable = VarRegex.Match(normalized);
        if (!variable.Success)
        {
            return null;
        }

        return variables.TryGetValue(variable.Groups["name"].Value, out var replacement)
            ? ResolveColorValue(replacement, variables, depth + 1)
            : variable.Groups["fallback"].Success
                ? ResolveColorValue(variable.Groups["fallback"].Value, variables, depth + 1)
                : null;
    }

    private static void AssertContrast(string themeFileName, string surfaceName, string linkColor, string? surfaceColor)
    {
        Assert.NotNull(surfaceColor);
        var ratio = ContrastRatio(linkColor, surfaceColor!);
        Assert.True(ratio >= MinimumContrastRatio, $"Theme '{themeFileName}' fails against {surfaceName}: {ratio.ToString("F2", CultureInfo.InvariantCulture)}:1 ({linkColor} on {surfaceColor}).");
    }

    private static double ContrastRatio(string first, string second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double RelativeLuminance(string color)
    {
        var red = Convert.ToInt32(color.Substring(1, 2), 16) / 255d;
        var green = Convert.ToInt32(color.Substring(3, 2), 16) / 255d;
        var blue = Convert.ToInt32(color.Substring(5, 2), 16) / 255d;
        return (0.2126 * Linearize(red)) + (0.7152 * Linearize(green)) + (0.0722 * Linearize(blue));
    }

    private static double Linearize(double channel)
        => channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static string RemoveComments(string css)
        => Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    private static string ReadThemeCss(string themeFileName)
        => File.ReadAllText(Path.Combine(CssDirectory, themeFileName));

    private static string LayoutPath => Path.Combine(ProjectDirectory, "Views", "Shared", "_Layout.cshtml");

    private static string CssDirectory => Path.Combine(ProjectDirectory, "wwwroot", "css");

    private static string ProjectDirectory => Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "DeckFlow.Web");
}
