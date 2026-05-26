using System.Data.Common;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests SQLite foreign-key enforcement at the relational connection helper layer.
/// </summary>
public sealed class RelationalDatabaseConnectionForeignKeyTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"fk-test-{Guid.NewGuid():N}.db");

    /// <summary>
    /// Verifies SQLite connections opened through the shared helper enable foreign keys.
    /// </summary>
    [Fact]
    public async Task OpenConnectionAsync_Sqlite_EnablesForeignKeysPragma()
    {
        var connectionInfo = RelationalDatabaseConnection.FromSqlitePath(_databasePath);
        await using var connection = await connectionInfo.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1L, Convert.ToInt64(result));
    }

    /// <summary>
    /// Verifies SQLite cascade deletes fire on connections opened through the shared helper.
    /// </summary>
    [Fact]
    public async Task OpenConnectionAsync_Sqlite_AllowsCascadeDeleteToFire()
    {
        var connectionInfo = RelationalDatabaseConnection.FromSqlitePath(_databasePath);
        await using var connection = await connectionInfo.OpenConnectionAsync();

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE parent (
                id INTEGER PRIMARY KEY
            );
            """);
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE child (
                id INTEGER PRIMARY KEY,
                parent_id INTEGER NOT NULL REFERENCES parent(id) ON DELETE CASCADE
            );
            """);
        await ExecuteNonQueryAsync(connection, "INSERT INTO parent (id) VALUES (1);");
        await ExecuteNonQueryAsync(connection, "INSERT INTO child (id, parent_id) VALUES (1, 1);");

        await ExecuteNonQueryAsync(connection, "DELETE FROM parent WHERE id = 1;");

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM child;";
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(0L, Convert.ToInt64(result));
    }

    /// <summary>
    /// Removes the temporary SQLite file created for each test.
    /// </summary>
    public void Dispose()
    {
        if (!File.Exists(_databasePath))
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        File.Delete(_databasePath);
    }

    private static async Task ExecuteNonQueryAsync(DbConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
