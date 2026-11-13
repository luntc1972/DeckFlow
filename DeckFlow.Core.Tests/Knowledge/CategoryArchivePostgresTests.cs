using DeckFlow.Core.Storage;
using Npgsql;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Exercises the archive migration against an isolated PostgreSQL schema.
/// </summary>
public sealed class CategoryArchivePostgresTests
{
    [CreatorSuppressionPostgresFact]
    public async Task SchemaUpgrade_FromPre0717Populated_AddsArchiveAndArchivedRows()
    {
        await VerifyAsync(false);
    }

    [CreatorSuppressionPostgresFact]
    public async Task SchemaEnsure_RunTwice_IsIdempotent()
    {
        await VerifyAsync(true);
    }

    private static async Task VerifyAsync(bool ensureTwice)
    {
        var schema = $"category_archive_{Guid.NewGuid():N}";
        var connectionString = Environment.GetEnvironmentVariable("DECKFLOW_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema};", admin))
        {
            await create.ExecuteNonQueryAsync();
        }
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema, Pooling = false };
            var first = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);
            builder.ApplicationName = schema;
            var second = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);
            await using var connection = new NpgsqlConnection(first.ConnectionString);
            await connection.OpenAsync();
            await CategoryArchiveTests.VerifyUpgradeAsync(connection, first, second, string.Empty, ensureTwice);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE;", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
