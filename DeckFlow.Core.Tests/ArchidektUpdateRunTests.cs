using System.Net;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;
using RestSharp;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Traces refresh-only Archidekt updates through the real listing importer and SQLite queue.
/// </summary>
[Collection(ArchidektThrottleCollection.Name)]
public sealed class ArchidektUpdateRunTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public ArchidektUpdateRunTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
        ArchidektThrottle.ResetForTests();
        ArchidektThrottle.ConfigureForTests(static () => DateTimeOffset.UtcNow, static (_, _) => Task.CompletedTask);
    }

    [Fact]
    public async Task UpdateRun_ListingShowsKnownDeckModified_DrainsOnlyRefreshAndLeavesNewIdsForBulk()
    {
        // Why: D-04, D-05, and HARV-11 require this tracer to make no live request.
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddListingRowsAsync([new ArchidektListingDeck("101", DateTimeOffset.Parse("2026-01-01T00:00:00Z"))]);
        await repository.MarkDeckProcessedAsync("101", commanderName: null, metadata: new ArchidektDeckMetadata(null, null, null, null, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.UtcNow));
        await repository.AddDeckIdsAsync(["pending-1"]);
        await repository.SetRecentDeckCrawlPageAsync(7);

        var handler = new UpdateListingFixtureHandler();
        var client = new RestClient(new RestClientOptions { BaseUrl = new Uri("https://archidekt.com"), UserAgent = null, ConfigureMessageHandler = _ => handler });
        var deckImporter = new ScriptedDeckImporter();
        var progressValues = new List<int>();
        var session = new ArchidektDeckCacheSession(repository, deckImporter, new ArchidektRecentDecksImporter(client), idlePollDelay: TimeSpan.FromMilliseconds(1));

        var result = await session.RunUpdateAsync(TimeSpan.FromSeconds(30), progress: new SynchronousProgress<int>(progressValues.Add));

        Assert.Equal(["101"], deckImporter.ImportedDeckIds);
        Assert.Equal(10, result.PagesPolled);
        Assert.Equal(1, result.RefreshesRequeued);
        Assert.Equal(1, result.RefreshesDrained);
        Assert.Equal(2, result.NewIdsSeen);
        Assert.Equal(0, result.DecksSkipped);
        Assert.True(result.Duration < TimeSpan.FromSeconds(30));
        Assert.Equal(Enumerable.Range(1, 10), handler.RequestPages);
        Assert.All(handler.RequestPaths, path => Assert.StartsWith("/api/decks/v3/?orderBy=-updatedAt&page=", path, StringComparison.Ordinal));
        Assert.Equal(7, await repository.GetRecentDeckCrawlPageAsync());
        Assert.True(await IsProcessedAsync("101"));
        Assert.Null(await GetRefreshRequestedUtcAsync("101"));
        Assert.Empty(await repository.GetNextRefreshDeckIdsAsync(10));
        Assert.Equal(["pending-1", "303", "505"], await repository.GetNextUnprocessedDeckIdsAsync(10));
        Assert.Equal(1, progressValues.Last());
    }

    public void Dispose()
    {
        ArchidektThrottle.ResetForTests();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, recursive: true);
    }

    private async Task<bool> IsProcessedAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT processed FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private async Task<string?> GetRefreshRequestedUtcAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT refresh_requested_utc FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        return await command.ExecuteScalarAsync() as string;
    }

    private sealed class UpdateListingFixtureHandler : HttpMessageHandler
    {
        public List<string> RequestPaths { get; } = [];
        public List<int> RequestPages { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            RequestPaths.Add(path);
            var page = int.Parse(request.RequestUri.Query.Split("page=")[1], System.Globalization.CultureInfo.InvariantCulture);
            RequestPages.Add(page);
            var body = page == 1
                ? "{\"results\":[{\"id\":101,\"updatedAt\":\"2026-01-02T00:00:00.123456Z\"},{\"id\":303,\"updatedAt\":\"2026-01-02T00:00:00.123456Z\"},{\"id\":505,\"updatedAt\":\"2026-01-02T00:00:00.123456Z\"}]}"
                : "{\"results\":[]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler) => _handler = handler;

        public void Report(T value) => _handler(value);
    }

    private sealed class ScriptedDeckImporter : IArchidektDeckImporter
    {
        public List<string> ImportedDeckIds { get; } = [];

        public Task<List<DeckEntry>> ImportAsync(string deckId, CancellationToken cancellationToken = default)
        {
            ImportedDeckIds.Add(deckId);
            return Task.FromResult(new List<DeckEntry> { new() { Name = $"Card {deckId}", NormalizedName = CardNormalizer.Normalize($"Card {deckId}"), Quantity = 1, Board = "mainboard", Category = "Ramp" } });
        }

        public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string deckId, CancellationToken cancellationToken = default)
            => new(await ImportAsync(deckId, cancellationToken), new ArchidektDeckMetadata(null, null, null, null, DateTimeOffset.Parse("2026-01-02T00:00:00Z"), DateTimeOffset.UtcNow));
    }
}
