using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="CategorySuggestionService"/> covering mode routing (cached, reference-deck, tagger, all)
/// and inferred-category precedence.
/// </summary>
public sealed class CategorySuggestionServiceTests
{
    [Fact]
    public async Task SuggestAsync_UsesInferredCategoriesFromCachedStore()
    {
        var totals = new CardDeckTotals(1, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["mainboard"] = 1
        });

        var store = new FakeKnowledgeStore(new[] { new[] { "Ramp" } }, processedDeckCount: 3, totals);
        var service = new CategorySuggestionService(store, new ArchidektParser(), new FakeImporter(), new FakeTaggerService());

        var request = new CategorySuggestionRequest
        {
            Mode = CategorySuggestionMode.CachedData,
            CardName = "Bird of Paradise"
        };

        var result = await service.SuggestAsync(request);

        Assert.False(result.NothingFound);
        Assert.Contains("Ramp", result.InferredCategories);
        Assert.Equal(0, store.RunCacheSweepCalls);
        Assert.Equal(1, result.CardDeckTotals.TotalDeckCount);
    }

    [Fact]
    public async Task SuggestAsync_ReadsCachedDataWithoutRunningCacheSweep()
    {
        var totals = new CardDeckTotals(1, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["mainboard"] = 1
        });

        var store = new FakeKnowledgeStore(new[] { new[] { "Ramp" } }, processedDeckCount: 3, totals)
        {
            RunCacheSweepException = new InvalidOperationException("Cache sweep should not run.")
        };
        var service = new CategorySuggestionService(store, new ArchidektParser(), new FakeImporter(), new FakeTaggerService());

        var request = new CategorySuggestionRequest
        {
            Mode = CategorySuggestionMode.CachedData,
            CardName = "Bird of Paradise"
        };

        var result = await service.SuggestAsync(request);

        Assert.False(result.NothingFound);
        Assert.Contains("Ramp", result.InferredCategories);
        Assert.Equal(0, store.RunCacheSweepCalls);
    }

    [Fact]
    public async Task SuggestAsync_UsesReferenceDeckWhenConfigured()
    {
        var totals = CardDeckTotals.Empty;
        var store = new FakeKnowledgeStore(new[] { Array.Empty<string>() }, processedDeckCount: 0, totals);
        var entries = new List<DeckEntry>
        {
            new() { Name = "Guardian Project", NormalizedName = CardNormalizer.Normalize("Guardian Project"), Category = "Draw,Ramp", Quantity = 1, Board = "mainboard" }
        };
        var importer = new FakeImporter(entries);
        var service = new CategorySuggestionService(store, new ArchidektParser(), importer, new FakeTaggerService());

        var request = new CategorySuggestionRequest
        {
            Mode = CategorySuggestionMode.ReferenceDeck,
            CardName = "Guardian Project",
            ArchidektInputSource = DeckInputSource.PublicUrl,
            ArchidektUrl = "deck-id"
        };

        var result = await service.SuggestAsync(request);

        Assert.Contains("Draw", result.ExactCategories);
        Assert.Contains("Ramp", result.ExactCategories);
        Assert.Contains("reference deck", result.UsedSources);
    }

    [Fact]
    public async Task SuggestAsync_UsesScryfallTaggerModeWithoutCacheSweep()
    {
        var store = new FakeKnowledgeStore(new[] { Array.Empty<string>() }, processedDeckCount: 0, CardDeckTotals.Empty);
        var tagger = new FakeTaggerService("Protection", "Value");
        var service = new CategorySuggestionService(store, new ArchidektParser(), new FakeImporter(), tagger);

        var result = await service.SuggestAsync(new CategorySuggestionRequest
        {
            Mode = CategorySuggestionMode.ScryfallTagger,
            CardName = "Esper Sentinel"
        });

        Assert.False(result.NothingFound);
        Assert.Equal(new[] { "Protection", "Value" }, result.TaggerCategories);
        Assert.Contains("Scryfall Tagger", result.UsedSources);
        Assert.Equal(1, tagger.LookupCalls);
        Assert.Equal(0, store.RunCacheSweepCalls);
    }

    [Fact]
    public async Task SuggestAsync_AllModeIncludesTaggerAndCachedSuggestions()
    {
        var totals = new CardDeckTotals(4, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["mainboard"] = 4
        });
        var store = new FakeKnowledgeStore(new[] { new[] { "Draw" } }, processedDeckCount: 4, totals);
        var tagger = new FakeTaggerService("Value");
        var service = new CategorySuggestionService(store, new ArchidektParser(), new FakeImporter(), tagger);

        var result = await service.SuggestAsync(new CategorySuggestionRequest
        {
            Mode = CategorySuggestionMode.All,
            CardName = "Rhystic Study"
        });

        Assert.Contains("Draw", result.InferredCategories);
        Assert.Contains("Value", result.TaggerCategories);
        Assert.Contains("cached store", result.UsedSources);
        Assert.Contains("Scryfall Tagger", result.UsedSources);
        Assert.Equal(0, store.RunCacheSweepCalls);
        Assert.Equal(1, tagger.LookupCalls);
    }

    private sealed class FakeKnowledgeStore : ICategoryKnowledgeStore
    {
        private readonly Queue<IReadOnlyList<string>> _responses;
        private readonly CardDeckTotals _totals;
        public int RunCacheSweepCalls { get; private set; }
        public int ProcessedDeckCount { get; private set; }
        public Exception? RunCacheSweepException { get; init; }
        private IReadOnlyList<string> _current = Array.Empty<string>();

        public FakeKnowledgeStore(IEnumerable<IReadOnlyList<string>> responses, int processedDeckCount, CardDeckTotals totals)
        {
            _responses = new Queue<IReadOnlyList<string>>(responses);
            ProcessedDeckCount = processedDeckCount;
            _totals = totals;
            _current = _responses.Count > 0 ? _responses.Dequeue() : Array.Empty<string>();
        }

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ProcessedDeckCount);

        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
        {
            RunCacheSweepCalls++;
            if (RunCacheSweepException is not null)
            {
                throw RunCacheSweepException;
            }

            ProcessedDeckCount++;
            _current = _responses.Count > 0 ? _responses.Dequeue() : _current;
            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult(_current);

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(new Dictionary<string, IReadOnlyList<string>>());

        public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopCommanderRow>>(Array.Empty<TopCommanderRow>());

        public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());

        public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(null);

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_totals);

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryDeckMembership>>(Array.Empty<CategoryDeckMembership>());

        public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeImporter : IArchidektDeckImporter
    {
        private readonly IEnumerable<DeckEntry> _entries;

        public FakeImporter(IEnumerable<DeckEntry>? entries = null)
        {
            _entries = entries ?? Array.Empty<DeckEntry>();
        }

        public Task<List<DeckEntry>> ImportAsync(string urlOrId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_entries.ToList());
        }
    }

    private sealed class FakeTaggerService : IScryfallTaggerLookupService
    {
        private readonly IReadOnlyList<string> _responses;

        public FakeTaggerService(params string[] responses)
        {
            _responses = responses;
        }

        public int LookupCalls { get; private set; }

        public Task<IReadOnlyList<string>> LookupOracleTagsAsync(string cardName, CancellationToken cancellationToken = default)
        {
            LookupCalls++;
            return Task.FromResult(_responses);
        }
    }
}
