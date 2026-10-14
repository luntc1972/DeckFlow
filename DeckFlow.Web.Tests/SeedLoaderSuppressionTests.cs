using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class SeedLoaderSuppressionTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    [Fact]
    public async Task ContentKbSeed_ManyEntries_ListsOnceAndResolvesInOneBatch()
    {
        var baseDir = CreateBase();
        WriteContent(baseDir, "first", "blocked", "third");
        var target = new FakeContentSiteIndexStore();
        var store = new CountingStore("blocked");
        var resolver = new CountingResolver();

        await ContentLoader(baseDir, target, resolver, store).LoadIfPresentAsync();

        Assert.Equal(1, store.ListCalls);
        Assert.Equal(1, resolver.ResolveManyCalls);
        Assert.Equal(0, resolver.ResolveCalls);
        Assert.Equal(["first", "third"], target.PreservingUpserts.Select(row => row.Source));
    }

    [Fact]
    public async Task CreatorStyleSeed_ManyProfiles_ListsOnceAndResolvesInOneBatch()
    {
        var baseDir = CreateBase();
        WriteProfiles(baseDir, "first", "blocked", "third");
        var target = new ProfileStore();
        var store = new CountingStore("blocked");
        var resolver = new CountingResolver();

        await StyleLoader(baseDir, target, new DeckStore(), resolver, store).LoadIfPresentAsync();

        Assert.Equal(1, store.ListCalls);
        Assert.Equal(1, resolver.ResolveManyCalls);
        Assert.Equal(0, resolver.ResolveCalls);
        Assert.Equal(["first", "third"], target.Upserts.Select(profile => profile.Slug));
    }

    [Fact]
    public async Task CreatorStyleSeed_ManyDeckCacheEntries_ListsOnceAndResolvesInOneBatch()
    {
        var baseDir = CreateBase();
        WriteDeckCache(baseDir, "first", "blocked", "third");
        var target = new DeckStore();
        var store = new CountingStore("blocked");
        var resolver = new CountingResolver();

        await StyleLoader(baseDir, new ProfileStore(), target, resolver, store).LoadIfPresentAsync();

        Assert.Equal(1, store.ListCalls);
        Assert.Equal(1, resolver.ResolveManyCalls);
        Assert.Equal(0, resolver.ResolveCalls);
        Assert.Equal(["first", "third"], target.Upserts.Select(entry => entry.CreatorSlug));
    }

    [Fact]
    public async Task ContentKbSeed_DisplayNameOnlySuppressed_IsSkipped()
    {
        var baseDir = CreateBase();
        WriteContent(baseDir, "creator", "control");
        var target = new FakeContentSiteIndexStore();
        var resolver = new CountingResolver(new KeyValuePair<string, CreatorIdentity>("creator", new CreatorIdentity("creator", [], ["Creator Display"], [])));

        await ContentLoader(baseDir, target, resolver, new CountingStore("Creator Display")).LoadIfPresentAsync();

        Assert.Equal(["control"], target.PreservingUpserts.Select(row => row.Source));
    }

    [Fact]
    public async Task CreatorStyleSeed_DisplayNameOnlySuppressed_IsSkipped()
    {
        var baseDir = CreateBase();
        WriteProfiles(baseDir, "creator", "control");
        var target = new ProfileStore();
        var resolver = new CountingResolver(new KeyValuePair<string, CreatorIdentity>("creator", new CreatorIdentity("creator", [], ["Creator Display"], [])));

        await StyleLoader(baseDir, target, new DeckStore(), resolver, new CountingStore("Creator Display")).LoadIfPresentAsync();

        Assert.Equal(["control"], target.Upserts.Select(profile => profile.Slug));
    }

    [Fact]
    public async Task ContentKbSeed_CaseDistinctEntries_SkipsOnlySuppressedEntry()
    {
        var baseDir = CreateBase();
        WriteContent(baseDir, "Foo", "foo");
        var target = new FakeContentSiteIndexStore();

        await ContentLoader(baseDir, target, new CaseResolver(), new CountingStore("Blocked")).LoadIfPresentAsync();

        Assert.Equal(["foo"], target.PreservingUpserts.Select(row => row.Source));
    }

    [Fact]
    public async Task CreatorStyleSeed_CaseDistinctEntries_SkipsOnlySuppressedEntry()
    {
        var baseDir = CreateBase();
        WriteProfiles(baseDir, "Foo", "foo");
        var target = new ProfileStore();

        await StyleLoader(baseDir, target, new DeckStore(), new CaseResolver(), new CountingStore("Blocked")).LoadIfPresentAsync();

        Assert.Equal(["foo"], target.Upserts.Select(profile => profile.Slug));
    }

    [Fact]
    public async Task ContentKbSeed_AliasConflictFallback_SkipsConflictingEntryAndLoadsOthers()
    {
        var baseDir = CreateBase();
        WriteContent(baseDir, "Conflict", "control", "third");
        var target = new FakeContentSiteIndexStore();
        var logger = new StructuredLogger<ContentKbSeedLoader>();

        await ContentLoader(baseDir, target, new ConflictResolver("Conflict"), new CountingStore(), logger).LoadIfPresentAsync();

        Assert.Equal(["control", "third"], target.PreservingUpserts.Select(row => row.Source));
        AssertConflictWarning(logger);
    }

    [Fact]
    public async Task CreatorStyleSeed_AliasConflictFallback_SkipsConflictingEntryAndLoadsOthers()
    {
        var baseDir = CreateBase();
        WriteProfiles(baseDir, "Conflict", "control", "third");
        var target = new ProfileStore();
        var logger = new StructuredLogger<CreatorStyleSeedLoader>();

        await StyleLoader(baseDir, target, new DeckStore(), new ConflictResolver("Conflict"), new CountingStore(), logger).LoadIfPresentAsync();

        Assert.Equal(["control", "third"], target.Upserts.Select(profile => profile.Slug));
        AssertConflictWarning(logger);
    }

    [Fact]
    public async Task CreatorDeckCacheSeed_AliasConflictFallback_SkipsConflictingEntryAndLoadsOthers()
    {
        var baseDir = CreateBase();
        WriteDeckCache(baseDir, "Conflict", "control", "third");
        var target = new DeckStore();
        var logger = new StructuredLogger<CreatorStyleSeedLoader>();

        await StyleLoader(baseDir, new ProfileStore(), target, new ConflictResolver("Conflict"), new CountingStore(), logger).LoadIfPresentAsync();

        Assert.Equal(["control", "third"], target.Upserts.Select(entry => entry.CreatorSlug));
        AssertConflictWarning(logger);
    }

    private static void AssertConflictWarning<T>(StructuredLogger<T> logger)
    {
        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal("Conflict", Assert.Single(warning.Values, value => value.Key == "Source").Value);
    }

    private ContentKbSeedLoader ContentLoader(string baseDir, IContentSiteIndexStore target, ICreatorIdentityResolver resolver, ICreatorSuppressionStore store, ILogger<ContentKbSeedLoader>? logger = null)
        => new(PathResolver(baseDir), target, resolver, store, logger ?? NullLogger<ContentKbSeedLoader>.Instance);

    private CreatorStyleSeedLoader StyleLoader(string baseDir, ICreatorStyleProfileStore profiles, ICreatorDeckCacheStore decks, ICreatorIdentityResolver resolver, ICreatorSuppressionStore store, ILogger<CreatorStyleSeedLoader>? logger = null)
        => new(PathResolver(baseDir), profiles, decks, resolver, store, logger ?? NullLogger<CreatorStyleSeedLoader>.Instance);

    private static ContentKbArtifactPathResolver PathResolver(string baseDir)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ContentKb:ContentBase"] = baseDir }).Build();
        return new ContentKbArtifactPathResolver(new TestEnvironment(baseDir), configuration, new FakeFeatureFlagCache(new Dictionary<string, bool> { ["sync.directpush-gitbody"] = false }), NullLogger<ContentKbArtifactPathResolver>.Instance);
    }

    private string CreateBase()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "seed-loader-suppression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(baseDir, "content-kb"));
        _tempDirs.Add(baseDir);
        return baseDir;
    }

    private static void WriteContent(string baseDir, params string[] sources)
        => Write(baseDir, "content-kb/seed/index-seed.json", JsonSerializer.Serialize(sources.Select((source, index) => new { naturalKeyType = "youtube_channel", naturalKeyValue = index.ToString(), source, title = source, videoUrl = "https://example.test/" + index, artifactPath = "content-kb/seed/" + index + ".md", indexedUtc = "2026-06-01T00:00:00Z", archetypeTags = Array.Empty<string>(), bracketTags = Array.Empty<string>(), cardCategoryTags = Array.Empty<string>() })));

    private static void WriteProfiles(string baseDir, params string[] slugs)
        => Write(baseDir, ContentKbPaths.CreatorStyleProfileSeedRelativePath, JsonSerializer.Serialize(slugs.Select(slug => new CreatorStyleProfile { Slug = slug, Platform = "youtube", MinDecks = 7, StatedRules = [], MeasuredMetrics = [], FusedTargets = [], UpdatedUtc = DateTimeOffset.Parse("2026-07-18T00:00:00Z") })));

    private static void WriteDeckCache(string baseDir, params string[] slugs)
        => Write(baseDir, ContentKbPaths.CreatorDeckCacheSeedRelativePath, JsonSerializer.Serialize(slugs.Select((slug, index) => new CreatorDeckCacheEntry { CreatorSlug = slug, DeckId = index.ToString(), ContentHash = "hash-" + index, FolderId = index, FolderName = slug, Size = 100, ConfidenceMarker = "exact", Entries = [], CachedUtc = DateTimeOffset.Parse("2026-07-18T00:00:00Z") })));

    private static void Write(string baseDir, string relativePath, string json)
    {
        var path = Path.Combine(baseDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    public void Dispose()
    {
        foreach (var directory in _tempDirs)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class CountingResolver(params KeyValuePair<string, CreatorIdentity>[] pairs) : ICreatorIdentityResolver
    {
        private readonly IReadOnlyDictionary<string, CreatorIdentity> _identities = pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        public int ResolveCalls { get; private set; }
        public int ResolveManyCalls { get; private set; }
        public Task<CreatorIdentity?> ResolveAsync(string representation, CancellationToken cancellationToken = default) { ResolveCalls++; return Task.FromResult<CreatorIdentity?>(_identities.GetValueOrDefault(representation)); }
        public Task<IReadOnlyDictionary<string, CreatorIdentity?>> ResolveManyAsync(IReadOnlyCollection<string> representations, CancellationToken cancellationToken = default) { ResolveManyCalls++; return Task.FromResult<IReadOnlyDictionary<string, CreatorIdentity?>>(representations.ToDictionary(value => value, value => _identities.GetValueOrDefault(value), StringComparer.Ordinal)); }
    }

    private sealed class ConflictResolver(string conflict) : ICreatorIdentityResolver
    {
        public Task<CreatorIdentity?> ResolveAsync(string representation, CancellationToken cancellationToken = default) => string.Equals(representation, conflict, StringComparison.Ordinal) ? throw new CreatorAliasConflictException(conflict, "first, second") : Task.FromResult<CreatorIdentity?>(null);
        public Task<IReadOnlyDictionary<string, CreatorIdentity?>> ResolveManyAsync(IReadOnlyCollection<string> representations, CancellationToken cancellationToken = default)
        {
            if (representations.Count > 1 || representations.Contains(conflict, StringComparer.Ordinal))
            {
                throw new CreatorAliasConflictException(conflict, "first, second");
            }

            return Task.FromResult<IReadOnlyDictionary<string, CreatorIdentity?>>(representations.ToDictionary(value => value, _ => (CreatorIdentity?)null, StringComparer.Ordinal));
        }
    }

    private sealed class CaseResolver : ICreatorIdentityResolver
    {
        private static readonly CreatorIdentity SuppressedIdentity = new("Blocked", [], [], []);
        public Task<CreatorIdentity?> ResolveAsync(string representation, CancellationToken cancellationToken = default) => Task.FromResult<CreatorIdentity?>(representation == "Foo" ? SuppressedIdentity : null);
        public Task<IReadOnlyDictionary<string, CreatorIdentity?>> ResolveManyAsync(IReadOnlyCollection<string> representations, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, CreatorIdentity?>>(new Dictionary<string, CreatorIdentity?>(StringComparer.Ordinal) { ["Foo"] = SuppressedIdentity, ["foo"] = null });
    }

    private sealed class CountingStore(params string[] suppressed) : ICreatorSuppressionStore
    {
        public int ListCalls { get; private set; }
        public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);
        public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new CreatorSuppressionSnapshot(0, []));
        public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default) { ListCalls++; return Task.FromResult<IReadOnlyList<CreatorSuppression>>(suppressed.Select(slug => new CreatorSuppression { Slug = slug, Aliases = [], Reason = "test", RequestedUtc = DateTimeOffset.UnixEpoch }).ToList()); }
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ProfileStore : ICreatorStyleProfileStore
    {
        public List<CreatorStyleProfile> Upserts { get; } = [];
        public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult<CreatorStyleProfile?>(null);
        public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default) { Upserts.Add(profile); return Task.CompletedTask; }
    }

    private sealed class DeckStore : ICreatorDeckCacheStore
    {
        public List<CreatorDeckCacheEntry> Upserts { get; } = [];
        public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetContentHashAsync(string creatorSlug, string deckId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>([]);
        public Task UpsertAsync(CreatorDeckCacheEntry entry, CancellationToken cancellationToken = default) { Upserts.Add(entry); return Task.CompletedTask; }
    }

    private sealed class StructuredLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, IReadOnlyList<KeyValuePair<string, object?>> Values)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                Entries.Add((logLevel, values.ToArray()));
            }
        }
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
