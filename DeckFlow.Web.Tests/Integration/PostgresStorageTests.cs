using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

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
}
