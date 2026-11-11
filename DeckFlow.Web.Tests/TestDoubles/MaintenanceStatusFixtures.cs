using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services.Harvest;

namespace DeckFlow.Web.Tests.TestDoubles;

internal static class MaintenanceStatusFixtures
{
    internal static DateTimeOffset CanonicalNow(string token) => new(2026, 11, 11, 7,
        token switch { "starting" => 1, "running" or "stop-requested" => 11, "stalled" => 12, _ => 30 },
        token == "stalled" ? 1 : 0, TimeSpan.Zero);

    internal static HarvestMaintenanceStatus For(string token, DateTimeOffset nowUtc)
    {
        if (token == "idle") return HarvestMaintenanceStatus.Idle;
        var start = token switch
        {
            "starting" => nowUtc.AddMinutes(-1),
            "running" or "stop-requested" => nowUtc.AddMinutes(-11),
            "stalled" => nowUtc.AddMinutes(-12).AddSeconds(-1),
            _ => nowUtc.AddMinutes(-30)
        };
        var active = token is "starting" or "running" or "stalled" or "stop-requested";
        return new HarvestMaintenanceStatus
        {
            State = token switch
            {
                "starting" => HarvestMaintenanceRunState.Starting,
                "running" or "stalled" or "stop-requested" => HarvestMaintenanceRunState.Running,
                "stopped" => HarvestMaintenanceRunState.Stopped,
                "interrupted" => HarvestMaintenanceRunState.Interrupted,
                "failed" => HarvestMaintenanceRunState.Failed,
                "refused" => HarvestMaintenanceRunState.Refused,
                _ => HarvestMaintenanceRunState.Finished
            },
            Operation = HarvestMaintenanceOperation.Drain,
            StartedUtc = start,
            LastProgressUtc = token is "starting" or "refused" ? null : start.AddMinutes(10),
            EndedUtc = active ? null : start.AddMinutes(20),
            DecksAffected = token is "starting" or "refused" ? 0 : 500,
            Batches = token is "starting" or "refused" ? 0 : 20,
            StopRequested = token == "stop-requested",
            RefusalReason = token == "refused" ? "purge_pending" : null,
            PreviewAtStart = token == "refused" ? new(0, 5, 0) : new(1500, 0, 0),
            PreviewAsOfUtc = start,
            StopReason = token switch
            {
                "disk-probe-unavailable" => DeckPruningStopReason.DiskProbeUnavailable,
                "disk-floor" => DeckPruningStopReason.DiskFloor,
                "marker-backfill-pending" => DeckPruningStopReason.MarkerBackfillPending,
                "no-progress" => DeckPruningStopReason.MaxDecksReached,
                _ => DeckPruningStopReason.Completed
            }
        };
    }
}
