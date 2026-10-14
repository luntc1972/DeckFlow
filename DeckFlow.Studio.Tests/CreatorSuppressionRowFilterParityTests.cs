using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;
using Xunit;

namespace DeckFlow.Studio.Tests;

public sealed class CreatorSuppressionRowFilterParityTests
{
    [Fact]
    public Task Parity_RawSlug_Suppressed() => AssertParityAsync("raw", "content-kb/other/entry.md", "raw", Identity("canonical"), true);

    [Fact]
    public Task Parity_ArtifactPathFolder_Suppressed() => AssertParityAsync("source", "creators/folder/entry.md", "folder", null, true);

    [Fact]
    public Task Parity_IdentityAlias_Suppressed() => AssertParityAsync("source", "content-kb/other/entry.md", "display", Identity("canonical", ["display"]), true);

    [Fact]
    public Task Parity_IdentityFolderSlug_Suppressed() => AssertParityAsync("source", "content-kb/other/entry.md", "folder", Identity("canonical", folders: ["folder"]), true);

    [Fact]
    public Task Parity_NullIdentity_RawFallbackSuppressed() => AssertParityAsync("fallback", "content-kb/other/entry.md", "fallback", null, true);

    [Fact]
    public Task Parity_IdentityDiffersFromSlug_CanonicalSuppressed() => AssertParityAsync("x", "content-kb/other/entry.md", "y", Identity("y"), true);

    [Fact]
    public Task Parity_NoRepresentationMatches_Allowed() => AssertParityAsync("source", "content-kb/path/entry.md", "unrelated", Identity("canonical", ["display"], ["folder"]), false);

    private static async Task AssertParityAsync(string source, string artifactPath, string suppressedValue, CreatorIdentity? identity, bool expectedSuppressed)
    {
        var resolver = new Resolver();
        if (identity is not null) resolver.Identities[source] = identity;
        var filter = new CreatorSuppressionRowFilter(new SuppressionStore([Suppression(suppressedValue)]), resolver);
        var row = Row(source, artifactPath);

        var isSuppressed = await filter.IsSuppressedAsync(row, CancellationToken.None);
        var allowed = await filter.GetAllowedAsync([row], CancellationToken.None);

        Assert.Equal(expectedSuppressed, isSuppressed);
        Assert.Equal(!expectedSuppressed, allowed.Contains(row));
    }

    private static ContentSiteIndexRow Row(string source, string artifactPath) => new()
    {
        Id = 0,
        Source = source,
        Title = source,
        VideoUrl = "https://example.test/video",
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
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
