namespace DeckFlow.Web.Services.Harvest;

/// <summary>Runs one operator-requested harvest maintenance operation at a time.</summary>
public interface IHarvestMaintenanceRunner
{
    /// <summary>Attempts to queue a maintenance operation.</summary>
    Task<HarvestMaintenanceStartResult> TryStartAsync(HarvestMaintenanceOperation operation, CancellationToken cancellationToken = default);

    /// <summary>Requests that the active maintenance operation stop.</summary>
    bool RequestStop();

    /// <summary>Gets the current or most recently completed operation status.</summary>
    HarvestMaintenanceStatus GetStatus();

    /// <summary>Gets current status or the latest persisted run after restart.</summary>
    Task<HarvestMaintenanceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
