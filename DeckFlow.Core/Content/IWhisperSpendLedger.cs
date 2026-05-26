namespace DeckFlow.Core.Content;

/// <summary>
/// Persists one row per Whisper transcription call and exposes monthly spend checks.
/// </summary>
public interface IWhisperSpendLedger
{
    /// <summary>
    /// Records a billed Whisper transcription call.
    /// </summary>
    /// <param name="videoId">Identifier of the content video that was transcribed.</param>
    /// <param name="secondsBilled">Number of audio seconds billed.</param>
    /// <param name="costUsd">USD cost recorded for the call.</param>
    /// <param name="monthKey">UTC month key in <c>YYYY-MM</c> form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordCallAsync(
        long videoId,
        int secondsBilled,
        decimal costUsd,
        string monthKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the exact monthly Whisper spend total for the supplied month key.
    /// </summary>
    /// <param name="yearMonth">UTC month key in <c>YYYY-MM</c> form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total USD spend recorded for the month.</returns>
    Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase-20 wiring seam that returns whether projected spend would exceed the configured monthly cap;
    /// deliberately no TOCTOU locking machinery is used for this local-harvester check (D-08).
    /// </summary>
    /// <param name="projectedCallCostUsd">Projected USD cost for the next Whisper call.</param>
    /// <param name="monthKey">UTC month key in <c>YYYY-MM</c> form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when current monthly spend plus projected spend is greater than the cap.</returns>
    Task<bool> WouldExceedCapAsync(
        decimal projectedCallCostUsd,
        string monthKey,
        CancellationToken cancellationToken = default);
}
