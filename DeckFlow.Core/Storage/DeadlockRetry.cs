using Microsoft.Extensions.Logging;
using Npgsql;

namespace DeckFlow.Core.Storage;

internal static class DeadlockRetry
{
    internal static async Task ExecuteAsync(Func<Task> transactionBody, ILogger? logger, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await transactionBody().ConfigureAwait(false);
                return;
            }
            catch (PostgresException exception) when (exception.SqlState == "40P01")
            {
                if (attempt == 3)
                {
                    logger?.LogError(exception, "Category write deadlock exhausted after {Attempts} attempts.", attempt);
                    throw;
                }

                logger?.LogWarning(exception, "Category write deadlock on attempt {Attempt}; retrying.", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(5, 25) * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
