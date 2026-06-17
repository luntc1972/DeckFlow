using DeckFlow.Core.Orchestration;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Bridges the synchronous <see cref="IOrchestratorProgress"/> contract to an async Blazor
/// <c>InvokeAsync(StateHasChanged)</c> sink.
/// </summary>
/// <remarks>
/// <see cref="IOrchestratorProgress.Report"/> is <c>void</c> by design — the Core contract
/// deliberately avoids async to prevent reordering of progress messages (Phase 42 D-08).
/// This bridge fire-and-forgets the async sink delegate; not awaiting inside Report is
/// intentional, not a bug.
/// </remarks>
internal sealed class ActionOrchestratorProgress : IOrchestratorProgress
{
    private readonly Func<string, Task> _sink;

    /// <summary>
    /// Initialises a new <see cref="ActionOrchestratorProgress"/>.
    /// </summary>
    /// <param name="sink">
    /// Async delegate invoked with each progress message; typically wraps
    /// <c>InvokeAsync(StateHasChanged)</c> on the Blazor component.
    /// </param>
    internal ActionOrchestratorProgress(Func<string, Task> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
    }

    /// <summary>
    /// Reports a progress message by fire-and-forgetting the async sink.
    /// </summary>
    /// <param name="message">Progress message from the orchestrator.</param>
    public void Report(string message)
    {
        // Why: Report is synchronous by design (OrchestratorProgress.cs contract).
        // Fire-and-forget the async StateHasChanged bridge; we cannot await here
        // because Report() is void and cannot be made async without changing the
        // IOrchestratorProgress contract.
        _ = _sink(message);
    }
}
