using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentKbSeedLoader"/>: absent seed → no-op; present seed →
/// curation-preserving upsert per row (Pitfall 1), explicit Id=0 (MED-2), and natural-key
/// mapping to YoutubeVideoId / RssGuid.
/// </summary>
public sealed class ContentKbSeedLoaderTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    [Fact]
    public async Task LoadIfPresentAsync_ReturnsZero_WhenSeedFileAbsent()
    {
        var baseDir = CreateContentKbBase();
        var store = new FakeContentSiteIndexStore();
        var loader = BuildLoader(baseDir, store);

        var count = await loader.LoadIfPresentAsync();

        Assert.Equal(0, count);
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task LoadIfPresentAsync_UsesPreservingUpsert_NotPlainUpsert()
    {
        var baseDir = CreateContentKbBase();
        WriteSeed(baseDir, """
        [
          {
            "naturalKeyType": "youtube_channel",
            "naturalKeyValue": "vid123",
            "source": "EDHRECast",
            "title": "Test",
            "videoUrl": "https://youtu.be/vid123",
            "artifactPath": "content-kb/edhrecast/vid123.md",
            "publishedUtc": null,
            "indexedUtc": "2026-06-01T00:00:00Z",
            "archetypeTags": ["ramp"],
            "bracketTags": [],
            "cardCategoryTags": []
          }
        ]
        """);
        var store = new FakeContentSiteIndexStore();
        var loader = BuildLoader(baseDir, store);

        var count = await loader.LoadIfPresentAsync();

        Assert.Equal(1, count);
        Assert.Single(store.PreservingUpserts);
        Assert.Empty(store.PlainUpserts);
    }

    [Fact]
    public async Task LoadIfPresentAsync_SetsIdZero_AndMapsYoutubeKey()
    {
        var baseDir = CreateContentKbBase();
        WriteSeed(baseDir, """
        [
          {
            "naturalKeyType": "youtube_channel",
            "naturalKeyValue": "ytKey",
            "source": "EDHRECast",
            "title": "YT",
            "videoUrl": "https://youtu.be/ytKey",
            "publishedUtc": null,
            "indexedUtc": "2026-06-01T00:00:00Z",
            "artifactPath": "content-kb/edhrecast/ytKey.md",
            "archetypeTags": [],
            "bracketTags": [],
            "cardCategoryTags": []
          }
        ]
        """);
        var store = new FakeContentSiteIndexStore();
        var loader = BuildLoader(baseDir, store);

        await loader.LoadIfPresentAsync();

        var row = Assert.Single(store.PreservingUpserts);
        Assert.Equal(0, row.Id);
        Assert.Equal("ytKey", row.YoutubeVideoId);
        Assert.Null(row.RssGuid);
        Assert.Equal("content-kb/edhrecast/ytKey.md", row.ArtifactPath);
    }

    [Fact]
    public async Task LoadIfPresentAsync_MapsBodySha256_WhenPresent()
    {
        var baseDir = CreateContentKbBase();
        WriteSeed(baseDir, """
        [
          {
            "naturalKeyType": "youtube_channel",
            "naturalKeyValue": "hashedKey",
            "source": "EDHRECast",
            "title": "Hashed",
            "videoUrl": "https://youtu.be/hashedKey",
            "publishedUtc": null,
            "indexedUtc": "2026-06-01T00:00:00Z",
            "artifactPath": "content-kb/edhrecast/hashedKey.md",
            "archetypeTags": [],
            "bracketTags": [],
            "cardCategoryTags": [],
            "bodySha256": "a1b2c3d4e5f60718293a4b5c6d7e8f90112233445566778899aabbccddeeff"
          }
        ]
        """);
        var store = new FakeContentSiteIndexStore();
        var loader = BuildLoader(baseDir, store);

        await loader.LoadIfPresentAsync();

        var row = Assert.Single(store.PreservingUpserts);
        Assert.Equal("a1b2c3d4e5f60718293a4b5c6d7e8f90112233445566778899aabbccddeeff", row.BodySha256);
    }

    [Fact]
    public async Task LoadIfPresentAsync_LeavesBodySha256Null_WhenLegacyEntryOmitsIt()
    {
        var baseDir = CreateContentKbBase();
        WriteSeed(baseDir, """
        [
          {
            "naturalKeyType": "youtube_channel",
            "naturalKeyValue": "legacyKey",
            "source": "EDHRECast",
            "title": "Legacy",
            "videoUrl": "https://youtu.be/legacyKey",
            "publishedUtc": null,
            "indexedUtc": "2026-06-01T00:00:00Z",
            "artifactPath": "content-kb/edhrecast/legacyKey.md",
            "archetypeTags": [],
            "bracketTags": [],
            "cardCategoryTags": []
          }
        ]
        """);
        var store = new FakeContentSiteIndexStore();
        var loader = BuildLoader(baseDir, store);

        await loader.LoadIfPresentAsync();

        var row = Assert.Single(store.PreservingUpserts);
        Assert.Null(row.BodySha256);
    }

    [Fact]
    public async Task LoadIfPresentAsync_StampsSeedManagedTrue_OnLoadedRow()
    {
        // SYNC-17/D-01: every row the seed loader builds is hardcoded seed_managed=true (Pitfall 4) —
        // presence in the loaded seed file proves seed-managed, regardless of the JSON's own fields.
        var baseDir = CreateContentKbBase();
        WriteSeed(baseDir, """
        [
          {
            "naturalKeyType": "youtube_channel",
            "naturalKeyValue": "markedKey",
            "source": "EDHRECast",
            "title": "Marked",
            "videoUrl": "https://youtu.be/markedKey",
            "publishedUtc": null,
            "indexedUtc": "2026-06-01T00:00:00Z",
            "artifactPath": "content-kb/edhrecast/markedKey.md",
            "archetypeTags": [],
            "bracketTags": [],
            "cardCategoryTags": []
          }
        ]
        """);
        var store = new FakeContentSiteIndexStore();
        var loader = BuildLoader(baseDir, store);

        await loader.LoadIfPresentAsync();

        var row = Assert.Single(store.PreservingUpserts);
        Assert.True(row.SeedManaged);
    }

    [Fact]
    public async Task LoadIfPresentAsync_MapsPodcastKey_ToRssGuid()
    {
        var baseDir = CreateContentKbBase();
        WriteSeed(baseDir, """
        [
          {
            "naturalKeyType": "podcast_rss",
            "naturalKeyValue": "guid-xyz",
            "source": "SomePodcast",
            "title": "Ep 1",
            "videoUrl": "https://example.com/ep1",
            "publishedUtc": null,
            "indexedUtc": "2026-06-01T00:00:00Z",
            "artifactPath": "content-kb/somepodcast/guid-xyz.md",
            "archetypeTags": [],
            "bracketTags": [],
            "cardCategoryTags": []
          }
        ]
        """);
        var store = new FakeContentSiteIndexStore();
        var loader = BuildLoader(baseDir, store);

        await loader.LoadIfPresentAsync();

        var row = Assert.Single(store.PreservingUpserts);
        Assert.Equal("guid-xyz", row.RssGuid);
        Assert.Null(row.YoutubeVideoId);
    }

    private ContentKbSeedLoader BuildLoader(string baseDir, FakeContentSiteIndexStore store)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ContentKb:ContentBase"] = baseDir })
            .Build();
        var resolver = new ContentKbArtifactPathResolver(
            new StubWebHostEnvironment(baseDir),
            configuration,
            new FakeFeatureFlagCache(new Dictionary<string, bool> { ["sync.directpush-gitbody"] = false }),
            NullLogger<ContentKbArtifactPathResolver>.Instance);
        return new ContentKbSeedLoader(resolver, store, NullLogger<ContentKbSeedLoader>.Instance);
    }

    private string CreateContentKbBase()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kbseed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "content-kb"));
        _tempDirs.Add(dir);
        return dir;
    }

    private static void WriteSeed(string baseDir, string json)
    {
        var seedDir = Path.Combine(baseDir, "content-kb", "seed");
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "index-seed.json"), json);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public StubWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new NullFileProvider();
            WebRootPath = contentRootPath;
            WebRootFileProvider = new NullFileProvider();
        }

        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
