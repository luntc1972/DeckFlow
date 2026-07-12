using Bunit;
using DeckFlow.Studio.Pages;

namespace DeckFlow.Studio.Tests;

public sealed class GuidePageTests : BunitContext
{
    [Fact]
    public void GuidePage_RendersWorkflowAnchorsAndKeyTerms()
    {
        var cut = Render<Guide>();

        Assert.NotNull(cut.Find("a[href='/harvest']"));
        Assert.NotNull(cut.Find("a[href='/review']"));
        Assert.NotNull(cut.Find("a[href='/publish']"));
        Assert.NotNull(cut.Find("a[href='/direct-push']"));
        Assert.Contains("distill", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("publish", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
