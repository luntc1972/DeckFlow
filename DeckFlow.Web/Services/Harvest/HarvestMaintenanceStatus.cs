using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Describes the current or most recently completed maintenance operation.</summary>
public sealed record HarvestMaintenanceStatus(
    bool IsRunning,
    HarvestMaintenanceOperation? Operation,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc,
    int DecksAffected,
    DeckPruningStopReason? StopReason,
    bool StopRequested,
    bool Failed)
{
    /// <summary>Gets the status before any maintenance operation has run.</summary>
    public static HarvestMaintenanceStatus Idle { get; } = new(false, null, null, null, 0, null, false, false);
}
