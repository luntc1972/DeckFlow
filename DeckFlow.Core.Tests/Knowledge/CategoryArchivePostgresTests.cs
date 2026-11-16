using DeckFlow.Core.Storage;
using Npgsql;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Exercises the archive migration against an isolated PostgreSQL schema.
/// </summary>
public sealed class CategoryArchivePostgresTests
{
    [CreatorSuppressionPostgresFact]
    public Task LegalityPurge_OfArchivedDeck_RestoresArchive() => VerifyAsync(false, "archived-purge");

    [CreatorSuppressionPostgresFact]
    public Task CountPurgeCandidates_IncludesArchivedIllegalDecks() => VerifyAsync(false, "archived-count");

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

    [CreatorSuppressionPostgresFact]
    public Task RetentionDrain_AllFourCardReadersUnchanged_CommanderReadersDrop() => VerifyAsync(false, "readers");

    [CreatorSuppressionPostgresFact]
    public Task Reentry_ListingRefresh_UnarchivesAndWritesLive_NoDeckHasBoth() => VerifyAsync(false, "reentry");

    [CreatorSuppressionPostgresFact]
    public Task Reentry_CanonicalizationChanged_RestoresExactly() => VerifyAsync(false, "canonical");

    [CreatorSuppressionPostgresFact]
    public Task DrainThenRemoveArchived_ArchiveEqualsPreDrain() => VerifyAsync(false, "remove");

    [CreatorSuppressionPostgresFact]
    public Task FaultMidUnarchive_RollsBackAll() => VerifyAsync(false, "fault");

    [CreatorSuppressionPostgresFact]
    public Task UrlImport_ArchivedDeck_RefusedAndNothingWritten() => VerifyAsync(false, "url-archived");

    [CreatorSuppressionPostgresFact]
    public Task Pg_FoldHoldsLock_UrlImportWaitsThenRefuses() => VerifyAsync(false, "url-fold-first");

    [CreatorSuppressionPostgresFact]
    public Task Pg_UrlImportInWindow_FoldWaitsUntilUrlCommits() => VerifyAsync(false, "url-import-first");

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
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema, Pooling = false, ApplicationName = schema };
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
