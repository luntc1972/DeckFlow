using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;

namespace DeckFlow.Core.Tests;

internal sealed class ThrowingBlockedVideoStore : IBlockedVideoStore
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(EnsureSchemaAsync)} must not be called by the distill path");

    public Task AddBlockAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(AddBlockAsync)} must not be called by the distill path");

    public Task<bool> RemoveBlockAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(RemoveBlockAsync)} must not be called by the distill path");

    public Task<bool> IsBlockedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(IsBlockedAsync)} must not be called by the distill path");

    public Task<IReadOnlyList<BlockedVideo>> ListBlockedAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(ListBlockedAsync)} must not be called by the distill path");
}

internal sealed class ThrowingWhisperSpendLedger : IWhisperSpendLedger
{
    public Task RecordCallAsync(long videoId, int secondsBilled, decimal costUsd, string monthKey, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingWhisperSpendLedger)}.{nameof(RecordCallAsync)} must not be called by the distill path");

    public Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingWhisperSpendLedger)}.{nameof(GetMonthlyTotalAsync)} must not be called by the distill path");

    public Task<bool> WouldExceedCapAsync(decimal projectedCallCostUsd, string monthKey, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingWhisperSpendLedger)}.{nameof(WouldExceedCapAsync)} must not be called by the distill path");
}

internal sealed class ThrowingYouTubeChannelVideoLister : IYouTubeChannelVideoLister
{
    public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(string channelUrl, int limit, CancellationToken ct = default)
        => throw new InvalidOperationException($"{nameof(ThrowingYouTubeChannelVideoLister)}.{nameof(ListRecentAsync)} must not be called by the distill path");

    public Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(IReadOnlyList<string> videoIds, CancellationToken ct = default)
        => throw new InvalidOperationException($"{nameof(ThrowingYouTubeChannelVideoLister)}.{nameof(GetByIdsAsync)} must not be called by the distill path");
}

internal sealed class ThrowingTranscriptSource : ITranscriptSource
{
    public string SourceType
        => throw new InvalidOperationException($"{nameof(ThrowingTranscriptSource)}.{nameof(SourceType)} must not be called by the distill path");

    public Task<TranscriptFetchResult> FetchTranscriptAsync(string naturalKey, TimeSpan? knownDuration, string monthKey, CancellationToken ct = default)
        => throw new InvalidOperationException($"{nameof(ThrowingTranscriptSource)}.{nameof(FetchTranscriptAsync)} must not be called by the distill path");
}

internal sealed class ThrowingFfmpegAudioChunker : IFfmpegAudioChunker
{
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => throw new InvalidOperationException($"{nameof(ThrowingFfmpegAudioChunker)}.{nameof(IsAvailableAsync)} must not be called by the distill path");

    public Task<IReadOnlyList<string>> ChunkAsync(string inputPath, string outputDirectory, int segmentSeconds = 300, CancellationToken ct = default)
        => throw new InvalidOperationException($"{nameof(ThrowingFfmpegAudioChunker)}.{nameof(ChunkAsync)} must not be called by the distill path");
}
