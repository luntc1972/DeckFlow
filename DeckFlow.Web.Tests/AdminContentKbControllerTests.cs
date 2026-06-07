using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    [Fact]
    public async Task Index_WithPreviewParams_CallsScoreAllAsync_AndPopulatesScores()
    {
        var store = new FakeContentSiteIndexStore();
        var firstRow = Row(1, visible: true);
        var secondRow = Row(2, visible: false);
        store.Rows.Add(firstRow);
        store.Rows.Add(secondRow);
        var relevanceService = new FakeContentKbRelevanceService
        {
            ScoreResults =
            [
                (firstRow, 2.75d),
                (secondRow, 0.50d),
            ],
        };
        var controller = Build(store, out _, crossOrigin: false, relevanceService: relevanceService);

        var result = await controller.Index(previewCommander: "Tymna", previewBracket: "cEDH", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal(1, relevanceService.ScoreAllCallCount);
        Assert.Equal("Tymna", relevanceService.LastCommanderName);
        Assert.Equal("cEDH", relevanceService.LastBracket);
        Assert.Equal("Tymna", model.PreviewCommander);
        Assert.Equal("cEDH", model.PreviewBracket);
        Assert.Contains("cEDH", model.BracketOptions);
        Assert.Equal(2.75d, Assert.Single(model.Entries, entry => entry.Id == 1).RelevanceScore);
        Assert.Equal(0.50d, Assert.Single(model.Entries, entry => entry.Id == 2).RelevanceScore);
    }

    [Fact]
    public async Task Index_WithNoPreviewParams_DoesNotCallScoreAllAsync_AndLeavesScoresNull()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        var relevanceService = new FakeContentKbRelevanceService();
        var controller = Build(store, out _, crossOrigin: false, relevanceService: relevanceService);

        var result = await controller.Index(previewCommander: null, previewBracket: null, cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal(0, relevanceService.ScoreAllCallCount);
        Assert.All(model.Entries, entry => Assert.Null(entry.RelevanceScore));
    }

    [Fact]
    public async Task Index_WithInvalidPreviewBracket_TreatsBracketAsNull()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        var relevanceService = new FakeContentKbRelevanceService
        {
            ScoreResults =
            [
                (store.Rows[0], 1.25d),
            ],
        };
        var controller = Build(store, out _, crossOrigin: false, relevanceService: relevanceService);

        var result = await controller.Index(previewCommander: "Kinnan", previewBracket: "Invalid", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal(1, relevanceService.ScoreAllCallCount);
        Assert.Equal("Kinnan", relevanceService.LastCommanderName);
        Assert.Null(relevanceService.LastBracket);
    }

    [Fact]
    public async Task Index_NormalizesPreviewCommander_BeforeScoring()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        var relevanceService = new FakeContentKbRelevanceService
        {
            ScoreResults =
            [
                (store.Rows[0], 3.00d),
            ],
        };
        var controller = Build(store, out _, crossOrigin: false, relevanceService: relevanceService);

        var result = await controller.Index(previewCommander: "  Tymna\nThrasios  ", previewBracket: "cEDH", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal("Tymna Thrasios", relevanceService.LastCommanderName);
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
    public async Task Index_WithInvalidVisibilityFilter_FallsBackToAllEntries()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        store.Rows.Add(Row(2, visible: false));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(visibilityFilter: "garbage", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal("all", model.VisibilityFilter);
        Assert.Equal(2, model.Entries.Count);
    }

    [Fact]
    public async Task Index_WithSortByScoreAndPreviewActive_OrdersEntriesByScoreDescendingWithNullsLast()
    {
        var store = new FakeContentSiteIndexStore();
        var firstRow = Row(1, visible: true);
        var secondRow = Row(2, visible: false);
        var thirdRow = Row(3, visible: true);
        store.Rows.Add(firstRow);
        store.Rows.Add(secondRow);
        store.Rows.Add(thirdRow);
        var relevanceService = new FakeContentKbRelevanceService
        {
            ScoreResults =
            [
                (secondRow, 3.50d),
                (firstRow, 1.25d),
            ],
        };
        var controller = Build(store, out _, crossOrigin: false, relevanceService: relevanceService);

        var result = await controller.Index(
            previewCommander: "Tymna",
            previewBracket: "cEDH",
            visibilityFilter: "all",
            sortBy: "score",
            cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal("score", model.SortBy);
        Assert.Collection(
            model.Entries,
            entry => Assert.Equal(2, entry.Id),
            entry => Assert.Equal(1, entry.Id),
            entry => Assert.Equal(3, entry.Id));
    }

    [Fact]
    public async Task Index_WithSortByScoreAndNoPreview_KeepsOriginalOrder()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, visible: true));
        store.Rows.Add(Row(2, visible: false));
        store.Rows.Add(Row(3, visible: true));
        var controller = Build(store, out _, crossOrigin: false);

        var result = await controller.Index(sortBy: "score", cancellationToken: default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminContentKbViewModel>(view.Model);
        Assert.Equal("score", model.SortBy);
        Assert.Collection(
            model.Entries,
            entry => Assert.Equal(1, entry.Id),
            entry => Assert.Equal(2, entry.Id),
            entry => Assert.Equal(3, entry.Id));
    }

    private static void AssertForbidden(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    private static AdminContentKbController Build(
        FakeContentSiteIndexStore store,
        out FakeContentKbSeedLoader loader,
        bool crossOrigin,
        FakeContentKbRelevanceService? relevanceService = null)
    {
        loader = new FakeContentKbSeedLoader();
        return Build(store, loader, out _, crossOrigin, relevanceService);
    }

    private static AdminContentKbController Build(
        FakeContentSiteIndexStore store,
        FakeContentKbSeedLoader loader,
        out FakeContentKbSeedLoader loaderOut,
        bool crossOrigin,
        FakeContentKbRelevanceService? relevanceService = null)
    {
        loaderOut = loader;
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool> { ["content.kb.enabled"] = false });
        var controller = new AdminContentKbController(
            store,
            loader,
            flagCache,
            relevanceService ?? new FakeContentKbRelevanceService(),
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

    private sealed class FakeContentKbRelevanceService : IContentKbRelevanceService
    {
        public int ScoreAllCallCount { get; private set; }

        public string? LastCommanderName { get; private set; }

        public string? LastBracket { get; private set; }

        public IReadOnlyList<(ContentSiteIndexRow Row, double Score)> ScoreResults { get; init; }
            = Array.Empty<(ContentSiteIndexRow Row, double Score)>();

        public Task<IReadOnlyList<ContentKbExcerpt>?> GetRelevantClipsAsync(
            string? commanderName,
            string? bracket,
            IReadOnlySet<string>? deckArchetypes = null,
            int maxRenderedChars = 4500,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ContentKbExcerpt>?>(null);
        }

        public Task<IReadOnlyList<ContentKbExcerpt>?> GetMergedClipsAsync(
            ExpertSelection selection,
            string? commanderName,
            string? bracket,
            IReadOnlySet<string>? deckArchetypes = null,
            int maxRenderedChars = 4500,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ContentKbExcerpt>?>(null);
        }

        public Task<IReadOnlyList<(ContentSiteIndexRow Row, double Score)>> ScoreAllAsync(
            string? commanderName,
            string? bracket,
            CancellationToken ct = default)
        {
            ScoreAllCallCount++;
            LastCommanderName = commanderName;
            LastBracket = bracket;
            return Task.FromResult(ScoreResults);
        }

        public Task<IReadOnlyDictionary<string, string>> ResolvePinTitlesAsync(
            IReadOnlyList<string> videoIds,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.Ordinal));
    }
}
