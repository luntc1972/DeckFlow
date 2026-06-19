using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="AdminContentKbController"/>: every mutating POST must reject a
/// cross-origin request with 403 BEFORE mutating state (SC4/P11), and the status panel
/// timestamp must be max(indexed_utc) exposed as IndexGeneratedUtc (D-22D honest label).
/// </summary>
public sealed class AdminContentKbControllerTests
{
    [Fact]
    public async Task SetVisibility_CrossOrigin_Returns403_AndDoesNotMutate()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: false));
        var controller = Build(store, out _, crossOrigin: true);

        var result = await controller.SetVisibility(1, visible: true, default, default);

        AssertForbidden(result);
        Assert.False(store.Rows[0].IsVisible);
    }

    [Fact]
    public async Task SetVisibility_SameOrigin_FlipsRow_AndRedirects()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: false, hidden: true));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.SetVisibility(1, visible: true, default, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(store.Rows[0].IsVisible);
        Assert.False(store.Rows[0].IsHidden);
    }

    [Fact]
    public async Task Hide_SameOrigin_SetsHidden_AndClearsVisible()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Hide(1, default, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(store.Rows[0].IsVisible);
        Assert.True(store.Rows[0].IsHidden);
    }

    [Fact]
    public async Task DeleteEntry_CrossOrigin_Returns403_AndDoesNotMutate()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        var controller = Build(store, out _, crossOrigin: true);

        var result = await controller.DeleteEntry(1, default, default);

        AssertForbidden(result);
        Assert.Empty(store.DeletedIds);
    }

    [Fact]
    public async Task DeleteEntry_SameOrigin_DeletesRow_AndRedirects()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.DeleteEntry(1, default, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains(1, store.DeletedIds);
    }

    [Fact]
    public async Task BulkSetVisibility_CrossOrigin_Returns403()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: false));
        var controller = Build(store, out _, crossOrigin: true);

        var result = await controller.BulkSetVisibility("EDHRECast", visible: true, default, default);

        AssertForbidden(result);
        Assert.False(store.Rows[0].IsVisible);
    }

    [Fact]
    public async Task BulkHide_CrossOrigin_Returns403()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        var controller = Build(store, out _, crossOrigin: true);

        var result = await controller.BulkHide("EDHRECast", default, default);

        AssertForbidden(result);
        Assert.True(store.Rows[0].IsVisible);
        Assert.False(store.Rows[0].IsHidden);
    }

    [Fact]
    public async Task ReloadSeed_CrossOrigin_Returns403_AndDoesNotReload()
    {
        var store = new FakeContentSiteIndexStore();
        var loader = new FakeContentKbSeedLoader(rowCount: 5);
        var controller = Build(store, loader, out _, crossOrigin: true);

        var result = await controller.ReloadSeed(default, default);

        AssertForbidden(result);
        Assert.Equal(0, loader.LoadCallCount);
    }

    [Fact]
    public async Task ReloadSeed_SameOrigin_InvokesLoader_AndRedirects()
    {
        var store = new FakeContentSiteIndexStore();
        var loader = new FakeContentKbSeedLoader(rowCount: 5);
        var controller = Build(store, loader, out _, crossOrigin: false);

        var result = await controller.ReloadSeed(default, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, loader.LoadCallCount);
    }

    [Fact]
    public async Task Index_StatusUsesMaxIndexedUtc_AndCounts()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true, indexed: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)));
        store.Rows.Add(Row(2, visible: false, indexed: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));
        store.Rows.Add(Row(3, visible: false, hidden: true, indexed: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero)));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal(3, model.Status.TotalCount);
        Assert.Equal(1, model.Status.PublishedCount);
        Assert.Equal(1, model.Status.UnpublishedCount);
        Assert.Equal(1, model.Status.HiddenCount);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), model.Status.IndexGeneratedUtc);
    }

    [Fact]
    public async Task Index_IndexGeneratedUtcIsNull_WhenNoRows()
    {
        var store = new FakeContentSiteIndexStore();
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Null(model.Status.IndexGeneratedUtc);
        Assert.Equal(0, model.Status.TotalCount);
    }

    [Fact]
    public async Task Index_WithVisibilityFilterPublished_ReturnsOnlyVisibleEntries()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        store.Rows.Add(Row(2, visible: false));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(visibilityFilter: "published", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal("published", model.VisibilityFilter);
        var entry = Assert.Single(model.Entries);
        Assert.True(entry.IsVisible);
    }

    [Fact]
    public async Task Index_WithVisibilityFilterUnpublished_ReturnsOnlyUnpublishedEntries()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        store.Rows.Add(Row(2, visible: false));
        store.Rows.Add(Row(3, visible: false, hidden: true));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(visibilityFilter: "unpublished", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal("unpublished", model.VisibilityFilter);
        var entry = Assert.Single(model.Entries);
        Assert.False(entry.IsVisible);
        Assert.False(entry.IsHidden);
    }

    [Fact]
    public async Task Index_WithVisibilityFilterHidden_ReturnsOnlyHiddenEntries()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        store.Rows.Add(Row(2, visible: false));
        store.Rows.Add(Row(3, visible: false, hidden: true));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(visibilityFilter: "hidden", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal("hidden", model.VisibilityFilter);
        var entry = Assert.Single(model.Entries);
        Assert.True(entry.IsHidden);
        Assert.False(entry.IsVisible);
    }

    [Fact]
    public async Task Index_WithVisibilityFilterAll_ExcludesHiddenEntries()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        store.Rows.Add(Row(2, visible: false));
        store.Rows.Add(Row(3, visible: false, hidden: true));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(visibilityFilter: "all", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal("all", model.VisibilityFilter);
        Assert.Equal(2, model.Entries.Count);
        Assert.DoesNotContain(model.Entries, entry => entry.IsHidden);
    }

    [Fact]
    public async Task Index_WithInvalidVisibilityFilter_FallsBackToAllEntries()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        store.Rows.Add(Row(2, visible: false));
        store.Rows.Add(Row(3, visible: false, hidden: true));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(visibilityFilter: "garbage", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal("all", model.VisibilityFilter);
        Assert.Equal(2, model.Entries.Count);
    }

    [Fact]
    public async Task Index_RowPublishFields_RoundTripFromStore()
    {
        var indexedUtc = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var pushedToProdUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true, indexed: indexedUtc, pushedToProdUtc: pushedToProdUtc));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(cancellationToken: default);

        var vm = Assert.IsType<ViewResult>(result).Model as AdminContentKbViewModel;
        Assert.NotNull(vm);
        Assert.Equal(pushedToProdUtc, vm.Entries[0].PushedToProdUtc);
        Assert.Equal(indexedUtc, vm.Entries[0].IndexedUtc);
    }

    [Fact]
    public async Task Index_PublishStateNeverPublished_WhenPushedToProdUtcIsNull()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true, pushedToProdUtc: null));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(cancellationToken: default);

        var vm = Assert.IsType<ViewResult>(result).Model as AdminContentKbViewModel;
        Assert.NotNull(vm);
        Assert.Equal(PublishState.NeverPublished, vm.Entries[0].PublishState);
    }

    [Fact]
    public async Task Index_PublishStatePublished_WhenVisibleAndPushedToProdUtcIsAtOrAfterIndexedUtc()
    {
        var indexedUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var pushedToProdUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true, indexed: indexedUtc, pushedToProdUtc: pushedToProdUtc));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(cancellationToken: default);

        var vm = Assert.IsType<ViewResult>(result).Model as AdminContentKbViewModel;
        Assert.NotNull(vm);
        Assert.Equal(PublishState.Published, vm.Entries[0].PublishState);
    }

    [Fact]
    public async Task Index_PublishStateLocalNewer_WhenIndexedUtcIsAfterPushedToProdUtc()
    {
        var indexedUtc = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var pushedToProdUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true, indexed: indexedUtc, pushedToProdUtc: pushedToProdUtc));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(cancellationToken: default);

        var vm = Assert.IsType<ViewResult>(result).Model as AdminContentKbViewModel;
        Assert.NotNull(vm);
        Assert.Equal(PublishState.LocalNewer, vm.Entries[0].PublishState);
    }

    [Fact]
    public async Task Index_PublishStatePushedHidden_WhenPushedButNotVisible()
    {
        // Why: PushedHidden is the one derived state whose precedence is non-trivial — the
        // !isVisible branch must fire BEFORE the timestamp compare. A row pushed to prod but
        // hidden from the site (visible:false, hidden:false) stays in the default grid and
        // must read Pushed-hidden, not Published/Local-newer.
        var indexedUtc = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var pushedToProdUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: false, hidden: false, indexed: indexedUtc, pushedToProdUtc: pushedToProdUtc));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(cancellationToken: default);

        var vm = Assert.IsType<ViewResult>(result).Model as AdminContentKbViewModel;
        Assert.NotNull(vm);
        Assert.Equal(PublishState.PushedHidden, vm.Entries[0].PublishState);
    }

    [Fact]
    public async Task Index_PublishStatePublished_WhenPushedStrictlyAfterIndexedUtc()
    {
        // Why: the "AtOrAfter" Published test pins only the == boundary; this pins the strictly
        // greater case so a future <=/< off-by-one in the deriver precedence is caught.
        var indexedUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var pushedToProdUtc = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true, indexed: indexedUtc, pushedToProdUtc: pushedToProdUtc));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(cancellationToken: default);

        var vm = Assert.IsType<ViewResult>(result).Model as AdminContentKbViewModel;
        Assert.NotNull(vm);
        Assert.Equal(PublishState.Published, vm.Entries[0].PublishState);
    }

    private static void AssertForbidden(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    private static AdminContentKbController Build(
        FakeContentSiteIndexStore store,
        out FakeContentKbSeedLoader loader,
        bool crossOrigin)
    {
        loader = new FakeContentKbSeedLoader();
        return Build(store, loader, out _, crossOrigin);
    }

    private static AdminContentKbController Build(
        FakeContentSiteIndexStore store,
        FakeContentKbSeedLoader loader,
        out FakeContentKbSeedLoader loaderOut,
        bool crossOrigin)
    {
        loaderOut = loader;
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool> { ["content.kb.enabled"] = false });
        var controller = new AdminContentKbController(
            store,
            loader,
            flagCache,
            new DeckFlow.Core.Content.PublishStateDeriver(),
            NullLogger<AdminContentKbController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("deckflow.test");
        httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new StubTempDataProvider());
        return controller;
    }

    private static ContentSiteIndexRow Row(
        long id,
        bool visible,
        bool hidden = false,
        DateTimeOffset? indexed = null,
        DateTimeOffset? pushedToProdUtc = null)
        => new()
        {
            Id = id,
            Source = "EDHRECast",
            Title = "Title " + id,
            VideoUrl = "https://youtu.be/x" + id,
            ArtifactPath = $"content-kb/edhrecast/{id}.md",
            IndexedUtc = indexed ?? new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = "x" + id,
            IsVisible = visible,
            IsHidden = hidden,
            PushedToProdUtc = pushedToProdUtc,
        };

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
