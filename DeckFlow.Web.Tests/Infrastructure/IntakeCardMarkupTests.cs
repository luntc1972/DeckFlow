using System.IO;
using System.Text.Encodings.Web;
using DeckFlow.Web.Infrastructure;
using Xunit;

namespace DeckFlow.Web.Tests.Infrastructure;

public sealed class IntakeCardMarkupTests
{
    [Fact]
    public void Open_Empty_ReturnsExpectedMarkup()
    {
        var options = new IntakeCardOptions(false, null);

        Assert.Equal(
            "<div class=\"cutlab-intake cutlab-intake--empty\">",
            IntakeCardMarkup.Open(options, HtmlEncoder.Default));
        Assert.Equal("</div>", IntakeCardMarkup.Close(false));
    }

    [Fact]
    public void Open_Result_ReturnsExpectedMarkup()
    {
        var options = new IntakeCardOptions(true, "Commander", "Change");

        Assert.Equal(
            "<details class=\"cutlab-intake\" data-cut-lab-intake-summary><summary class=\"cutlab-intake-summary\"><span class=\"cutlab-intake-summary__commander\">Commander</span><span class=\"cutlab-intake-summary__change\">Change</span></summary>",
            IntakeCardMarkup.Open(options, HtmlEncoder.Default));
        Assert.Equal("</details>", IntakeCardMarkup.Close(true));
    }

    [Fact]
    public void Open_Result_EncodesLabelsAndUsesFallback()
    {
        var options = new IntakeCardOptions(true, "<script>x</script>", "&'");

        Assert.Equal(
            "<details class=\"cutlab-intake\" data-cut-lab-intake-summary><summary class=\"cutlab-intake-summary\"><span class=\"cutlab-intake-summary__commander\">&lt;script&gt;x&lt;/script&gt;</span><span class=\"cutlab-intake-summary__change\">&amp;&#x27;</span></summary>",
            IntakeCardMarkup.Open(options, HtmlEncoder.Default));
        Assert.Contains("Deck input", IntakeCardMarkup.Open(new IntakeCardOptions(true, " \t"), HtmlEncoder.Default));
    }

    [Fact]
    public void Open_Result_UsesDefaultChangeLabel()
    {
        Assert.Contains(">Edit</span>", IntakeCardMarkup.Open(new IntakeCardOptions(true, "Commander"), HtmlEncoder.Default));
    }

    [Fact]
    public void Scope_Dispose_WritesCloseMarkupOnlyOnce()
    {
        using var writer = new StringWriter();
        var scope = new IntakeCardScope(writer, "</div>");

        scope.Dispose();
        scope.Dispose();

        Assert.Equal("</div>", writer.ToString());
    }
}
