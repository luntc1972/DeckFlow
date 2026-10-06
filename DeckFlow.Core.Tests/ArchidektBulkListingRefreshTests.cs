using System.Net;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;
using RestSharp;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Traces bulk listing refreshes through the real importer, session, and SQLite queue.
/// </summary>
[Collection(ArchidektThrottleCollection.Name)]
public sealed class ArchidektBulkListingRefreshTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public ArchidektBulkListingRefreshTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
        ArchidektThrottle.ResetForTests();
        ArchidektThrottle.ConfigureForTests(static () => DateTimeOffset.UtcNow, static (_, _) => Task.CompletedTask);
    }

    [Fact]
    public async Task BulkRun_ListingShowsKnownDeckModified_RefetchesItOnceAndCountsOnlyNewIds()
    {
        // Why: D-01 and HARV-10 require listing timestamps to refresh known decks without live requests.
        var handler = new ListingFixtureHandler { PageOneJson = "{\"results\":[{\"id\":101,\"updatedAt\":\"2026-01-01T00:00:00.123456Z\"}]}" };
        var client = new RestClient(new RestClientOptions { BaseUrl = new Uri("https://archidekt.com"), UserAgent = null, ConfigureMessageHandler = _ => handler });
        var deckImporter = new FakeDeckImporter();
        var session = new ArchidektDeckCacheSession(new CategoryKnowledgeRepository(_databasePath), deckImporter, new ArchidektRecentDecksImporter(client), idlePollDelay: TimeSpan.FromMilliseconds(1));

        await session.RunAsync(TimeSpan.FromSeconds(1));
        handler.PageOneJson = "{\"results\":[{\"id\":101,\"updatedAt\":\"2026-01-02T00:00:00.123456Z\"}]}";
        var result = await session.RunAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(2, deckImporter.ImportCalls);
        Assert.Equal(0, result.DecksEnqueued);
        Assert.All(handler.RequestPaths, path => Assert.StartsWith("/api/decks/v3/?orderBy=-updatedAt&page=", path, StringComparison.Ordinal));
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT processed, refresh_requested_utc FROM deck_queue WHERE deck_id = '101';";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.IsDBNull(1));
    }

    public void Dispose()
    {
        ArchidektThrottle.ResetForTests();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, recursive: true);
    }

    private sealed class ListingFixtureHandler : HttpMessageHandler
    {
        public string PageOneJson { get; set; } = "{\"results\":[]}";
        public List<string> RequestPaths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            RequestPaths.Add(path);
            var body = path.EndsWith("&page=1", StringComparison.Ordinal) ? PageOneJson : "{\"results\":[]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class FakeDeckImporter : IArchidektDeckImporter
    {
        public int ImportCalls { get; private set; }
        public Task<List<DeckEntry>> ImportAsync(string deckId, CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            return Task.FromResult(new List<DeckEntry> { new() { Name = $"Card {deckId}", NormalizedName = CardNormalizer.Normalize($"Card {deckId}"), Quantity = 1, Board = "mainboard", Category = "Ramp" } });
        }

        public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string deckId, CancellationToken cancellationToken = default)
            => new(await ImportAsync(deckId, cancellationToken), new ArchidektDeckMetadata(null, null, null, null, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.UtcNow));
    }
}
