using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DeckFlow.Web.Services.Harvest;
using Xunit;

namespace DeckFlow.Web.Tests.Services;

public sealed class HarvestMaintenanceRolloutGateTests
{
    [Theory]
    [InlineData(HarvestMaintenanceOperation.Drain, "null", "preview_unavailable")]
    [InlineData(HarvestMaintenanceOperation.Drain, "pending", "marker_backfill_pending")]
    [InlineData(HarvestMaintenanceOperation.Drain, "purge", "purge_pending")]
    [InlineData(HarvestMaintenanceOperation.Drain, "clean", null)]
    [InlineData(HarvestMaintenanceOperation.Drain, "retention", null)]
    [InlineData(HarvestMaintenanceOperation.Refetch, "null", "preview_unavailable")]
    [InlineData(HarvestMaintenanceOperation.Refetch, "pending", "marker_backfill_pending")]
    [InlineData(HarvestMaintenanceOperation.Refetch, "purge", "purge_pending")]
    [InlineData(HarvestMaintenanceOperation.Refetch, "retention", "retention_pending")]
    [InlineData(HarvestMaintenanceOperation.Refetch, "clean", null)]
    [InlineData(HarvestMaintenanceOperation.Purge, "null", null)]
    [InlineData(HarvestMaintenanceOperation.Backfill, "null", null)]
    public void Evaluate_OperationAndPreview_ReturnsReason(HarvestMaintenanceOperation operation, string previewKind, string? expected)
    {
        var preview = previewKind == "null" ? null : new HarvestMaintenancePreview(previewKind == "retention" ? 7 : 0, previewKind == "purge" ? 5 : 0, 0) { MarkerBackfillPending = previewKind == "pending" };
        Assert.Equal(expected, HarvestMaintenanceRolloutGate.Evaluate(operation, preview));
    }
}
