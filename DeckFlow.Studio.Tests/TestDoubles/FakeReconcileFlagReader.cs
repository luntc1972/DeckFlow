using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Minimal <see cref="IProdContentReader"/> test double for
/// <see cref="DeckFlow.Studio.ViewModels.ReconcileCoordinator"/>'s <c>sync.reconcile</c> tri-state
/// flag dependency — kept separate from <see cref="FakeProdContentReader"/> (the pull-from-prod
/// read-all double) and <see cref="FakeDirectPushFlagReader"/> (the DirectPush flag double), mirroring
/// the established "one fake flag reader per coordinator" convention (90-04 Task 2 Interfaces note).
/// <see cref="ReadAllAsync"/> is unused by <c>ReconcileCoordinator</c> and throws if ever called.
/// </summary>
internal sealed class FakeReconcileFlagReader : IProdContentReader
{
    /// <summary>The canned flag value returned by <see cref="TryReadFlagAsync"/> when not indeterminate.</summary>
    public bool? FlagValue { get; set; }

    /// <summary>When <see langword="true"/>, <see cref="TryReadFlagAsync"/> returns <see langword="null"/>
    /// regardless of <see cref="FlagValue"/> — the indeterminate/read-failed signal.</summary>
    public bool FlagIndeterminate { get; set; }

    public Task<bool?> TryReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
        => Task.FromResult(FlagIndeterminate ? (bool?)null : FlagValue);

    public Task<bool> ReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("ReconcileCoordinator never calls the fail-closed ReadFlagAsync.");

    public Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(string connectionString, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("ReconcileCoordinator never calls ReadAllAsync on the flag reader.");
}
