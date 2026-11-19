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
            await connection.ExecuteAsync("DELETE FROM deck_queue;");
            await connection.ExecuteAsync(
                """
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
                VALUES
                    (@legacyBefore, '2026-06-01 00:00:00+00', 1, 0, '2026-06-09T13:44:40.8474546+00:00', 'Legacy Before'),
                    (@legacyAfter, '2026-06-01 00:00:00+00', 1, 0, '2026-06-11T13:44:40.8474546+00:00', 'Legacy After'),
                    (@currentBefore, '2026-06-01 00:00:00+00', 1, 0, '2026-06-09 21:05:01.198232+00', 'Current Before'),
                    (@currentAfter, '2026-06-01 00:00:00+00', 1, 0, '2026-06-11 21:05:01.198232+00', 'Current After'),
                    (@unprocessed, '2026-06-01 00:00:00+00', 0, 0, '2026-06-11 21:05:01.198232+00', 'Unprocessed');
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

    [PostgresFact]
    public async Task GetTotalProcessedDeckCountSinceAsync_QueuedBeforeCutoff_CountsDeckProcessedInWindow()
    {
        await WithProcessingStatsStoreAsync(async (store, connection) =>
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
                VALUES ('old-queue', '2026-06-07 15:06:22.123456+00', 1, 0, '2026-11-19 17:19:21.77526+00', 'Commander');
                """);

            Assert.Equal(1, await store.GetTotalProcessedDeckCountSinceAsync(
                new DateTime(2026, 10, 20, 17, 20, 0, DateTimeKind.Utc)));
        });
    }

    [PostgresFact]
    public async Task GetTotalProcessedDeckCountSinceAsync_MixedQueueStates_UsesProcessingTime()
    {
        await WithProcessingStatsStoreAsync(async (store, connection) =>
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
                VALUES
                    ('recent', @old, 1, 0, @processedRecently, 'Commander'),
                    ('same-day', @old, 1, 0, '2026-10-20 17:20:00.5+00', 'Commander'),
                    ('at-cutoff', @old, 1, 0, '2026-10-20 17:20:00+00', 'Commander'),
                    ('before-cutoff', @old, 1, 0, '2026-10-20 17:19:59.999+00', 'Commander'),
                    ('old', @old, 1, 0, '2026-10-10 12:00:00.123456+00', 'Commander'),
                    ('skipped', @old, 1, 1, @processedRecently, 'Commander'),
                    ('pending', @recent, 0, 0, NULL, 'Commander'),
                    ('requeued', @old, 0, 0, @processedRecently, 'Commander'),
                    ('missing-time', @recent, 1, 0, NULL, 'Commander'),
                    ('legacy', @old, 1, 0, '2026-06-09T13:44:40.8474546+00:00', 'Commander');
                """,
                new
                {
                    old = "2026-06-07 15:06:22.123456+00",
                    recent = "2026-11-19 10:00:00+00",
                    processedRecently = "2026-11-19 17:19:21.77526+00"
                });

            Assert.Equal(3, await store.GetTotalProcessedDeckCountSinceAsync(
                new DateTime(2026, 10, 20, 17, 20, 0, DateTimeKind.Utc)));
        });
    }

    [PostgresFact]
    public async Task GetTotalProcessedDeckCountSinceAsync_MarkedProcessedByStore_CountsDeck()
    {
        await WithProcessingStatsStoreAsync(async (store, connection) =>
        {
            var now = DateTime.UtcNow;
            await connection.ExecuteAsync(
                """
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
                VALUES ('old-queue', CAST(@inserted AS timestamptz)::text, 0, 0, NULL, 'Commander');
                """,
                new { inserted = now.AddDays(-61) });

            await store.MarkUrlDeckProcessedAsync("old-queue", "Commander");

            var stored = await connection.QuerySingleAsync<string>(
                "SELECT last_checked_utc FROM deck_queue WHERE deck_id = 'old-queue';");
            Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(\.\d{1,6})?\+00$", stored);
            Assert.Equal(1, await store.GetTotalProcessedDeckCountSinceAsync(now.AddDays(-30)));
        });
    }

    [PostgresFact]
    public async Task EnsureSchemaAsync_DeckQueue_CreatesProcessedLastCheckedIndex()
    {
        await WithProcessingStatsStoreAsync(async (_, connection) =>
        {
            var definition = await connection.QuerySingleAsync<string>(
                """
                SELECT indexdef FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND tablename = 'deck_queue'
                  AND indexname = 'ix_deck_queue_processed_last_checked';
                """);

            Assert.Contains("USING btree (last_checked_utc)", definition);
            Assert.Contains("processed = 1", definition);
            Assert.Contains("skipped = 0", definition);
        });
    }

    [PostgresFact]
    public async Task ProcessedSinceQuery_Postgres_UsesLastCheckedIndex()
    {
        await WithProcessingStatsStoreAsync(async (_, connection) =>
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc)
                SELECT 'skipped-' || n, '2026-06-07 15:06:22+00', 1, 1, '2026-11-19 17:19:21+00'
                FROM generate_series(1, 20000) AS rows(n);
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc)
                VALUES
                    ('recent', '2026-06-07 15:06:22+00', 1, 0, '2026-11-19 17:19:21+00'),
                    ('at-cutoff', '2026-06-07 15:06:22+00', 1, 0, '2026-10-20 17:20:00+00');
                ANALYZE deck_queue;
                """);

            using var transaction = await connection.BeginTransactionAsync();
            await connection.ExecuteAsync("SET LOCAL enable_seqscan = off;", transaction: transaction);
            var plan = await connection.QueryAsync<string>(
                "EXPLAIN " + CategoryKnowledgeStore.ProcessedSinceSql,
                new { cutoff = "2026-10-20 17:20:00" },
                transaction: transaction);

            Assert.Contains("ix_deck_queue_processed_last_checked", string.Join(Environment.NewLine, plan));
        });
    }

    private async Task WithProcessingStatsStoreAsync(Func<CategoryKnowledgeStore, NpgsqlConnection, Task> verify)
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

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync("DELETE FROM deck_queue;");
            await verify(store, connection);
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
