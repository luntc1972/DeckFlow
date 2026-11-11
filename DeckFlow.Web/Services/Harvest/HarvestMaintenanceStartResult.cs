namespace DeckFlow.Web.Services.Harvest;

/// <summary>Outcome of reserving and recording a maintenance start.</summary>
public enum HarvestMaintenanceStartResult
{
    /// <summary>The run was persisted and queued.</summary>
    Started,
    /// <summary>Another run is already active.</summary>
    AlreadyActive,
    /// <summary>The run could not be persisted and was not queued.</summary>
    NotRecorded
}
