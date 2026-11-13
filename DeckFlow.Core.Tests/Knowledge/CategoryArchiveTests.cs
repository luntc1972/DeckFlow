using System.Data.Common;
using Dapper;
using DeckFlow.Core.Models;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Covers additive archive schema upgrades and the reversible binary manifest.
/// </summary>
public sealed class CategoryArchiveTests
{
    [Fact]
    public Task RetentionDrain_CardDeckTotalsUnchanged() => WithArchiveSqliteAsync("totals");

    [Fact]
    public Task NonRetentionRemoval_WritesNoArchive() => WithArchiveSqliteAsync("purge");

    [Fact]
    public Task RetentionDrain_Twice_WritesOneManifest() => WithArchiveSqliteAsync("twice");

    private static async Task WithArchiveSqliteAsync(string scenario)
    {
        var path = Path.Combine(Path.GetTempPath(), $"archive_drain_{Guid.NewGuid():N}.db");
        var info = new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite,
            new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString);
        try
        {
            await using var connection = new SqliteConnection(info.ConnectionString);
            await connection.OpenAsync();
            await VerifyDrainAsync(connection, info, scenario);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static async Task VerifyDrainAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        var repository = new CategoryKnowledgeRepository(info);
        await repository.EnsureSchemaAsync();
        foreach (var deckId in new[] { "711", "712" })
        {
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId,
            [
                new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" },
                new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "sideboard", Category = "Artifact" }
            ]);
            await repository.MarkDeckProcessedAsync(deckId, "Test Commander", skip: false);
        }
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);
        var id = await connection.QuerySingleAsync<long>("SELECT id FROM deck_queue WHERE deck_id = '711';");
        if (scenario == "purge")
        {
            Assert.Equal(1, await repository.RemoveStoredDecksAsync([(id, DeckSkipReasons.NotCommanderFormat)]));
            foreach (var table in new[] { "card_category_archive", "card_deck_totals_archive", "card_category_deck_archive", "deck_category_archive_manifest" })
            {
                Assert.Equal(0, await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table};"));
            }
            return;
        }
        var before = await repository.GetCardDeckTotalsAsync("Sol Ring");
        var service = new DeckPruningService(repository,
            new DeckPruningOptions { PerCommanderCap = 1, Pause = TimeSpan.Zero }, _ => Task.FromResult<long?>(long.MaxValue));
        await service.RunRetentionAsync();
        var after = await repository.GetCardDeckTotalsAsync("Sol Ring");
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM deck_queue WHERE skip_reason = 'retention';"));
        if (scenario == "totals")
        {
            Assert.Equal(before.TotalDeckCount, after.TotalDeckCount);
            Assert.Equal(before.BoardDeckCounts.OrderBy(row => row.Key), after.BoardDeckCounts.OrderBy(row => row.Key));
            foreach (var board in before.BoardDeckCounts.Keys)
            {
                Assert.Equal(before.BoardDeckCounts[board], (await repository.GetCardDeckTotalsAsync("Sol Ring", board)).TotalDeckCount);
            }
        }
        await service.RunRetentionAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM deck_category_archive_manifest;"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT SUM(deck_count) FROM card_deck_totals_archive;"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT SUM(raw_rows) FROM card_category_archive;"));
        var payload = await connection.QuerySingleAsync<byte[]>("SELECT payload FROM deck_category_archive_manifest;");
        var manifest = DeckCategoryArchiveManifest.Decode(1, payload);
        Assert.Equal(2, manifest.Totals.Count);
        Assert.Equal(2, manifest.Observations.Count);
    }

    [Fact]
    public void Manifest_RoundTrip_And_UnknownVersionThrows()
    {
        var observations = new (long CardId, string Category, string Board, int DeckCount)[]
        {
            (1, "Mana Ramp", "mainboard", 2), (1, "Pioche — 水", "sideboard", 3)
        };
        var totals = new (long CardId, string Board, int DeckCount)[] { (1, "mainboard", 2), (2, "sideboard", 3) };
        var contributions = new (long CardId, string CanonicalCategory, string Board, int IncludedDeckCount, int FallbackDeckCount, int IncludedLabelRows)[]
        {
            (1, "ramp", "mainboard", 2, 3, 4)
        };
        var manifest = new DeckCategoryArchiveManifest(observations, totals, contributions);
        var payload = manifest.Encode();
        var decoded = DeckCategoryArchiveManifest.Decode(DeckCategoryArchiveManifest.FormatVersion, payload);
        Assert.Equal(observations, decoded.Observations);
        Assert.Equal(totals, decoded.Totals);
        Assert.Equal(contributions, decoded.Contributions);
        Assert.Throws<InvalidDataException>(() => DeckCategoryArchiveManifest.Decode(2, payload));
        var empty = new DeckCategoryArchiveManifest([], [], []);
        var emptyDecoded = DeckCategoryArchiveManifest.Decode(1, empty.Encode());
        Assert.Empty(emptyDecoded.Observations);
        Assert.Empty(emptyDecoded.Totals);
        Assert.Empty(emptyDecoded.Contributions);
    }

    [Fact]
    public async Task SchemaUpgrade_FromPre0717Populated_AddsArchiveAndArchivedRows()
    {
        await WithSqliteAsync(false);
    }

    [Fact]
    public async Task SchemaEnsure_RunTwice_IsIdempotent()
    {
        await WithSqliteAsync(true);
    }

    private static async Task WithSqliteAsync(bool ensureTwice)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"category_archive_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "cache.db"),
            Pooling = false
        }.ConnectionString;
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await VerifyUpgradeAsync(connection,
                new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite, connectionString),
                new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite, connectionString + ";Default Timeout=31"),
                directory, ensureTwice);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static async Task VerifyUpgradeAsync(DbConnection connection,
        RelationalDatabaseConnection database, RelationalDatabaseConnection secondDatabase,
        string directory, bool ensureTwice)
    {
        // Why: these are the populated pre-07-17 aggregate tables, before archived_rows existed.
        await ExecuteAsync(connection, """
            CREATE TABLE card_category_summary (
                card_id INTEGER NOT NULL, category TEXT NOT NULL, observation_rows INTEGER NOT NULL,
                PRIMARY KEY (card_id, category));
            CREATE TABLE card_category_qualified (
                card_id INTEGER NOT NULL, category TEXT NOT NULL, observation_rows INTEGER NOT NULL,
                PRIMARY KEY (card_id, category));
            INSERT INTO card_category_summary VALUES (1, 'Ramp', 5), (2, 'Draw', 4);
            INSERT INTO card_category_qualified VALUES (1, 'Ramp', 5);
            """);
        var schema = new CategoryCacheSchema(database, directory, null);
        await schema.EnsureSchemaAsync();
        foreach (var table in new[] { "card_category_summary", "card_category_qualified" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT card_id, category, observation_rows, archived_rows FROM {table} ORDER BY card_id;";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, Convert.ToInt64(reader.GetValue(0)));
            Assert.Equal("Ramp", reader.GetString(1));
            Assert.Equal(5L, Convert.ToInt64(reader.GetValue(2)));
            Assert.Equal(0L, Convert.ToInt64(reader.GetValue(3)));
            if (table == "card_category_summary")
            {
                Assert.True(await reader.ReadAsync());
                Assert.Equal(2L, Convert.ToInt64(reader.GetValue(0)));
                Assert.Equal("Draw", reader.GetString(1));
                Assert.Equal(4L, Convert.ToInt64(reader.GetValue(2)));
                Assert.Equal(0L, Convert.ToInt64(reader.GetValue(3)));
            }
            Assert.False(await reader.ReadAsync());
        }

        var rows = new Dictionary<string, string>
        {
            ["card_category_archive"] = "1, 'Mana Ramp', 'mainboard', 2",
            ["card_category_deck_archive"] = "1, 'ramp', 'mainboard', 1, 2, 3",
            ["card_deck_totals_archive"] = "1, 'mainboard', 2",
            ["deck_category_archive_manifest"] = "42, '2026-11-13T00:00:00Z', 1, @payload"
        };
        foreach (var (table, values) in rows)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            command.CommandText = $"INSERT INTO {table} VALUES ({values});";
            if (table == "deck_category_archive_manifest")
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@payload";
                parameter.Value = new byte[] { 1, 2, 3 };
                command.Parameters.Add(parameter);
            }
            await command.ExecuteNonQueryAsync();
            await Assert.ThrowsAnyAsync<DbException>(() => command.ExecuteNonQueryAsync());
        }

        if (ensureTwice)
        {
            // Why: a distinct connection-string cache key forces DDL to run again, rather than taking the fast path.
            var tables = rows.Keys.Concat(new[] { "card_category_summary", "card_category_qualified" }).ToArray();
            var snapshots = new Dictionary<string, string>();
            foreach (var table in tables)
            {
                snapshots[table] = await ReadSnapshotAsync(connection, table);
            }
            await new CategoryCacheSchema(secondDatabase, directory, null).EnsureSchemaAsync();
            foreach (var table in tables)
            {
                Assert.Equal(snapshots[table], await ReadSnapshotAsync(connection, table));
            }
            foreach (var table in rows.Keys)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM {table};";
                Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            }
        }

        await ExecuteAsync(connection, """
            DELETE FROM card_category_qualified;
            UPDATE card_category_summary SET archived_rows = 1 WHERE card_id = 2;
            """);
        await schema.EnsureCardCategoryQualifiedBackfilledAsync();
        await using var backfill = connection.CreateCommand();
        backfill.CommandText = "SELECT observation_rows, archived_rows FROM card_category_qualified WHERE card_id = 2;";
        await using var backfilled = await backfill.ExecuteReaderAsync();
        Assert.True(await backfilled.ReadAsync());
        Assert.Equal(4L, Convert.ToInt64(backfilled.GetValue(0)));
        Assert.Equal(1L, Convert.ToInt64(backfilled.GetValue(1)));
    }

    private static async Task<string> ReadSnapshotAsync(DbConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {table} ORDER BY 1, 2;";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(System.Text.Json.JsonSerializer.Serialize(values));
        }
        return string.Join("\n", rows);
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
