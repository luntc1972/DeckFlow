using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Describes the current or most recent maintenance operation.</summary>
public sealed record HarvestMaintenanceStatus
{
    /// <summary>Gets the unique run identifier.</summary>
    public Guid? RunId { get; init; }

    /// <summary>Gets the lifecycle state, or null before any run.</summary>
    public HarvestMaintenanceRunState? State { get; init; }

    /// <summary>Gets the maintenance operation.</summary>
    public HarvestMaintenanceOperation? Operation { get; init; }

    /// <summary>Gets the start time.</summary>
    public DateTimeOffset? StartedUtc { get; init; }

    /// <summary>Gets the last progress time.</summary>
    public DateTimeOffset? LastProgressUtc { get; init; }

    /// <summary>Gets the end time.</summary>
    public DateTimeOffset? EndedUtc { get; init; }

    /// <summary>Gets the number of decks affected.</summary>
    public int DecksAffected { get; init; }

    /// <summary>Gets the number of completed batches.</summary>
    public int Batches { get; init; }

    /// <summary>Gets the pruning stop reason.</summary>
    public DeckPruningStopReason? StopReason { get; init; }

    /// <summary>Gets the whether the operator requested Stop.</summary>
    public bool StopRequested { get; init; }

    /// <summary>Gets the rollout refusal reason.</summary>
    public string? RefusalReason { get; init; }

    /// <summary>Gets the counts sampled at start.</summary>
    public HarvestMaintenancePreview? PreviewAtStart { get; init; }

    /// <summary>Gets the preview sample time.</summary>
    public DateTimeOffset? PreviewAsOfUtc { get; init; }

    /// <summary>Gets whether the run is active.</summary>
    public bool IsRunning => State is HarvestMaintenanceRunState.Starting or HarvestMaintenanceRunState.Running;

    /// <summary>Gets the status before any maintenance operation has run.</summary>
    public static HarvestMaintenanceStatus Idle { get; } = new();
}
