using System.Data.Common;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services.Harvest;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Proves the D-04/D-08 HARV-11 Postgres run-shape migration preserves Phase 5 telemetry.
/// </summary>
public sealed class HarvestRunStoreKindMigrationPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public HarvestRunStoreKindMigrationPostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task EnsureSchemaAsync_Phase5PostgresTable_SwapsKindCheckAndAddsColumns_Idempotently()
    {
        var connectionString = await ResetPhase5Async();
        var store = CreateStore(connectionString);
        await store.EnsureSchemaAsync();
        var checks = await GetKindChecksAsync(connectionString);
        Assert.Single(checks);
        Assert.Equal("ck_harvest_runs_kind", checks[0].Name);
        Assert.Contains("'update'", checks[0].Definition, StringComparison.Ordinal);
        Assert.True(await ConstraintExistsAsync(connectionString, "ck_harvest_runs_trigger_source"));
        Assert.Equal(18, await GetColumnCountAsync(connectionString));

        var id = await store.InsertQueuedAsync(HarvestRunKind.Update, 600, null, DateTimeOffset.Parse("2026-10-06T15:00:00Z"), HarvestTriggerSource.Scheduled);
        await store.UpdateStateAsync(id, HarvestRunState.Succeeded, null, DateTimeOffset.Parse("2026-10-06T15:01:00Z"), null, null, null);
        await store.SetUpdateCountsAsync(id, 3, 5, 4, 2);
        var snapshot = (await store.GetByIdAsync(id))!;
        Assert.Equal(HarvestRunKind.Update, snapshot.Kind);
        Assert.Equal(HarvestTriggerSource.Scheduled, snapshot.TriggerSource);
        Assert.Equal(3, snapshot.PagesPolled);
        var kindOid = await GetConstraintOidAsync(connectionString, "ck_harvest_runs_kind");
        var triggerOid = await GetConstraintOidAsync(connectionString, "ck_harvest_runs_trigger_source");

        await CreateStore(connectionString).EnsureSchemaAsync();
        Assert.Equal(kindOid, await GetConstraintOidAsync(connectionString, "ck_harvest_runs_kind"));
        Assert.Equal(triggerOid, await GetConstraintOidAsync(connectionString, "ck_harvest_runs_trigger_source"));
        Assert.Equal(snapshot, await CreateStore(connectionString).GetByIdAsync(id));
        var kindException = await Assert.ThrowsAnyAsync<DbException>(() => ExecuteAsync(connectionString, "INSERT INTO harvest_runs (id, kind, state, duration_seconds) VALUES (gen_random_uuid(), @value, 'Succeeded', 1);", "@value", "bogus"));
        Assert.Equal("23514", kindException.SqlState);
        var triggerException = await Assert.ThrowsAnyAsync<DbException>(() => ExecuteAsync(connectionString, "INSERT INTO harvest_runs (id, kind, state, duration_seconds, trigger_source) VALUES (gen_random_uuid(), 'bulk', 'Succeeded', 1, @value);", "@value", "bogus"));
        Assert.Equal("23514", triggerException.SqlState);
    }

    [PostgresFact]
    public async Task EnsureSchemaAsync_FreshPostgresTable_CreatesNamedChecksWithoutMigration()
    {
        var connectionString = await ResetAsync();
        await CreateStore(connectionString).EnsureSchemaAsync();
        var checks = await GetKindChecksAsync(connectionString);
        Assert.Single(checks); Assert.Equal("ck_harvest_runs_kind", checks[0].Name);
        var kindOid = await GetConstraintOidAsync(connectionString, "ck_harvest_runs_kind");
        var triggerOid = await GetConstraintOidAsync(connectionString, "ck_harvest_runs_trigger_source");
        await CreateStore(connectionString).EnsureSchemaAsync();
        Assert.Equal(kindOid, await GetConstraintOidAsync(connectionString, "ck_harvest_runs_kind"));
        Assert.Equal(triggerOid, await GetConstraintOidAsync(connectionString, "ck_harvest_runs_trigger_source"));
    }

    [PostgresFact]
    public async Task PerKindQueriesAndUpdateCounts_OnPostgres_MatchSqliteSemantics()
    {
        var store = CreateStore(await ResetAsync()); await store.EnsureSchemaAsync();
        await SeedAsync(store, HarvestRunKind.Bulk, null, HarvestRunState.Succeeded, "2026-10-06T10:00:00Z");
        await SeedAsync(store, HarvestRunKind.Bulk, HarvestTriggerSource.Scheduled, HarvestRunState.Failed, "2026-10-06T11:00:00Z");
        await SeedAsync(store, HarvestRunKind.Bulk, HarvestTriggerSource.Scheduled, HarvestRunState.Interrupted, "2026-10-06T11:30:00Z");
        await SeedAsync(store, HarvestRunKind.Bulk, HarvestTriggerSource.Manual, HarvestRunState.Succeeded, "2026-10-06T12:00:00Z");
        var update = await SeedAsync(store, HarvestRunKind.Update, HarvestTriggerSource.Scheduled, HarvestRunState.Succeeded, "2026-10-06T13:00:00Z");
        await SeedAsync(store, HarvestRunKind.Update, HarvestTriggerSource.Scheduled, HarvestRunState.Failed, "2026-10-06T14:00:00Z");
        Assert.Equal(DateTimeOffset.Parse("2026-10-06T10:00:00Z"), await store.GetLastScheduledSuccessUtcAsync(HarvestRunKind.Bulk));
        Assert.Equal(DateTimeOffset.Parse("2026-10-06T13:00:00Z"), await store.GetLastScheduledSuccessUtcAsync(HarvestRunKind.Update));
        Assert.Equal(2, (await store.GetFailureStreakSinceLastSuccessAsync(HarvestRunKind.Bulk)).ConsecutiveFailures);
        Assert.Equal(1, (await store.GetFailureStreakSinceLastSuccessAsync(HarvestRunKind.Update)).ConsecutiveFailures);
        await store.SetUpdateCountsAsync(update, 3, 5, 4, 2);
        var row = (await store.GetByIdAsync(update))!;
        Assert.Equal(3, row.PagesPolled); Assert.Equal(5, row.RefreshesRequeued); Assert.Equal(4, row.RefreshesDrained); Assert.Equal(2, row.NewIdsSeen);
        Assert.Null(row.DecksEnqueued); Assert.Null(row.DecksDrained);
    }

    [PostgresFact]
    public async Task EnsureSchemaAsync_PostgresShapeStepFails_RollsBackColumnAdds()
    {
        var connectionString = await ResetPhase5Async();
        var check = Assert.Single(await GetKindChecksAsync(connectionString));
        await ExecuteAsync(connectionString, $"ALTER TABLE harvest_runs DROP CONSTRAINT \"{check.Name.Replace("\"", "\"\"")}\"; INSERT INTO harvest_runs (id, kind, state, duration_seconds) VALUES (gen_random_uuid(), @kind, 'Succeeded', 1);", "@kind", "bogus");
        var exception = await Assert.ThrowsAnyAsync<DbException>(() => CreateStore(connectionString).EnsureSchemaAsync());
        Assert.Equal("23514", exception.SqlState);
        Assert.Equal(13, await GetColumnCountAsync(connectionString));
        Assert.False(await ConstraintExistsAsync(connectionString, "ck_harvest_runs_trigger_source"));
    }

    private async Task<string> ResetAsync()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await ExecuteAsync(connectionString, "DROP TABLE IF EXISTS harvest_runs;");
        return connectionString;
    }

    private async Task<string> ResetPhase5Async()
    {
        var connectionString = await ResetAsync();
        await ExecuteAsync(connectionString, Phase5Sql);
        await ExecuteAsync(connectionString, "INSERT INTO harvest_runs (id, kind, state, duration_seconds) VALUES (gen_random_uuid(), 'bulk', 'Succeeded', 7), (gen_random_uuid(), 'url', 'Succeeded', 11), (gen_random_uuid(), 'bulk', 'Failed', 1);");
        return connectionString;
    }

    private static HarvestRunStore CreateStore(string connectionString) => new(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));

    private static async Task<Guid> SeedAsync(HarvestRunStore store, HarvestRunKind kind, HarvestTriggerSource? trigger, HarvestRunState state, string completedUtc)
    {
        var utc = DateTimeOffset.Parse(completedUtc); var id = await store.InsertQueuedAsync(kind, 60, null, utc, trigger);
        await store.UpdateStateAsync(id, state, null, utc, null, null, null); return id;
    }

    private static async Task<List<(string Name, string Definition)>> GetKindChecksAsync(string connectionString)
    {
        await using var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString).CreateConnection(); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = KindChecksSql;
        await using var reader = await command.ExecuteReaderAsync(); var rows = new List<(string, string)>();
        while (await reader.ReadAsync()) rows.Add((reader.GetString(0), reader.GetString(1))); return rows;
    }

    private static async Task<long> GetConstraintOidAsync(string connectionString, string name)
    {
        await using var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString).CreateConnection(); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT con.oid::bigint FROM pg_constraint con WHERE con.conname = @name;"; AddParameter(command, "@name", name);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> ConstraintExistsAsync(string connectionString, string name) => await GetConstraintCountAsync(connectionString, name) == 1;
    private static async Task<long> GetConstraintCountAsync(string connectionString, string name) { await using var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString).CreateConnection(); await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM pg_constraint WHERE conname = @name;"; AddParameter(command, "@name", name); return Convert.ToInt64(await command.ExecuteScalarAsync()); }
    private static async Task<long> GetColumnCountAsync(string connectionString) { await using var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString).CreateConnection(); await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @table;"; AddParameter(command, "@table", "harvest_runs"); return Convert.ToInt64(await command.ExecuteScalarAsync()); }
    private static async Task ExecuteAsync(string connectionString, string sql, string? parameterName = null, object? value = null) { await using var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString).CreateConnection(); await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = sql; if (parameterName is not null) AddParameter(command, parameterName, value); await command.ExecuteNonQueryAsync(); }
    private static void AddParameter(DbCommand command, string name, object? value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }

    private const string KindChecksSql = "SELECT con.conname, pg_get_constraintdef(con.oid) FROM pg_constraint con INNER JOIN pg_class rel ON rel.oid = con.conrelid INNER JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace WHERE rel.relname = 'harvest_runs' AND nsp.nspname = current_schema() AND con.contype = 'c' AND pg_get_constraintdef(con.oid) LIKE '%kind%' AND pg_get_constraintdef(con.oid) LIKE '%''bulk''%';";
    private const string Phase5Sql = "CREATE TABLE harvest_runs (id UUID PRIMARY KEY, kind TEXT NOT NULL CHECK (kind IN ('bulk','url')), state TEXT NOT NULL, requested_utc TIMESTAMPTZ NOT NULL DEFAULT now(), started_utc TIMESTAMPTZ NULL, completed_utc TIMESTAMPTZ NULL, duration_seconds INT NOT NULL, decks_processed INT NOT NULL DEFAULT 0, additional_decks_found INT NOT NULL DEFAULT 0, decks_enqueued INT NULL, decks_drained INT NULL, error_message TEXT NULL, url TEXT NULL, CONSTRAINT ck_harvest_runs_state CHECK (state IN ('Queued','Running','Stopping','Succeeded','Interrupted','Failed','Cancelled')));";
}
