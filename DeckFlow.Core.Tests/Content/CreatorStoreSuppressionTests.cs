using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Core.Tests;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Content;

public sealed class CreatorStoreSuppressionTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"creator-store-suppression-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task StatedRules_IdentityAliasSuppressed_Excluded()
    {
        var suppressions = new CountingSuppressionStore([Suppression("Suppressed Display")]);
        var store = new CreatorStyleStatedRuleStore(_databasePath, suppressions, new Lazy<ICreatorIdentityResolver>(() => Resolver("creator-slug", "Suppressed Display")));
        await store.UpsertAsync(new StatedRuleCandidate { Category = "deckbuilding", Metric = "ramp", Value = 1, Comparator = "gte", SourceClip = "test", Confidence = 1, VideoDateUtc = DateTimeOffset.UtcNow }, "creator-slug");

        Assert.Empty(await store.GetBySlugAsync("creator-slug"));
    }

    [Fact]
    public async Task ProfileGetAll_ManyProfiles_ListsOnce()
    {
        var suppressions = new CountingSuppressionStore([Suppression("blocked")]);
        var store = new CreatorStyleProfileStore(_databasePath, suppressions);
        await store.UpsertAsync(Profile("one"));
        await store.UpsertAsync(Profile("blocked"));
        await store.UpsertAsync(Profile("three"));
        var profiles = await store.GetAllAsync();
        Assert.Equal(1, suppressions.ListCalls);
        Assert.Equal(["one", "three"], profiles.Select(profile => profile.Slug).Order());
    }

    [Fact]
    public async Task ProfileGetAll_IdentityAliasSuppressed_Excluded()
    {
        var suppressions = new CountingSuppressionStore([Suppression("blocked-alias")]);
        var store = new CreatorStyleProfileStore(_databasePath, suppressions, new Lazy<ICreatorIdentityResolver>(() => Resolver("profile-slug", "blocked-alias")));
        await store.UpsertAsync(Profile("profile-slug"));
        await store.UpsertAsync(Profile("control"));

        var profiles = await store.GetAllAsync();

        Assert.Equal(["control"], profiles.Select(profile => profile.Slug));
    }

    [Fact]
    public async Task ProfileGetAll_CaseFoldVariant_DoesNotUnsuppress()
    {
        var suppressions = new CountingSuppressionStore([Suppression("µ-creator")]);
        var store = new CreatorStyleProfileStore(_databasePath, suppressions);
        await store.UpsertAsync(Profile("µ-creator"));
        await store.UpsertAsync(Profile("μ-creator"));

        var profiles = await store.GetAllAsync();

        Assert.Equal(["μ-creator"], profiles.Select(profile => profile.Slug));
    }

    [Fact]
    public async Task ProfileGetBySlug_IdentityAliasSuppressed_ReturnsNull()
    {
        var suppressions = new CountingSuppressionStore([Suppression("blocked-alias")]);
        var store = new CreatorStyleProfileStore(_databasePath, suppressions, new Lazy<ICreatorIdentityResolver>(() => Resolver("profile-slug", "blocked-alias")));
        await store.UpsertAsync(Profile("profile-slug"));
        Assert.Null(await store.GetBySlugAsync("profile-slug"));
    }

    [Fact]
    public async Task DeckCache_IdentityAliasSuppressed_Excluded()
    {
        var suppressions = new CountingSuppressionStore([Suppression("blocked-alias")]);
        var store = new CreatorDeckCacheStore(_databasePath, suppressions, new Lazy<ICreatorIdentityResolver>(() => Resolver("deck-creator", "blocked-alias")));
        await store.UpsertAsync(new CreatorDeckCacheEntry { CreatorSlug = "deck-creator", DeckId = "deck", ContentHash = "hash", FolderId = 1, FolderName = "folder", Size = 1, ConfidenceMarker = "test", Entries = [], CachedUtc = DateTimeOffset.UtcNow });
        Assert.Empty(await store.GetByCreatorAsync("deck-creator"));
    }

    [Fact]
    public async Task StatedRules_SuppressedCreator_Excluded()
    {
        var suppressions = new CountingSuppressionStore([Suppression("blocked")]);
        var store = new CreatorStyleStatedRuleStore(_databasePath, suppressions);
        await store.UpsertAsync(Rule(), "blocked");
        await store.UpsertAsync(Rule(), "control");
        Assert.Empty(await store.GetBySlugAsync("blocked"));
        Assert.Single(await store.GetBySlugAsync("control"));
    }

    [Fact]
    public async Task SourceListEnabled_ManySources_ListsOnce()
    {
        var suppressions = new CountingSuppressionStore([Suppression("blocked")]);
        var store = new ContentSourceStore(_databasePath, suppressions);
        await InsertAsync(store, "one", "One");
        await InsertAsync(store, "blocked", "Blocked");
        await InsertAsync(store, "three", "Three");
        var sources = await store.ListEnabledSourcesAsync();
        Assert.Equal(1, suppressions.ListCalls);
        Assert.Equal(["one", "three"], sources.Select(source => source.SourceSlug));
    }

    [Fact]
    public async Task SourceListEnabled_DisplayNameSuppressedSlugClean_Excluded()
    {
        var suppressions = new CountingSuppressionStore([Suppression("Blocked Name")]);
        var store = new ContentSourceStore(_databasePath, suppressions);
        await InsertAsync(store, "ok-source", "Blocked Name");
        await InsertAsync(store, "control", "Control");

        var sources = await store.ListEnabledSourcesAsync();

        Assert.Equal(["control"], sources.Select(source => source.SourceSlug));
    }

    [Fact]
    public async Task SourceListEnabled_CaseFoldVariant_DoesNotUnsuppress()
    {
        var suppressions = new CountingSuppressionStore([Suppression("µ-creator")]);
        var store = new ContentSourceStore(_databasePath, suppressions);
        await InsertAsync(store, "µ-creator", "Micro Sign Creator");
        await InsertAsync(store, "μ-creator", "Greek Mu Creator");

        var sources = await store.ListEnabledSourcesAsync();

        Assert.Equal(["μ-creator"], sources.Select(source => source.SourceSlug));
    }

    [Fact]
    public async Task SourceListEnabled_IdentityAliasSuppressed_Excluded()
    {
        var suppressions = new CountingSuppressionStore([Suppression("blocked-alias")]);
        var store = new ContentSourceStore(_databasePath, suppressions, new Lazy<ICreatorIdentityResolver>(() => Resolver("source-slug", "blocked-alias")));
        await InsertAsync(store, "source-slug", "Source Display Name");
        await InsertAsync(store, "control", "Control");

        var sources = await store.ListEnabledSourcesAsync();

        Assert.Equal(["control"], sources.Select(source => source.SourceSlug));
    }

    [Fact]
    public async Task SourceListEnabled_FilterWiredThroughRealResolver_CompletesWithoutReentry()
    {
        var suppressions = new CountingSuppressionStore([Suppression("blocked")]);
        ContentSourceStore? store = null;
        var resolver = new Lazy<ICreatorIdentityResolver>(() => new CreatorIdentityResolver(suppressions, store!, new ContentSiteIndexStore(_databasePath)));
        store = new ContentSourceStore(_databasePath, suppressions, resolver);
        await InsertAsync(store, "blocked", "Blocked");
        await InsertAsync(store, "allowed", "Allowed");
        var sources = await store.ListEnabledSourcesAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(["allowed"], sources.Select(source => source.SourceSlug));
    }

    [Fact]
    public async Task SourceListEnabled_BlankDisplayName_NotSuppressed()
    {
        var suppressions = new CountingSuppressionStore([Suppression("unrelated")]);
        var store = new ContentSourceStore(_databasePath, suppressions);
        var sourceId = await InsertAsync(store, "blank-name", "Temporary name");
        await SetBlankDisplayNameAsync(sourceId);
        var sources = await store.ListEnabledSourcesAsync();
        Assert.Single(sources);
        Assert.Equal("blank-name", sources[0].SourceSlug);
    }

    public void Dispose()
    {
        SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(_databasePath)}"));
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    private static CreatorStyleProfile Profile(string slug) => CreatorStyleProfileTestData.CreateFullProfile(slug);
    private static StatedRuleCandidate Rule() => new() { Category = "deckbuilding", Metric = "lands", Comparator = "lte", SourceClip = "test", Confidence = 0.9, VideoDateUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z") };
    private static MappingResolver Resolver(string representation, string displayName) => new(representation, new CreatorIdentity("canonical", [], [displayName], []));
    private static CreatorSuppression Suppression(string slug) => new() { Slug = slug, Aliases = [], Reason = "test", RequestedUtc = DateTimeOffset.UtcNow };
    private static Task<long> InsertAsync(ContentSourceStore store, string slug, string name) => store.InsertSourceAsync(slug, name, ContentSourceType.Podcast, $"https://example.test/{slug}");

    private async Task SetBlankDisplayNameAsync(long sourceId)
    {
        await using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(_databasePath)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE content_sources SET display_name = '' WHERE id = $id;";
        command.Parameters.AddWithValue("$id", sourceId);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class MappingResolver(string representation, CreatorIdentity identity) : ICreatorIdentityResolver
    {
        public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default) => Task.FromResult<CreatorIdentity?>(string.Equals(representation, anyRepresentation, StringComparison.OrdinalIgnoreCase) ? identity : null);
    }

    private sealed class CountingSuppressionStore(IReadOnlyList<CreatorSuppression> rows) : ICreatorSuppressionStore
    {
        public int ListCalls { get; private set; }
        public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default) { ListCalls++; return Task.FromResult(rows); }
        public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(0L);
        public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new CreatorSuppressionSnapshot(0, []));
        public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
