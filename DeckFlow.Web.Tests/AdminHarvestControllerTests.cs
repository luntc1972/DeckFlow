using DeckFlow.Core.Integration;
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
    public async Task Index_DoesNotCallCommanderCountOrPagedQuery()
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

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "AdminHarvest" })),
            new ActionDescriptor());
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

        public Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int decksProcessed, int additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateProgressAsync(Guid id, int decksProcessed, int additionalDecksFound, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("0");

        public Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(null);

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
                Array.Empty<HarvestRunRow>(),
                null,
                null,
                null));

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
