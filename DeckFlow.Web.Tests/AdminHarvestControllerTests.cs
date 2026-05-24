using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="AdminHarvestController"/> covering harvested-deck paging and page clamps.
/// </summary>
public sealed class AdminHarvestControllerTests
{
    [Fact]
    public async Task Index_ClampsHugePageToDeckTotalPages()
    {
        var store = NewStore(totalProcessedDeckCount: 3);
        var controller = Build(store);

        var result = await controller.Index(page: 999999);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminHarvestViewModel>(view.Model);
        Assert.Equal(model.DeckTotalPages, model.DeckPage);
        Assert.Equal(1, model.DeckPage);
    }

    [Fact]
    public async Task Index_ClampsZeroPageToOne()
    {
        var store = NewStore(totalProcessedDeckCount: 3);
        var controller = Build(store);

        var result = await controller.Index(page: 0);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminHarvestViewModel>(view.Model);
        Assert.Equal(1, model.DeckPage);
    }

    [Fact]
    public async Task Index_PassesClampedPageToPagedDeckStore()
    {
        var store = NewStore(totalProcessedDeckCount: 125);
        var controller = Build(store);

        var result = await controller.Index(page: 999999);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminHarvestViewModel>(view.Model);
        Assert.Equal(model.DeckTotalPages, store.LastPagedDeckPage);
        Assert.Equal(AdminHarvestViewModel.DefaultDeckPageSize, store.LastPagedDeckPageSize);
        Assert.NotEqual(999999, store.LastPagedDeckPage);
    }

    private static FakeCategoryKnowledgeStore NewStore(int totalProcessedDeckCount)
        => new()
        {
            TotalProcessedDeckCount = totalProcessedDeckCount,
            PagedDecksResult = new[]
            {
                new HarvestedDeckRow("deck-1", "Commander One", "2026-01-01T00:00:00.0000000Z", null),
                new HarvestedDeckRow("deck-2", "Commander Two", "2026-01-02T00:00:00.0000000Z", null),
                new HarvestedDeckRow("deck-3", "Commander Three", "2026-01-03T00:00:00.0000000Z", null),
            },
        };

    private static AdminHarvestController Build(ICategoryKnowledgeStore store)
    {
        var httpContext = new DefaultHttpContext();
        return new AdminHarvestController(
            new StubArchidektCacheJobService(),
            new StubHarvestRunStore(),
            new StubHarvestScheduleStore(),
            new StubHarvestScheduleCache(),
            new StubHarvestStatsAggregator(),
            new StubArchidektDeckImporter(),
            store,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AdminHarvestController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new StubTempDataProvider()),
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
            => throw new NotImplementedException();

        public Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int decksProcessed, int additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

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
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private static HarvestScheduleSnapshot DefaultSchedule
        => new(null, Paused: false, DateTimeOffset.MinValue);

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
