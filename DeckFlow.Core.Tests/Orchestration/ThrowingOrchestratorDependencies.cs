using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;

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

internal sealed class ThrowingContentSourceStore : IContentSourceStore
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSourceStore)}.{nameof(EnsureSchemaAsync)} must not be called by the current path");

    public Task<long> InsertSourceAsync(string sourceSlug, string displayName, string sourceType, string sourceUrl, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSourceStore)}.{nameof(InsertSourceAsync)} must not be called by the current path");

    public Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSourceStore)}.{nameof(GetSourceAsync)} must not be called by the current path");

    public Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSourceStore)}.{nameof(ListEnabledSourcesAsync)} must not be called by the current path");
}

internal sealed class ThrowingContentVideoStore : IContentVideoStore
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(EnsureSchemaAsync)} must not be called by the current path");

    public Task<long> InsertVideoAsync(long sourceId, string? youtubeVideoId, string? rssGuid, string title, string videoUrl, DateTimeOffset? publishedUtc, string transcriptStatus, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertVideoAsync)} must not be called by the current path");

    public Task<ContentVideo?> GetVideoByYoutubeIdAsync(long sourceId, string youtubeVideoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(GetVideoByYoutubeIdAsync)} must not be called by the current path");

    public Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(long sourceId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(ListVideosPendingDistillAsync)} must not be called by the current path");

    public Task<IReadOnlyList<PendingDistillProjection>> ListPendingDistillDisplayAsync(long sourceId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(ListPendingDistillDisplayAsync)} must not be called by the current path");

    public Task UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(UpdateTranscriptStatusAsync)} must not be called by the current path");

    public Task<long> InsertTranscriptAsync(long videoId, string source, string body, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertTranscriptAsync)} must not be called by the current path");

    public Task<ContentTranscriptBody?> GetLatestTranscriptAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(GetLatestTranscriptAsync)} must not be called by the current path");

    public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertSummaryAsync)} must not be called by the current path");

    public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertClipAsync)} must not be called by the current path");

    public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertTagAsync)} must not be called by the current path");

    public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(DeleteVideoAsync)} must not be called by the current path");

    public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(DeleteVideoByYoutubeIdAsync)} must not be called by the current path");

    public Task<int> DeleteAllVideosAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(DeleteAllVideosAsync)} must not be called by the current path");

    public Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(ClearDistillOutputAsync)} must not be called by the current path");

    public Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(GetDistillStatusAsync)} must not be called by the current path");

    public Task SetDistillStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(SetDistillStatusAsync)} must not be called by the current path");

    public Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(CountTranscriptsByVideoAsync)} must not be called by the current path");

    public Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(CountSummariesByVideoAsync)} must not be called by the current path");

    public Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(CountClipsByVideoAsync)} must not be called by the current path");

    public Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(CountTagsByVideoAsync)} must not be called by the current path");
}

internal sealed class ThrowingContentSiteIndexStore : IContentSiteIndexStore
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(EnsureSchemaAsync)} must not be called by the current path");

    public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(UpsertRowAsync)} must not be called by the current path");

    public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(UpsertRowPreservingVisibilityAsync)} must not be called by the current path");

    public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(UpsertContentColumnsOnlyAsync)} must not be called by the current path");

    public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetByNaturalKeyAsync)} must not be called by the current path");

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetPublishedRowsAsync)} must not be called by the current path");

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetApprovedRowsAsync)} must not be called by the current path");

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetAllRowsAsync)} must not be called by the current path");

    public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetByIdAsync)} must not be called by the current path");

    public Task<ContentSiteIndexRow?> GetPublishedByIdAsync(long id, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetPublishedByIdAsync)} must not be called by the current path");

    public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetVisibilityAsync)} must not be called by the current path");

    public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetHiddenAsync)} must not be called by the current path");

    public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(DeleteByIdAsync)} must not be called by the current path");

    public Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(DeleteAllRowsAsync)} must not be called by the current path");

    public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetEvergreenAsync)} must not be called by the current path");

    public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetVisibilityBySourceAsync)} must not be called by the current path");

    public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetHiddenBySourceAsync)} must not be called by the current path");

    public Task<int> SetApprovalStatusAsync(string naturalKeyType, string naturalKeyValue, string status, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetApprovalStatusAsync)} must not be called by the current path");

    public Task<int> SetApprovalStatusAsync(IReadOnlyList<(string Type, string Value)> keys, string status, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetApprovalStatusAsync)} must not be called by the current path");

    public Task<int> StampPushedToProdAsync(IReadOnlyList<(string Type, string Value)> keys, DateTimeOffset pushedUtc, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(StampPushedToProdAsync)} must not be called by the current path");

    public Task<int> SetVisibilityAsync(IReadOnlyList<(string Type, string Value)> keys, bool visible, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetVisibilityAsync)} must not be called by the current path");
}

internal sealed class ThrowingContentHarvestRunStore : IContentHarvestRunStore
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentHarvestRunStore)}.{nameof(EnsureSchemaAsync)} must not be called by the current path");

    public Task<long> StartRunAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentHarvestRunStore)}.{nameof(StartRunAsync)} must not be called by the current path");

    public Task CompleteRunAsync(long runId, int sourcesProcessed, int videosProcessed, int transcriptsFetched, int whisperCalls, decimal spendUsd, string? abortedReason, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentHarvestRunStore)}.{nameof(CompleteRunAsync)} must not be called by the current path");

    public Task<ContentHarvestRun?> GetRunAsync(long runId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingContentHarvestRunStore)}.{nameof(GetRunAsync)} must not be called by the current path");
}

internal sealed class ThrowingLlmSpendLedger : ILlmSpendLedger
{
    public Task RecordCallAsync(long videoId, int inputTokens, int outputTokens, decimal costUsd, string monthKey, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingLlmSpendLedger)}.{nameof(RecordCallAsync)} must not be called by the current path");

    public Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingLlmSpendLedger)}.{nameof(GetMonthlyTotalAsync)} must not be called by the current path");

    public Task<bool> WouldExceedCapAsync(decimal projectedCallCostUsd, string monthKey, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingLlmSpendLedger)}.{nameof(WouldExceedCapAsync)} must not be called by the current path");

    public decimal GetMonthlyCapUsd()
        => throw new InvalidOperationException($"{nameof(ThrowingLlmSpendLedger)}.{nameof(GetMonthlyCapUsd)} must not be called by the current path");
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
    public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(string channelUrl, int limit, int skip = 0, CancellationToken ct = default)
        => throw new InvalidOperationException($"{nameof(ThrowingYouTubeChannelVideoLister)}.{nameof(ListRecentAsync)} must not be called by the distill path");

    public Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(IReadOnlyList<string> videoIds, CancellationToken ct = default)
        => throw new InvalidOperationException($"{nameof(ThrowingYouTubeChannelVideoLister)}.{nameof(GetByIdsAsync)} must not be called by the distill path");
}

internal sealed class ThrowingLlmDistillationService : ILlmDistillationService
{
    public Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingLlmDistillationService)}.{nameof(SummarizeAsync)} must not be called by the current path");

    public Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingLlmDistillationService)}.{nameof(ClassifyAsync)} must not be called by the current path");

    public Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingLlmDistillationService)}.{nameof(ExtractClipsAsync)} must not be called by the current path");

    public Task<CombinedExtractionResult> ExtractCombinedAsync(string transcript, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingLlmDistillationService)}.{nameof(ExtractCombinedAsync)} must not be called by the current path");

    public Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"{nameof(ThrowingLlmDistillationService)}.{nameof(InferTagsAsync)} must not be called by the current path");
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
