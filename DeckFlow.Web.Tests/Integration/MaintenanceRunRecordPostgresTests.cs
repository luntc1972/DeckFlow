using DeckFlow.Core.Storage;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace DeckFlow.Web.Tests.Integration;

[Collection("ArchidektThrottleSerial")]
public sealed class MaintenanceRunRecordPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;
    public MaintenanceRunRecordPostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;
    private async Task<HarvestMaintenanceRunStore> StoreAsync(bool clear = false)
    {
        var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync());
        var store = new HarvestMaintenanceRunStore(connection, NullLogger<HarvestMaintenanceRunStore>.Instance);
        await store.EnsureSchemaAsync();
        if (clear)
        {
            await using var database = connection.CreateConnection(); await database.OpenAsync();
            await using var command = database.CreateCommand(); command.CommandText = "DELETE FROM harvest_maintenance_runs"; await command.ExecuteNonQueryAsync();
        }
        return store;
    }
    [PostgresFact]
    public async Task EnsureSchema_Postgres_CreatesTableAndReapsRunningRow()
    {
        var store = await StoreAsync(clear: true); var row = HarvestMaintenanceRunStoreTests.Row() with { StartedUtc = HarvestMaintenanceRunStoreTests.Now };
        await store.InsertAsync(row);
        var next = await StoreAsync(); await next.EnsureSchemaAsync();
        Assert.Equal(HarvestMaintenanceRunState.Interrupted, (await next.GetLatestAsync())!.State);
    }
    [PostgresFact]
    public async Task RoundTrip_Postgres_InsertSaveGetLatest()
    {
        var store = await StoreAsync(clear: true); var row = HarvestMaintenanceRunStoreTests.Row() with { StartedUtc = HarvestMaintenanceRunStoreTests.Now };
        await store.InsertAsync(row); var final = row with { State = HarvestMaintenanceRunState.Finished };
        await store.SaveAsync(final); Assert.Equal(final, await store.GetLatestAsync());
    }
    [PostgresFact]
    public async Task SaveAsync_Postgres_ConditionalStateRule()
    {
        var store = await StoreAsync(clear: true); var row = HarvestMaintenanceRunStoreTests.Row(HarvestMaintenanceRunState.Interrupted) with { StartedUtc = HarvestMaintenanceRunStoreTests.Now };
        await store.InsertAsync(row); await store.SaveAsync(row with { State = HarvestMaintenanceRunState.Running, DecksAffected = 99 });
        Assert.Equal(row with { DecksAffected = 99 }, await store.GetLatestAsync());
        var final = row with { State = HarvestMaintenanceRunState.Finished }; await store.SaveAsync(final);
        await store.SaveAsync(final with { State = HarvestMaintenanceRunState.Failed }); Assert.Equal(final, await store.GetLatestAsync());
    }
}
