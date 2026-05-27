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
                ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is HttpRequestException or TimeoutRejectedException),
            })
            .Build();
}
