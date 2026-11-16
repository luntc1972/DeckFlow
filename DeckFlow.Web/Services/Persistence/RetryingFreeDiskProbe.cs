using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services;

/// <summary>Retries disk headroom checks without relaxing their safety decisions.</summary>
internal sealed class RetryingFreeDiskProbe(
    Func<CancellationToken, Task<long?>> inner,
    ILogger? logger,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync = delayAsync ?? Task.Delay;

    internal async Task<long?> GetFreeBytesAsync(CancellationToken cancellationToken)
    {
        const int Attempts = 3;
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exceptionType = "none";
            try
            {
                var freeBytes = await inner(cancellationToken).ConfigureAwait(false);
                if (freeBytes.HasValue) return freeBytes;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                exceptionType = exception.GetType().Name;
            }
            if (attempt == Attempts) break;
            logger?.LogWarning("Harvest.Maintenance.DiskProbeRetry attempt={Attempt} attempts={Attempts} exceptionType={ExceptionType}", attempt, Attempts, exceptionType);
            await _delayAsync(TimeSpan.FromSeconds(attempt == 1 ? 5 : 15), cancellationToken).ConfigureAwait(false);
        }
        // Why: D-DP applies unchanged on every attempt; exhausted checks must still fail closed.
        return null;
    }
}
