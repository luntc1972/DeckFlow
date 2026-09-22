using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CreatorSuppressionLoaderTests : IDisposable
{
    private readonly List<string> _directories = [];

    [Fact]
    public async Task ContentKbSeedLoader_SkipsSuppressedResolvedCreator()
    {
        var baseDir = CreateBase();
        Write(baseDir, "content-kb/seed/index-seed.json", "[{\"naturalKeyType\":\"youtube_video\",\"naturalKeyValue\":\"1\",\"source\":\"Display Name\",\"title\":\"title\",\"videoUrl\":\"https://example.test\",\"artifactPath\":\"content-kb/a.md\",\"indexedUtc\":\"2026-01-01T00:00:00Z\",\"archetypeTags\":[],\"bracketTags\":[],\"cardCategoryTags\":[]}]");
        var identities = new FakeCreatorIdentityResolver();
        identities.Identities["Display Name"] = new CreatorIdentity("canonical", [], ["Display Name"], []);
        var suppressions = new FakeCreatorSuppressionStore();
        suppressions.Suppressed.Add("canonical");
        var store = new FakeContentSiteIndexStore();

        var count = await CreateContentLoader(baseDir, store, identities, suppressions).LoadIfPresentAsync();

        Assert.Equal(0, count);
        Assert.Empty(store.PreservingUpserts);
    }

    [Fact]
    public async Task CreatorStyleSeedLoader_SkipsSuppressedProfilesAndDeckCache()
    {
        var baseDir = CreateBase();
        Write(baseDir, ContentKbPaths.CreatorStyleProfileSeedRelativePath, JsonSerializer.Serialize(new[] { Profile("historical-slug") }));
        Write(baseDir, ContentKbPaths.CreatorDeckCacheSeedRelativePath, JsonSerializer.Serialize(new[] { Deck("historical-slug") }));
        var identities = new FakeCreatorIdentityResolver();
        identities.Identities["historical-slug"] = new CreatorIdentity("canonical", [], [], ["historical-slug"]);
        var suppressions = new FakeCreatorSuppressionStore();
        suppressions.Suppressed.Add("canonical");
        var profiles = new FakeCreatorStyleProfileSeedStore();
        var decks = new FakeCreatorDeckCacheSeedStore();

        var count = await CreateStyleLoader(baseDir, profiles, decks, identities, suppressions).LoadIfPresentAsync();

        Assert.Equal(0, count);
        Assert.Empty(profiles.Upserts);
        Assert.Empty(decks.Upserts);
    }

    [Fact]
    public async Task ContentKbSeedLoader_ReloadDoesNotResurrectSuppressedCreator()
    {
        var baseDir = CreateBase();
        Write(baseDir, "content-kb/seed/index-seed.json", "[{\"naturalKeyType\":\"youtube_video\",\"naturalKeyValue\":\"1\",\"source\":\"canonical\",\"title\":\"title\",\"videoUrl\":\"https://example.test\",\"artifactPath\":\"content-kb/a.md\",\"indexedUtc\":\"2026-01-01T00:00:00Z\",\"archetypeTags\":[],\"bracketTags\":[],\"cardCategoryTags\":[]}]");
        var suppressions = new FakeCreatorSuppressionStore();
        suppressions.Suppressed.Add("canonical");
        var store = new FakeContentSiteIndexStore();
        var loader = CreateContentLoader(baseDir, store, new FakeCreatorIdentityResolver(), suppressions);

        await loader.LoadIfPresentAsync();
        await loader.LoadIfPresentAsync();

        Assert.Empty(store.PreservingUpserts);
    }

    [Fact]
    public async Task ContentKbSeedLoader_ThrowsWhenSuppressionStoreUnreadable()
    {
        var baseDir = CreateBase();
        Write(baseDir, "content-kb/seed/index-seed.json", "[{\"naturalKeyType\":\"youtube_video\",\"naturalKeyValue\":\"1\",\"source\":\"creator\",\"title\":\"title\",\"videoUrl\":\"https://example.test\",\"artifactPath\":\"content-kb/a.md\",\"indexedUtc\":\"2026-01-01T00:00:00Z\",\"archetypeTags\":[],\"bracketTags\":[],\"cardCategoryTags\":[]}]");
        var suppressions = new FakeCreatorSuppressionStore { ReadException = new InvalidOperationException("unreadable") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateContentLoader(baseDir, new FakeContentSiteIndexStore(), new FakeCreatorIdentityResolver(), suppressions).LoadIfPresentAsync());
    }

    [Fact]
    public async Task CreatorStyleSeedLoader_ThrowsWhenSuppressionStoreUnreadable()
    {
        var baseDir = CreateBase();
        Write(baseDir, ContentKbPaths.CreatorStyleProfileSeedRelativePath, JsonSerializer.Serialize(new[] { Profile("creator") }));
        var suppressions = new FakeCreatorSuppressionStore { ReadException = new InvalidOperationException("unreadable") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateStyleLoader(baseDir, new FakeCreatorStyleProfileSeedStore(), new FakeCreatorDeckCacheSeedStore(), new FakeCreatorIdentityResolver(), suppressions).LoadIfPresentAsync());
    }

    private ContentKbSeedLoader CreateContentLoader(string baseDir, FakeContentSiteIndexStore store, FakeCreatorIdentityResolver identities, FakeCreatorSuppressionStore suppressions)
        => new(Resolver(baseDir), store, identities, suppressions, NullLogger<ContentKbSeedLoader>.Instance);

    private CreatorStyleSeedLoader CreateStyleLoader(string baseDir, FakeCreatorStyleProfileSeedStore profiles, FakeCreatorDeckCacheSeedStore decks, FakeCreatorIdentityResolver identities, FakeCreatorSuppressionStore suppressions)
        => new(Resolver(baseDir), profiles, decks, identities, suppressions, NullLogger<CreatorStyleSeedLoader>.Instance);

    private static CreatorStyleProfile Profile(string slug) => new() { Slug = slug, Platform = "youtube", MinDecks = 1, InsufficientSample = false, StatedRules = [], MeasuredMetrics = [], FusedTargets = [], UpdatedUtc = DateTimeOffset.UtcNow };
    private static CreatorDeckCacheEntry Deck(string slug) => new() { CreatorSlug = slug, DeckId = "deck", ContentHash = "hash", Size = 1, ConfidenceMarker = "exact", Entries = [], CachedUtc = DateTimeOffset.UtcNow };
    private static ContentKbArtifactPathResolver Resolver(string baseDir) => new(new EnvironmentStub(baseDir), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ContentKb:ContentBase"] = baseDir }).Build(), new FakeFeatureFlagCache(), NullLogger<ContentKbArtifactPathResolver>.Instance);
    private string CreateBase() { var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(directory, "content-kb")); _directories.Add(directory); return directory; }
    private static void Write(string baseDir, string path, string contents) { var fullPath = Path.Combine(baseDir, path); Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!); File.WriteAllText(fullPath, contents); }
    public void Dispose() { foreach (var directory in _directories) { if (Directory.Exists(directory)) Directory.Delete(directory, true); } }

    private sealed class EnvironmentStub(string path) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = path;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = path;
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
