using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Integration tests for the Postgres storage path covering feedback and category knowledge persistence
/// against a real PostgreSQL container started via <see cref="PostgresContainerFixture"/>.
/// </summary>
public sealed class PostgresStorageTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public PostgresStorageTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static RelationalDatabaseConnection CreateConnection(string connectionString)
        => new(RelationalDatabaseProvider.Postgres, connectionString);

    private async Task<FeedbackStore> CreateFeedbackStoreAsync()
        => new(CreateConnection(await _fixture.GetConnectionStringOrSkipAsync()));

    private async Task<CategoryKnowledgeRepository> CreateRepositoryAsync()
        => new(CreateConnection(await _fixture.GetConnectionStringOrSkipAsync()));

    [PostgresFact]
    public async Task MarkDeckProcessedAsync_Metadata_RoundTripsAllValues()
    {
        var repository = await CreateRepositoryAsync();
        var deckId = $"metadata-{Guid.NewGuid():N}";
        var capturedUtc = DateTimeOffset.UtcNow;
        var metadata = new ArchidektDeckMetadata(3, 1, true, capturedUtc, capturedUtc, capturedUtc);

        await repository.AddDeckIdsAsync(new[] { deckId });
        await repository.MarkDeckProcessedAsync(deckId, "Commander", false, metadata, CancellationToken.None);

        var row = await ReadArchidektMetadataRowAsync(deckId);

        Assert.Equal(metadata.EdhBracket, row.EdhBracket);
        Assert.Equal(metadata.DeckFormat, row.DeckFormat);
        Assert.Equal(1, row.Theorycrafted);
        Assert.Equal(metadata.CreatedUtc?.ToUniversalTime().ToString("O"), row.CreatedUtc);
        Assert.Equal(metadata.UpdatedUtc?.ToUniversalTime().ToString("O"), row.UpdatedUtc);
        Assert.Equal(metadata.CapturedUtc.ToUniversalTime().ToString("O"), row.CapturedUtc);
    }

    [PostgresFact]
    public async Task MarkUrlDeckProcessedAsync_Metadata_RoundTripsAllValues()
    {
        var repository = await CreateRepositoryAsync();
        var deckId = $"url-metadata-{Guid.NewGuid():N}";
        var capturedUtc = DateTimeOffset.UtcNow;
        var metadata = new ArchidektDeckMetadata(3, 1, true, capturedUtc, capturedUtc, capturedUtc);

        await repository.MarkUrlDeckProcessedAsync(deckId, "Commander", metadata, CancellationToken.None);

        var row = await ReadArchidektMetadataRowAsync(deckId);

        Assert.Equal("Commander", row.CommanderName);
        Assert.Equal(metadata.EdhBracket, row.EdhBracket);
        Assert.Equal(metadata.DeckFormat, row.DeckFormat);
        Assert.Equal(1, row.Theorycrafted);
        Assert.Equal(metadata.CreatedUtc?.ToUniversalTime().ToString("O"), row.CreatedUtc);
        Assert.Equal(metadata.UpdatedUtc?.ToUniversalTime().ToString("O"), row.UpdatedUtc);
        Assert.Equal(metadata.CapturedUtc.ToUniversalTime().ToString("O"), row.CapturedUtc);
    }

    [PostgresFact]
    public async Task FeedbackStore_Insert_Get_List_Update_Delete_Roundtrips()
    {
        var store = await CreateFeedbackStoreAsync();
        var unique = Guid.NewGuid().ToString("N");
        var submission = new FeedbackSubmission
        {
            Type = FeedbackType.Bug,
            Message = $"postgres integration test message {unique}",
            Email = "user@example.com",
        };
        var context = new FeedbackRequestContext(
            "198.51.100.25",
            "integration-agent/1.0",
            "https://example.com/feedback",
            "2.0.0");

        var id = await store.AddAsync(submission, context);
        Assert.True(id > 0);

        var fetched = await store.GetAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal(submission.Type, fetched!.Type);
        Assert.Equal(submission.Message, fetched.Message);
        Assert.Equal(submission.Email, fetched.Email);
        Assert.Equal(context.PageUrl, fetched.PageUrl);
        Assert.Equal(context.UserAgent, fetched.UserAgent);
        Assert.Equal(context.AppVersion, fetched.AppVersion);
        Assert.Equal(FeedbackStatus.New, fetched.Status);
        Assert.Equal(DateTimeKind.Utc, fetched.CreatedUtc.Kind);

        var listed = await store.ListAsync(new FeedbackListQuery
        {
            Status = null,
            Page = 1,
            PageSize = 50,
        });
        Assert.Single(listed);
        Assert.Equal(id, listed[0].Id);

        await store.UpdateStatusAsync(id, FeedbackStatus.Read);
        Assert.Equal(FeedbackStatus.Read, (await store.GetAsync(id))!.Status);

        await store.DeleteAsync(id);
        Assert.Null(await store.GetAsync(id));
    }

    [PostgresFact]
    public async Task CategoryKnowledgeRepository_CrudAndDeckQueue_Roundtrips()
    {
        var repo = await CreateRepositoryAsync();
        var source = $"pg-test-{Guid.NewGuid():N}";

        Assert.False(await repo.HasSourceDataAsync(source));

        var rows = new[]
        {
            new CategoryKnowledgeRow("Ramp", "Sol Ring", 5, 3),
            new CategoryKnowledgeRow("Draw", "Sol Ring", 2, 4),
            new CategoryKnowledgeRow("Ramp", "Birds of Paradise", 1, 1),
        };

        await repo.ReplaceSourceRowsAsync(source, rows, board: "mainboard", deckCount: 7);
        await repo.PersistCardDeckTotalsAsync(source, "Sol Ring", board: "mainboard", deckCountIncrement: 7);

        Assert.True(await repo.HasSourceDataAsync(source));

        var categories = await repo.GetCategoriesAsync("Sol Ring");
        Assert.Equal(new[] { "Draw", "Ramp" }, categories);

        var rowResults = await repo.GetCategoryRowsForCardAsync("Sol Ring", boardFilter: "mainboard");
        Assert.Equal(2, rowResults.Count);
        Assert.Contains(rowResults, row => row.Category == "Ramp" && row.CardName == "Sol Ring" && row.Count == 5 && row.DeckCount == 3);
        Assert.Contains(rowResults, row => row.Category == "Draw" && row.CardName == "Sol Ring" && row.Count == 2 && row.DeckCount == 4);

        var totals = await repo.GetCardDeckTotalsAsync("Sol Ring", boardFilter: "mainboard");
        Assert.Equal(7, totals.TotalDeckCount);
        Assert.Single(totals.BoardDeckCounts);
        Assert.Equal(7, totals.BoardDeckCounts["mainboard"]);

        await repo.DeleteSourceDataAsync(source);
        Assert.False(await repo.HasSourceDataAsync(source));
        Assert.Empty(await repo.GetCategoryRowsForCardAsync("Sol Ring"));
        var emptyTotals = await repo.GetCardDeckTotalsAsync("Sol Ring");
        Assert.Equal(0, emptyTotals.TotalDeckCount);
        Assert.Empty(emptyTotals.BoardDeckCounts);
    }

    [PostgresFact]
    public async Task CategoryKnowledgeRepository_CommanderRows_UseLiveSourceIntegerLink()
    {
        var repo = await CreateRepositoryAsync();
        var unique = Guid.NewGuid().ToString("N");
        var deckId = $"pg-live-{unique}";
        var commander = $"Postgres Commander {unique}";
        var cardName = $"Live Test Card {unique}";

        await repo.AddDeckIdsAsync(new[] { deckId });
        await repo.PersistObservedCategoriesAsync(
            $"archidekt_live:{deckId}",
            cardName,
            new[] { "Ramp" },
            quantity: 2,
            board: "mainboard",
            deckCountIncrement: 1);
        await repo.MarkDeckProcessedAsync(deckId, commander);

        var rows = await repo.GetCategoryRowsForCommanderAsync(commander);

        var row = Assert.Single(rows);
        Assert.Equal("Ramp", row.Category);
        Assert.Equal(cardName, row.CardName);
        Assert.Equal(2, row.Count);
        Assert.Equal(1, row.DeckCount);
        Assert.Equal(1, await CountLinkedSourceRowsAsync($"archidekt_live:{deckId}"));
    }

    [PostgresFact]
    public async Task CategoryKnowledgeRepository_NonLiveSources_DoNotEnterCommanderAggregateOrQueue()
    {
        var repo = await CreateRepositoryAsync();
        var unique = Guid.NewGuid().ToString("N");
        var deckId = $"pg-non-live-{unique}";
        var commander = $"Postgres Isolation {unique}";
        var urlSource = $"archidekt_url:https://archidekt.com/decks/{unique}/test";
        var edhrecCardName = $"Edhrec Test Card {unique}";
        var urlCardName = $"Url Test Card {unique}";

        await repo.AddDeckIdsAsync(new[] { deckId });
        await repo.MarkDeckProcessedAsync(deckId, commander);
        await repo.PersistObservedCategoriesAsync("edhrec", edhrecCardName, new[] { "Ramp" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repo.PersistObservedCategoriesAsync(urlSource, urlCardName, new[] { "Ramp" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);

        var rows = await repo.GetCategoryRowsForCommanderAsync(commander);

        Assert.Empty(rows);
        Assert.Equal(0, await CountLinkedSourceRowsAsync("edhrec"));
        Assert.Equal(0, await CountLinkedSourceRowsAsync(urlSource));
        Assert.Equal(0, await CountDeckQueueRowsAsync(urlSource));
    }

    [PostgresFact]
    public async Task CategoryKnowledgeRepository_DeckQueue_AddClaimAndMarkProcessed_Roundtrips()
    {
        var repo = await CreateRepositoryAsync();
        var deckIds = new[]
        {
            $"deck-{Guid.NewGuid():N}",
            $"deck-{Guid.NewGuid():N}",
        };

        await repo.AddDeckIdsAsync(deckIds);

        Assert.Equal(2, await repo.GetUnprocessedCountAsync());
        Assert.Equal(deckIds, await repo.GetNextUnprocessedDeckIdsAsync(10));

        await repo.MarkDecksProcessedAsync(deckIds, skip: false);

        Assert.Equal(2, await repo.GetProcessedDeckCountAsync());
        Assert.Empty(await repo.GetNextUnprocessedDeckIdsAsync(10));

        await repo.SetRecentDeckCrawlPageAsync(7);
        Assert.Equal(7, await repo.GetRecentDeckCrawlPageAsync());
    }

    private async Task<long> CountLinkedSourceRowsAsync(string source)
    {
        var connectionInfo = CreateConnection(await _fixture.GetConnectionStringOrSkipAsync());
        await using var connection = connectionInfo.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sources
            WHERE source = @source
              AND deck_queue_id IS NOT NULL;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@source", source);

        return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L);
    }

    private async Task<long> CountDeckQueueRowsAsync(string deckId)
    {
        var connectionInfo = CreateConnection(await _fixture.GetConnectionStringOrSkipAsync());
        await using var connection = connectionInfo.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM deck_queue
            WHERE deck_id = @deckId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@deckId", deckId);

        return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L);
    }

    private async Task<ArchidektMetadataRow> ReadArchidektMetadataRowAsync(string deckId)
    {
        var connectionInfo = CreateConnection(await _fixture.GetConnectionStringOrSkipAsync());
        await using var connection = connectionInfo.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT commander_name, archidekt_edh_bracket, archidekt_deck_format, archidekt_theorycrafted,
                   archidekt_created_utc, archidekt_updated_utc, archidekt_metadata_captured_utc
            FROM deck_queue
            WHERE deck_id = @deckId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@deckId", deckId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    private sealed record ArchidektMetadataRow(
        string? CommanderName,
        int? EdhBracket,
        int? DeckFormat,
        int? Theorycrafted,
        string? CreatedUtc,
        string? UpdatedUtc,
        string? CapturedUtc);
}
