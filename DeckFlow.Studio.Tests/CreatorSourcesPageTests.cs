using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Studio.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit behavioral tests for CreatorSources.razor (SRC-01: add / view / remove curated creators).
/// </summary>
public sealed class CreatorSourcesPageTests : BunitContext
{
    private IRenderedComponent<CreatorSources> RenderPage(FakeCreatorSourceStore store)
    {
        Services.AddSingleton<ICreatorSourceStore>(store);
        return Render<CreatorSources>();
    }

    [Fact]
    public void CreatorSources_NoCreators_ShowsEmptyState()
    {
        var cut = RenderPage(new FakeCreatorSourceStore());

        cut.WaitForAssertion(() => Assert.Contains("No creators yet", cut.Markup));
    }

    [Fact]
    public void CreatorSources_WithCreators_RendersRows()
    {
        var store = new FakeCreatorSourceStore();
        store.Seed(("The Command Zone", "https://youtube.com/@TheCommandZone"));

        var cut = RenderPage(store);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("The Command Zone", cut.Markup);
            Assert.Contains("Remove", cut.Markup);
        });
    }

    [Fact]
    public void CreatorSources_Add_PersistsAndRendersRow()
    {
        var store = new FakeCreatorSourceStore();
        var cut = RenderPage(store);

        cut.InvokeAsync(() =>
        {
            cut.Find("#creatorName").Change("Salubrious Snail");
            cut.Find("#creatorRef").Change("https://youtube.com/@SalubriousSnail");
        });

        cut.WaitForAssertion(() => Assert.False(cut.Find("button.btn-primary").HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(store.AddCalls);
            Assert.Contains("Salubrious Snail", cut.Markup);
        });
    }

    [Fact]
    public void CreatorSources_Remove_DropsRowAndCallsStore()
    {
        var store = new FakeCreatorSourceStore();
        store.Seed(("Creator A", "https://youtube.com/@A"));
        var cut = RenderPage(store);

        cut.WaitForAssertion(() => Assert.Contains("Creator A", cut.Markup));
        cut.Find("button[aria-label='Remove Creator A']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Single(store.RemoveCalls);
            Assert.Contains("No creators yet", cut.Markup);
        });
    }
}
