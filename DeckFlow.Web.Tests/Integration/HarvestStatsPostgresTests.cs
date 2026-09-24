using Dapper;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

public sealed class HarvestStatsPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public HarvestStatsPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task GetTotalProcessedDeckCountSinceAsync_MixedTimestampFormats_ReturnsOnlyProcessedRowsAtOrAfterCutoff()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var originalProvider = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER");
        var originalConnectionString = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING");
        try
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", "Postgres");
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", connectionString);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var store = new CategoryKnowledgeStore(new TestWebHostEnvironment(), cache);
            await store.GetTotalProcessedDeckCountAsync();
            var prefix = $"pg-harvest-stats-{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.ExecuteAsync(
                """
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
                VALUES
                    (@legacyBefore, '2026-06-09T13:44:40.8474546+00:00', 1, 0, NULL, 'Legacy Before'),
                    (@legacyAfter, '2026-06-11T13:44:40.8474546+00:00', 1, 0, NULL, 'Legacy After'),
                    (@currentBefore, '2026-06-09 21:05:01.198232+00', 1, 0, NULL, 'Current Before'),
                    (@currentAfter, '2026-06-11 21:05:01.198232+00', 1, 0, NULL, 'Current After'),
                    (@unprocessed, '2026-06-11 21:05:01.198232+00', 0, 0, NULL, 'Unprocessed');
                """,
                new
                {
                    legacyBefore = $"{prefix}-legacy-before",
                    legacyAfter = $"{prefix}-legacy-after",
                    currentBefore = $"{prefix}-current-before",
                    currentAfter = $"{prefix}-current-after",
                    unprocessed = $"{prefix}-unprocessed"
                });

            var count = await store.GetTotalProcessedDeckCountSinceAsync(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

            Assert.Equal(2, count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", originalProvider);
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", originalConnectionString);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeckFlow.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
