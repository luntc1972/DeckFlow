using System.Data.Common;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Proves D-01 through D-04 queue refresh parity on Postgres, including F-51-PG-01.
/// </summary>
public sealed class DeckQueueRefreshPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public DeckQueueRefreshPostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task LegacyNullBaseline_RequeuesOnceAndMarkerClearsOnProcessed()
    {
        var repository = await CreateRepositoryAsync();
        var id = NewId("a");
        await repository.AddDeckIdsAsync(new[] { id });
        await repository.MarkDeckProcessedAsync(id, commanderName: null);
        Assert.Equal(1, (await repository.AddListingRowsAsync(new[] { Listing(id) })).RefreshesRequeued);
        await repository.MarkDeckProcessedAsync(id, commanderName: null);
        Assert.Equal(0, (await repository.AddListingRowsAsync(new[] { Listing(id) })).RefreshesRequeued);
    }

    [PostgresFact]
    public async Task WholeSecondCompare_RequeuesOnlyTheNextSecond()
    {
        var repository = await CreateRepositoryAsync();
        var id = NewId("b");
        await repository.AddDeckIdsAsync(new[] { id });
        await repository.MarkDeckProcessedAsync(id, commanderName: null, metadata: Metadata("2026-01-01T00:00:05Z"));
        Assert.Equal(0, (await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck(id, DateTimeOffset.Parse("2026-01-01T00:00:05.999Z")) })).RefreshesRequeued);
        Assert.Equal(1, (await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck(id, DateTimeOffset.Parse("2026-01-01T00:00:06Z")) })).RefreshesRequeued);
    }

    [PostgresFact]
    public async Task PendingRow_KeepsInsertedUtcOnListingAndAddDeckIdsReadd()
    {
        var repository = await CreateRepositoryAsync();
        var id = NewId("c");
        await repository.AddDeckIdsAsync(new[] { id });
        var before = await ReadColumnAsync(id, "inserted_utc");
        await Task.Delay(20);
        await repository.AddListingRowsAsync(new[] { Listing(id) });
        await repository.AddDeckIdsAsync(new[] { id });
        Assert.Equal(before, await ReadColumnAsync(id, "inserted_utc"));
        Assert.Null(await ReadColumnAsync(id, "refresh_requested_utc"));
    }

    [PostgresFact]
    public async Task RefreshDrain_IsFifoByInsertedUtcThenDeckId()
    {
        var repository = await CreateRepositoryAsync();
        var prefix = $"pgr-{Guid.NewGuid():N}";
        var ids = new[] { prefix + "c", prefix + "a", prefix + "b" };
        await repository.AddDeckIdsAsync(new[] { ids[0] });
        await Task.Delay(20);
        await repository.AddDeckIdsAsync(ids[1..]);
        foreach (var id in ids) { await repository.MarkDeckProcessedAsync(id, commanderName: null); await repository.AddListingRowsAsync(new[] { Listing(id) }); }
        Assert.Equal(new[] { ids[0], ids[1], ids[2] }, (await repository.GetNextRefreshDeckIdsAsync(1000)).Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).ToArray());
    }

    [PostgresFact]
    public async Task RefreshMarker_ClearsOnSkipAndOnUrlPath()
    {
        var repository = await CreateRepositoryAsync();
        var id = NewId("d");
        await repository.AddDeckIdsAsync(new[] { id }); await repository.MarkDeckProcessedAsync(id, null); await repository.AddListingRowsAsync(new[] { Listing(id) });
        await repository.MarkDeckProcessedAsync(id, null, skip: true);
        Assert.Null(await ReadColumnAsync(id, "refresh_requested_utc"));
    }

    [PostgresFact]
    public async Task AddDeckIdsAsync_NeverRequeuesProcessedDeckWithOldLastChecked()
    {
        var repository = await CreateRepositoryAsync(); var id = NewId("e");
        await repository.AddDeckIdsAsync(new[] { id }); await repository.MarkDeckProcessedAsync(id, null);
        Assert.Equal(0, await repository.AddDeckIdsAsync(new[] { id }));
    }

    [PostgresFact]
    public async Task EnsureSchemaTwice_PreservesRowsAndCreatesRefreshIndex()
    {
        var repository = await CreateRepositoryAsync(); var id = NewId("f");
        await repository.AddDeckIdsAsync(new[] { id }); await repository.MarkDeckProcessedAsync(id, null); await repository.AddListingRowsAsync(new[] { Listing(id) });
        await repository.GetNextRefreshDeckIdsAsync(1);
        Assert.NotNull(await ReadColumnAsync(id, "refresh_requested_utc"));
    }

    private async Task<CategoryKnowledgeRepository> CreateRepositoryAsync() => new(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync()));
    private static ArchidektListingDeck Listing(string id) => new(id, DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
    private static string NewId(string suffix) => $"pgr-{Guid.NewGuid():N}{suffix}";
    private static ArchidektDeckMetadata Metadata(string updated) => new(1, 1, false, DateTimeOffset.Parse(updated), DateTimeOffset.Parse(updated), DateTimeOffset.Parse(updated));
    private async Task<string?> ReadColumnAsync(string id, string column)
    {
        var connectionInfo = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync());
        await using var connection = connectionInfo.CreateConnection(); await connection.OpenAsync();
        await using DbCommand command = connection.CreateCommand(); command.CommandText = $"SELECT {column} FROM deck_queue WHERE deck_id = @id;";
        var parameter = command.CreateParameter(); parameter.ParameterName = "id"; parameter.Value = id; command.Parameters.Add(parameter);
        var value = await command.ExecuteScalarAsync(); return value is DBNull or null ? null : Convert.ToString(value);
    }
}
