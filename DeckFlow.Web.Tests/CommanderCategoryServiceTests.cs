using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="CommanderCategoryService"/> covering cached commander category lookups.
/// </summary>
public sealed class CommanderCategoryServiceTests
{
    [Fact]
    public async Task LookupAsync_ReadsCachedDataWithoutRunningCacheSweep()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            RunCacheSweepException = new InvalidOperationException("Cache sweep should not run."),
            CategoryRowsResult = new[] { new CategoryKnowledgeRow("Ramp", "Birds of Paradise", 2) },
            CommanderDeckCount = 1
        };
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Atraxa, Praetors' Voice", CancellationToken.None);

        Assert.Equal(0, store.RunCacheSweepCalls);
        Assert.Single(result.Rows);
        Assert.Single(result.Summaries);
        Assert.Equal(1, result.CardDeckTotals.TotalDeckCount);
    }

    private sealed class FakeCategoryKnowledgeStore : ICategoryKnowledgeStore
    {
        public int RunCacheSweepCalls { get; private set; }
        public Exception? RunCacheSweepException { get; init; }
        public IReadOnlyList<CategoryKnowledgeRow> CategoryRowsResult { get; init; } = Array.Empty<CategoryKnowledgeRow>();
        public int CommanderDeckCount { get; init; }

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult(CategoryRowsResult);

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(3);

        public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult(CommanderDeckCount);

        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
        {
            RunCacheSweepCalls++;
            if (RunCacheSweepException is not null)
            {
                throw RunCacheSweepException;
            }

            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not reached in this test.");
    }
}
