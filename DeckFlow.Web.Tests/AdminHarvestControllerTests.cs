using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="AdminHarvestController"/> covering harvested-commander paging, same-origin guards, and render paths.
/// </summary>
public sealed class AdminHarvestControllerTests
{
    [Fact]
    public async Task Commanders_ClampsHugePageToDeckTotalPages()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 999999);

        var view = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsType<CommandersGridViewModel>(view.Model);
        Assert.Equal(model.DeckTotalPages, model.DeckPage);
        Assert.Equal(1, model.DeckPage);
    }

    [Fact]
    public async Task Commanders_ClampsZeroPageToOne()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 0);

        var view = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsType<CommandersGridViewModel>(view.Model);
        Assert.Equal(1, model.DeckPage);
    }

    [Fact]
    public async Task Commanders_PassesClampedPageToPagedCommanderStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 125);
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 999999);

        var view = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsType<CommandersGridViewModel>(view.Model);
        Assert.Equal(model.DeckTotalPages, store.LastPagedCommanderPage);
        Assert.Equal(AdminHarvestViewModel.DefaultDeckPageSize, store.LastPagedCommanderPageSize);
        Assert.NotEqual(999999, store.LastPagedCommanderPage);
    }

    [Fact]
    public async Task Commanders_Search_PassesNormalizedSearchTermToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        await controller.Commanders(page: 1, search: "tef");

        Assert.Equal("tef", store.LastCommanderGridQuery?.SearchTerm);
    }

    [Fact]
    public async Task Commanders_Search_BlankTermPassesNullToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        await controller.Commanders(page: 1, search: "   ");

        Assert.Null(store.LastCommanderGridQuery?.SearchTerm);
    }

    [Fact]
    public async Task Commanders_Search_TruncatesTermBeforePassingToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        await controller.Commanders(page: 1, search: new string('a', 101));

        Assert.Equal(100, store.LastCommanderGridQuery?.SearchTerm?.Length);
    }

    [Fact]
    public async Task Commanders_Search_ClampsPageUsingFilteredCount()
    {
        var store = NewStore(distinctProcessedCommanderCount: 500);
        store.FilteredCommanderCount = 3;
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 2, search: "tef");

        var view = Assert.IsType<PartialViewResult>(result);
        Assert.Equal(1, Assert.IsType<CommandersGridViewModel>(view.Model).DeckPage);
    }

    [Fact]
    public async Task Commanders_Search_PassesSortToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        await controller.Commanders(page: 1, search: "tef", sortBy: "name", sortDir: "asc");

        Assert.Equal(CommanderSortColumn.Name, store.LastCommanderGridQuery?.SortBy);
        Assert.False(store.LastCommanderGridQuery?.Descending);
    }

    [Fact]
    public async Task Index_DoesNotQueryCommanderStoreDirectly()
    {
        var store = NewStore(distinctProcessedCommanderCount: 125);
        var controller = Build(store);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<AdminHarvestViewModel>(view.Model);
        Assert.Equal(0, store.LastPagedCommanderPage);
        Assert.Equal(0, store.GetDistinctProcessedCommanderCountCalls);
    }

    [Fact]
    public async Task Commanders_SameOrigin_ReturnsPartialView()
    {
        var store = NewStore(distinctProcessedCommanderCount: 125);
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 1);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_CommandersGrid", partial.ViewName);
        var model = Assert.IsType<CommandersGridViewModel>(partial.Model);
        Assert.Equal(1, model.DeckPage);
    }

    [Fact]
    public async Task Commanders_CrossOrigin_Returns403()
    {
        var store = NewStore(distinctProcessedCommanderCount: 125);
        var controller = Build(store, crossOrigin: true);

        var result = await controller.Commanders(page: 1);

        AssertForbidden(result);
    }

    [Fact]
    public async Task CommandersGrid_EmptyModel_RendersEmptyStateWithoutTable()
    {
        var model = new CommandersGridViewModel
        {
            DeckPage = 1,
            DeckPageSize = AdminHarvestViewModel.DefaultDeckPageSize,
            DeckTotalCount = 0,
        };

        var html = await RenderPartialViewAsync("_CommandersGrid", model);

        Assert.Contains("class=\"admin-empty\"", html, StringComparison.Ordinal);
        Assert.Contains("No harvested commanders yet.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table class=\"admin-table\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandersGrid_MultiPageModel_RendersNumberedPaginationWithCurrentPageStrong()
    {
        var model = new CommandersGridViewModel
        {
            HarvestedCommanders = new[]
            {
                new HarvestedCommanderRow("Commander One", 3, "2026-01-01T00:00:00.0000000Z"),
            },
            DeckPage = 2,
            DeckPageSize = AdminHarvestViewModel.DefaultDeckPageSize,
            DeckTotalCount = 250,
        };

        var html = await RenderPartialViewAsync("_CommandersGrid", model);

        Assert.Contains("data-page=\"1\"", html, StringComparison.Ordinal);
        Assert.Contains("data-page=\"3\"", html, StringComparison.Ordinal);
        Assert.Contains("<strong aria-current=\"page\">2</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-page=\"2\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_ErrorMessage_RendersErrorColumn()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun(errorMessage: "upstream failed") });

        Assert.Contains("upstream failed", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_Runs_RendersOneTableWithSixColumnHeadings()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun() });

        Assert.Equal(1, html.Split("<table", StringSplitOptions.None).Length - 1);
        Assert.Equal(6, html.Split("<th scope=\"col\">", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task HarvestRunLog_EmptyList_RendersNamedEmptyStateWithoutTable()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", Array.Empty<HarvestRunRow>());

        Assert.Contains("id=\"harvest-run-log-heading\"", html, StringComparison.Ordinal);
        Assert.Contains("No runs recorded yet.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HarvestIndex_StatsAvailability_RendersSameRunLogColumns(bool includesStats)
    {
        var runs = new[] { CreateHarvestRun() };
        var model = CreateHarvestViewModel(runs, includesStats);

        var html = await RenderPartialViewAsync("Index", model);

        Assert.Equal(6, html.Split("<th scope=\"col\">", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task HarvestRunLog_ShortError_RendersPlainEncodedText()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun(errorMessage: "brief failure") });

        Assert.Contains("brief failure", html, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-harvest__run-error", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_LongError_RendersDisclosureWithSummaryAndBody()
    {
        var error = new string('x', 81);
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun(errorMessage: error) });

        Assert.Contains("admin-harvest__run-error", html, StringComparison.Ordinal);
        Assert.Contains(new string('x', 80) + "&#x2026;", html, StringComparison.Ordinal);
        Assert.Contains(error, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_NullError_RendersEmDashWithoutDisclosure()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun() });

        Assert.Contains("&#x2014;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-harvest__run-error", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_HtmlError_EncodesSummaryAndBody()
    {
        var error = "<tag attr=\"quoted\">" + new string('x', 80);
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun(errorMessage: error) });

        Assert.Equal(2, html.Split("&lt;tag attr=&quot;quoted&quot;&gt;", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(error, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestIndex_ImportPanel_RendersProtectedSubmitUrlForm()
    {
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), includesStats: false));
        var importPanel = html[html.IndexOf("id=\"harvest-import-panel\"", StringComparison.Ordinal)..];

        Assert.Contains("<form method=\"post\"", importPanel, StringComparison.Ordinal);
        Assert.Contains("SubmitUrl", importPanel, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", importPanel, StringComparison.Ordinal);
        Assert.Contains("id=\"url\" type=\"url\" name=\"url\"", importPanel, StringComparison.Ordinal);
    }

    private static HarvestRunRow CreateHarvestRun(string? errorMessage = null)
        => new(Guid.NewGuid(), HarvestRunKind.Bulk, HarvestRunState.Failed, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, null, 900, 2, 0, null, null, errorMessage, null);

    private static AdminHarvestViewModel CreateHarvestViewModel(IReadOnlyList<HarvestRunRow> runs, bool includesStats)
        => new()
        {
            Schedule = new HarvestScheduleSnapshot(null, false, DateTimeOffset.Parse("2026-01-01T00:00:00Z")),
            RecentRuns = runs,
            Stats = includesStats
                ? new HarvestStatsPayload(0, 0, 0, 0, 0, runs, null, null, null, new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false))
                : null,
        };

    [Fact]
    public void Commanders_Sort_NextDirectionUsesAscendingForInactiveColumn()
        => Assert.Equal("asc", CommanderGridQuery.Default.NextDirectionToken(CommanderSortColumn.Name));

    [Theory]
    [InlineData(true, "asc")]
    [InlineData(false, "desc")]
    public void Commanders_Sort_NextDirectionReversesActiveColumn(bool descending, string expected)
    {
        var query = CommanderGridQuery.FromRequest(null, "name", descending ? "desc" : "asc");
        Assert.Equal(expected, query.NextDirectionToken(CommanderSortColumn.Name));
    }

    [Fact]
    public void Commanders_Sort_IsActiveMatchesSelectedColumn()
    {
        var query = CommanderGridQuery.FromRequest(null, "last_processed", "asc");
        Assert.True(query.IsActive(CommanderSortColumn.LastProcessed));
        Assert.False(query.IsActive(CommanderSortColumn.Name));
    }

    [Fact]
    public async Task Commanders_Sort_ControllerPassesNameAscendingQuery()
    {
        var store = NewStore(3);
        var result = await Build(store, crossOrigin: false).Commanders(sortBy: "name", sortDir: "asc");
        Assert.IsType<PartialViewResult>(result);
        Assert.Equal("name", store.LastCommanderGridQuery!.SortByToken);
        Assert.Equal("asc", store.LastCommanderGridQuery.SortDirToken);
    }

    [Fact]
    public async Task Commanders_Sort_ControllerPassesLastProcessedDescendingQuery()
    {
        var store = NewStore(3);
        await Build(store, crossOrigin: false).Commanders(sortBy: "last_processed", sortDir: "desc");
        Assert.Equal("last_processed", store.LastCommanderGridQuery!.SortByToken);
    }

    [Fact]
    public async Task CommandersGrid_Sort_DefaultRendersActiveDeckCountAndAriaSort()
    {
        var html = await RenderPartialViewAsync("_CommandersGrid", CreateSortGridModel(CommanderGridQuery.Default));
        AssertHeaderSort(html, "deck_count", "descending", active: true);
        AssertHeaderSort(html, "name", "none");
        AssertHeaderSort(html, "last_processed", "none");
    }

    [Fact]
    public async Task CommandersGrid_Sort_NameAscendingRendersAriaSortAndNextDirection()
    {
        var html = await RenderPartialViewAsync("_CommandersGrid", CreateSortGridModel(CommanderGridQuery.FromRequest("Te", "name", "asc")));
        Assert.Contains("data-sort-next-dir=\"desc\"", html, StringComparison.Ordinal);
        AssertHeaderSort(html, "name", "ascending", active: true);
        AssertHeaderSort(html, "deck_count", "none");
        AssertHeaderSort(html, "last_processed", "none");
    }

    [Fact]
    public async Task CommandersGrid_Sort_PaginationPreservesQueryState()
    {
        var html = await RenderPartialViewAsync("_CommandersGrid", CreateSortGridModel(CommanderGridQuery.FromRequest("Te", "last_processed", "desc")));
        Assert.Contains("data-search=\"Te\"", html, StringComparison.Ordinal);
        Assert.Contains("data-sort-by=\"last_processed\"", html, StringComparison.Ordinal);
        Assert.Contains("data-sort-dir=\"desc\"", html, StringComparison.Ordinal);
    }

    private static void AssertHeaderSort(string html, string column, string expectedAriaSort, bool active = false)
    {
        var match = Regex.Match(html, $"<th(?:(?!</th>).)*data-sort-column=\\\"{Regex.Escape(column)}\\\"(?:(?!</th>).)*</th>", RegexOptions.Singleline);
        Assert.True(match.Success, $"Missing sort header for {column}.");
        Assert.Contains($"aria-sort=\"{expectedAriaSort}\"", match.Value, StringComparison.Ordinal);
        if (active)
        {
            Assert.Contains("data-sort-active=\"true\"", match.Value, StringComparison.Ordinal);
            Assert.Single(Regex.Matches(match.Value, "<span class=\"admin-table__sort-indicator\" aria-hidden=\"true\">").Cast<Match>());
        }
    }

    private static CommandersGridViewModel CreateSortGridModel(CommanderGridQuery query)
        => new()
        {
            Query = query,
            DeckPage = 1,
            DeckPageSize = 1,
            DeckTotalCount = 2,
            HarvestedCommanders = new[] { new HarvestedCommanderRow("Commander One", 1, null) },
        };

    private static FakeCategoryKnowledgeStore NewStore(int distinctProcessedCommanderCount)
        => new()
        {
            DistinctProcessedCommanderCount = distinctProcessedCommanderCount,
            PagedCommandersResult = new[]
            {
                new HarvestedCommanderRow("Commander One", 3, "2026-01-01T00:00:00.0000000Z"),
                new HarvestedCommanderRow("Commander Two", 2, "2026-01-02T00:00:00.0000000Z"),
                new HarvestedCommanderRow("Commander Three", 1, "2026-01-03T00:00:00.0000000Z"),
            },
        };

    private static void AssertForbidden(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    [Fact]
    public async Task SubmitUrl_MetadataBearingImport_PassesMetadataToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 0);
        var metadata = new ArchidektDeckMetadata(3, 1, true, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"), DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        var importer = new StubArchidektDeckImporter { Metadata = metadata };
        var controller = Build(store, importer: importer);

        await controller.SubmitUrl("https://archidekt.com/decks/123", CancellationToken.None);

        Assert.Equal("123", store.LastUrlDeckId);
        Assert.Same(metadata, store.LastUrlMetadata);
    }

    [Fact]
    public async Task SubmitUrl_CommanderBoard_RecordsCommanderAndBanner()
    {
        var store = NewStore(distinctProcessedCommanderCount: 0);
        var importer = new StubArchidektDeckImporter
        {
            Entries = new List<DeckEntry>
            {
                new() { Name = "Kenrith, the Returned King", NormalizedName = "Kenrith, the Returned King", Board = "commander", Quantity = 1 },
                new() { Name = "Sol Ring", NormalizedName = "Sol Ring", Board = "mainboard", Quantity = 1 },
            },
        };
        var controller = Build(store, importer: importer);

        await controller.SubmitUrl("https://archidekt.com/decks/123", CancellationToken.None);

        Assert.Equal("Kenrith, the Returned King", store.LastUrlCommanderName);
        Assert.Equal("Harvested Kenrith, the Returned King: 2 new observations.", controller.TempData["AdminHarvestBanner"]);
    }

    private static AdminHarvestController Build(ICategoryKnowledgeStore store, bool crossOrigin = false, IArchidektDeckImporter? importer = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("deckflow.test");
        httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";

        return new AdminHarvestController(
            new StubArchidektCacheJobService(),
            new StubHarvestRunStore(),
            new StubHarvestScheduleStore(),
            new StubHarvestScheduleCache(),
            new StubHarvestStatsAggregator(),
            importer ?? new StubArchidektDeckImporter(),
            store,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AdminHarvestController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new StubTempDataProvider()),
        };
    }

    [Fact]
    public async Task HarvestHealthStrip_RendersValuesAndUnknownDatabaseSize()
    {
        var known = new HarvestStatsPayload(1234, 0, 56, 78, 0, Array.Empty<HarvestRunRow>(), 2048, null, null, new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false));
        var unknown = known with { DatabaseSizeBytes = null };

        var knownHtml = await RenderPartialViewAsync("_HarvestHealthStrip", known);
        var unknownHtml = await RenderPartialViewAsync("_HarvestHealthStrip", unknown);

        Assert.Contains("health-processed-decks", knownHtml, StringComparison.Ordinal);
        Assert.Contains("1,234", knownHtml, StringComparison.Ordinal);
        Assert.Contains("health-queued-decks", knownHtml, StringComparison.Ordinal);
        Assert.Contains("56", knownHtml, StringComparison.Ordinal);
        Assert.Contains("health-distinct-commanders", knownHtml, StringComparison.Ordinal);
        Assert.Contains("78", knownHtml, StringComparison.Ordinal);
        Assert.Contains("health-database-size", knownHtml, StringComparison.Ordinal);
        Assert.Contains("2 KB", knownHtml, StringComparison.Ordinal);
        Assert.Contains("&#x2014;", unknownHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HarvestBacklogReason.AboveFloor, "Backlog exceeds floor", "configured floor 100")]
    [InlineData(HarvestBacklogReason.Growing, "Backlog growing", "3 consecutive runs")]
    [InlineData(HarvestBacklogReason.AboveFloorAndGrowing, "Backlog exceeds floor and growing", "grew for 3 consecutive runs")]
    public async Task HarvestHealthStrip_FlaggedReason_RendersReasonAndThreshold(
        HarvestBacklogReason reason,
        string expectedText,
        string expectedTitle)
    {
        var payload = new HarvestStatsPayload(0, 0, 0, 0, 0, Array.Empty<HarvestRunRow>(), null, null, null, new HarvestHealthSignals(true, reason, 100, 3, 0, false));

        var html = await RenderPartialViewAsync("_HarvestHealthStrip", payload);

        Assert.Contains("health-backlog-flag", html, StringComparison.Ordinal);
        Assert.Contains(expectedText, html, StringComparison.Ordinal);
        Assert.Contains(expectedTitle, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestHealthStrip_NoneReason_DoesNotRenderBacklogBadge()
    {
        var payload = new HarvestStatsPayload(0, 0, 0, 0, 0, Array.Empty<HarvestRunRow>(), null, null, null, new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false));

        var html = await RenderPartialViewAsync("_HarvestHealthStrip", payload);

        Assert.DoesNotContain("health-backlog-flag", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, false, 3, "Discovering new decks", false)]
    [InlineData(2, false, 3, "Zero-discovery streak: 2", false)]
    [InlineData(3, true, 3, "Zero-discovery streak: 3&#x2B;", true)]
    [InlineData(2, false, 2, "Zero-discovery streak: 2", true)]
    public async Task HarvestZeroDiscovery_RendersExpectedStreakState(
        int streak,
        bool capped,
        int threshold,
        string expectedText,
        bool expectsWarning)
    {
        var signals = new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, threshold, streak, capped);

        var html = await RenderPartialViewAsync("_HarvestZeroDiscovery", signals);

        Assert.Contains("harvest-zero-discovery", html, StringComparison.Ordinal);
        Assert.Contains(expectedText, html, StringComparison.Ordinal);
        Assert.Equal(expectsWarning, html.Contains("admin-harvest__health-badge", StringComparison.Ordinal));
    }

    private static async Task<string> RenderPartialViewAsync(string viewName, object model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(serviceProvider => serviceProvider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(serviceProvider => serviceProvider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddControllersWithViews().AddApplicationPart(typeof(AdminHarvestController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
        };

        var routeData = new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "AdminHarvest" }));
        routeData.Routers.Add(new TestRouter());
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, viewName, isMainPage: false);
        Assert.True(viewResult.Success, $"View '{viewName}' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View!,
            viewData,
            new TempDataDictionary(httpContext, new StubTempDataProvider()),
            writer,
            new HtmlHelperOptions());

        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment()
    {
        var contentRoot = AppContext.BaseDirectory;
        var fileProvider = new NullFileProvider();
        return new TestWebHostEnvironment
        {
            ApplicationName = typeof(AdminHarvestController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = contentRoot,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = contentRoot,
            WebRootFileProvider = fileProvider,
        };
    }

    private sealed class TestRouter : IRouter
    {
        public VirtualPathData? GetVirtualPath(VirtualPathContext context)
        {
            var action = context.Values["action"]?.ToString();
            return action is null ? null : new VirtualPathData(this, $"Admin/Harvest/{action}");
        }

        public Task RouteAsync(RouteContext context) => Task.CompletedTask;
    }

    private sealed class StubArchidektCacheJobService : IArchidektCacheJobService
    {
        public Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ArchidektCacheJobStatus? GetJob(Guid jobId) => null;

        public ArchidektCacheJobStatus? GetActiveJob() => null;

        public Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class StubHarvestRunStore : IHarvestRunStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid> InsertQueuedAsync(HarvestRunKind kind, int durationSeconds, string? url, DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int? decksProcessed, int? additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateProgressAsync(Guid id, int decksProcessed, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SetSweepCountsAsync(Guid id, int decksEnqueued, int decksDrained, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentHealthSignalRunsAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("0");

        public Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(null);

        public Task<HarvestFailureStreak> GetFailureStreakSinceLastSuccessAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HarvestFailureStreak(0, null, null));

        public Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0L);
    }

    private sealed class StubHarvestScheduleStore : IHarvestScheduleStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HarvestScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultSchedule);

        public Task SaveAsync(int? intervalHours, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubHarvestScheduleCache : IHarvestScheduleCache
    {
        public HarvestScheduleSnapshot Snapshot() => DefaultSchedule;

        public Task ReloadAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubHarvestStatsAggregator : IHarvestStatsAggregator
    {
        public Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HarvestStatsPayload(
                0,
                0,
                0,
                0,
                0,
                Array.Empty<HarvestRunRow>(),
                null,
                null,
                null,
                new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false)));

        public void Invalidate()
        {
        }
    }

    private sealed class StubArchidektDeckImporter : IArchidektDeckImporter
    {
        public List<DeckEntry> Entries { get; set; } = new();

        public ArchidektDeckMetadata? Metadata { get; set; }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries);

        public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => new(await ImportAsync(urlOrDeckId, cancellationToken), Metadata);
    }

    private static HarvestScheduleSnapshot DefaultSchedule
        => new(null, Paused: false, DateTimeOffset.MinValue);

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }
}
