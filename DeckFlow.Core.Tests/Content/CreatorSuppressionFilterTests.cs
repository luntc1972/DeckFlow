using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Content;

public sealed class CreatorSuppressionFilterTests
{
    [Fact]
    public async Task FilterRowsAsync_MicroSignVariants_SuppressesOnlyMatchingValue()
    {
        var filter = new CreatorSuppressionFilter(
            new CountingStore([Suppression("\u00B5x")]),
            new Lazy<ICreatorIdentityResolver>(() => new NullResolver()));

        var rows = new[]
        {
            Row("\u03BCx", "content-kb/greek/item.md"),
            Row("\u00B5x", "content-kb/micro/item.md"),
        };

        var result = await filter.FilterRowsAsync(rows);

        Assert.Equal(["\u03BCx"], result.Select(row => row.Source));
    }

    [Fact]
    public async Task GetUnsuppressedAsync_MicroSignVariants_SuppressesOnlyMatchingValue()
    {
        // Why: only the U+00B5 slug resolves to the suppressed folder, so a shared resolver key would leak it.
        var filter = new CreatorSuppressionFilter(
            new CountingStore([Suppression("suppressed-folder")]),
            new Lazy<ICreatorIdentityResolver>(() => new MicroSignResolver()));

        var result = await filter.GetUnsuppressedAsync(["\u03BCx", "\u00B5x"]);

        Assert.Equal(["\u03BCx"], result);
    }

    [Fact]
    public async Task Filter_SuppressionAddedBetweenCalls_SecondCallSuppresses()
    {
        var store = new CountingStore();
        var filter = new CreatorSuppressionFilter(store, new Lazy<ICreatorIdentityResolver>(() => new CountingResolver()));
        var row = Row("source", "content-kb/folder/item.md");

        Assert.Single(await filter.FilterRowsAsync([row]));
        store.Rows.Add(Suppression("source"));
        Assert.Empty(await filter.FilterRowsAsync([row]));
    }

    [Fact]
    public async Task Filter_GetUnsuppressed_SuppressionAddedBetweenCalls_SecondCallSuppresses()
    {
        var store = new CountingStore();
        var filter = new CreatorSuppressionFilter(store, new Lazy<ICreatorIdentityResolver>(() => new CountingResolver()));
        Assert.Equal(["source"], await filter.GetUnsuppressedAsync(["source"]));
        store.Rows.Add(Suppression("source"));
        Assert.Empty(await filter.GetUnsuppressedAsync(["source"]));
    }

    [Fact]
    public async Task Filter_TwoOperations_ListsOncePerOperation()
    {
        var store = new CountingStore();
        var filter = new CreatorSuppressionFilter(store, new Lazy<ICreatorIdentityResolver>(() => new CountingResolver()));

        await filter.FilterRowsAsync([Row("one", "content-kb/one/item.md")]);
        Assert.Equal(1, store.ListCalls);
        await filter.IsSuppressedAsync("two");
        Assert.Equal(2, store.ListCalls);
    }

    [Fact]
    public async Task Filter_ManyRows_ResolvesIdentitiesInOneBatch()
    {
        var resolver = new CountingResolver();
        var filter = new CreatorSuppressionFilter(new CountingStore(), new Lazy<ICreatorIdentityResolver>(() => resolver));

        await filter.FilterRowsAsync([Row("one", "content-kb/one/item.md"), Row("two", "content-kb/two/item.md")]);

        Assert.Equal(1, resolver.ResolveManyCalls);
        Assert.Equal(0, resolver.ResolveCalls);
    }

    [Fact]
    public async Task Filter_GetUnsuppressed_KeepsOrderAndDuplicates_OneListOneResolve()
    {
        var store = new CountingStore([Suppression("blocked")]);
        var resolver = new CountingResolver();
        var filter = new CreatorSuppressionFilter(store, new Lazy<ICreatorIdentityResolver>(() => resolver));

        var result = await filter.GetUnsuppressedAsync(["c", "a", "blocked", "a", "b"]);

        Assert.Equal(["c", "a", "a", "b"], result);
        Assert.Equal(1, store.ListCalls);
        Assert.Equal(1, resolver.ResolveManyCalls);
        Assert.Equal(0, resolver.ResolveCalls);
    }

    [Fact]
    public async Task Filter_GetUnsuppressed_IdentityDisplayNameSuppressed_Excluded()
    {
        var resolver = new CountingResolver();
        resolver.Identities["source-slug"] = Identity("canonical", ["blocked-alias"], []);
        var filter = new CreatorSuppressionFilter(new CountingStore([Suppression("blocked-alias")]), new Lazy<ICreatorIdentityResolver>(() => resolver));

        var result = await filter.GetUnsuppressedAsync(["source-slug", "control"]);

        Assert.Equal(["control"], result);
    }

    [Fact]
    public async Task Filter_RealResolver_SuppressionLoadCountIndependentOfRowCount()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"creator-filter-{Guid.NewGuid():N}.db");
        try
        {
            var suppressions = new CountingStore();
            var sources = new ContentSourceStore(databasePath);
            var index = new ContentSiteIndexStore(databasePath);
            var resolver = new CreatorIdentityResolver(suppressions, sources, index);
            var filter = new CreatorSuppressionFilter(suppressions, new Lazy<ICreatorIdentityResolver>(() => resolver));

            await filter.FilterRowsAsync([Row("one", "content-kb/one/item.md")]);
            var oneRowCalls = suppressions.ListCalls;
            await filter.FilterRowsAsync([Row("two", "content-kb/two/item.md"), Row("three", "content-kb/three/item.md")]);

            Assert.Equal(oneRowCalls * 2, suppressions.ListCalls);
        }
        finally
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(databasePath)}"));
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task Filter_ResolvedArtifactFolder_RawFolderStillSuppressed()
    {
        var resolver = new CountingResolver();
        resolver.Identities["folder"] = Identity("canonical", ["display"], ["other-folder"]);
        var filter = new CreatorSuppressionFilter(new CountingStore([Suppression("folder")]), new Lazy<ICreatorIdentityResolver>(() => resolver));

        Assert.Empty(await filter.FilterRowsAsync([Row("source", "content-kb/folder/item.md")]));
    }

    [Fact]
    public async Task Filter_SingleSlugAgreesWithBatchCall()
    {
        var filter = new CreatorSuppressionFilter(new CountingStore([Suppression("source")]), new Lazy<ICreatorIdentityResolver>(() => new CountingResolver()));

        Assert.True(await filter.IsSuppressedAsync("source"));
        Assert.Empty(await filter.FilterRowsAsync([Row("source", "content-kb/folder/item.md")]));
    }

    [Fact]
    public async Task Filter_SingleSlug_IdentityDisplayNameSuppressed_ReturnsTrue()
    {
        var resolver = new CountingResolver();
        resolver.Identities["source"] = Identity("canonical", ["blocked-name"], []);
        var filter = new CreatorSuppressionFilter(new CountingStore([Suppression("blocked-name")]), new Lazy<ICreatorIdentityResolver>(() => resolver));

        Assert.True(await filter.IsSuppressedAsync("source"));
    }

    [Fact]
    public async Task Filter_IdentityFolderSlugSuppressed_RowSuppressed()
    {
        var resolver = new CountingResolver();
        resolver.Identities["source"] = Identity("canonical", [], ["blocked-folder"]);
        var filter = new CreatorSuppressionFilter(new CountingStore([Suppression("blocked-folder")]), new Lazy<ICreatorIdentityResolver>(() => resolver));

        Assert.Empty(await filter.FilterRowsAsync([Row("source", "content-kb/folder/item.md")]));
    }

    [Fact]
    public async Task Filter_SingleSlug_Blank_Throws()
    {
        var filter = new CreatorSuppressionFilter(new CountingStore(), new Lazy<ICreatorIdentityResolver>(() => new CountingResolver()));

        await Assert.ThrowsAsync<ArgumentException>(() => filter.IsSuppressedAsync(" "));
    }

    [Fact]
    public async Task Filter_GetUnsuppressed_BlankElement_Throws()
    {
        var filter = new CreatorSuppressionFilter(new CountingStore(), new Lazy<ICreatorIdentityResolver>(() => new CountingResolver()));

        await Assert.ThrowsAsync<ArgumentException>(() => filter.GetUnsuppressedAsync([" "]));
    }

    [Fact]
    public async Task Filter_NoRepresentationMatches_AllowsRow()
    {
        var filter = new CreatorSuppressionFilter(new CountingStore([Suppression("blocked")]), new Lazy<ICreatorIdentityResolver>(() => new CountingResolver()));

        Assert.Single(await filter.FilterRowsAsync([Row("source", "content-kb/folder/item.md")]));
    }

    [Fact]
    public async Task Filter_NullIdentity_UsesRawFallback()
    {
        var filter = new CreatorSuppressionFilter(new CountingStore([Suppression("source")]), new Lazy<ICreatorIdentityResolver>(() => new CountingResolver()));

        Assert.Empty(await filter.FilterRowsAsync([Row("source", "content-kb/folder/item.md")]));
    }

    [Fact]
    public async Task Filter_IdentityAlias_SuppressesRow()
    {
        var resolver = new CountingResolver();
        resolver.Identities["source"] = Identity("canonical", ["alias"], []);
        var filter = new CreatorSuppressionFilter(new CountingStore([Suppression("alias")]), new Lazy<ICreatorIdentityResolver>(() => resolver));

        Assert.Empty(await filter.FilterRowsAsync([Row("source", "content-kb/folder/item.md")]));
    }

    private static ContentSiteIndexRow Row(string source, string artifactPath) => new()
    {
        Id = 1,
        Source = source,
        Title = source,
        VideoUrl = "https://example.test",
        YoutubeVideoId = "video",
        ArtifactPath = artifactPath,
        IndexedUtc = DateTimeOffset.UtcNow,
        ArchetypeTags = [],
        BracketTags = [],
        CardCategoryTags = []
    };

    private static CreatorIdentity Identity(string canonical, IReadOnlyList<string> names, IReadOnlyList<string> folders)
        => new(canonical, [], names, folders);

    private static CreatorSuppression Suppression(string slug) => new() { Slug = slug, Aliases = [], Reason = "test", RequestedUtc = DateTimeOffset.UtcNow };

    private sealed class CountingResolver : ICreatorIdentityResolver
    {
        public Dictionary<string, CreatorIdentity?> Identities { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int ResolveCalls { get; private set; }
        public int ResolveManyCalls { get; private set; }
        public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default) { ResolveCalls++; return Task.FromResult(Identities.GetValueOrDefault(anyRepresentation)); }
        public Task<IReadOnlyDictionary<string, CreatorIdentity?>> ResolveManyAsync(IReadOnlyCollection<string> representations, CancellationToken cancellationToken = default)
        {
            ResolveManyCalls++;
            return Task.FromResult<IReadOnlyDictionary<string, CreatorIdentity?>>(representations.ToDictionary(value => value, value => Identities.GetValueOrDefault(value), StringComparer.OrdinalIgnoreCase));
        }
    }

    private sealed class MicroSignResolver : ICreatorIdentityResolver
    {
        public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default)
            => Task.FromResult(anyRepresentation == "\u00B5x" ? new CreatorIdentity("\u00B5x", [], [], ["suppressed-folder"]) : null);
    }

    private sealed class NullResolver : ICreatorIdentityResolver
    {
        public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default)
            => Task.FromResult<CreatorIdentity?>(null);
    }

    private sealed class CountingStore : ICreatorSuppressionStore
    {
        public CountingStore(IReadOnlyList<CreatorSuppression>? rows = null) => Rows = rows?.ToList() ?? [];
        public List<CreatorSuppression> Rows { get; }
        public int ListCalls { get; private set; }
        public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default) { ListCalls++; return Task.FromResult<IReadOnlyList<CreatorSuppression>>(Rows.ToList()); }
        public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);
        public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new CreatorSuppressionSnapshot(0, Rows));
        public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
