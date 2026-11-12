using System.Globalization;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Provides the canonical operator text for maintenance runs.</summary>
public static class HarvestMaintenanceStatusText
{
    /// <summary>The headline shown after an operator requests a stop.</summary>
    public const string StopRequestedHeadline = "Stop requested — finishing current batch.";

    /// <summary>Returns the operator label for an operation.</summary>
    public static string Label(HarvestMaintenanceOperation? operation) => operation switch
    {
        HarvestMaintenanceOperation.Backfill => "Marker backfill",
        HarvestMaintenanceOperation.Purge => "Purge",
        HarvestMaintenanceOperation.Drain => "Retention drain",
        HarvestMaintenanceOperation.Refetch => "Refetch",
        null => "Maintenance",
        _ => operation.Value.ToString(),
    };

    /// <summary>Returns the message for a duplicate start request.</summary>
    public static string AlreadyActive(HarvestMaintenanceStatus status) => status.State switch
    {
        HarvestMaintenanceRunState.Starting => $"{Label(status.Operation)} is already starting. Nothing new was started.",
        HarvestMaintenanceRunState.Running => $"{Label(status.Operation)} is already running. Nothing new was started.",
        _ => "A maintenance run is already starting or running. Nothing new was started.",
    };

    /// <summary>Describes a run at the supplied UTC time.</summary>
    public static HarvestMaintenanceStatusView Describe(HarvestMaintenanceStatus status, DateTimeOffset nowUtc)
    {
        if (status.State is null) return new("idle", "info", "Preview counts before each rollout step.", Array.Empty<string>(), false);

        var label = Label(status.Operation);
        var end = status.EndedUtc ?? status.LastProgressUtc ?? status.StartedUtc;
        var state = status.State switch
        {
            _ when status.IsRunning && status.StopRequested => "stop-requested",
            _ when status.IsRunning && nowUtc - (status.LastProgressUtc ?? status.StartedUtc) > TimeSpan.FromMinutes(2) => "stalled",
            HarvestMaintenanceRunState.Starting => "starting",
            HarvestMaintenanceRunState.Running => "running",
            HarvestMaintenanceRunState.Finished => status.StopReason switch
            {
                DeckPruningStopReason.DiskProbeUnavailable => "disk-probe-unavailable",
                DeckPruningStopReason.DiskFloor => "disk-floor",
                DeckPruningStopReason.MarkerBackfillPending => "marker-backfill-pending",
                DeckPruningStopReason.MaxDecksReached => "no-progress",
                _ => "completed",
            },
            HarvestMaintenanceRunState.Stopped => "stopped",
            HarvestMaintenanceRunState.Interrupted => "interrupted",
            HarvestMaintenanceRunState.Failed => "failed",
            _ => "refused",
        };
        var tone = state switch
        {
            "completed" => "success",
            "disk-floor" or "failed" or "refused" => "danger",
            "stalled" or "stop-requested" or "disk-probe-unavailable" or "marker-backfill-pending" or "no-progress" or "interrupted" => "warning",
            _ => "info",
        };
        var headline = state switch
        {
            "starting" => $"Starting {label}…",
            "running" => $"{label} running.",
            "stalled" => $"{label}: no progress for more than 2 minutes.",
            "stop-requested" => StopRequestedHeadline,
            "completed" => $"{label}: Done.",
            "disk-probe-unavailable" => $"{label}: Disk probe failed (transient Render API error); safe to restart.",
            "disk-floor" => $"{label}: Stopped at disk floor; free space needed.",
            "marker-backfill-pending" => $"{label}: waits for the marker backfill. Run Marker backfill first.",
            "no-progress" => $"{label}: a chunk changed no decks; see the logs, then press {label} again.",
            "stopped" => $"Stopped by operator at {Time(end)}.",
            "interrupted" => FormattableString.Invariant($"{label} interrupted by restart at {Time(end)}, {status.DecksAffected} decks done — press {label} to resume."),
            "failed" => FormattableString.Invariant($"{label} failed after {status.DecksAffected} decks; see the logs."),
            _ => Refusal(status, label),
        };
        var details = new List<string> { $"Started {Time(status.StartedUtc)}." };
        if (state != "refused")
        {
            details.Add(status.LastProgressUtc is null ? "No batch finished yet." : status.Batches > 0
                ? FormattableString.Invariant($"{status.DecksAffected} decks in {status.Batches} batches; last batch {Clock(status.LastProgressUtc)}.")
                : FormattableString.Invariant($"{status.DecksAffected} decks; last update {Clock(status.LastProgressUtc)}."));
        }

        if (status.IsRunning && status.Batches > 0)
        {
            var minutes = Math.Max(((status.LastProgressUtc ?? status.StartedUtc) - status.StartedUtc)?.TotalMinutes ?? 0, 1.0 / 60);
            details.Add(FormattableString.Invariant($"Rate: {status.DecksAffected / minutes:0.0} decks/min."));
        }

        if (status.IsRunning && status.PreviewAtStart is { } preview && status.Operation != HarvestMaintenanceOperation.Backfill)
        {
            var total = status.Operation switch
            {
                HarvestMaintenanceOperation.Drain => preview.RetentionExcessDecks,
                HarvestMaintenanceOperation.Purge => preview.PurgeCandidates,
                _ => preview.RefetchCandidates,
            };
            details.Add(FormattableString.Invariant($"Remaining: about {Math.Max(0, total - status.DecksAffected)} of {total} decks (Preview counts as of {Clock(status.PreviewAsOfUtc)})."));
        }

        if (state == "stalled") details.Add("It may be ranking candidates or waiting for a fresh disk sample; pressing Stop is safe.");
        if (state is "completed" or "disk-probe-unavailable" or "disk-floor" or "marker-backfill-pending" or "no-progress" or "failed" or "refused") details.Add($"Ended {Time(status.EndedUtc)}.");
        if (state == "stopped") details.Add($"Press {label} to resume; completed batches are not repeated.");
        return new(state, tone, headline, details, status.IsRunning);
    }

    private static string Time(DateTimeOffset? value) => value?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "";
    private static string Clock(DateTimeOffset? value) => value?.UtcDateTime.ToString("HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "";

    private static string Refusal(HarvestMaintenanceStatus status, string label) => status.RefusalReason switch
    {
        "preview_unavailable" => $"{label} refused: maintenance counts are unavailable. Nothing was changed.",
        "marker_backfill_pending" => $"{label} refused: run Marker backfill first; maintenance counts wait for it. Nothing was changed.",
        "purge_pending" => FormattableString.Invariant($"{label} refused: run the purge first; {status.PreviewAtStart?.PurgeCandidates ?? 0} decks still match it. Nothing was changed."),
        "retention_pending" => FormattableString.Invariant($"{label} refused: finish the retention drain first; {status.PreviewAtStart?.RetentionExcessDecks ?? 0} decks are still over the per-commander cap. Nothing was changed."),
        _ => $"{label} refused. Nothing was changed.",
    };
}
