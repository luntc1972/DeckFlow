using System.ClientModel;
using System.ClientModel.Primitives;
using DeckFlow.Core.Content;
using OpenAI;
using OpenAI.Audio;
using Polly;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Pure Whisper transcription service with spend-cap gating and large-audio chunking.
/// </summary>
public sealed class WhisperTranscriptionService : IWhisperTranscriptionService
{
    private const long ChunkThresholdBytes = 24_000_000;
    private const decimal WhisperUsdPerMinute = 0.006m;
    private const string OpenAiApiKeyEnvironmentKey = "OPENAI_API_KEY";

    private readonly IWhisperSpendLedger _ledger;
    private readonly IFfmpegAudioChunker _chunker;
    private readonly Func<Stream, string, CancellationToken, Task<(string Body, int BilledSeconds)>> _transcribeAsync;
    private readonly ResiliencePipeline _pipeline;

    /// <summary>
    /// Initializes a Whisper transcription service with production OpenAI transcription.
    /// </summary>
    /// <param name="ledger">Spend ledger read gate.</param>
    /// <param name="chunker">ffmpeg chunker for large audio.</param>
    /// <param name="httpClient">HTTP client used by the OpenAI SDK transport.</param>
    public WhisperTranscriptionService(
        IWhisperSpendLedger ledger,
        IFfmpegAudioChunker chunker,
        HttpClient httpClient)
        : this(ledger, chunker, httpClient, transcribeAsyncOverride: null)
    {
    }

    internal WhisperTranscriptionService(
        IWhisperSpendLedger ledger,
        IFfmpegAudioChunker chunker,
        HttpClient httpClient,
        Func<Stream, string, CancellationToken, Task<(string Body, int BilledSeconds)>>? transcribeAsyncOverride)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(chunker);
        ArgumentNullException.ThrowIfNull(httpClient);
        _ledger = ledger;
        _chunker = chunker;
        _transcribeAsync = transcribeAsyncOverride ?? CreateProductionTranscriber(httpClient);
        _pipeline = WhisperResiliencePipeline.Build();
    }

    /// <inheritdoc />
    public async Task<WhisperTranscriptionResult> TranscribeAsync(
        AudioDownloadResult audio,
        TimeSpan? knownDuration,
        string monthKey,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentException.ThrowIfNullOrWhiteSpace(monthKey);

        var projectionSeconds = Math.Max(knownDuration?.TotalSeconds ?? 0d, audio.DurationSeconds);
        if (double.IsNaN(projectionSeconds) || double.IsInfinity(projectionSeconds) || projectionSeconds <= 0d)
        {
            // Why: KB-05 forbids projecting a real video at $0 when both duration sources are unknown.
            return Failed("duration unknown - cannot project Whisper cap cost", monthKey);
        }

        var projectedCost = CalculateCost(projectionSeconds);
        if (await _ledger.WouldExceedCapAsync(projectedCost, monthKey, ct).ConfigureAwait(false))
        {
            return SkippedOverCap(monthKey);
        }

        return await TranscribeAfterCapCheckAsync(audio, monthKey, ct).ConfigureAwait(false);
    }

    private async Task<WhisperTranscriptionResult> TranscribeAfterCapCheckAsync(
        AudioDownloadResult audio,
        string monthKey,
        CancellationToken ct)
    {
        try
        {
            return audio.SizeBytes <= ChunkThresholdBytes
                ? await TranscribeSingleAsync(audio, monthKey, ct).ConfigureAwait(false)
                : await TranscribeChunkedAsync(audio, monthKey, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failed(ex.Message, monthKey);
        }
    }

    private async Task<WhisperTranscriptionResult> TranscribeSingleAsync(
        AudioDownloadResult audio,
        string monthKey,
        CancellationToken ct)
    {
        var transcribed = await TranscribeFileAsync(audio.TempFilePath, audio.FileName, ct).ConfigureAwait(false);
        return Succeeded(transcribed, monthKey);
    }

    private async Task<WhisperTranscriptionResult> TranscribeChunkedAsync(
        AudioDownloadResult audio,
        string monthKey,
        CancellationToken ct)
    {
        if (!await _chunker.IsAvailableAsync(ct).ConfigureAwait(false))
        {
            return Failed("ffmpeg not available", monthKey);
        }

        var outputDirectory = Path.Combine(Path.GetTempPath(), "deckflow-whisper-chunks", Guid.NewGuid().ToString("N"));
        try
        {
            var chunkPaths = await _chunker.ChunkAsync(audio.TempFilePath, outputDirectory, 300, ct).ConfigureAwait(false);
            return await TranscribeChunkFilesAsync(chunkPaths, monthKey, ct).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteDirectory(outputDirectory);
        }
    }

    private async Task<WhisperTranscriptionResult> TranscribeChunkFilesAsync(
        IReadOnlyList<string> chunkPaths,
        string monthKey,
        CancellationToken ct)
    {
        if (chunkPaths.Count == 0)
        {
            return Failed("ffmpeg produced no audio chunks", monthKey);
        }

        var parts = new List<string>();
        var secondsBilled = 0;
        foreach (var chunkPath in chunkPaths)
        {
            var transcribed = await TranscribeFileAsync(chunkPath, Path.GetFileName(chunkPath), ct).ConfigureAwait(false);
            parts.Add(transcribed.Body);
            secondsBilled += transcribed.SecondsBilled;
        }

        return Succeeded(new TranscribedAudio(string.Join(" ", parts), secondsBilled), monthKey);
    }

    private async Task<TranscribedAudio> TranscribeFileAsync(string path, string fileName, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(ExecuteAttemptAsync, ct).ConfigureAwait(false);

        async ValueTask<TranscribedAudio> ExecuteAttemptAsync(CancellationToken attemptCt)
        {
            await using var stream = File.OpenRead(path);
            var result = await _transcribeAsync(stream, fileName, attemptCt).ConfigureAwait(false);
            return new TranscribedAudio(result.Body, result.BilledSeconds);
        }
    }

    private static Func<Stream, string, CancellationToken, Task<(string Body, int BilledSeconds)>> CreateProductionTranscriber(
        HttpClient httpClient)
        => async (stream, fileName, ct) =>
        {
            var audioClient = CreateAudioClient(httpClient, ReadApiKey());
            var options = new AudioTranscriptionOptions
            {
                ResponseFormat = AudioTranscriptionFormat.Verbose,
            };
            var result = await audioClient.TranscribeAudioAsync(stream, fileName, options, ct).ConfigureAwait(false);
            var transcription = result.Value;
            return (transcription.Text, ReadBilledSeconds(transcription));
        };

    private static AudioClient CreateAudioClient(HttpClient httpClient, string apiKey)
    {
        var options = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = Timeout.InfiniteTimeSpan,
        };

        return new AudioClient("whisper-1", new ApiKeyCredential(apiKey), options);
    }

#pragma warning disable OPENAI001
    private static int ReadBilledSeconds(AudioTranscription transcription)
    {
        double? usageSeconds = transcription.Usage is AudioTranscriptionDurationUsage durationUsage
            ? durationUsage.Duration.TotalSeconds
            : null;
        var seconds = usageSeconds ?? transcription.Duration?.TotalSeconds ?? 0d;
        return (int)Math.Ceiling(seconds);
    }
#pragma warning restore OPENAI001

    private static string ReadApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable(OpenAiApiKeyEnvironmentKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"{OpenAiApiKeyEnvironmentKey} is not set.");
        }

        return apiKey;
    }

    private static WhisperTranscriptionResult Succeeded(TranscribedAudio transcribed, string monthKey)
        => new()
        {
            Outcome = TranscriptOutcome.Whisper,
            Body = transcribed.Body,
            SecondsBilled = transcribed.SecondsBilled,
            CostUsd = CalculateCost(transcribed.SecondsBilled),
            MonthKey = monthKey,
        };

    private static WhisperTranscriptionResult SkippedOverCap(string monthKey)
        => new()
        {
            Outcome = TranscriptOutcome.SkippedOverCap,
            MonthKey = monthKey,
        };

    private static WhisperTranscriptionResult Failed(string reason, string monthKey)
        => new()
        {
            Outcome = TranscriptOutcome.Failed,
            FailureReason = reason,
            MonthKey = monthKey,
        };

    private static decimal CalculateCost(int secondsBilled)
        => secondsBilled * WhisperUsdPerMinute / 60m;

    private static decimal CalculateCost(double seconds)
        => (decimal)seconds * WhisperUsdPerMinute / 60m;

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record TranscribedAudio(string Body, int SecondsBilled);
}
