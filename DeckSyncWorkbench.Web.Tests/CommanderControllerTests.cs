using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeckSyncWorkbench.Core.Reporting;
using DeckSyncWorkbench.Web.Controllers;
using DeckSyncWorkbench.Web.Models;
using DeckSyncWorkbench.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckSyncWorkbench.Web.Tests;

public sealed class CommanderControllerTests
{
    [Fact]
    public async Task Index_ReturnsSummaries_WhenCacheHasData()
    {
        var rows = new[]
        {
            new CategoryKnowledgeRow("Ramp", "Bird of Paradise", 3),
            new CategoryKnowledgeRow("Ramp", "Llanowar Elves", 1),
            new CategoryKnowledgeRow("Draw", "Guardian Project", 2)
        };

        var store = new FakeCategoryKnowledgeStore(new[] { rows }, processedDeckCount: 5);
        store.CardDeckTotals = new CardDeckTotals(2, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["commander"] = 2
        });
        var controller = new CommanderController(store, new DummyCommanderSearchService(), NullLogger<CommanderController>.Instance);

        var result = await controller.Index(new CommanderCategoryRequest { CommanderName = "Bello" });
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CommanderCategoryViewModel>(viewResult.Model);

        Assert.Equal(2, model.CategorySummaries.Count);
        Assert.Equal(5, model.HarvestedDeckCount);
        Assert.Equal(1, store.EnsureHarvestFreshCalls);
        Assert.Equal(0, store.ProcessNextDecksCalled);
        Assert.Equal(1, store.GetCategoryRowsCallCount);
        Assert.Equal("commander", store.LastBoardFilter);
        Assert.True(model.ExtendedHarvestTriggered);
        Assert.Equal(0, model.AdditionalDecksFound);
        Assert.Equal("commander", store.LastCardTotalsBoardFilter);
        Assert.Equal(2, model.CardDeckTotals.TotalDeckCount);
    }

    [Fact]
    public async Task Index_ImportsMoreDecks_WhenCacheEmpty()
    {
        var rows = new[] { new CategoryKnowledgeRow("Ramp", "Bird of Paradise", 3) };
        var store = new FakeCategoryKnowledgeStore(new[]
        {
            Array.Empty<CategoryKnowledgeRow>(),
            rows
        }, processedDeckCount: 2);

        var controller = new CommanderController(store, new DummyCommanderSearchService(), NullLogger<CommanderController>.Instance);

        var result = await controller.Index(new CommanderCategoryRequest { CommanderName = "Bello" });
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CommanderCategoryViewModel>(viewResult.Model);

        Assert.Single(model.CategorySummaries);
        Assert.Equal(3, model.HarvestedDeckCount);
        Assert.Equal(1, store.ProcessNextDecksCalled);
        Assert.Equal(2, store.GetCategoryRowsCallCount);
        Assert.Equal("commander", store.LastBoardFilter);
        Assert.True(model.ExtendedHarvestTriggered);
        Assert.Equal(1, model.AdditionalDecksFound);
        Assert.Equal("commander", store.LastCardTotalsBoardFilter);
    }

    private sealed class FakeCategoryKnowledgeStore : ICategoryKnowledgeStore
    {
        private readonly Queue<IReadOnlyList<CategoryKnowledgeRow>> _responses;

        public FakeCategoryKnowledgeStore(IEnumerable<IReadOnlyList<CategoryKnowledgeRow>> responses, int processedDeckCount)
        {
            _responses = new Queue<IReadOnlyList<CategoryKnowledgeRow>>(responses);
            ProcessedDeckCount = processedDeckCount;
            CurrentRows = Array.Empty<CategoryKnowledgeRow>();
        }

        public int EnsureHarvestFreshCalls { get; private set; }
        public int ProcessNextDecksCalled { get; private set; }
        public int GetCategoryRowsCallCount { get; private set; }
        public int RunCacheSweepCalled { get; private set; }
        public int PersistObservedCategoryCalls { get; private set; }
        public int GetCategoriesCalls { get; private set; }
        public int ProcessedDeckCount { get; private set; }
        public CardDeckTotals CardDeckTotals { get; set; } = CardDeckTotals.Empty;
        private IReadOnlyList<CategoryKnowledgeRow> CurrentRows { get; set; }
        public string? LastCardTotalsBoardFilter { get; private set; }

        public Task EnsureHarvestFreshAsync(ILogger logger, CancellationToken cancellationToken = default)
        {
            EnsureHarvestFreshCalls++;
            return Task.CompletedTask;
        }

        public string? LastBoardFilter { get; private set; }

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
        {
            LastBoardFilter = boardFilter;
            GetCategoryRowsCallCount++;
            if (_responses.Count > 0)
            {
                CurrentRows = _responses.Dequeue();
            }

            return Task.FromResult(CurrentRows);
        }

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProcessedDeckCount);
        }

        public Task<int> ProcessNextDecksAsync(ILogger logger, CancellationToken cancellationToken = default)
        {
            ProcessNextDecksCalled++;
            ProcessedDeckCount++;
            return Task.FromResult(1);
        }

        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default)
        {
            RunCacheSweepCalled++;
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
        {
            GetCategoriesCalls++;
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        Task ICategoryKnowledgeStore.PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity, string board, int deckCountIncrement, CancellationToken cancellationToken)
        {
            PersistObservedCategoryCalls++;
            return Task.CompletedTask;
        }

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
        {
            LastCardTotalsBoardFilter = boardFilter;
            return Task.FromResult(CardDeckTotals);
        }
    }

    private sealed class DummyCommanderSearchService : ICommanderSearchService
    {
        public Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }
}
