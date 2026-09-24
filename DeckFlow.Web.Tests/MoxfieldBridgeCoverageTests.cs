using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class MoxfieldBridgeCoverageTests
{
    [Fact]
    public void BridgeHintPages_BridgeDispatchContainsTheirFormSelector()
    {
        var viewsDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DeckFlow.Web", "Views");
        var bridgeSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DeckFlow.Web", "wwwroot", "ts", "moxfield-extension-bridge.ts"));
        var pages = Directory.EnumerateFiles(viewsDirectory, "*.cshtml", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).StartsWith('_'))
            .Where(path => RendersBridgeHint(File.ReadAllText(path)))
            .ToList();

        Assert.NotEmpty(pages);

        foreach (var page in pages)
        {
            var content = File.ReadAllText(page);
            var selector = ResolveBridgeSelector(content);
            Assert.True(selector is not null, $"{page} renders the DeckFlow Bridge hint but has no bridge-dispatch selector.");

            // Why: the lookup table fails open - an unrecognised key returns [] with no error, so the hint keeps promising interception that never happens.
            Assert.True(BridgeDispatchesSelector(selector!, bridgeSource),
                $"{page} selector {selector} is not used by bridge dispatch logic.");
        }
    }

    [Fact]
    public void BridgeDispatchSelector_IgnoresCommentOnlyMentions()
    {
        const string source = "// cacheKey === 'bracket'\n/* form.matches('[data-deck-modules-import-form]') */";

        Assert.False(BridgeDispatchesSelector("'bracket'", source));
        Assert.False(BridgeDispatchesSelector("[data-deck-modules-import-form]", source));
    }

    private static bool BridgeDispatchesSelector(string selector, string source)
    {
        var uncommentedSource = Regex.Replace(source, @"//[^\r\n]*|/\*[\s\S]*?\*/", string.Empty);
        if (selector == "[data-deck-modules-import-form]")
        {
            return Regex.IsMatch(uncommentedSource,
                @"form\.matches\(\s*'\[data-deck-modules-import-form\]'\s*\)", RegexOptions.CultureInvariant);
        }

        var cacheKey = selector.Trim('\'');
        var quotedKey = Regex.Escape(cacheKey);
        return Regex.IsMatch(uncommentedSource, $@"cacheKey\s*===\s*'{quotedKey}'", RegexOptions.CultureInvariant)
            || Regex.IsMatch(uncommentedSource,
                $@"new\s+Set\s*\([\s\S]*?'{quotedKey}'[\s\S]*?\)", RegexOptions.CultureInvariant);
    }

    private static bool RendersBridgeHint(string content)
        => content.Contains("_DeckFlowBridgeHint", StringComparison.Ordinal)
            || content.Contains("_DeckImportControl", StringComparison.Ordinal);

    private static string? ResolveBridgeSelector(string content)
    {
        if (content.Contains("data-deck-modules-import-form", StringComparison.Ordinal))
        {
            return "[data-deck-modules-import-form]";
        }

        var cacheKey = Regex.Match(content, "data-cache-key=\\\"([^\\\"]+)\\\"");
        return cacheKey.Success ? $"'{cacheKey.Groups[1].Value}'" : null;
    }
}
