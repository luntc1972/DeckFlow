using System.Threading.Channels;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Enforces the maintenance rollout order using the runner's preview.</summary>
internal static class HarvestMaintenanceRolloutGate
{
    /// <summary>Returns the refusal token, or null when this operation may run.</summary>
    internal static string? Evaluate(HarvestMaintenanceOperation operation, HarvestMaintenancePreview? preview)
    {
        if (operation is HarvestMaintenanceOperation.Purge or HarvestMaintenanceOperation.Backfill) return null;
        if (preview is null) return "preview_unavailable";
        if (preview.MarkerBackfillPending) return "marker_backfill_pending";
        if (preview.PurgeCandidates > 0) return "purge_pending";
        if (operation == HarvestMaintenanceOperation.Refetch && preview.RetentionExcessDecks > 0) return "retention_pending";
        return null;
    }
}
