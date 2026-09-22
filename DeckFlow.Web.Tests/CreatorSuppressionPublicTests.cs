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

public sealed class CreatorSuppressionPublicTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"creator-suppression-public-{Guid.NewGuid():N}");

    [Fact]
    public async Task Index_SuppressedCreatorIsAbsentFromCards()
    {
        var (controller, store, suppressionStore) = CreateController();
        await AddPublishedRowAsync(store, "suppressed-cards", "Suppressed cards");
        await suppressionStore.SuppressAsync("suppressed-cards", new[] { "Suppressed cards" }, "request", DateTimeOffset.UtcNow, null);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Empty(Assert.IsType<ContentKbBrowseViewModel>(view.Model).Entries);
    }

    [Fact]
    public async Task Index_SuppressedCreatorIsAbsentFromSourceDropdown()
    {
        var (controller, store, suppressionStore) = CreateController();
        await AddPublishedRowAsync(store, "suppressed-source", "Suppressed source");
        await suppressionStore.SuppressAsync("suppressed-source", new[] { "Suppressed source" }, "request", DateTimeOffset.UtcNow, null);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Empty(Assert.IsType<ContentKbBrowseViewModel>(view.Model).Sources);
    }

    [Fact]
    public async Task Detail_SuppressedCreatorReturnsNotFound()
    {
        var (controller, store, suppressionStore) = CreateController();
        var id = await AddPublishedRowAsync(store, "suppressed-detail", "Suppressed detail");
        await suppressionStore.SuppressAsync("suppressed-detail", new[] { "Suppressed detail" }, "request", DateTimeOffset.UtcNow, null);

        Assert.IsType<NotFoundResult>(await controller.Detail(id));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private (ContentKbController Controller, ContentSiteIndexStore Store, CreatorSuppressionStore SuppressionStore) CreateController()
    {
        Directory.CreateDirectory(_directory);
        var connection = RelationalDatabaseConnection.FromSqlitePath(Path.Combine(_directory, "content.db"));
        var suppressionStore = new CreatorSuppressionStore(connection);
        var store = new ContentSiteIndexStore(connection, suppressionStore: suppressionStore);
        var flags = new FakeFeatureFlagCache(new Dictionary<string, bool> { ["sync.directpush-gitbody"] = false });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ContentKb:ContentBase"] = _directory }).Build();
        var resolver = new ContentKbArtifactPathResolver(new TestWebHostEnvironment(_directory), configuration, flags, NullLogger<ContentKbArtifactPathResolver>.Instance);
        return (new ContentKbController(store, resolver, flags, NullLogger<ContentKbController>.Instance), store, suppressionStore);
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
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = $"public-{folder}",
            ApprovalStatus = "approved"
        });
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, $"public-{folder}");
        Assert.NotNull(row);
        Assert.Equal(1, await store.SetVisibilityAsync(row!.Id, visible: true));
        return row.Id;
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
