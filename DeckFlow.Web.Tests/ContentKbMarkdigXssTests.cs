using Markdig;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Pins the Content KB Markdown render posture: the controller must keep
/// <c>UseAdvancedExtensions().DisableHtml()</c> so harvested raw HTML stays inert.
/// </summary>
public sealed class ContentKbMarkdigXssTests
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    [Fact]
    public void ToHtml_DisableHtmlPipeline_DoesNotEmitScriptTags()
    {
        var rendered = Markdown.ToHtml("<script>alert(1)</script>", Pipeline);

        Assert.DoesNotContain("<script>", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToHtml_DisableHtmlPipeline_StillRendersMarkdownFormatting()
    {
        var rendered = Markdown.ToHtml("**bold**", Pipeline);

        Assert.Contains("<strong>bold</strong>", rendered, StringComparison.Ordinal);
    }
}
