using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Specifies the Postgres schema and query contract for stored live rows.
/// </summary>
public sealed class StoredLiveRowsMarkerPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public StoredLiveRowsMarkerPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task EmptiedDeck_Postgres_KeepsSourceClearsMarkerAndIsNotMatched()
    {
        var repository = await CreateRepositoryAsync();
        var deckId = "marker-empty-" + Guid.NewGuid().ToString("N");
        await StoreAsync(repository, deckId);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, []);

        Assert.Equal(1L, await ScalarAsync<long>("SELECT COUNT(1) FROM sources WHERE source = @source;", ("source", "archidekt_live:" + deckId)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT COUNT(1) FROM card_deck_totals t JOIN sources s ON s.id = t.source_id WHERE s.source = @source;", ("source", "archidekt_live:" + deckId)));
        await AssertMarkerAsync(deckId, 0L);
        Assert.DoesNotContain((await repository.GetRetentionCandidatesAsync("Atraxa")).Select(candidate => candidate.DeckId), id => id == deckId);
        var queueId = await ScalarAsync<long>("SELECT id FROM deck_queue WHERE deck_id = @deckId;", ("deckId", deckId));
        Assert.DoesNotContain((await repository.GetPurgeCandidatesAsync()).Select(candidate => candidate.DeckQueueId), id => id == queueId);
    }

    [PostgresFact]
    public async Task WritePaths_Postgres_SetAndClearMarker()
    {
        var repository = await CreateRepositoryAsync();
        var prefix = "marker-write-" + Guid.NewGuid().ToString("N");
        var deleteId = prefix + "-delete";
        await StoreAsync(repository, deleteId);
        await AssertMarkerAsync(deleteId, 1L);
        await repository.DeleteSourceDataAsync("archidekt_live:" + deleteId);
        await AssertMarkerAsync(deleteId, 0L);

        var rejectId = prefix + "-reject";
        await StoreAsync(repository, rejectId);
        await repository.RejectUrlDeckAsync("archidekt_url:https://archidekt.com/decks/" + rejectId, rejectId, "Atraxa", null, DeckSkipReasons.NotCommanderFormat);
        await AssertMarkerAsync(rejectId, 0L);

        var removeId = prefix + "-remove";
        await StoreAsync(repository, removeId);
        var queueId = await ScalarAsync<long>("SELECT id FROM deck_queue WHERE deck_id = @deckId;", ("deckId", removeId));
        await repository.RemoveStoredDecksAsync([(queueId, DeckSkipReasons.Retention)]);
        await AssertMarkerAsync(removeId, 0L);
    }

    [PostgresFact]
    public async Task StoredLiveIndex_Postgres_IsValidAndUsedForMarkerPredicate()
    {
        await CreateRepositoryAsync();
        var valid = await ScalarAsync<bool>("SELECT indisvalid FROM pg_index WHERE indexrelid = 'ix_deck_queue_stored_live'::regclass;");
        Assert.True(valid);

        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var setting = new NpgsqlCommand("SET LOCAL enable_seqscan = off;", connection, transaction))
        {
            await setting.ExecuteNonQueryAsync();
        }

        var plan = new List<string>();
        await using (var explain = new NpgsqlCommand("EXPLAIN SELECT COUNT(1) FROM deck_queue q WHERE q.has_stored_live_rows = 1;", connection, transaction))
        await using (var reader = await explain.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                plan.Add(reader.GetString(0));
            }
        }

        Assert.Contains(plan, line => line.Contains("ix_deck_queue_stored_live", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<CategoryKnowledgeRepository> CreateRepositoryAsync()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        await repository.EnsureSchemaAsync();
        return repository;
    }

    private static async Task StoreAsync(CategoryKnowledgeRepository repository, string deckId)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, Entries());
        await repository.MarkDeckProcessedAsync(deckId, "Atraxa", false, null);
    }

    private async Task AssertMarkerAsync(string deckId, long expected)
    {
        Assert.Equal(expected, await ScalarAsync<long>("SELECT has_stored_live_rows FROM deck_queue WHERE deck_id = @deckId;", ("deckId", deckId)));
        Assert.Equal(expected, await ScalarAsync<long>("SELECT CASE WHEN " + OracleExistsSql + " THEN 1 ELSE 0 END FROM deck_queue q WHERE q.deck_id = @deckId;", ("deckId", deckId)));
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }

    private static IReadOnlyList<DeckEntry> Entries() => [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }];

    private const string OracleExistsSql = "EXISTS (SELECT 1 FROM sources s INNER JOIN card_deck_totals t ON t.source_id = s.id WHERE s.deck_queue_id = q.id AND SUBSTR(s.source, 1, 15) = 'archidekt_live:')";
}
