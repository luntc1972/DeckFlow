namespace DeckFlow.Core.Content;

#pragma warning disable CS1591

/// <summary>Persists creator suppression requests.</summary>
public interface ICreatorSuppressionStore
{
    Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default);
    Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default);
    Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default);
    Task<long> GetRevisionAsync(CancellationToken cancellationToken = default);
    Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default);
    Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default);
    Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default);
    Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default);
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
}
#pragma warning restore CS1591
