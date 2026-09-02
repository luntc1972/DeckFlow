using DeckFlow.Core.Reporting;
using DeckFlow.Core.Integration;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Stateful fake <see cref="ICategoryKnowledgeStore"/> that returns configurable processed-deck counts
/// and captures submitted knowledge rows for assertion in tests.
/// </summary>
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

    public int TotalProcessedDeckCount { get; set; }

    public string? LastUrlDeckId { get; private set; }

    public string? LastUrlCommanderName { get; private set; }

    public ArchidektDeckMetadata? LastUrlMetadata { get; private set; }

    public int DistinctProcessedCommanderCount { get; set; }

    public int GetDistinctProcessedCommanderCountCalls { get; private set; }

    public IReadOnlyList<CategoryKnowledgeRow> CategoryRowsResult { get; set; } = Array.Empty<CategoryKnowledgeRow>();

    public List<CategoryDeckMembership> Memberships { get; } = new();

    public int CommanderDeckCount { get; set; }

    public IReadOnlyList<HarvestedCommanderRow> PagedCommandersResult { get; set; } = Array.Empty<HarvestedCommanderRow>();

    public int LastPagedCommanderPage { get; private set; }

    public int LastPagedCommanderPageSize { get; private set; }

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

    public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
    {
        RunCacheSweepCalls++;

        if (RunCacheSweepException is not null)
        {
            throw RunCacheSweepException;
        }

        return Task.FromResult(RunCacheSweepResult);
    }

    /// <summary>Configurable per-card categories; empty by default so unset cards resolve to no roles.</summary>
    public Dictionary<string, IReadOnlyList<string>> CategoriesByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Configurable per-card weighted deck counts keyed by canonical category label.</summary>
    public Dictionary<string, IReadOnlyDictionary<string, int>> CategoryDeckCountsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Number of times the batch lookup ran — a plan-presence analysis must call it exactly once.</summary>
    public int GetCategoriesForNamesCalls { get; private set; }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
        => Task.FromResult(CategoriesByName.TryGetValue(cardName, out var categories)
            ? categories
            : (IReadOnlyList<string>)Array.Empty<string>());

    public Task<IReadOnlyDictionary<string, int>> GetCategoryDeckCountsAsync(string cardName, CancellationToken cancellationToken = default)
        => Task.FromResult(CategoryDeckCountsByName.TryGetValue(cardName, out var categoryDeckCounts)
            ? categoryDeckCounts
            : (IReadOnlyDictionary<string, int>)new Dictionary<string, int>(StringComparer.Ordinal));

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default)
    {
        GetCategoriesForNamesCalls++;
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in cardNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            result[name] = CategoriesByName.TryGetValue(name, out var categories) ? categories : Array.Empty<string>();
        }

        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(result);
    }

    public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
    {
        LastUrlDeckId = deckId;
        LastUrlCommanderName = commanderName;
        return Task.CompletedTask;
    }

    public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, ArchidektDeckMetadata? metadata, CancellationToken cancellationToken = default)
    {
        LastUrlDeckId = deckId;
        LastUrlCommanderName = commanderName;
        LastUrlMetadata = metadata;
        return Task.CompletedTask;
    }

    public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(TotalProcessedDeckCount);

    public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TopCommanderRow>>(Array.Empty<TopCommanderRow>());

    public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        LastPagedCommanderPage = page;
        LastPagedCommanderPageSize = pageSize;
        return Task.FromResult(PagedCommandersResult);
    }

    public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
    {
        GetDistinctProcessedCommanderCountCalls++;
        return Task.FromResult(DistinctProcessedCommanderCount);
    }

    public Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<long?>(null);

    public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(CardDeckTotals.Empty);

    public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
        => Task.FromResult(CategoryRowsResult);

    public Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CategoryDeckMembership>>(Memberships);

    public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
        => Task.FromResult(CommanderDeckCount);
}
