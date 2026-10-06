namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Source which initiated a harvest run, stored as <c>manual</c> or <c>scheduled</c>
/// in <c>harvest_runs.trigger_source</c>. NULL means unknown or legacy origin and
/// counts as scheduled for per-kind anchors (D-08).
/// </summary>
public enum HarvestTriggerSource
{
    /// <summary>Operator-initiated run, stored as <c>manual</c>.</summary>
    Manual,

    /// <summary>Scheduler-initiated run, stored as <c>scheduled</c>.</summary>
    Scheduled
}
