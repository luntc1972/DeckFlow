using DeckFlow.Core.Reporting;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Tests;

public sealed class FakeCategoryKnowledgeStore : ICategoryKnowledgeStore
{
    private readonly Queue<int> _processedDeckCounts = new();
    private int _lastProcessedDeckCount;

    public FakeCategoryKnowledgeStore(int initialProcessedDeckCount = 0, int finalProcessedDeckCount = 0)
    {
        SetProcessedDeckCounts(initialProcessedDeckCount, finalProcessedDeckCount);
    }

    public int GetProcessedDeckCountCalls { get; private set; }

    public int RunCacheSweepCalls { get; private set; }

    public int RunCacheSweepResult { get; set; }

    public Exception? RunCacheSweepException { get; set; }

    public void SetProcessedDeckCounts(params int[] counts)
    {
        _processedDeckCounts.Clear();

        foreach (var count in counts)
        {
            _processedDeckCounts.Enqueue(count);
        }

        _lastProcessedDeckCount = counts.Length > 0 ? counts[^1] : 0;
    }

    public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

    public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
    {
        GetProcessedDeckCountCalls++;

        if (_processedDeckCounts.Count > 0)
        {
            _lastProcessedDeckCount = _processedDeckCounts.Dequeue();
        }

        return Task.FromResult(_lastProcessedDeckCount);
    }

    public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default)
    {
        RunCacheSweepCalls++;

        if (RunCacheSweepException is not null)
        {
            throw RunCacheSweepException;
        }

        return Task.FromResult(RunCacheSweepResult);
    }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(CardDeckTotals.Empty);
}
