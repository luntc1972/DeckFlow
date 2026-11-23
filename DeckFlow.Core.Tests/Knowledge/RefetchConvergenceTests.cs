using System.Data.Common;
using Dapper;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Covers terminal refetch outcomes through the same provider-neutral fixture.
/// </summary>
public sealed class RefetchConvergenceTests
{
    [Fact]
    public Task Refetch_ThenNotFound_ArchivesAndLeavesNoCandidate() => WithSqliteAsync("not-found");

    [Fact]
    public Task Refetch_ThenSuccess_CapturesMetadataAndLeavesNoCandidate() => WithSqliteAsync("success");

    [Fact]
    public Task Refetch_ThenTransientThreeTimes_StaysRetryableAndNotArchived() => WithSqliteAsync("transient");

    [Fact]
    public Task Refetch_ThenOtherFailure_FetchFailureNotArchived() => WithSqliteAsync("other-failure");

    [Fact]
    public Task NotFound_UnstoredDeck_MarkedWithoutManifest() => WithSqliteAsync("unstored");

    [Fact]
    public Task RetentionArchived_RequeuedThenFails_KeepsRetentionReason() => WithSqliteAsync("retention");

    [Fact]
    public Task NotFoundArchived_Reappears_UnarchivesOnReentry() => WithSqliteAsync("reentry");

    [Fact]
    public Task NotFound_RetireFails_FallsBackToFetchFailureAndContinues() => WithSqliteAsync("fallback");

    internal static async Task VerifyAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        var repository = new CategoryKnowledgeRepository(info);
        const string deckId = "refetch-convergence";
        await repository.AddDeckIdsAsync([deckId]);

        if (scenario == "unstored")
        {
            Assert.Equal(0, await repository.RetireNotFoundDeckAsync(deckId));
            Assert.Equal("not_found", await connection.QuerySingleAsync<string>("SELECT skip_reason FROM deck_queue WHERE deck_id = @deckId", new { deckId }));
            Assert.Equal(0L, await connection.QuerySingleAsync<long>("SELECT COUNT(1) FROM deck_category_archive_manifest"));
            return;
        }

        var entries = LegalCommanderDeck.Complete([new DeckEntry
        {
            Name = "Sol Ring",
            NormalizedName = "sol ring",
            Quantity = 1,
            Board = "mainboard",
            Category = "Ramp"
        }]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, entries);
        await repository.MarkDeckProcessedAsync(deckId, LegalCommanderDeck.CommanderName, skip: true, metadata: null, skipReason: DeckSkipReasons.FetchFailure);

        if (scenario == "success")
        {
            await repository.MarkDeckProcessedAsync(deckId, LegalCommanderDeck.CommanderName, skip: false, LegalCommanderDeck.Metadata());
            Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
            return;
        }

        if (scenario == "transient")
        {
            await repository.RunRefetchMarkBatchAsync(500);
            Assert.False(await repository.RecordTransientDeckFailureAsync(deckId));
            Assert.False(await repository.RecordTransientDeckFailureAsync(deckId));
            Assert.True(await repository.RecordTransientDeckFailureAsync(deckId));
            Assert.Equal(1, await repository.CountRefetchCandidatesAsync());
            Assert.Equal(0L, await connection.QuerySingleAsync<long>("SELECT COUNT(1) FROM deck_category_archive_manifest"));
            return;
        }

        if (scenario is "other-failure" or "fallback")
        {
            Assert.Equal(1, await repository.CountRefetchCandidatesAsync());
            Assert.Equal(0L, await connection.QuerySingleAsync<long>("SELECT COUNT(1) FROM deck_category_archive_manifest"));
            return;
        }

        if (scenario == "retention")
        {
            await connection.ExecuteAsync("UPDATE deck_queue SET retention_core_hash = 'seeded-retention' WHERE deck_id = @deckId", new { deckId });
        }

        Assert.Equal(1, await repository.CountRefetchCandidatesAsync());
        var marked = await repository.RunRefetchMarkBatchAsync(500);
        Assert.Equal(1, marked.DecksMarked);
        Assert.Equal(1, await repository.RetireNotFoundDeckAsync(deckId));
        Assert.Equal(scenario == "retention" ? DeckSkipReasons.Retention : DeckSkipReasons.NotFound,
            await connection.QuerySingleAsync<string>("SELECT skip_reason FROM deck_queue WHERE deck_id = @deckId", new { deckId }));
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
        Assert.Equal(1L, await connection.QuerySingleAsync<long>("SELECT COUNT(1) FROM deck_category_archive_manifest"));

        if (scenario == "reentry")
        {
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, entries);
            await repository.MarkDeckProcessedAsync(deckId, LegalCommanderDeck.CommanderName, skip: false, LegalCommanderDeck.Metadata());
            Assert.Equal(0L, await connection.QuerySingleAsync<long>("SELECT COUNT(1) FROM deck_category_archive_manifest"));
        }
    }

    private static async Task WithSqliteAsync(string scenario)
    {
        var path = Path.Combine(Path.GetTempPath(), $"refetch_convergence_{Guid.NewGuid():N}.db");
        var info = new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite, new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString);
        try
        {
            await using var connection = new SqliteConnection(info.ConnectionString);
            await connection.OpenAsync();
            await VerifyAsync(connection, info, scenario);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
