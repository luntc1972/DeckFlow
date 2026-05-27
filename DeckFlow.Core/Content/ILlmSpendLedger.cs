namespace DeckFlow.Core.Content;

/// <summary>
/// Persists one row per LLM distillation call and exposes monthly spend checks.
/// </summary>
public interface ILlmSpendLedger
{
    /// <summary>
    /// Records a billed LLM distillation call.
    /// </summary>
    /// <param name="videoId">Identifier of the content video that was distilled.</param>
    /// <param name="inputTokens">Number of input tokens billed.</param>
    /// <param name="outputTokens">Number of output tokens billed.</param>
    /// <param name="costUsd">USD cost recorded for the call.</param>
    /// <param name="monthKey">UTC month key in <c>YYYY-MM</c> form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordCallAsync(
        long videoId,
        int inputTokens,
        int outputTokens,
        decimal costUsd,
        string monthKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the exact monthly LLM spend total for the supplied month key.
    /// </summary>
    /// <param name="yearMonth">UTC month key in <c>YYYY-MM</c> form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total USD spend recorded for the month.</returns>
    Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase-21 wiring seam that returns whether projected spend would exceed the configured monthly cap;
    /// deliberately no TOCTOU locking machinery is used for this local-distillation check (D-05).
    /// </summary>
    /// <param name="projectedCallCostUsd">Projected USD cost for the next LLM call.</param>
    /// <param name="monthKey">UTC month key in <c>YYYY-MM</c> form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when current monthly spend plus projected spend is greater than the cap.</returns>
    Task<bool> WouldExceedCapAsync(
        decimal projectedCallCostUsd,
        string monthKey,
        CancellationToken cancellationToken = default);
}
