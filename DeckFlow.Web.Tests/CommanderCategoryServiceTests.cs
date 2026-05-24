using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="CommanderCategoryService"/> covering commander category lookup sweep behavior.
/// </summary>
public sealed class CommanderCategoryServiceTests
{
    [Fact]
    public async Task LookupAsync_SkipsCacheSweep_WhenHarvestActive()
    {
        var store = new FakeCategoryKnowledgeStore();
        var service = new CommanderCategoryService(
            store,
            new FakeActiveJobService(),
            NullLogger<CommanderCategoryService>.Instance);

        await service.LookupAsync("Atraxa, Praetors' Voice", CancellationToken.None);

        Assert.Equal(0, store.RunCacheSweepCalls);
    }

    private sealed class FakeCategoryKnowledgeStore : ICategoryKnowledgeStore
    {
        public int RunCacheSweepCalls { get; private set; }

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(3);

        public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
        {
            RunCacheSweepCalls++;
            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task<IReadOnlyList<HarvestedDeckRow>> GetPagedProcessedDecksAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached via skip-sweep branch.");
    }

    private sealed class FakeActiveJobService : IArchidektCacheJobService
    {
        public Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used in these tests.");

        public ArchidektCacheJobStatus? GetJob(Guid jobId) => null;

        public ArchidektCacheJobStatus? GetActiveJob() => new(
            Guid.NewGuid(),
            ArchidektCacheJobState.Running,
            100,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            0,
            0,
            null);

        public Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
