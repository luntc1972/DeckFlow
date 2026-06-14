using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the per-step completeness rules the deck-analysis step strip renders from.
/// Regression cover for steps 2 and 4 losing their "complete" check when the service
/// returns null prompt text on the results steps (3 and 5).
/// </summary>
public sealed class DeckAnalysisViewModelTests
{
    /// <summary>
    /// Step 2 is complete once the analysis prompt text has been generated.
    /// </summary>
    [Fact]
    public void IsAnalysisPromptStepComplete_TrueWhenPromptTextPresent()
    {
        var model = new DeckAnalysisViewModel { AnalysisPromptText = "analysis.txt body" };

        Assert.True(model.IsAnalysisPromptStepComplete);
    }

    /// <summary>
    /// Regression: step 2 stays complete on the results step where the service returns null
    /// prompt text but a parsed analysis response (which proves the prompt was generated).
    /// </summary>
    [Fact]
    public void IsAnalysisPromptStepComplete_TrueWhenResponsePresentEvenWithNullPromptText()
    {
        var model = new DeckAnalysisViewModel
        {
            AnalysisPromptText = null,
            AnalysisResponse = new DeckAnalysisResponse(),
        };

        Assert.True(model.IsAnalysisPromptStepComplete);
    }

    /// <summary>
    /// Step 2 is incomplete before any prompt or response exists; whitespace does not count.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsAnalysisPromptStepComplete_FalseWhenNoPromptAndNoResponse(string? promptText)
    {
        var model = new DeckAnalysisViewModel { AnalysisPromptText = promptText };

        Assert.False(model.IsAnalysisPromptStepComplete);
    }

    /// <summary>
    /// Step 4 is complete once the set-upgrade prompt text has been generated.
    /// </summary>
    [Fact]
    public void IsSetUpgradePromptStepComplete_TrueWhenPromptTextPresent()
    {
        var model = new DeckAnalysisViewModel { SetUpgradePromptText = "set-upgrade-analysis.txt body" };

        Assert.True(model.IsSetUpgradePromptStepComplete);
    }

    /// <summary>
    /// Regression: step 4 stays complete on the set-upgrade results step where the service
    /// returns null prompt text but a parsed set-upgrade response.
    /// </summary>
    [Fact]
    public void IsSetUpgradePromptStepComplete_TrueWhenResponsePresentEvenWithNullPromptText()
    {
        var model = new DeckAnalysisViewModel
        {
            SetUpgradePromptText = null,
            SetUpgradeResponse = new SetUpgradeResponse(),
        };

        Assert.True(model.IsSetUpgradePromptStepComplete);
    }

    /// <summary>
    /// Step 4 is incomplete before any set-upgrade prompt or response exists.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSetUpgradePromptStepComplete_FalseWhenNoPromptAndNoResponse(string? promptText)
    {
        var model = new DeckAnalysisViewModel { SetUpgradePromptText = promptText };

        Assert.False(model.IsSetUpgradePromptStepComplete);
    }
}
