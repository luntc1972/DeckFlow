using System.Data.Common;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>Postgres coverage for the single active harvest-run invariant.</summary>
public sealed class HarvestRunPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public HarvestRunPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task EnsureSchemaAsync_OneActiveRow_RejectsSecondActiveRow()
    {
        var connectionString = await ResetAsync();
        var store = CreateStore(connectionString);
        await store.EnsureSchemaAsync();
        await InsertActiveRowAsync(connectionString);

        var exception = await Assert.ThrowsAnyAsync<DbException>(() => InsertActiveRowAsync(connectionString));

        Assert.Contains("ux_harvest_runs_one_active", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task EnqueueAsync_ConcurrentRequests_ReturnOneNewJobAndOneExistingJob()
    {
        var connectionString = await ResetAsync();
        var store = CreateStore(connectionString);
        await store.EnsureSchemaAsync();
        var service = new ArchidektCacheJobService(null!, store, null!, null!, null!, NullLogger<ArchidektCacheJobService>.Instance);

        var results = await Task.WhenAll(
            service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromMinutes(1), HarvestTriggerSource.Manual),
            service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromMinutes(1), HarvestTriggerSource.Manual));

        Assert.Single(results, result => result.StartedNewJob);
        Assert.Single(results, result => !result.StartedNewJob);
        Assert.Equal(results[0].Job.JobId, results[1].Job.JobId);
        Assert.Equal(1, await CountActiveRowsAsync(connectionString));
    }

    private async Task<string> ResetAsync()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await ExecuteAsync(connectionString, "DROP TABLE IF EXISTS harvest_runs;");
        return connectionString;
    }

    private static HarvestRunStore CreateStore(string connectionString) => new(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));

    private static async Task InsertActiveRowAsync(string connectionString)
    {
        await ExecuteAsync(connectionString, "INSERT INTO harvest_runs (id, kind, state, duration_seconds) VALUES (gen_random_uuid(), 'bulk', 'Queued', 60);");
    }

    private static async Task<long> CountActiveRowsAsync(string connectionString)
    {
        await using var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString).CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM harvest_runs WHERE state IN ('Queued', 'Running');";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString).CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
