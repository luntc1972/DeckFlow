using DeckFlow.Core.Content;
using DeckFlow.Web.Services.CreatorStyle;
using Xunit;

namespace DeckFlow.Web.Tests.Services.CreatorStyle;

public sealed class CreatorSuppressionGateParityTests
{
    [Fact]
    public Task Parity_RawSlug_Suppressed() => AssertParityAsync("raw", "raw", Identity("canonical"), true);

    [Fact]
    public Task Parity_IdentityAlias_Suppressed() => AssertParityAsync("source", "display", Identity("canonical", ["display"]), true);

    [Fact]
    public Task Parity_IdentityFolderSlug_Suppressed() => AssertParityAsync("source", "folder", Identity("canonical", folders: ["folder"]), true);

    [Fact]
    public Task Parity_NullIdentity_RawFallbackSuppressed() => AssertParityAsync("fallback", "fallback", null, true);

    [Fact]
    public Task Parity_IdentityDiffersFromSlug_CanonicalSuppressed() => AssertParityAsync("x", "y", Identity("y"), true);

    [Fact]
    public Task Parity_NoRepresentationMatches_Allowed() => AssertParityAsync("source", "unrelated", Identity("canonical", ["display"], ["folder"]), false);

    private static async Task AssertParityAsync(string slug, string suppressedValue, CreatorIdentity? identity, bool expectedSuppressed)
    {
        var resolver = new Resolver();
        if (identity is not null) resolver.Identities[slug] = identity;
        var gate = new CreatorSuppressionGate(resolver, new SuppressionStore([Suppression(suppressedValue)]));

        var isSuppressed = await gate.IsSuppressedAsync(slug);
        var unsuppressed = await gate.GetUnsuppressedAsync([slug]);

        Assert.Equal(expectedSuppressed, isSuppressed);
        Assert.Equal(!expectedSuppressed, unsuppressed.Contains(slug));
    }

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
        public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default) => Task.FromResult(rows.Any(row => row.Slug == nameOrAlias || row.Aliases.Contains(nameOrAlias)));
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
