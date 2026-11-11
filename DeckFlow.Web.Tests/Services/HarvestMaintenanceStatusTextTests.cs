using DeckFlow.Web.Services.Harvest;
using DeckFlow.Web.Tests.TestDoubles;
using Xunit;

namespace DeckFlow.Web.Tests.Services;

public sealed class HarvestMaintenanceStatusTextTests
{
    [Theory]
    [InlineData("idle", "info", "Preview counts before each rollout step.")]
    [InlineData("starting", "info", "Starting Retention drain…")]
    [InlineData("running", "info", "Retention drain running.")]
    [InlineData("stalled", "warning", "Retention drain: no progress for more than 2 minutes.")]
    [InlineData("stop-requested", "warning", "Stop requested — finishing current batch.")]
    [InlineData("completed", "success", "Retention drain: Done.")]
    [InlineData("disk-probe-unavailable", "warning", "Retention drain: Disk probe failed (transient Render API error); safe to restart.")]
    [InlineData("disk-floor", "danger", "Retention drain: Stopped at disk floor; free space needed.")]
    [InlineData("marker-backfill-pending", "warning", "Retention drain: waits for the marker backfill. Run Marker backfill first.")]
    [InlineData("no-progress", "warning", "Retention drain: a chunk changed no decks; see the logs, then press Retention drain again.")]
    [InlineData("stopped", "info", "Stopped by operator at 2026-11-11 07:20:00 UTC.")]
    [InlineData("interrupted", "warning", "Retention drain interrupted by restart at 2026-11-11 07:20:00 UTC, 500 decks done — press Retention drain to resume.")]
    [InlineData("failed", "danger", "Retention drain failed after 500 decks; see the logs.")]
    [InlineData("refused", "danger", "Retention drain refused: run the purge first; 5 decks still match it. Nothing was changed.")]
    public void Describe_State_ReturnsTokenToneAndHeadline(string token, string tone, string headline)
    {
        var now = MaintenanceStatusFixtures.CanonicalNow(token);
        var status = MaintenanceStatusFixtures.For(token, now);
        var view = HarvestMaintenanceStatusText.Describe(status, now);
        Assert.Equal(token, view.State);
        Assert.Equal(tone, view.Tone);
        Assert.Equal(headline, view.Headline);
        Assert.Equal(status.IsRunning, view.IsActive);
    }

    [Fact]
    public void Describe_Running_ShowsBatchRateAndRemaining()
    {
        var now = MaintenanceStatusFixtures.CanonicalNow("running");
        Assert.Equal(new[] { "Started 2026-11-11 07:00:00 UTC.", "500 decks in 20 batches; last batch 07:10:00 UTC.", "Rate: 50.0 decks/min.", "Remaining: about 1000 of 1500 decks (Preview counts as of 07:00:00 UTC)." },
            HarvestMaintenanceStatusText.Describe(MaintenanceStatusFixtures.For("running", now), now).Details);
    }

    [Fact]
    public void Describe_StallBoundary_TwoMinutesIsRunningMoreIsStalled()
    {
        var now = MaintenanceStatusFixtures.CanonicalNow("running");
        var status = MaintenanceStatusFixtures.For("running", now);
        Assert.Equal("running", HarvestMaintenanceStatusText.Describe(status, status.LastProgressUtc!.Value.AddMinutes(2)).State);
        var stalled = HarvestMaintenanceStatusText.Describe(status, status.LastProgressUtc.Value.AddMinutes(2).AddSeconds(1));
        Assert.Equal("stalled", stalled.State);
        Assert.Equal("It may be ranking candidates or waiting for a fresh disk sample; pressing Stop is safe.", stalled.Details[^1]);
        var starting = status with { State = HarvestMaintenanceRunState.Starting, LastProgressUtc = null, Batches = 0 };
        var view = HarvestMaintenanceStatusText.Describe(starting, starting.StartedUtc!.Value.AddMinutes(2).AddSeconds(1));
        Assert.Equal("stalled", view.State);
        Assert.Contains("No batch finished yet.", view.Details);
        Assert.Equal(stalled.Details[^1], view.Details[^1]);
    }

    [Fact]
    public void Describe_StopRequestedWhileStalled_ShowsStopRequested()
    {
        var now = MaintenanceStatusFixtures.CanonicalNow("stalled");
        Assert.Equal("stop-requested", HarvestMaintenanceStatusText.Describe(MaintenanceStatusFixtures.For("stalled", now) with { StopRequested = true }, now).State);
    }

    [Fact]
    public void Label_EveryOperation_ReturnsOperatorLabel()
    {
        Assert.Equal("Marker backfill", HarvestMaintenanceStatusText.Label(HarvestMaintenanceOperation.Backfill));
        Assert.Equal("Purge", HarvestMaintenanceStatusText.Label(HarvestMaintenanceOperation.Purge));
        Assert.Equal("Retention drain", HarvestMaintenanceStatusText.Label(HarvestMaintenanceOperation.Drain));
        Assert.Equal("Refetch", HarvestMaintenanceStatusText.Label(HarvestMaintenanceOperation.Refetch));
    }

    [Fact]
    public void Describe_BackfillRunning_HasNoRemainingLine()
    {
        var now = MaintenanceStatusFixtures.CanonicalNow("running");
        var status = MaintenanceStatusFixtures.For("running", now) with { Operation = HarvestMaintenanceOperation.Backfill, Batches = 0 };
        Assert.Equal(new[] { "Started 2026-11-11 07:00:00 UTC.", "500 decks; last update 07:10:00 UTC." }, HarvestMaintenanceStatusText.Describe(status, now).Details);
    }

    [Theory]
    [InlineData("preview_unavailable", "Retention drain refused: maintenance counts are unavailable. Nothing was changed.")]
    [InlineData("marker_backfill_pending", "Retention drain refused: run Marker backfill first; maintenance counts wait for it. Nothing was changed.")]
    [InlineData("purge_pending", "Retention drain refused: run the purge first; 5 decks still match it. Nothing was changed.")]
    [InlineData("retention_pending", "Refetch refused: finish the retention drain first; 7 decks are still over the per-commander cap. Nothing was changed.")]
    public void Describe_Refused_EveryReasonNamesItsFix(string reason, string headline)
    {
        var now = MaintenanceStatusFixtures.CanonicalNow("refused");
        var status = MaintenanceStatusFixtures.For("refused", now) with { RefusalReason = reason, Operation = reason == "retention_pending" ? HarvestMaintenanceOperation.Refetch : HarvestMaintenanceOperation.Drain, PreviewAtStart = new(7, 5, 0) };
        Assert.Equal(headline, HarvestMaintenanceStatusText.Describe(status, now).Headline);
    }

    [Fact]
    public void Describe_Fallbacks_InterruptedWithoutEndAndUnknownRefusal()
    {
        var now = MaintenanceStatusFixtures.CanonicalNow("interrupted");
        var status = MaintenanceStatusFixtures.For("interrupted", now) with { EndedUtc = null };
        Assert.Equal("Retention drain interrupted by restart at 2026-11-11 07:10:00 UTC, 500 decks done — press Retention drain to resume.", HarvestMaintenanceStatusText.Describe(status, now).Headline);
        Assert.Equal("Retention drain refused. Nothing was changed.", HarvestMaintenanceStatusText.Describe(status with { State = HarvestMaintenanceRunState.Refused, RefusalReason = "unknown" }, now).Headline);
    }
}
