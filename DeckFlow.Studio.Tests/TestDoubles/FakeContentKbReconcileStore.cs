using DeckFlow.Core.Content;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// In-memory test fake for <see cref="IContentKbReconcileStore"/>. Seed <see cref="OpenDiscrepancies"/>
/// for <see cref="GetOpenAsync"/> to return; <see cref="EnsureSchemaCallCount"/> and
/// <see cref="PersistRunCallCount"/> let a test prove a caller (e.g.
/// <see cref="ReconcileCoordinator"/>) never writes to the store directly — all persistence is the
/// orchestrator's responsibility (91-06), so a coordinator-level dry-run call must never touch this
/// store's write surface.
/// </summary>
internal sealed class FakeContentKbReconcileStore : IContentKbReconcileStore
{
    /// <summary>Seeded rows returned by <see cref="GetOpenAsync"/> (optionally filtered by scope tag).</summary>
    public List<StoredReconcileDiscrepancy> OpenDiscrepancies { get; } = new();

    /// <summary>Number of times <see cref="EnsureSchemaAsync"/> was called.</summary>
    public int EnsureSchemaCallCount { get; private set; }

    /// <summary>Number of times <see cref="PersistRunAsync"/> was called.</summary>
    public int PersistRunCallCount { get; private set; }

    /// <summary>Number of times <see cref="GetOpenAsync"/> was called.</summary>
    public int GetOpenCallCount { get; private set; }

    /// <summary>The scope tag most recently passed to <see cref="GetOpenAsync"/>.</summary>
    public string? LastGetOpenScopeTag { get; private set; }

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        EnsureSchemaCallCount++;
        return Task.CompletedTask;
    }

    public Task PersistRunAsync(
        string scopeTag,
        IReadOnlyList<ContentKbReconcileDiscrepancy> seen,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        PersistRunCallCount++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredReconcileDiscrepancy>> GetOpenAsync(
        string? scopeTag,
        CancellationToken cancellationToken = default)
    {
        GetOpenCallCount++;
        LastGetOpenScopeTag = scopeTag;
        var results = scopeTag is null
            ? OpenDiscrepancies.ToList()
            : OpenDiscrepancies.Where(d => string.Equals(d.ScopeTag, scopeTag, StringComparison.Ordinal)).ToList();
        return Task.FromResult<IReadOnlyList<StoredReconcileDiscrepancy>>(results);
    }
}
