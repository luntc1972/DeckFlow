using DeckFlow.Core.Content;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

internal sealed class FakeCreatorSuppressionStore : ICreatorSuppressionStore
{
    public ISet<string> Suppressed { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public int ListCalls { get; private set; }
    public int IsSuppressedCalls { get; private set; }

    public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);
    public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default)
    {
        ListCalls++;
        return Task.FromResult<IReadOnlyList<CreatorSuppression>>(Suppressed.Select(slug => new CreatorSuppression
        {
            Slug = slug,
            Aliases = Array.Empty<string>(),
            Reason = "test",
            RequestedUtc = DateTimeOffset.UtcNow,
        }).ToList());
    }
    public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default) { IsSuppressedCalls++; return Task.FromResult(Suppressed.Contains(nameOrAlias)); }
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeStudioProdConnectionSource : IStudioProdConnectionSource
{
    public string ConnectionString => string.Empty;
}

internal sealed class ThrowingCreatorSuppressionStore : ICreatorSuppressionStore
{
    private static readonly InvalidOperationException ReadFailure = new("Suppression store unavailable.");

    public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);
    public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => throw ReadFailure;
    public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => throw ReadFailure;
    public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default) => throw ReadFailure;
    public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default) => throw ReadFailure;
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class PerGroupThrowingCreatorSuppressionStore : ICreatorSuppressionStore
{
    private readonly FakeCreatorSuppressionStore _inner = new();

    public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => _inner.SuppressAsync(slug, aliases, reason, requestedUtc, note, cancellationToken);
    public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => _inner.UnsuppressAsync(slug, cancellationToken);
    public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => _inner.SetAliasesAsync(slug, aliases, cancellationToken);
    public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => _inner.GetRevisionAsync(cancellationToken);
    public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => _inner.GetSyncedRevisionAsync(cancellationToken);
    public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => _inner.ReadSnapshotAsync(cancellationToken);
    public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => _inner.ApplySnapshotAsync(snapshot, cancellationToken);
    public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default) => _inner.ListAsync(cancellationToken);
    public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Suppression store unavailable.");
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => _inner.EnsureSchemaAsync(cancellationToken);
}

internal sealed class FakeCreatorIdentityResolver : ICreatorIdentityResolver
{
    public Dictionary<string, CreatorIdentity> Identities { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default)
        => Task.FromResult(Identities.GetValueOrDefault(anyRepresentation));
}
