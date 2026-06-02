using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models;
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

        var result = await controller.SetVisibility(1, visible: true, default);

        AssertForbidden(result);
        Assert.False(store.Rows[0].IsVisible); // unchanged
    }

    [Fact]
    public async Task SetVisibility_SameOrigin_FlipsRow_AndRedirects()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: false));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.SetVisibility(1, visible: true, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(store.Rows[0].IsVisible);
    }

    [Fact]
    public async Task BulkSetVisibility_CrossOrigin_Returns403()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: false));
        var controller = Build(store, out _, crossOrigin: true);

        var result = await controller.BulkSetVisibility("EDHRECast", visible: true, default);

        AssertForbidden(result);
        Assert.False(store.Rows[0].IsVisible);
    }

    [Fact]
    public async Task ReloadSeed_CrossOrigin_Returns403_AndDoesNotReload()
    {
        var store = new FakeContentSiteIndexStore();
        var loader = new FakeContentKbSeedLoader(rowCount: 5);
        var controller = Build(store, loader, out _, crossOrigin: true);

        var result = await controller.ReloadSeed(default);

        AssertForbidden(result);
        Assert.Equal(0, loader.LoadCallCount);
    }

    [Fact]
    public async Task ReloadSeed_SameOrigin_InvokesLoader_AndRedirects()
    {
        var store = new FakeContentSiteIndexStore();
        var loader = new FakeContentKbSeedLoader(rowCount: 5);
        var controller = Build(store, loader, out _, crossOrigin: false);

        var result = await controller.ReloadSeed(default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, loader.LoadCallCount);
    }

    [Fact]
    public async Task Index_StatusUsesMaxIndexedUtc_AndCounts()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true, indexed: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)));
        store.Rows.Add(Row(2, visible: false, indexed: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal(2, model.Status.TotalCount);
        Assert.Equal(1, model.Status.PublishedCount);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), model.Status.IndexGeneratedUtc);
    }

    [Fact]
    public async Task Index_IndexGeneratedUtcIsNull_WhenNoRows()
    {
        var store = new FakeContentSiteIndexStore();
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Null(model.Status.IndexGeneratedUtc);
        Assert.Equal(0, model.Status.TotalCount);
    }

    private static void AssertForbidden(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    private static AdminContentKbController Build(FakeContentSiteIndexStore store, out FakeContentKbSeedLoader loader, bool crossOrigin)
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
            NullLogger<AdminContentKbController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("deckflow.test");
        httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new StubTempDataProvider());
        return controller;
    }

    private static ContentSiteIndexRow Row(long id, bool visible, DateTimeOffset? indexed = null)
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
        };

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
