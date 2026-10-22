namespace DeckFlow.Web.Services.Harvest;

/// <summary>Runs one operator-requested harvest maintenance operation at a time.</summary>
public interface IHarvestMaintenanceRunner
{
    /// <summary>Attempts to queue a maintenance operation.</summary>
    bool TryStart(HarvestMaintenanceOperation operation);

    /// <summary>Gets the current or most recently completed operation status.</summary>
    HarvestMaintenanceStatus GetStatus();
}
