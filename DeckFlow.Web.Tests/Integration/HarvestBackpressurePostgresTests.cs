using DeckFlow.Core.Storage;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services.Harvest;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

public sealed class HarvestBackpressurePostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public HarvestBackpressurePostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task GetFailureStreakSinceLastSuccessAsync_UsesCompletionBoundaryAfterSuccess()
    {
        var store = new HarvestRunStore(new RelationalDatabaseConnection(
            RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync()));
        await store.EnsureSchemaAsync();
        var before = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.Parse("2026-06-12T10:00:00Z"));
        await store.UpdateStateAsync(before, HarvestRunState.Failed, null, DateTimeOffset.Parse("2026-06-12T10:30:00Z"), null, null, null);
        var success = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.Parse("2026-06-12T11:00:00Z"));
        await store.UpdateStateAsync(success, HarvestRunState.Succeeded, null, DateTimeOffset.Parse("2026-06-12T11:30:00Z"), null, null, null);
        var failure = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.Parse("2026-06-12T12:00:00Z"));
        await store.UpdateStateAsync(failure, HarvestRunState.Failed, null, DateTimeOffset.Parse("2026-06-12T12:30:00Z"), null, null, null);

        var streak = await store.GetFailureStreakSinceLastSuccessAsync();

        Assert.Equal(1, streak.ConsecutiveFailures);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T12:30:00Z"), streak.LastFailureUtc);
    }

    [PostgresFact]
    public async Task HasMoreThanUnprocessedDecksAsync_UsesExclusiveThresholdAndExcludesTerminalRows()
    {
        var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(
            RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync()));
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(new[] { "one", "two" });

        Assert.False(await repository.HasMoreThanUnprocessedDecksAsync(2));
        Assert.True(await repository.HasMoreThanUnprocessedDecksAsync(1));
        await repository.MarkDeckProcessedAsync("one", null, skip: true, metadata: null);
        await repository.MarkDeckProcessedAsync("two", null, skip: false, metadata: null);
        Assert.False(await repository.HasMoreThanUnprocessedDecksAsync(0));
    }
}
