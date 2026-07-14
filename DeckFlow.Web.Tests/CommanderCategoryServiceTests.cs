using DeckFlow.Core.Reporting;
using DeckFlow.Web.Services;
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
            CategoryRowsResult = [new CategoryKnowledgeRow("Ramp", "Birds of Paradise", 2, 1)],
            CommanderDeckCount = 20
        };
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Atraxa, Praetors' Voice", CancellationToken.None);

        Assert.Equal(0, store.RunCacheSweepCalls);
        Assert.Single(result.Rows);
        Assert.Single(result.Summaries);
        Assert.Equal(20, result.CardDeckTotals.TotalDeckCount);
    }

    [Fact]
    public async Task LookupAsync_DeckCountMeetsThreshold_KeepsSummary()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            CategoryRowsResult =
            [
                new CategoryKnowledgeRow("Ramp", "Birds of Paradise", 4, 3),
            ],
            CommanderDeckCount = 100
        };
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Bello", CancellationToken.None);

        var summary = Assert.Single(result.Summaries);
        Assert.Equal("Ramp", summary.Category);
    }

    [Fact]
    public async Task LookupAsync_BelowDeckCountAndShareThreshold_DropsSummary()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            CategoryRowsResult =
            [
                new CategoryKnowledgeRow("Ramp", "Birds of Paradise", 2, 2),
            ],
            CommanderDeckCount = 100
        };
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Bello", CancellationToken.None);

        Assert.Empty(result.Summaries);
    }

    [Fact]
    public async Task LookupAsync_DeckShareMeetsThreshold_KeepsSummary()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            CategoryRowsResult =
            [
                new CategoryKnowledgeRow("Niche Value", "Guardian Project", 2, 2),
            ],
            CommanderDeckCount = 40
        };
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Bello", CancellationToken.None);

        var summary = Assert.Single(result.Summaries);
        Assert.Equal("Niche Value", summary.Category);
    }

    [Fact]
    public async Task LookupAsync_CategoryVariantsCollapseIntoSingleSummary()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            CategoryRowsResult =
            [
                new CategoryKnowledgeRow("Ramp", "Birds of Paradise", 3, 2),
                new CategoryKnowledgeRow("ramp", "Llanowar Elves", 1, 1),
            ],
            CommanderDeckCount = 20
        };
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Bello", CancellationToken.None);

        var summary = Assert.Single(result.Summaries);
        Assert.Equal("Ramp", summary.Category);
        Assert.Equal(4, summary.Count);
        Assert.Equal(3, summary.DeckCount);
    }

    [Fact]
    public async Task LookupAsync_ComputesDeckShare()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            CategoryRowsResult =
            [
                new CategoryKnowledgeRow("Ramp", "Birds of Paradise", 3, 5),
            ],
            CommanderDeckCount = 20
        };
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Bello", CancellationToken.None);

        var summary = Assert.Single(result.Summaries);
        Assert.Equal(0.25d, summary.DeckShare, 6);
    }

    [Fact]
    public async Task LookupAsync_RanksByDeckShareThenDeckCountThenCategory()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            CategoryRowsResult =
            [
                new CategoryKnowledgeRow("Tokens", "Card A", 5, 8),
                new CategoryKnowledgeRow("Ramp", "Card B", 4, 5),
                new CategoryKnowledgeRow("Draw", "Card C", 3, 5),
            ],
            CommanderDeckCount = 20
        };
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Bello", CancellationToken.None);

        Assert.Equal(["Tokens", "Draw", "Ramp"], result.Summaries.Select(summary => summary.Category));
    }
}
