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
    public async Task Index_ReturnsSummaries_WhenServiceHasData()
    {
        var rows = new[]
        {
            new CategoryKnowledgeRow("Ramp", "Bird of Paradise", 3),
            new CategoryKnowledgeRow("Ramp", "Llanowar Elves", 1),
            new CategoryKnowledgeRow("Draw", "Guardian Project", 2)
        };

        var summaries = new[]
        {
            new CommanderCategorySummary("Ramp", 4, 3),
            new CommanderCategorySummary("Draw", 2, 2)
        };

        var cardTotals = new CardDeckTotals(2, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["commander"] = 2
        });

        var result = new CommanderCategoryResult(
            "Bello",
            rows,
            summaries,
            HarvestedDeckCount: 5,
            CardDeckTotals: cardTotals,
            AdditionalDecksFound: 0,
            CacheSweepPerformed: true);

        var controller = new CommanderController(
            new DummyCategoryKnowledgeStore(),
            new DummyCommanderSearchService(),
            new FakeCommanderCategoryService(result),
            NullLogger<CommanderController>.Instance);

        var response = await controller.Index(new CommanderCategoryRequest { CommanderName = "Bello" });
        var viewResult = Assert.IsType<ViewResult>(response);
        var model = Assert.IsType<CommanderCategoryViewModel>(viewResult.Model);

        Assert.Equal(rows.Length, model.CategoryRows.Count);
        Assert.Equal(summaries.Length, model.CategorySummaries.Count);
        Assert.Equal(5, model.HarvestedDeckCount);
        Assert.True(model.HasResults);
        Assert.True(model.ExtendedHarvestTriggered);
        Assert.Equal(0, model.AdditionalDecksFound);
        Assert.Equal(cardTotals.TotalDeckCount, model.CardDeckTotals.TotalDeckCount);
    }

    [Fact]
    public async Task Index_ShowsNoResults_WhenServiceReturnsEmpty()
    {
        var result = new CommanderCategoryResult(
            "Bello",
            Array.Empty<CategoryKnowledgeRow>(),
            Array.Empty<CommanderCategorySummary>(),
            HarvestedDeckCount: 0,
            CardDeckTotals: CardDeckTotals.Empty,
            AdditionalDecksFound: 0,
            CacheSweepPerformed: false);

        var controller = new CommanderController(
            new DummyCategoryKnowledgeStore(),
            new DummyCommanderSearchService(),
            new FakeCommanderCategoryService(result),
            NullLogger<CommanderController>.Instance);

        var response = await controller.Index(new CommanderCategoryRequest { CommanderName = "Bello" });
        var viewResult = Assert.IsType<ViewResult>(response);
        var model = Assert.IsType<CommanderCategoryViewModel>(viewResult.Model);

        Assert.Empty(model.CategorySummaries);
        Assert.False(model.HasResults);
        Assert.False(model.ExtendedHarvestTriggered);
        Assert.Equal(0, model.AdditionalDecksFound);
    }

    private sealed class DummyCategoryKnowledgeStore : ICategoryKnowledgeStore
    {
        public Task EnsureHarvestFreshAsync(ILogger logger, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());
        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> ProcessNextDecksAsync(ILogger logger, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CardDeckTotals.Empty);
    }

    private sealed class DummyCommanderSearchService : ICommanderSearchService
    {
        public Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class FakeCommanderCategoryService : ICommanderCategoryService
    {
        private readonly CommanderCategoryResult _result;

        public FakeCommanderCategoryService(CommanderCategoryResult result)
        {
            _result = result;
        }

        public Task<CommanderCategoryResult> LookupAsync(string commanderName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }
}
