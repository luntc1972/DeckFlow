using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckModulesGuidanceCssTests
{
    [Fact]
    public void DeckModulesGuidance_UsesThemeSafeTokensAndAccessibleDisabledTreatment()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex(@"\.deck-modules__hint\s*\{[^}]*font-size:\s*var\(--fs-sm,\s*0\.85rem\);", RegexOptions.Singleline), content);
        Assert.Matches(new Regex(@"\.deck-modules \[aria-disabled=\""true\""\]\s*\{[^}]*color:\s*var\(--muted\);[^}]*background:\s*var\(--panel\);[^}]*cursor:\s*default;", RegexOptions.Singleline), content);
        Assert.Matches(new Regex(@"@media \(forced-colors: active\)\s*\{[^}]*\.deck-modules \[aria-disabled=\""true\""\]\s*\{[^}]*color:\s*GrayText;", RegexOptions.Singleline), content);
    }

    [Fact]
    public void DeckModulesGuidance_CollapsesWorkspaceOnlyForTheClientEmptyStage()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(new Regex(@"\[data-deck-modules-stage=\""empty\""\] \.deck-modules__commander,[^}]*\.deck-modules__actions\s*\{\s*display:\s*none;", RegexOptions.Singleline), content);
        Assert.Matches(new Regex(@"\[data-deck-modules-stage=\""empty\""\] \.deck-modules__empty\s*\{\s*display:\s*block;", RegexOptions.Singleline), content);
    }

    [Fact]
    public void DeckModulesGuidance_KeepsThemeForksCleanAndDefinesGuidanceElements()
    {
        string content = ReadSiteCommonCss();
        string cssDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DeckFlow.Web", "wwwroot", "css");

        foreach (string path in Directory.GetFiles(cssDirectory, "site-*.css"))
        {
            string name = Path.GetFileName(path);
            if (name is "site-common.css" or "site-mobile.css") continue;
            Assert.DoesNotContain("deck-modules", File.ReadAllText(path));
        }

        foreach (string selector in new[] { ".deck-modules__next", ".deck-modules__howto", ".deck-modules__empty", ".deck-modules__blocked" })
            Assert.Contains(selector, content);
    }

    [Fact]
    public void DeckModulesGuidance_ProvidesFallbacksForThemeSensitiveTokens()
    {
        string content = ReadSiteCommonCss();
        Match block = Regex.Match(content, @"/\* Deck Modules: shared responsive layout for all guild themes\. \*/.*?/\* end Phase 3: deck-modules analysis \*/", RegexOptions.Singleline);
        Regex bareToken = new(@"var\(--(?:fs-|panel-soft-bg|on-accent|focus|link)[^,)]*\)", RegexOptions.Singleline);

        Assert.True(block.Success, "Deck Modules CSS block was not found.");
        Assert.Matches(bareToken, "var(--focus)");
        Assert.DoesNotMatch(bareToken, block.Value);
    }

    private static string ReadSiteCommonCss()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DeckFlow.Web", "wwwroot", "css", "site-common.css"));
}
