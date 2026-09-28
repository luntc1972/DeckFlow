using DeckFlow.Web.Services.Manabase;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class ManabaseStageTrackerTests
{
    [Fact]
    public void StartStage_WhileInFlight_ReportsCurrentStage()
    {
        var tracker = new ManabaseStageTracker();

        tracker.StartStage("card resolution");

        Assert.Equal("card resolution", tracker.CurrentStage);
    }

    [Fact]
    public void FinishStage_AfterStart_RecordsElapsedTime()
    {
        var tracker = new ManabaseStageTracker();
        tracker.StartStage("card resolution");

        tracker.FinishStage();

        var completedStage = Assert.Single(tracker.CompletedStages);
        Assert.Equal("card resolution", completedStage.Key);
        Assert.True(completedStage.Value >= 0);
    }

    [Fact]
    public void CompletedStagesSummary_AfterMultipleStages_FormatsStageNamesAndDurations()
    {
        var tracker = new ManabaseStageTracker();
        tracker.StartStage("card resolution");
        tracker.FinishStage();
        tracker.StartStage("bracket classification");
        tracker.FinishStage();

        Assert.Contains("card resolution", tracker.CompletedStagesSummary);
        Assert.Contains("bracket classification", tracker.CompletedStagesSummary);
        Assert.Contains("ms", tracker.CompletedStagesSummary);
    }
}
