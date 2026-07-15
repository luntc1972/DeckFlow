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
        store.Memberships.Add(new CategoryDeckMembership("Ramp", "Birds of Paradise", 1));
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
        store.Memberships.AddRange(
        [
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 1),
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 2),
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 3),
        ]);
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
        store.Memberships.AddRange(
        [
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 1),
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 2),
        ]);
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
        store.Memberships.AddRange(
        [
            new CategoryDeckMembership("Niche Value", "Guardian Project", 1),
            new CategoryDeckMembership("Niche Value", "Guardian Project", 2),
        ]);
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
        store.Memberships.AddRange(
        [
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 1),
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 2),
            new CategoryDeckMembership("ramp", "Llanowar Elves", 2),
        ]);
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Bello", CancellationToken.None);

        var summary = Assert.Single(result.Summaries);
        Assert.Equal("Ramp", summary.Category);
        Assert.Equal(4, summary.Count);
        Assert.Equal(2, summary.DeckCount);
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
        store.Memberships.AddRange(
        [
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 1),
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 2),
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 3),
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 4),
            new CategoryDeckMembership("Ramp", "Birds of Paradise", 5),
        ]);
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
        store.Memberships.AddRange(
        [
            new CategoryDeckMembership("Tokens", "Card A", 1),
            new CategoryDeckMembership("Tokens", "Card A", 2),
            new CategoryDeckMembership("Tokens", "Card A", 3),
            new CategoryDeckMembership("Tokens", "Card A", 4),
            new CategoryDeckMembership("Tokens", "Card A", 5),
            new CategoryDeckMembership("Tokens", "Card A", 6),
            new CategoryDeckMembership("Tokens", "Card A", 7),
            new CategoryDeckMembership("Tokens", "Card A", 8),
            new CategoryDeckMembership("Ramp", "Card B", 1),
            new CategoryDeckMembership("Ramp", "Card B", 2),
            new CategoryDeckMembership("Ramp", "Card B", 3),
            new CategoryDeckMembership("Ramp", "Card B", 4),
            new CategoryDeckMembership("Ramp", "Card B", 5),
            new CategoryDeckMembership("Draw", "Card C", 1),
            new CategoryDeckMembership("Draw", "Card C", 2),
            new CategoryDeckMembership("Draw", "Card C", 3),
            new CategoryDeckMembership("Draw", "Card C", 4),
            new CategoryDeckMembership("Draw", "Card C", 5),
        ]);
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Bello", CancellationToken.None);

        Assert.Equal(["Tokens", "Draw", "Ramp"], result.Summaries.Select(summary => summary.Category));
    }

    [Fact]
    public async Task LookupAsync_UsesDistinctDeckMembershipUnionForDeckShare()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            CategoryRowsResult =
            [
                new CategoryKnowledgeRow("Ramp", "Sol Ring", 6, 6),
                new CategoryKnowledgeRow("ramp", "Arcane Signet", 5, 5),
            ],
            CommanderDeckCount = 7
        };
        store.Memberships.AddRange(
        [
            new CategoryDeckMembership("Ramp", "Sol Ring", 101),
            new CategoryDeckMembership("Ramp", "Sol Ring", 102),
            new CategoryDeckMembership("Ramp", "Sol Ring", 103),
            new CategoryDeckMembership("Ramp", "Sol Ring", 104),
            new CategoryDeckMembership("Ramp", "Sol Ring", 105),
            new CategoryDeckMembership("Ramp", "Sol Ring", 106),
            new CategoryDeckMembership("ramp", "Arcane Signet", 103),
            new CategoryDeckMembership("ramp", "Arcane Signet", 104),
            new CategoryDeckMembership("ramp", "Arcane Signet", 105),
            new CategoryDeckMembership("ramp", "Arcane Signet", 106),
            new CategoryDeckMembership("ramp", "Arcane Signet", 107),
        ]);
        var service = new CommanderCategoryService(store);

        var result = await service.LookupAsync("Bello", CancellationToken.None);

        var summary = Assert.Single(result.Summaries);
        Assert.Equal("Ramp", summary.Category);
        Assert.Equal(11, summary.Count);
        Assert.Equal(7, summary.DeckCount);
        Assert.Equal(1d, summary.DeckShare, 6);
        Assert.True(summary.DeckShare <= 1d);
    }
}
