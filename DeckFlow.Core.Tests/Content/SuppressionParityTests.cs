using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests.Content;

public sealed class SuppressionParityTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"suppression-parity-{Guid.NewGuid():N}.db");

    [Fact]
    public Task Parity_RawSlug_Suppressed() => AssertFilteredAsync("raw", "content-kb/other/entry.md", "raw", Identity("canonical"), true);

    [Fact]
    public Task Parity_ArtifactPathFolder_Suppressed() => AssertFilteredAsync("source", "creators/folder/entry.md", "folder", null, true);

    [Fact]
    public Task Parity_IdentityAlias_Suppressed() => AssertFilteredAsync("source", "content-kb/other/entry.md", "display", Identity("canonical", ["display"]), true);

    [Fact]
    public Task Parity_IdentityFolderSlug_Suppressed() => AssertFilteredAsync("source", "content-kb/other/entry.md", "folder", Identity("canonical", folders: ["folder"]), true);

    [Fact]
    public Task Parity_NullIdentity_RawFallbackSuppressed() => AssertFilteredAsync("fallback", "content-kb/other/entry.md", "fallback", null, true);

    [Fact]
    public Task Parity_IdentityDiffersFromSlug_CanonicalSuppressed() => AssertFilteredAsync("x", "content-kb/other/entry.md", "y", Identity("y"), true);

    [Fact]
    public Task Parity_NoRepresentationMatches_Allowed() => AssertFilteredAsync("source", "content-kb/path/entry.md", "unrelated", Identity("canonical", ["display"], ["folder"]), false);

    public void Dispose()
    {
        SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(_databasePath)}"));
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    private async Task AssertFilteredAsync(string source, string artifactPath, string suppressedValue, CreatorIdentity? identity, bool expectedSuppressed)
    {
        var suppressionStore = new SuppressionStore([Suppression(suppressedValue)]);
        var resolver = new Resolver();
        if (identity is not null) resolver.Identities[source] = identity;
        var store = new ContentSiteIndexStore(_databasePath, suppressionStore, new Lazy<ICreatorIdentityResolver>(() => resolver));
        await store.UpsertRowPreservingVisibilityAsync(Row(source, artifactPath));

        var rows = await store.GetApprovedRowsAsync();

        Assert.Equal(expectedSuppressed, rows.Count == 0);
    }

    private static ContentSiteIndexRow Row(string source, string artifactPath) => new()
    {
        Id = 0,
        Source = source,
        Title = source,
        VideoUrl = "https://example.test/video",
        YoutubeVideoId = "video",
        ArtifactPath = artifactPath,
        IndexedUtc = DateTimeOffset.UtcNow,
        ApprovalStatus = "approved",
        ArchetypeTags = [],
        BracketTags = [],
        CardCategoryTags = []
    };

    private static CreatorIdentity Identity(string canonical, IReadOnlyList<string>? names = null, IReadOnlyList<string>? folders = null)
        => new(canonical, [], names ?? [], folders ?? []);

    private static CreatorSuppression Suppression(string value) => new() { Slug = value, Aliases = [], Reason = "test", RequestedUtc = DateTimeOffset.UtcNow };

    private sealed class Resolver : ICreatorIdentityResolver
    {
        public Dictionary<string, CreatorIdentity> Identities { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Task<CreatorIdentity?> ResolveAsync(string representation, CancellationToken cancellationToken = default) => Task.FromResult(Identities.GetValueOrDefault(representation));
        public Task<IReadOnlyDictionary<string, CreatorIdentity?>> ResolveManyAsync(IReadOnlyCollection<string> representations, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, CreatorIdentity?>>(representations.ToDictionary(value => value, value => (CreatorIdentity?)Identities.GetValueOrDefault(value), StringComparer.OrdinalIgnoreCase));
    }

    private sealed class SuppressionStore(IReadOnlyList<CreatorSuppression> rows) : ICreatorSuppressionStore
    {
        public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);
        public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new CreatorSuppressionSnapshot(0, rows));
        public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);
        public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default) => Task.FromResult(rows.Any(row => row.Slug == nameOrAlias || row.Aliases.Contains(nameOrAlias)));
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
