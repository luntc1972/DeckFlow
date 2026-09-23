using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

public sealed class HarvestPostgresInListTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public HarvestPostgresInListTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task MarkDeckProcessedAsync_RefreshesCommanderSummary()
    {
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(
                RelationalDatabaseProvider.Postgres,
                await _fixture.GetConnectionStringOrSkipAsync()));
        var commander = $"pg-{Guid.NewGuid():N}";
        var deckId = $"pg-{Guid.NewGuid():N}";

        await repository.AddDeckIdsAsync(new[] { deckId });
        await repository.MarkDeckProcessedAsync(deckId, commander);

        Assert.Equal(1, await repository.GetCommanderDeckCountAsync(commander));
        Assert.Equal(1, (await repository.GetPagedProcessedCommanderRowsAsync(1, 10))
            .Single(row => row.CommanderName == commander).DeckCount);
    }

    [PostgresFact]
    public async Task MarkDeckProcessedAsync_RefreshesExistingCommanderSummary()
    {
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(
                RelationalDatabaseProvider.Postgres,
                await _fixture.GetConnectionStringOrSkipAsync()));
        var commander = $"pg-{Guid.NewGuid():N}";
        var deckIds = new[] { $"pg-{Guid.NewGuid():N}", $"pg-{Guid.NewGuid():N}" };

        await repository.AddDeckIdsAsync(deckIds);
        await repository.MarkDeckProcessedAsync(deckIds[0], commander);
        await repository.MarkDeckProcessedAsync(deckIds[1], commander);

        Assert.Equal(2, await repository.GetCommanderDeckCountAsync(commander));
    }

    [PostgresFact]
    public async Task GetCategoriesForNamesAsync_WithMultipleNames_DoesNotThrow()
    {
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(
                RelationalDatabaseProvider.Postgres,
                await _fixture.GetConnectionStringOrSkipAsync()));

        var names = new[] { $"pg-card-{Guid.NewGuid():N}", $"pg-card-{Guid.NewGuid():N}" };

        var categories = await repository.GetCategoriesForNamesAsync(names);

        // Why: the batch lookup keys every requested name, mapping unknown cards to an empty list.
        Assert.Equal(2, categories.Count);
        Assert.All(categories.Values, Assert.Empty);
    }
}
