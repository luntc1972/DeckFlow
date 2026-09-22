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
    private IRenderedComponent<CreatorSources> RenderPage(
        FakeCreatorSourceStore store,
        FakeContentSourceStore? sources = null,
        ICreatorSuppressionStore? suppressionStore = null,
        FakeCreatorIdentityResolver? identityResolver = null)
    {
        Services.AddSingleton<ICreatorSourceStore>(store);
        Services.AddSingleton<IContentSourceStore>(sources ?? new FakeContentSourceStore());
        Services.AddSingleton<ICreatorSuppressionStore>(suppressionStore ?? new FakeCreatorSuppressionStore());
        Services.AddSingleton<ICreatorIdentityResolver>(identityResolver ?? new FakeCreatorIdentityResolver());
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
            cut.Find("#creatorName").Change("Example Creator");
            cut.Find("#creatorRef").Change("https://youtube.com/@ExampleCreator");
        });

        cut.WaitForAssertion(() => Assert.False(cut.Find("button.btn-primary").HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(store.AddCalls);
            Assert.Contains("Example Creator", cut.Markup);
        });
    }

    [Fact]
    public async Task CreatorSources_Add_RefusesSuppressedResolvedCreator()
    {
        var store = new FakeCreatorSourceStore();
        var suppression = new FakeCreatorSuppressionStore();
        suppression.Suppressed.Add("suppressed-creator");
        var resolver = new FakeCreatorIdentityResolver();
        resolver.Identities["Suppressed Creator"] = new CreatorIdentity("suppressed-creator", [], [], []);
        var cut = RenderPage(store, suppressionStore: suppression, identityResolver: resolver);

        await cut.InvokeAsync(() =>
        {
            cut.Find("#creatorName").Change("Suppressed Creator");
            cut.Find("#creatorRef").Change("https://youtube.com/@suppressed");
        });
        await cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Suppressed creators cannot be added.", cut.Markup);
        });
        Assert.Empty(store.AddCalls);

        await cut.InvokeAsync(() =>
        {
            cut.Find("#creatorName").Change("Allowed Creator");
            cut.Find("#creatorRef").Change("https://youtube.com/@allowed");
        });
        await cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());
        cut.WaitForAssertion(() => Assert.Single(store.AddCalls));
    }

    [Fact]
    public async Task CreatorSources_Add_FailsClosedWhenSuppressionStoreThrows()
    {
        var store = new FakeCreatorSourceStore();
        var resolver = new FakeCreatorIdentityResolver();
        resolver.Identities["Creator A"] = new CreatorIdentity("creator-a", [], [], []);
        var cut = RenderPage(store, suppressionStore: new ThrowingCreatorSuppressionStore(), identityResolver: resolver);

        await cut.InvokeAsync(() =>
        {
            cut.Find("#creatorName").Change("Creator A");
            cut.Find("#creatorRef").Change("https://youtube.com/@creator-a");
        });
        await cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(store.AddCalls);
            Assert.Contains("Could not verify creator suppression status. The creator was not added.", cut.Markup);
        });
    }

    [Fact]
    public async Task CreatorSources_Add_ReadableSuppressionStore_AddsCreator()
    {
        var store = new FakeCreatorSourceStore();
        var resolver = new FakeCreatorIdentityResolver();
        resolver.Identities["Creator A"] = new CreatorIdentity("creator-a", [], [], []);
        var cut = RenderPage(store, suppressionStore: new FakeCreatorSuppressionStore(), identityResolver: resolver);

        await cut.InvokeAsync(() =>
        {
            cut.Find("#creatorName").Change("Creator A");
            cut.Find("#creatorRef").Change("https://youtube.com/@creator-a");
        });
        await cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());
        cut.WaitForAssertion(() => Assert.Single(store.AddCalls));
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
            cut.Find("#creatorName").Change("Example Creator");
            cut.Find("#creatorRef").Change("https://youtube.com/@ExampleCreator");
        });
        cut.WaitForAssertion(() => Assert.False(cut.Find("button.btn-primary").HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());

        // Provisional display-derived slug is shown immediately, before any harvest.
        cut.WaitForAssertion(() => Assert.Contains("example-creator", cut.Markup));
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
