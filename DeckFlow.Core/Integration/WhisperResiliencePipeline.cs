using System.ClientModel;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Builds the concrete Polly pipeline used for Whisper transcription calls.
/// </summary>
public static class WhisperResiliencePipeline
{
    /// <summary>
    /// Builds the Whisper timeout and retry pipeline.
    /// </summary>
    /// <returns>A Polly resilience pipeline with a 12 minute timeout and transient retry.</returns>
    public static ResiliencePipeline Build()
        => new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromMinutes(12))
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(10),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome.Exception)),
            })
            .Build();

    private static bool ShouldRetry(Exception? exception)
        => exception switch
        {
            HttpRequestException or TimeoutRejectedException => true,
            ClientResultException clientResultException => IsTransientStatus(clientResultException),
            _ => false,
        };

    private static bool IsTransientStatus(ClientResultException exception)
    {
        var status = exception.Status;
        if (status == 0)
        {
            status = exception.GetRawResponse()?.Status ?? 0;
        }

        return status is 0 or 408 or 429 || status >= 500;
    }
}
