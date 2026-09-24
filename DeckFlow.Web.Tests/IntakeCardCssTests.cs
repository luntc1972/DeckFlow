using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class IntakeCardCssTests
{
    [Fact]
    public void IntakeCardCss_ProvidesDisclosureAndPrintContracts()
    {
        string content = ReadSiteCommonCss();

        Assert.Matches(@"\.cutlab-intake-summary\s*\{[^}]*min-height:\s*44px;[^}]*box-sizing:\s*border-box;", content);
        Assert.Matches(@"\.cutlab-intake-summary\s*\{[^}]*list-style:\s*none;", content);
        Assert.Matches(@"\.cutlab-intake-summary::-webkit-details-marker\s*\{[^}]*display:\s*none;", content);
        Assert.Contains(".cutlab-intake-summary__change::after", content);
        Assert.Matches(@"\.cutlab-intake-summary__change::after\s*\{[^}]*display:\s*inline-block", content);
        Assert.Contains("content: \"\\25BE\";", content);
        Assert.Contains("content: \"\\25BE\" / \"\";", content);
        Assert.Matches(@"\.cutlab-intake\[open\][\s\S]*?\.cutlab-intake-summary__change::after\s*\{[^}]*transform:\s*rotate\(180deg\);", content);
        Assert.Matches(@"\[data-scroll-on-load\]\[tabindex=""-1""\]:focus\s*\{[^}]*outline:\s*none;", content);
        Assert.DoesNotMatch(@"details\.cutlab-intake", content);
        Assert.Matches(@"@media\s+print\s*\{", content);
        Assert.Contains(".cutlab-intake-summary__change,", content);
        Assert.Contains(".cutlab-intake > form.result-panel", content);
        Assert.Contains("display: none !important;", content);
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
