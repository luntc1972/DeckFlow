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
    private IRenderedComponent<CreatorSources> RenderPage(FakeCreatorSourceStore store, FakeContentSourceStore? sources = null)
    {
        Services.AddSingleton<ICreatorSourceStore>(store);
        Services.AddSingleton<IContentSourceStore>(sources ?? new FakeContentSourceStore());
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
        // Why: wrap Find+Click in InvokeAsync so the async status-load re-render can't invalidate the
        // button's event-handler id between the two calls (bUnit UnknownEventHandlerIdException).
        cut.InvokeAsync(() => cut.Find("button[aria-label='Remove Creator A']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(store.RemoveCalls);
            Assert.Contains("No creators yet", cut.Markup);
        });
    }

    [Fact]
    public void CreatorSources_Add_ShowsProvisionalSlug()
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

        // Provisional display-derived slug is shown immediately, before any harvest.
        cut.WaitForAssertion(() => Assert.Contains("salubrious-snail", cut.Markup));
    }

    [Fact]
    public void CreatorSources_UnharvestedCreator_ShowsPendingStatus()
    {
        var store = new FakeCreatorSourceStore();
        store.Seed(("Creator A", "https://youtube.com/@A"));

        var cut = RenderPage(store);

        cut.WaitForAssertion(() => Assert.Contains("Pending first harvest", cut.Markup));
    }

    [Fact]
    public void CreatorSources_LinkedToEnabledSource_ShowsLinked()
    {
        var sources = new FakeContentSourceStore();
        var sourceId = sources.Seed("creator-a", "https://youtube.com/@A", isEnabled: true);
        var store = new FakeCreatorSourceStore();
        store.SeedLinked("Creator A", "https://youtube.com/@A", "creator-a", sourceId);

        var cut = RenderPage(store, sources);

        cut.WaitForAssertion(() => Assert.Contains("Linked", cut.Markup));
    }

    [Fact]
    public void CreatorSources_LinkedToDisabledSource_ShowsDisabled()
    {
        var sources = new FakeContentSourceStore();
        var sourceId = sources.Seed("creator-a", "https://youtube.com/@A", isEnabled: false);
        var store = new FakeCreatorSourceStore();
        store.SeedLinked("Creator A", "https://youtube.com/@A", "creator-a", sourceId);

        var cut = RenderPage(store, sources);

        cut.WaitForAssertion(() => Assert.Contains("Disabled", cut.Markup));
    }

    [Fact]
    public void CreatorSources_DanglingContentSourceId_ShowsMissingSource()
    {
        var store = new FakeCreatorSourceStore();
        // ContentSourceId points at a row that does not exist in the (empty) content-source store.
        store.SeedLinked("Creator A", "https://youtube.com/@A", "creator-a", contentSourceId: 999);

        var cut = RenderPage(store, new FakeContentSourceStore());

        cut.WaitForAssertion(() => Assert.Contains("Missing source", cut.Markup));
    }

    [Fact]
    public void CreatorSources_RemoveCreator_KeepsSourceEnabled_WhenAnotherAliasSharesIt()
    {
        // Two creator aliases (@handle and /channel/UC...) that canonicalize to the same content
        // source. Removing one must NOT disable the shared source — the surviving alias still links it.
        var sources = new FakeContentSourceStore();
        var sourceId = sources.Seed("shared", "https://youtube.com/channel/UCx", isEnabled: true);
        var store = new FakeCreatorSourceStore();
        store.SeedLinked("Alias One", "https://youtube.com/@one", "shared", sourceId);
        store.SeedLinked("Alias Two", "https://youtube.com/channel/UCx", "shared", sourceId);

        var cut = RenderPage(store, sources);
        cut.WaitForAssertion(() => Assert.Contains("Alias One", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button[aria-label='Remove Alias One']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(store.RemoveCalls);
            Assert.Empty(sources.SetEnabledCalls);
        });
    }

    [Fact]
    public void CreatorSources_RemoveLinkedCreator_DisablesSourceAndKeepsArtifacts()
    {
        var sources = new FakeContentSourceStore();
        var sourceId = sources.Seed("creator-a", "https://youtube.com/@A", isEnabled: true);
        var store = new FakeCreatorSourceStore();
        store.SeedLinked("Creator A", "https://youtube.com/@A", "creator-a", sourceId);

        var cut = RenderPage(store, sources);
        cut.WaitForAssertion(() => Assert.Contains("Creator A", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button[aria-label='Remove Creator A']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(store.RemoveCalls);
            // Cascade disabled the linked content source (never deleted it) and kept the artifacts.
            Assert.Contains((sourceId, false), sources.SetEnabledCalls);
            Assert.Contains("were kept", cut.Markup);
        });
    }
}
