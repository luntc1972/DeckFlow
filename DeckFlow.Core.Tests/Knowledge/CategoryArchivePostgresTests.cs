using DeckFlow.Core.Storage;
using Npgsql;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Exercises the archive migration against an isolated PostgreSQL schema.
/// </summary>
public sealed class CategoryArchivePostgresTests
{
    [CreatorSuppressionPostgresFact]
    public Task RetentionDrain_CardDeckTotalsUnchanged() => VerifyAsync(false, "totals");

    [CreatorSuppressionPostgresFact]
    public Task NonRetentionRemoval_WritesNoArchive() => VerifyAsync(false, "purge");

    [CreatorSuppressionPostgresFact]
    public Task RetentionDrain_Twice_WritesOneManifest() => VerifyAsync(false, "twice");

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

    [CreatorSuppressionPostgresFact]
    public Task R3H1_ArtifactOnlyDeckDrained_WithRampElsewhere_DeckCountsUnchanged() => VerifyAsync(false, "r3h1");

    [CreatorSuppressionPostgresFact]
    public Task AliasRows_ArchiveMax_NotSum() => VerifyAsync(false, "aliases");

    private static async Task VerifyAsync(bool ensureTwice, string? scenario = null)
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
            if (scenario is null)
            {
                await CategoryArchiveTests.VerifyUpgradeAsync(connection, first, second, string.Empty, ensureTwice);
            }
            else
            {
                await CategoryArchiveTests.VerifyDrainAsync(connection, first, scenario);
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE;", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
