using DeckFlow.Core.Content;

namespace DeckFlow.Web.Tests;

internal sealed class FakeCreatorSuppressionStore : ICreatorSuppressionStore
{
    public ISet<string> Suppressed { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public List<CreatorSuppression> Rows { get; } = [];

    public Exception? ReadException { get; set; }

    public long Revision { get; set; }

    public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult(Revision);
    public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);
    public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public int ListCallCount { get; private set; }

    public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default)
    {
        ListCallCount++;
        if (ReadException is not null)
        {
            throw ReadException;
        }

        return Task.FromResult<IReadOnlyList<CreatorSuppression>>(Rows);
    }
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
