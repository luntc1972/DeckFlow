using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the cEDH Meta Gap reference-deck checkbox touch target on narrow viewports.
/// </summary>
public sealed class CedhMetaGapMobileLayoutCssTests
{
    [Fact]
    public void CedhMetaGapReferenceDeckCheckbox_HasAtLeast44PixelTouchTarget()
    {
        string content = ReadSiteCss();

        Assert.Matches(
            new Regex(
                "@media\\s*\\(max-width:\\s*600px\\)[^{]*\\{(?:(?!@media).)*table\\[data-prompt-cedh-reference-table\\]\\s+td\\[data-label=\\\"Select\\\"\\]\\s+input\\[type=\\\"checkbox\\\"\\]\\s*\\{[^}]*width:\\s*(?:44px|4\\.4rem|4\\.5rem|[5-9]rem|[1-9][0-9]rem)[^}]*height:\\s*(?:44px|4\\.4rem|4\\.5rem|[5-9]rem|[1-9][0-9]rem)",
                RegexOptions.Singleline),
            content);
    }

    private static string ReadSiteCss()
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
