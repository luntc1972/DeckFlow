namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Consecutive failed harvest runs completed after the most recent success.
/// </summary>
/// <param name="ConsecutiveFailures">Number of failed runs in the current streak.</param>
/// <param name="LastFailureUtc">Most recent failed-run completion time.</param>
/// <param name="LastSuccessUtc">Parameterless query reports latest success of any kind and trigger; per-kind query reports that kind's latest scheduled-or-NULL success.</param>
public sealed record HarvestFailureStreak(
    int ConsecutiveFailures,
    DateTimeOffset? LastFailureUtc,
    DateTimeOffset? LastSuccessUtc);
