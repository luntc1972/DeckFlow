using DeckFlow.Core.Content;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// In-memory test fake for <see cref="IContentKbReconcileOrchestrator"/>. Returns a seeded
/// <see cref="ReconcileDryRunResult"/> from <see cref="RunDryRunAsync"/> and records the call count
/// and the scope tag it was invoked with, so tests can assert <see cref="ReconcileCoordinator"/>
/// delegates without transformation and passes the scope tag through unchanged.
/// </summary>
internal sealed class FakeContentKbReconcileOrchestrator : IContentKbReconcileOrchestrator
{
    /// <summary>The result <see cref="RunDryRunAsync"/> returns.</summary>
    public ReconcileDryRunResult Result { get; set; } = new(true, Array.Empty<ContentKbReconcileDiscrepancy>());

    /// <summary>Number of times <see cref="RunDryRunAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <summary>The scope tag most recently passed to <see cref="RunDryRunAsync"/>.</summary>
    public string? LastScopeTag { get; private set; }

    public Task<ReconcileDryRunResult> RunDryRunAsync(string scopeTag, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastScopeTag = scopeTag;
        return Task.FromResult(Result);
    }
}
