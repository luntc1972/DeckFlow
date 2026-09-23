using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CreatorSuppressionConsumerTests : IDisposable
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"creator-suppression-consumer-{Guid.NewGuid():N}");

    [Fact]
    public async Task Detail_SuppressedCreatorReturnsNotFoundAndControlHasCopyPrompt()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(Path.Combine(_directory, "content.db"));
        var suppressions = new CreatorSuppressionStore(connection);
        var store = new ContentSiteIndexStore(connection, suppressionStore: suppressions);
        var controller = CreateController(store);
        var suppressedId = await AddPublishedRowAsync(store, "suppressed", "Suppressed Creator");
        var controlId = await AddPublishedRowAsync(store, "control", "Control Creator");
        Directory.CreateDirectory(Path.Combine(_directory, "content-kb", "control"));
        await File.WriteAllTextAsync(Path.Combine(_directory, "content-kb", "control", "public-entry.md"), "# Control\n\nUseful notes.");
        await suppressions.SuppressAsync("suppressed", ["Suppressed Creator"], "request", DateTimeOffset.UtcNow, null);

        Assert.IsType<NotFoundResult>(await controller.Detail(suppressedId));
        var control = Assert.IsType<ViewResult>(await controller.Detail(controlId));
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<ContentKbDetailViewModel>(control.Model).CleanBodyText));
    }

    [Fact]
    public async Task Detail_ThrowingSuppressionStoreRefusesPrompt()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(Path.Combine(_directory, "content.db"));
        var store = new ContentSiteIndexStore(connection, suppressionStore: new ThrowingSuppressionStore());
        var controller = CreateController(store);
        var id = await AddPublishedRowAsync(store, "control", "Control Creator");

        var readableStore = new ContentSiteIndexStore(
            connection,
            suppressionStore: new CreatorSuppressionStore(connection));
        var readableController = CreateController(readableStore);
        Assert.IsType<ViewResult>(await readableController.Detail(id));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Detail(id));
        Assert.Equal("Suppression table unreadable.", exception.Message);
    }

    public void Dispose()
    {
        ClearPool(Path.Combine(_directory, "content.db"));
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private ContentKbController CreateController(ContentSiteIndexStore store)
    {
        Directory.CreateDirectory(_directory);
        var flags = new FakeFeatureFlagCache(new Dictionary<string, bool> { ["sync.directpush-gitbody"] = false });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ContentKb:ContentBase"] = _directory }).Build();
        var resolver = new ContentKbArtifactPathResolver(new TestWebHostEnvironment(_directory), configuration, flags, NullLogger<ContentKbArtifactPathResolver>.Instance);
        return new ContentKbController(store, resolver, flags, NullLogger<ContentKbController>.Instance);
    }

    private static async Task<long> AddPublishedRowAsync(ContentSiteIndexStore store, string folder, string source)
    {
        await store.UpsertRowPreservingVisibilityAsync(new ContentSiteIndexRow
        {
            Id = 0,
            Source = source,
            Title = "Public entry",
            VideoUrl = "https://www.youtube.com/watch?v=public-entry",
            ArtifactPath = $"content-kb/{folder}/public-entry.md",
            PublishedUtc = DateTimeOffset.UtcNow,
            IndexedUtc = DateTimeOffset.UtcNow,
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = [],
            YoutubeVideoId = $"public-{folder}",
            ApprovalStatus = "approved"
        });
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, $"public-{folder}");
        Assert.NotNull(row);
        Assert.Equal(1, await store.SetVisibilityAsync(row!.Id, visible: true));
        return row.Id;
    }

    private sealed class ThrowingSuppressionStore : ICreatorSuppressionStore
    {
        private static InvalidOperationException Failure() => new("Suppression table unreadable.");
        public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => throw Failure();
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => throw Failure();
        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => throw Failure();
        public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => throw Failure();
        public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => throw Failure();
        public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default) => throw Failure();
        public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default) => throw Failure();
        public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => throw Failure();
        public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => throw Failure();
        public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => throw Failure();
        public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => throw Failure();
    }

    private sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
