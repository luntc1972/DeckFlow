using DeckFlow.Core.Orchestration;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Proves Studio can resolve the Content KB maintenance slice from DeckFlow.Core orchestration.
/// </summary>
public sealed class ContentKbOrchestratorSmokeService
{
    private readonly IContentMaintenanceOrchestrator _maintenanceOrchestrator;

    /// <summary>
    /// Initializes the smoke service with the maintenance slice forwarded from the core orchestrator.
    /// </summary>
    /// <param name="maintenanceOrchestrator">Read-only maintenance slice used for the blocked-video probe.</param>
    public ContentKbOrchestratorSmokeService(IContentMaintenanceOrchestrator maintenanceOrchestrator)
    {
        ArgumentNullException.ThrowIfNull(maintenanceOrchestrator);
        _maintenanceOrchestrator = maintenanceOrchestrator;
    }

    /// <summary>
    /// Executes a read-only blocked-video probe and returns the blocked row count.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of blocked-video rows returned by the orchestrator slice.</returns>
    public async Task<int> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var result = await _maintenanceOrchestrator
            .ListBlockedAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Items.Count;
    }
}
