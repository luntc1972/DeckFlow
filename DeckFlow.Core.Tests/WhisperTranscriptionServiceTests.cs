using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the pure Whisper transcription service.
/// </summary>
public sealed class WhisperTranscriptionServiceTests
{
    [Fact]
    public async Task TranscribeAsync_OverCapSkipsWithoutInvokingDelegate()
    {
        using var audio = CreateAudio(sizeBytes: 1_000, durationSeconds: 600);
        using var httpClient = new HttpClient();
        var ledger = new FakeWhisperSpendLedger { WouldExceed = true };
        var invoked = false;
        var service = CreateService(ledger, new FakeFfmpegAudioChunker(), httpClient, (stream, filename, ct) =>
        {
            invoked = true;
            return Task.FromResult(("body", 60));
        });

        var result = await service.TranscribeAsync(audio, TimeSpan.FromMinutes(10), "2026-05");

        Assert.Equal(TranscriptOutcome.SkippedOverCap, result.Outcome);
        Assert.Equal("2026-05", result.MonthKey);
        Assert.False(invoked);
        Assert.Equal(1, ledger.WouldExceedCalls);
        Assert.Equal("2026-05", ledger.LastMonthKey);
        Assert.Equal(0, ledger.RecordCallCalls);
    }

    [Fact]
    public async Task TranscribeAsync_UsesKnownDurationForCapWhenAudioDurationIsZero()
    {
        using var audio = CreateAudio(sizeBytes: 1_000, durationSeconds: 0);
        using var httpClient = new HttpClient();
        var ledger = new FakeWhisperSpendLedger { WouldExceed = true };
        var invoked = false;
        var service = CreateService(ledger, new FakeFfmpegAudioChunker(), httpClient, (stream, filename, ct) =>
        {
            invoked = true;
            return Task.FromResult(("body", 60));
        });

        var result = await service.TranscribeAsync(audio, TimeSpan.FromMinutes(25), "2026-05");

        Assert.Equal(TranscriptOutcome.SkippedOverCap, result.Outcome);
        Assert.False(invoked);
        Assert.True(ledger.LastProjectedCostUsd > 0m);
        Assert.Equal(0, ledger.RecordCallCalls);
    }

    [Fact]
    public async Task TranscribeAsync_UsesAudioDurationForCapWhenKnownDurationIsUnknown()
    {
        using var audio = CreateAudio(sizeBytes: 1_000, durationSeconds: 300);
        using var httpClient = new HttpClient();
        var ledger = new FakeWhisperSpendLedger { WouldExceed = true };
        var invoked = false;
        var service = CreateService(ledger, new FakeFfmpegAudioChunker(), httpClient, (stream, filename, ct) =>
        {
            invoked = true;
            return Task.FromResult(("body", 60));
        });

        var result = await service.TranscribeAsync(audio, knownDuration: null, "2026-05");

        Assert.Equal(TranscriptOutcome.SkippedOverCap, result.Outcome);
        Assert.False(invoked);
        Assert.True(ledger.LastProjectedCostUsd > 0m);
        Assert.Equal(0, ledger.RecordCallCalls);
    }

    [Fact]
    public async Task TranscribeAsync_FailsWhenBothDurationsAreUnknown()
    {
        using var audio = CreateAudio(sizeBytes: 1_000, durationSeconds: 0);
        using var httpClient = new HttpClient();
        var ledger = new FakeWhisperSpendLedger();
        var invoked = false;
        var service = CreateService(ledger, new FakeFfmpegAudioChunker(), httpClient, (stream, filename, ct) =>
        {
            invoked = true;
            return Task.FromResult(("body", 60));
        });

        var result = await service.TranscribeAsync(audio, knownDuration: null, "2026-05");

        Assert.Equal(TranscriptOutcome.Failed, result.Outcome);
        Assert.Equal("2026-05", result.MonthKey);
        Assert.Contains("duration", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(invoked);
        Assert.Equal(0, ledger.WouldExceedCalls);
        Assert.Equal(0, ledger.RecordCallCalls);
    }

    [Fact]
    public async Task TranscribeAsync_UnderCapSingleFileReturnsBillingAndMonthKey()
    {
        using var audio = CreateAudio(sizeBytes: 1_000, durationSeconds: 600);
        using var httpClient = new HttpClient();
        var ledger = new FakeWhisperSpendLedger();
        var service = CreateService(ledger, new FakeFfmpegAudioChunker(), httpClient, (stream, filename, ct) =>
            Task.FromResult(("delegate body", 125)));

        var result = await service.TranscribeAsync(audio, TimeSpan.FromMinutes(10), "2026-05");

        Assert.Equal(TranscriptOutcome.Whisper, result.Outcome);
        Assert.Equal("delegate body", result.Body);
        Assert.Equal(125, result.SecondsBilled);
        Assert.Equal(0.0125m, result.CostUsd);
        Assert.Equal("2026-05", result.MonthKey);
        Assert.Equal(1, ledger.WouldExceedCalls);
        Assert.Equal(0, ledger.RecordCallCalls);
    }

    [Fact]
    public async Task TranscribeAsync_RetriesTransientFailureThroughPollyPipeline()
    {
        using var audio = CreateAudio(sizeBytes: 1_000, durationSeconds: 60);
        using var httpClient = new HttpClient();
        var attempts = 0;
        var service = CreateService(new FakeWhisperSpendLedger(), new FakeFfmpegAudioChunker(), httpClient, (stream, filename, ct) =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new HttpRequestException("transient");
            }

            return Task.FromResult(("retried body", 60));
        });

        var result = await service.TranscribeAsync(audio, TimeSpan.FromMinutes(1), "2026-05");

        Assert.Equal(TranscriptOutcome.Whisper, result.Outcome);
        Assert.Equal("retried body", result.Body);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task TranscribeAsync_LargeAudioChunksAndConcatenatesInOrder()
    {
        using var audio = CreateAudio(sizeBytes: 25_000_000, durationSeconds: 600);
        using var httpClient = new HttpClient();
        var chunker = new FakeFfmpegAudioChunker
        {
            IsAvailable = true,
            ChunkBodies = ["first", "second"],
        };
        var filenames = new List<string>();
        var service = CreateService(new FakeWhisperSpendLedger(), chunker, httpClient, (stream, filename, ct) =>
        {
            filenames.Add(filename);
            var body = filename.Contains("0000", StringComparison.Ordinal) ? "first text" : "second text";
            var seconds = filename.Contains("0000", StringComparison.Ordinal) ? 30 : 45;
            return Task.FromResult((body, seconds));
        });

        var result = await service.TranscribeAsync(audio, TimeSpan.FromMinutes(10), "2026-05");

        Assert.Equal(TranscriptOutcome.Whisper, result.Outcome);
        Assert.Equal("first text second text", result.Body);
        Assert.Equal(75, result.SecondsBilled);
        Assert.Equal(0.0075m, result.CostUsd);
        Assert.Equal(2, filenames.Count);
        Assert.Equal(1, chunker.ChunkCalls);
    }

    [Fact]
    public async Task TranscribeAsync_LargeAudioFailsWhenFfmpegUnavailable()
    {
        using var audio = CreateAudio(sizeBytes: 25_000_000, durationSeconds: 600);
        using var httpClient = new HttpClient();
        var chunker = new FakeFfmpegAudioChunker { IsAvailable = false };
        var invoked = false;
        var service = CreateService(new FakeWhisperSpendLedger(), chunker, httpClient, (stream, filename, ct) =>
        {
            invoked = true;
            return Task.FromResult(("body", 60));
        });

        var result = await service.TranscribeAsync(audio, TimeSpan.FromMinutes(10), "2026-05");

        Assert.Equal(TranscriptOutcome.Failed, result.Outcome);
        Assert.Equal("2026-05", result.MonthKey);
        Assert.Contains("ffmpeg", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(invoked);
        Assert.Equal(0, chunker.ChunkCalls);
    }

    private static WhisperTranscriptionService CreateService(
        FakeWhisperSpendLedger ledger,
        FakeFfmpegAudioChunker chunker,
        HttpClient httpClient,
        Func<Stream, string, CancellationToken, Task<(string Body, int BilledSeconds)>> transcribeAsync)
        => new(ledger, chunker, httpClient, transcribeAsync);

    private static AudioDownloadResult CreateAudio(long sizeBytes, double durationSeconds)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "deckflow-audio", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, "audio.webm");
        File.WriteAllText(tempPath, "audio");
        return new AudioDownloadResult
        {
            TempFilePath = tempPath,
            FileName = "audio.webm",
            SizeBytes = sizeBytes,
            DurationSeconds = durationSeconds,
        };
    }

    private sealed class FakeWhisperSpendLedger : IWhisperSpendLedger
    {
        public bool WouldExceed { get; init; }

        public int WouldExceedCalls { get; private set; }

        public int RecordCallCalls { get; private set; }

        public decimal LastProjectedCostUsd { get; private set; }

        public string? LastMonthKey { get; private set; }

        public Task RecordCallAsync(
            long videoId,
            int secondsBilled,
            decimal costUsd,
            string monthKey,
            CancellationToken cancellationToken = default)
        {
            RecordCallCalls++;
            return Task.CompletedTask;
        }

        public Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
            => Task.FromResult(0m);

        public Task<bool> WouldExceedCapAsync(
            decimal projectedCallCostUsd,
            string monthKey,
            CancellationToken cancellationToken = default)
        {
            WouldExceedCalls++;
            LastProjectedCostUsd = projectedCallCostUsd;
            LastMonthKey = monthKey;
            return Task.FromResult(WouldExceed);
        }
    }

    private sealed class FakeFfmpegAudioChunker : IFfmpegAudioChunker
    {
        public bool IsAvailable { get; init; } = true;

        public IReadOnlyList<string> ChunkBodies { get; init; } = [];

        public int ChunkCalls { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
            => Task.FromResult(IsAvailable);

        public Task<IReadOnlyList<string>> ChunkAsync(
            string inputPath,
            string outputDirectory,
            int segmentSeconds = 300,
            CancellationToken ct = default)
        {
            ChunkCalls++;
            Directory.CreateDirectory(outputDirectory);
            var paths = ChunkBodies
                .Select((body, index) => WriteChunk(outputDirectory, index, body))
                .ToArray();
            return Task.FromResult<IReadOnlyList<string>>(paths);
        }

        private static string WriteChunk(string outputDirectory, int index, string body)
        {
            var path = Path.Combine(outputDirectory, $"chunk_{index:0000}.webm");
            File.WriteAllText(path, body);
            return path;
        }
    }
}
