namespace DeckFlow.Web.Services.Harvest;

/// <summary>Identifies the lifecycle state of a maintenance run.</summary>
public enum HarvestMaintenanceRunState
{
    /// <summary>Identifies a starting run.</summary>
    Starting,
    /// <summary>Identifies a running run.</summary>
    Running,
    /// <summary>Identifies a finished run.</summary>
    Finished,
    /// <summary>Identifies a stopped run.</summary>
    Stopped,
    /// <summary>Identifies a interrupted run.</summary>
    Interrupted,
    /// <summary>Identifies a failed run.</summary>
    Failed,
    /// <summary>Identifies a refused run.</summary>
    Refused,
}
