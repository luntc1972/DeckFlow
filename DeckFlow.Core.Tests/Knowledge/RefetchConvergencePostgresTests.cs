using Npgsql;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Runs refetch convergence facts against a temporary PostgreSQL schema.
/// </summary>
public sealed class RefetchConvergencePostgresTests
{
    [CreatorSuppressionPostgresFact] public Task Refetch_ThenNotFound_ArchivesAndLeavesNoCandidate() => VerifyAsync("not-found");
    [CreatorSuppressionPostgresFact] public Task Refetch_ThenSuccess_CapturesMetadataAndLeavesNoCandidate() => VerifyAsync("success");
    [CreatorSuppressionPostgresFact] public Task Refetch_ThenTransientThreeTimes_StaysRetryableAndNotArchived() => VerifyAsync("transient");
    [CreatorSuppressionPostgresFact] public Task Refetch_ThenOtherFailure_FetchFailureNotArchived() => VerifyAsync("other-failure");
    [CreatorSuppressionPostgresFact] public Task NotFound_UnstoredDeck_MarkedWithoutManifest() => VerifyAsync("unstored");
    [CreatorSuppressionPostgresFact] public Task RetentionArchived_RequeuedThenFails_KeepsRetentionReason() => VerifyAsync("retention");
    [CreatorSuppressionPostgresFact] public Task NotFoundArchived_Reappears_UnarchivesOnReentry() => VerifyAsync("reentry");
    [CreatorSuppressionPostgresFact] public Task NotFound_RetireFails_FallsBackToFetchFailureAndContinues() => VerifyAsync("fallback");

    private static async Task VerifyAsync(string scenario)
    {
        var schema = $"refetch_convergence_{Guid.NewGuid():N}";
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
            var info = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);
            await using var connection = new NpgsqlConnection(info.ConnectionString);
            await connection.OpenAsync();
            await RefetchConvergenceTests.VerifyAsync(connection, info, scenario);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE;", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
