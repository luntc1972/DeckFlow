using DeckFlow.Core.Reporting;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services;

public interface ICategoryKnowledgeStore
{
    Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default);
    /// <summary>Returns all card-category observations from decks led by the specified commander.</summary>
    Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default);
    Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns the count of processed decks led by the specified commander.</summary>
    Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default);
    Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null);
    Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default);
    Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default);
    Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default);
    Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
    Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default);
    Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default);
    Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default);
}
