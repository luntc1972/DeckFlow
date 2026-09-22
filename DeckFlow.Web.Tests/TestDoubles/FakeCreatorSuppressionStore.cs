using DeckFlow.Core.Content;

namespace DeckFlow.Web.Tests;

internal sealed class FakeCreatorSuppressionStore : ICreatorSuppressionStore
{
    public ISet<string> Suppressed { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public Exception? ReadException { get; set; }

    public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);
    public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CreatorSuppression>>([]);
    public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default)
    {
        if (ReadException is not null)
        {
            throw ReadException;
        }

        return Task.FromResult(Suppressed.Contains(nameOrAlias));
    }

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeCreatorIdentityResolver : ICreatorIdentityResolver
{
    public Dictionary<string, CreatorIdentity> Identities { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ThrowOn { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default)
    {
        if (string.Equals(anyRepresentation, ThrowOn, StringComparison.OrdinalIgnoreCase))
        {
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            throw new CreatorAliasConflictException(anyRepresentation, "first, second");
        }

        return Task.FromResult(Identities.GetValueOrDefault(anyRepresentation));
    }
}
