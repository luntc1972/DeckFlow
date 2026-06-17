using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Lifts the Content KB CLI domain orchestration into DeckFlow.Core so multiple hosts can share the same
/// harvest, distill, maintenance, source-management, and export behavior.
/// </summary>
public sealed class ContentKbOrchestrator : IContentKbOrchestrator
{
    private readonly IContentSourceStore _sourceStore;
    private readonly IContentVideoStore _videoStore;
    private readonly IContentSiteIndexStore _indexStore;
    private readonly IBlockedVideoStore _blockedVideoStore;
    private readonly IContentHarvestRunStore _runStore;
    private readonly ILlmSpendLedger _llmLedger;
    private readonly IWhisperSpendLedger _whisperLedger;
    private readonly ILlmDistillationService _distiller;
    private readonly IYouTubeChannelVideoLister _lister;
    private readonly ITranscriptSource _transcriptSource;
    private readonly IFfmpegAudioChunker _chunker;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly string _artifactRoot;
    private readonly ILogger<ContentKbOrchestrator> _logger;

    /// <summary>
    /// Initializes a new Content KB orchestrator from the host-provided stores, services, and artifact-root options.
    /// </summary>
    /// <param name="sourceStore">Content source store.</param>
    /// <param name="videoStore">Content video store.</param>
    /// <param name="indexStore">Content site-index store.</param>
    /// <param name="blockedVideoStore">Blocked-video store.</param>
    /// <param name="runStore">Harvest-run store.</param>
    /// <param name="llmLedger">LLM spend ledger.</param>
    /// <param name="whisperLedger">Whisper spend ledger.</param>
    /// <param name="distiller">LLM distillation service.</param>
    /// <param name="lister">YouTube channel video lister.</param>
    /// <param name="transcriptSource">Transcript source.</param>
    /// <param name="chunker">ffmpeg chunker.</param>
    /// <param name="utcNow">UTC clock function.</param>
    /// <param name="options">Resolved orchestrator options.</param>
    /// <param name="logger">Optional structured logger.</param>
    public ContentKbOrchestrator(
        IContentSourceStore sourceStore,
        IContentVideoStore videoStore,
        IContentSiteIndexStore indexStore,
        IBlockedVideoStore blockedVideoStore,
        IContentHarvestRunStore runStore,
        ILlmSpendLedger llmLedger,
        IWhisperSpendLedger whisperLedger,
        ILlmDistillationService distiller,
        IYouTubeChannelVideoLister lister,
        ITranscriptSource transcriptSource,
        IFfmpegAudioChunker chunker,
        Func<DateTimeOffset> utcNow,
        ContentKbOrchestratorOptions options,
        ILogger<ContentKbOrchestrator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sourceStore);
        ArgumentNullException.ThrowIfNull(videoStore);
        ArgumentNullException.ThrowIfNull(indexStore);
        ArgumentNullException.ThrowIfNull(blockedVideoStore);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(llmLedger);
        ArgumentNullException.ThrowIfNull(whisperLedger);
        ArgumentNullException.ThrowIfNull(distiller);
        ArgumentNullException.ThrowIfNull(lister);
        ArgumentNullException.ThrowIfNull(transcriptSource);
        ArgumentNullException.ThrowIfNull(chunker);
        ArgumentNullException.ThrowIfNull(utcNow);
        ArgumentNullException.ThrowIfNull(options);

        _sourceStore = sourceStore;
        _videoStore = videoStore;
        _indexStore = indexStore;
        _blockedVideoStore = blockedVideoStore;
        _runStore = runStore;
        _llmLedger = llmLedger;
        _whisperLedger = whisperLedger;
        _distiller = distiller;
        _lister = lister;
        _transcriptSource = transcriptSource;
        _chunker = chunker;
        _utcNow = utcNow;
        _artifactRoot = options.ArtifactRoot;
        ArgumentException.ThrowIfNullOrWhiteSpace(_artifactRoot);
        _logger = logger ?? NullLogger<ContentKbOrchestrator>.Instance;
    }

    /// <inheritdoc />
    public async Task<ContentSourceResult> AddSourceAsync(
        string url,
        string name,
        string type,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidContentSourceType(type))
        {
            return new ContentSourceResult
            {
                Success = false,
                Outcome = ContentSourceResult.ContentSourceOutcome.InvalidType,
                Message = $"Unsupported content source type '{type}'. Use youtube_channel or podcast_rss.",
            };
        }

        var slug = SlugifySourceName.Slugify(name);
        progress?.Report($"Computed slug: {slug}");

        try
        {
            var id = await _sourceStore.InsertSourceAsync(slug, name, type, url, cancellationToken).ConfigureAwait(false);
            progress?.Report($"Added content source {id}: {slug}");
            return new ContentSourceResult
            {
                Success = true,
                Outcome = ContentSourceResult.ContentSourceOutcome.Added,
                Slug = slug,
                Id = id,
                Message = $"Added content source {id}: {slug}",
            };
        }
        catch (Exception exception) when (IsContentSourceUniqueViolation(exception))
        {
            return await HandleContentSourceUniqueViolationAsync(_sourceStore, slug, url, exception, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ContentSourceResult
            {
                Success = false,
                Outcome = ContentSourceResult.ContentSourceOutcome.Error,
                Slug = slug,
                Message = exception.Message,
            };
        }
    }

    /// <inheritdoc />
    public async Task<ContentSourceResult> SetSourceEnabledAsync(
        long id,
        bool enabled,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _sourceStore.SetEnabledAsync(id, enabled, cancellationToken).ConfigureAwait(false);
            progress?.Report($"Source {id} enabled={enabled}.");
            return new ContentSourceResult
            {
                Success = true,
                Outcome = ContentSourceResult.ContentSourceOutcome.Added,
                Id = id,
                Message = $"Source {id} enabled={enabled}.",
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Content source set-enabled failed {SourceId}", id);
            return new ContentSourceResult
            {
                Success = false,
                Outcome = ContentSourceResult.ContentSourceOutcome.Error,
                Id = id,
                Message = exception.Message,
            };
        }
    }

    /// <inheritdoc />
    public async Task<ContentSourceResult> EnsureYoutubeSourceAsync(
        string url,
        string name,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: attempt to add. On success (Added) we have the new id immediately.
        var addResult = await AddSourceAsync(url, name, ContentSourceType.Youtube, progress, cancellationToken).ConfigureAwait(false);

        long id;
        if (addResult.Outcome == ContentSourceResult.ContentSourceOutcome.Added)
        {
            // Why: AddSourceAsync returns Id on Added; assert to surface any logic regression.
            id = addResult.Id ?? throw new InvalidOperationException("AddSourceAsync returned Added but Id was null.");
        }
        else if (addResult.Outcome == ContentSourceResult.ContentSourceOutcome.AlreadyExistsSameUrl)
        {
            // Why: AlreadyExistsSameUrl means the row already exists but may be disabled.
            // IContentSourceStore has no enabled-agnostic lookup by URL except GetSourceByUrlAsync (added Task 1).
            var existing = await _sourceStore.GetSourceByUrlAsync(url, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return new ContentSourceResult
                {
                    Success = false,
                    Outcome = ContentSourceResult.ContentSourceOutcome.Error,
                    Message = $"Source already exists for URL '{url}' but could not be retrieved by URL.",
                };
            }

            id = existing.Id;
        }
        else
        {
            // SlugConflict / InvalidType / Error — propagate as-is.
            return addResult;
        }

        // Step 2: idempotent enable — covers both new sources and previously-disabled ones.
        await _sourceStore.SetEnabledAsync(id, true, cancellationToken).ConfigureAwait(false);
        progress?.Report($"Source {id} ensured and enabled.");

        return new ContentSourceResult
        {
            Success = true,
            Outcome = addResult.Outcome,
            Id = id,
            Slug = addResult.Slug,
            Message = $"Source {id} ensured and enabled.",
        };
    }

    /// <inheritdoc />
    public async Task<DistillResult> DistillAsync(
        int limit,
        bool dryRun,
        bool isSubscriptionProvider,
        bool redistill = false,
        IReadOnlyList<string>? videoIds = null,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!dryRun && !isSubscriptionProvider)
            {
                const string message = "classifier requires the subscription LLM CLI (set DECKFLOW_LLM_PROVIDER to a subscription provider); refusing to run an unmetered classifier on a metered provider.";
                _logger.LogError(message);
                return new DistillResult
                {
                    Success = false,
                    AbortedReason = message,
                    DryRun = false,
                };
            }

            // Why: explicit --video-ids means "exactly these", so the recent-N clip is bypassed.
            var requestedKeys = videoIds is { Count: > 0 }
                ? new HashSet<string>(videoIds, StringComparer.Ordinal)
                : null;
            var maxVideosPerSource = requestedKeys?.Count ?? Math.Max(1, limit);
            var monthKey = _utcNow().UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var generatedUtc = _utcNow().ToUniversalTime();
            var counts = new DistillCounts();
            string? abortedReason = null;
            var stopRun = false;
            var runId = dryRun ? 0 : await _runStore.StartRunAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var sources = await _sourceStore.ListEnabledSourcesAsync(cancellationToken).ConfigureAwait(false);
                foreach (var source in sources)
                {
                    if (stopRun)
                    {
                        break;
                    }

                    counts.SourcesProcessed++;
                    IReadOnlyList<ContentVideo> pendingVideos;
                    try
                    {
                        pendingVideos = await _videoStore
                            .ListVideosPendingDistillAsync(source.Id, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        _logger.LogError(exception, "distill source failed {SourceSlug}", source.SourceSlug);
                        continue;
                    }

                    if (requestedKeys is not null)
                    {
                        pendingVideos = pendingVideos
                            .Where(video => requestedKeys.Contains(GetContentNaturalKey(video)))
                            .ToList();
                    }

                    var attemptedForSource = 0;
                    foreach (var video in pendingVideos)
                    {
                        if (stopRun || attemptedForSource >= maxVideosPerSource)
                        {
                            break;
                        }

                        var naturalKey = GetContentNaturalKey(video);
                        var status = await _videoStore.GetDistillStatusAsync(video.Id, cancellationToken).ConfigureAwait(false);
                        if (string.Equals(status, DistillationValidation.DistillStatusDistilled, StringComparison.Ordinal))
                        {
                            // Why: redistill=true bypasses the already-distilled skip ONLY for videos
                            // explicitly listed in requestedKeys; a distilled video outside the targeted
                            // set is still skipped so a blanket re-distill cannot occur (T-45-15).
                            if (redistill && requestedKeys is not null && requestedKeys.Contains(naturalKey))
                            {
                                _logger.LogInformation("re-distilling {VideoId} (redistill=true)", naturalKey);
                                progress?.Report($"re-distilling {naturalKey}");
                                if (!dryRun)
                                {
                                    // Why: clear prior child rows before re-distilling so no orphaned
                                    // half-old/half-new rows remain (T-45-16). Status reset is implicit:
                                    // DistillVideoAsync re-sets it to "distilled" on success.
                                    await _videoStore.ClearDistillOutputAsync(video.Id, cancellationToken).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                _logger.LogInformation("already distilled {VideoId}", naturalKey);
                                progress?.Report($"already distilled {naturalKey}");
                                continue;
                            }
                        }

                        attemptedForSource++;
                        if (dryRun)
                        {
                            var transcript = await _videoStore.GetLatestTranscriptAsync(video.Id, cancellationToken).ConfigureAwait(false);
                            if (transcript is null)
                            {
                                _logger.LogWarning("WOULD skip {VideoId} because transcript is missing", naturalKey);
                                progress?.Report($"WOULD skip {naturalKey} (missing transcript)");
                                continue;
                            }

                            var projectedCost = isSubscriptionProvider ? 0m : DistillationValidation.ComputeProjectedVideoCostUsd(transcript.Body);
                            var wouldSkip = !isSubscriptionProvider
                                && await _llmLedger.WouldExceedCapAsync(projectedCost, monthKey, cancellationToken).ConfigureAwait(false);
                            var disposition = wouldSkip ? "WOULD skip over cap"
                                : isSubscriptionProvider ? "WOULD distill ($0, subscription)"
                                : "WOULD distill";
                            _logger.LogInformation(
                                "{Disposition} {VideoId} (~${ProjectedCostUsd:F4})",
                                disposition,
                                naturalKey,
                                projectedCost);
                            progress?.Report($"{disposition} {naturalKey} (~${projectedCost:F4})");
                            if (wouldSkip)
                            {
                                stopRun = true;
                                break;
                            }

                            counts.ProjectedSpendUsd += projectedCost;
                            counts.WouldRun++;
                            continue;
                        }

                        var outcome = await DistillVideoAsync(
                            source,
                            video,
                            monthKey,
                            generatedUtc,
                            progress,
                            cancellationToken,
                            isSubscriptionProvider).ConfigureAwait(false);

                        counts.Add(outcome);
                        if (outcome.AbortedReason is not null)
                        {
                            abortedReason = outcome.AbortedReason;
                            stopRun = true;
                        }
                    }
                }
            }
            finally
            {
                if (!dryRun && runId > 0)
                {
                    // Why: distill run overloads whisper_calls=LLM calls, spend_usd=LLM spend;
                    // transcripts_fetched=0; distill-failed surfaced in log not a column (Q4/D-11/LOW).
                    await _runStore.CompleteRunAsync(
                        runId,
                        counts.SourcesProcessed,
                        counts.VideosDistilled,
                        transcriptsFetched: 0,
                        whisperCalls: counts.LlmCalls,
                        spendUsd: counts.LlmSpendUsd,
                        abortedReason,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (dryRun)
            {
                _logger.LogInformation(
                    "dry-run distill complete would_run={WouldRun} projected_spend_usd={ProjectedSpendUsd:F6}",
                    counts.WouldRun,
                    counts.ProjectedSpendUsd);
                var spendDisplay = isSubscriptionProvider ? "$0 (subscription)" : $"${counts.ProjectedSpendUsd:F6}";
                progress?.Report($"Dry run complete. Would distill {counts.WouldRun} videos; projected spend {spendDisplay}.");
                return new DistillResult
                {
                    Success = true,
                    WouldRun = counts.WouldRun,
                    ProjectedSpendUsd = counts.ProjectedSpendUsd,
                    DryRun = true,
                };
            }

            _logger.LogInformation(
                "distill complete sources={SourcesProcessed} videos_distilled={VideosDistilled} videos_filtered={VideosFiltered} llm_calls={LlmCalls} spend_usd={SpendUsd:F6} distill_failed={DistillFailed} failed_video_ids={FailedVideoIds}",
                counts.SourcesProcessed,
                counts.VideosDistilled,
                counts.VideosFiltered,
                counts.LlmCalls,
                counts.LlmSpendUsd,
                counts.DistillFailed,
                string.Join(",", counts.FailedVideoIds));
            return new DistillResult
            {
                Success = true,
                SourcesProcessed = counts.SourcesProcessed,
                VideosDistilled = counts.VideosDistilled,
                VideosFiltered = counts.VideosFiltered,
                DistillFailed = counts.DistillFailed,
                LlmCalls = counts.LlmCalls,
                LlmSpendUsd = counts.LlmSpendUsd,
                FailedVideoIds = counts.FailedVideoIds,
                AbortedReason = abortedReason,
                DryRun = false,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Content KB distill failed.");
            return new DistillResult
            {
                Success = false,
                AbortedReason = exception.Message,
                DryRun = dryRun,
            };
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingDistillVideo>> ListPendingDistillAsync(CancellationToken cancellationToken = default)
    {
        var sources = await _sourceStore.ListEnabledSourcesAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<PendingDistillVideo>();
        // Why: dedup by youtube id across sources so a video harvested under two enabled sources lists once.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            IReadOnlyList<ContentVideo> pending;
            try
            {
                pending = await _videoStore.ListVideosPendingDistillAsync(source.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "list pending-distill source failed {SourceSlug}", source.SourceSlug);
                continue;
            }

            foreach (var video in pending)
            {
                if (string.IsNullOrEmpty(video.YoutubeVideoId) || !seen.Add(video.YoutubeVideoId))
                {
                    continue;
                }

                results.Add(new PendingDistillVideo
                {
                    YoutubeVideoId = video.YoutubeVideoId,
                    Title = video.Title,
                    VideoUrl = video.VideoUrl,
                    PublishedUtc = video.PublishedUtc,
                });
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<HarvestResult> HarvestAsync(
        int limit,
        IReadOnlyList<string>? videoIds = null,
        long? sourceId = null,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await WarnIfFfmpegUnavailableAsync(progress, cancellationToken).ConfigureAwait(false);
            var sources = await _sourceStore.ListEnabledSourcesAsync(cancellationToken).ConfigureAwait(false);

            if (videoIds is { Count: > 0 })
            {
                return await HarvestExplicitVideoIdsAsync(
                    sources,
                    videoIds,
                    sourceId,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            var aggregate = new HarvestCounts();
            foreach (var source in sources.Where(source => source.SourceType == ContentSourceType.Youtube))
            {
                try
                {
                    var sourceCounts = await HarvestSourceAsync(
                        source,
                        Math.Max(1, limit),
                        progress,
                        cancellationToken).ConfigureAwait(false);
                    aggregate.Add(sourceCounts);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogError(exception, "harvest source failed {SourceSlug}", source.SourceSlug);
                    continue;
                }
            }

            LogFallbackRatio(sourceSlug: "aggregate", aggregate);
            // Phase 21 owns distillation, artifact emit, slim-index rows, and run records.
            return new HarvestResult
            {
                Success = true,
                Captions = aggregate.Captions,
                Whisper = aggregate.Whisper,
                SkippedNoCaptions = aggregate.SkippedNoCaptions,
                WhisperFallbackRatio = aggregate.WhisperFallbackRatio,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Content KB harvest failed.");
            return new HarvestResult
            {
                Success = false,
                Message = exception.Message,
            };
        }
    }

    /// <inheritdoc />
    public async Task<ContentMaintenanceResult> BlockVideoAsync(
        string youtubeVideoId,
        string? reason,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

            // Why: writing the block row first ensures a partial failure cannot leave the
            // video deleted-but-reharvestable across the separate content/site-index stores.
            await _blockedVideoStore.AddBlockAsync(youtubeVideoId, reason, cancellationToken).ConfigureAwait(false);
            var deletedRows = await _videoStore.DeleteVideoByYoutubeIdAsync(youtubeVideoId, cancellationToken).ConfigureAwait(false);
            var row = await _indexStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, youtubeVideoId, cancellationToken).ConfigureAwait(false);
            var deletedSiteIndexRows = 0;
            if (row is not null)
            {
                deletedSiteIndexRows = await _indexStore.DeleteByIdAsync(row.Id, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "blocked video {VideoId} content_rows_deleted={DeletedRows} site_index_rows_deleted={SiteIndexDeletedRows}",
                youtubeVideoId,
                deletedRows,
                deletedSiteIndexRows);
            progress?.Report($"blocked video {youtubeVideoId} content_rows_deleted={deletedRows} site_index_rows_deleted={deletedSiteIndexRows}");
            return new ContentMaintenanceResult
            {
                Success = true,
                DeletedContentRows = deletedRows,
                DeletedSiteIndexRows = deletedSiteIndexRows,
                Message = $"blocked video {youtubeVideoId} content_rows_deleted={deletedRows} site_index_rows_deleted={deletedSiteIndexRows}",
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Block video failed.");
            return new ContentMaintenanceResult
            {
                Success = false,
                Message = exception.Message,
            };
        }
    }

    /// <inheritdoc />
    public async Task<ContentMaintenanceResult> UnblockVideoAsync(
        string youtubeVideoId,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

            var removed = await _blockedVideoStore.RemoveBlockAsync(youtubeVideoId, cancellationToken).ConfigureAwait(false);
            if (!removed)
            {
                _logger.LogInformation("unblocked video {VideoId}; no row removed", youtubeVideoId);
                progress?.Report($"unblocked video {youtubeVideoId}; no row removed");
                return new ContentMaintenanceResult
                {
                    Success = true,
                    RemovedExistingBlock = false,
                    Message = $"unblocked video {youtubeVideoId}; no row removed",
                };
            }

            _logger.LogInformation("unblocked video {VideoId}", youtubeVideoId);
            progress?.Report($"unblocked video {youtubeVideoId}");
            return new ContentMaintenanceResult
            {
                Success = true,
                RemovedExistingBlock = true,
                Message = $"unblocked video {youtubeVideoId}",
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Unblock video failed.");
            return new ContentMaintenanceResult
            {
                Success = false,
                Message = exception.Message,
            };
        }
    }

    /// <inheritdoc />
    public async Task<ContentMaintenanceResult> ResetCorpusAsync(
        bool dryRun,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (dryRun)
            {
                _logger.LogInformation("corpus reset dry-run preserving blocked_videos and content_sources");
                progress?.Report("corpus reset dry-run preserving blocked_videos and content_sources");
                return new ContentMaintenanceResult
                {
                    Success = true,
                    DryRun = true,
                    Message = "corpus reset dry-run preserving blocked_videos and content_sources",
                };
            }

            var deletedVideos = await _videoStore.DeleteAllVideosAsync(cancellationToken).ConfigureAwait(false);
            var deletedSiteIndexRows = await _indexStore.DeleteAllRowsAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "corpus reset deleted_videos={DeletedVideos} deleted_site_index_rows={DeletedSiteIndexRows}",
                deletedVideos,
                deletedSiteIndexRows);
            progress?.Report($"corpus reset deleted_videos={deletedVideos} deleted_site_index_rows={deletedSiteIndexRows}");
            return new ContentMaintenanceResult
            {
                Success = true,
                DeletedVideos = deletedVideos,
                DeletedSiteIndexRows = deletedSiteIndexRows,
                Message = $"corpus reset deleted_videos={deletedVideos} deleted_site_index_rows={deletedSiteIndexRows}",
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Corpus reset failed.");
            return new ContentMaintenanceResult
            {
                Success = false,
                DryRun = dryRun,
                Message = exception.Message,
            };
        }
    }

    /// <inheritdoc />
    public async Task<BlockedVideoListResult> ListBlockedAsync(
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        var blocked = await _blockedVideoStore.ListBlockedAsync(cancellationToken).ConfigureAwait(false);
        return new BlockedVideoListResult
        {
            Items = blocked
                .Select(row => new BlockedVideoListResult.BlockedVideoListItem
                {
                    YoutubeVideoId = row.YoutubeVideoId,
                    BlockedUtc = row.BlockedUtc,
                    Reason = row.Reason,
                })
                .ToArray(),
        };
    }

    /// <inheritdoc />
    public async Task<ContentIndexExportResult> ExportIndexAsync(
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exportRows = await GetApprovedExportRowsAsync(cancellationToken).ConfigureAwait(false);
            return new ContentIndexExportResult
            {
                Success = true,
                Rows = exportRows,
                RowCount = exportRows.Count,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ContentIndexExportResult
            {
                Success = false,
                Message = exception.Message,
            };
        }
    }

    /// <inheritdoc />
    public async Task<ContentIndexExportResult> ExportIndexToFileAsync(
        string seedPath,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedPath);
        try
        {
            var exportRows = await GetApprovedExportRowsAsync(cancellationToken).ConfigureAwait(false);

            var json = JsonSerializer.Serialize(
                exportRows,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                });

            // Why: WriteIndented on Windows may emit \r\n; D-13 / SC5 require pure LF so a
            // Windows-run Studio never commits a CRLF seed file into the repo.
            var body = json.Replace("\r\n", "\n") + "\n";

            var dir = Path.GetDirectoryName(seedPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(seedPath, body, cancellationToken).ConfigureAwait(false);

            return new ContentIndexExportResult
            {
                Success = true,
                Rows = exportRows,
                RowCount = exportRows.Count,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ContentIndexExportResult
            {
                Success = false,
                Message = exception.Message,
            };
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> CopyApprovedArtifactsToRepoAsync(
        string dataRoot,
        string repoRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var dataRootFull = Path.GetFullPath(dataRoot);
        var repoRootFull = Path.GetFullPath(repoRoot);

        var exportRows = await GetApprovedExportRowsAsync(cancellationToken).ConfigureAwait(false);

        var copiedPaths = new List<string>(exportRows.Count);
        foreach (var row in exportRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Why: containment-guard both source (under dataRoot) and dest (under repoRoot)
            // to prevent path traversal out of the data dir or the repo tree (T-46-02-06).
            var sourceFull = ResolveContainedPath(dataRootFull, row.ArtifactPath);
            var destFull = ResolveContainedPath(repoRootFull, row.ArtifactPath);

            // Why: missing/unreadable source is a publish-blocking error (D-10); never
            // silently skip — callers must not commit a seed referencing absent files.
            if (!File.Exists(sourceFull))
            {
                throw new InvalidOperationException(
                    $"Approved artifact source missing: {row.ArtifactPath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destFull)!);
            File.Copy(sourceFull, destFull, overwrite: true);

            copiedPaths.Add(row.ArtifactPath);
        }

        return copiedPaths;
    }

    /// <summary>
    /// Resolves <paramref name="relativePath"/> under <paramref name="rootFull"/> and asserts
    /// the result is strictly contained within that root (no traversal, no rooted paths,
    /// no git pathspec-magic). Throws <see cref="ArgumentException"/> on any violation.
    /// </summary>
    private static string ResolveContainedPath(string rootFull, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Artifact path must not be null or whitespace.", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException(
                $"Artifact path must be relative, not rooted: {relativePath}", nameof(relativePath));
        }

        // Why: leading ':' is a git pathspec-magic prefix; reject to prevent git injection (T-46-02-06).
        if (relativePath.StartsWith(":", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Artifact path must not start with ':' (git pathspec-magic): {relativePath}", nameof(relativePath));
        }

        // Why: any '..' segment could escape the root; validate every segment.
        var segments = relativePath.Split('/', '\\');
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                throw new ArgumentException(
                    $"Artifact path must not contain '..' traversal segments: {relativePath}", nameof(relativePath));
            }
        }

        var fullPath = Path.GetFullPath(Path.Combine(rootFull, relativePath));

        // Why: belt-and-suspenders — ensure the resolved absolute path is strictly under root.
        if (!fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Artifact path '{relativePath}' resolves outside the expected root '{rootFull}'.",
                nameof(relativePath));
        }

        return fullPath;
    }

    /// <summary>
    /// Fetches approved rows from the index store and projects them to export rows.
    /// Shared by <see cref="ExportIndexAsync"/>, <see cref="ExportIndexToFileAsync"/>, and
    /// <see cref="CopyApprovedArtifactsToRepoAsync"/> so all three produce exactly the same
    /// approved-row set.
    /// </summary>
    private async Task<List<ContentIndexExportRow>> GetApprovedExportRowsAsync(
        CancellationToken cancellationToken)
    {
        await _indexStore.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        var rows = await _indexStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(ContentIndexExportRow.From).ToList();
    }

    private async Task<HarvestResult> HarvestExplicitVideoIdsAsync(
        IReadOnlyList<ContentSource> sources,
        IReadOnlyList<string> videoIds,
        long? sourceId,
        IOrchestratorProgress? progress,
        CancellationToken cancellationToken)
    {
        var youtubeSources = sources
            .Where(source => source.SourceType == ContentSourceType.Youtube)
            .ToList();
        ContentSource? target;
        if (sourceId is { } id)
        {
            target = youtubeSources.FirstOrDefault(source => source.Id == id);
            if (target is null)
            {
                return new HarvestResult
                {
                    Success = false,
                    Message = $"--source-id {id} does not match an enabled YouTube source.",
                };
            }
        }
        else if (youtubeSources.Count == 1)
        {
            target = youtubeSources[0];
        }
        else
        {
            return new HarvestResult
            {
                Success = false,
                Message = $"--video-ids needs a single target source but {youtubeSources.Count} YouTube sources are enabled; pass --source-id.",
            };
        }

        var videos = await _lister.GetByIdsAsync(videoIds, cancellationToken).ConfigureAwait(false);
        if (videos.Count < videoIds.Count)
        {
            var resolved = videos.Select(video => video.VideoId).ToHashSet(StringComparer.Ordinal);
            foreach (var missing in videoIds.Where(requested => !resolved.Contains(requested)))
            {
                _logger.LogWarning("requested video id did not resolve {VideoId}", missing);
            }
        }

        var counts = new HarvestCounts();
        foreach (var video in videos)
        {
            await HarvestVideoAsync(target, video, counts, progress, cancellationToken).ConfigureAwait(false);
        }

        LogFallbackRatio(target.SourceSlug, counts);
        return new HarvestResult
        {
            Success = true,
            Captions = counts.Captions,
            Whisper = counts.Whisper,
            SkippedNoCaptions = counts.SkippedNoCaptions,
            WhisperFallbackRatio = counts.WhisperFallbackRatio,
        };
    }

    private async Task<HarvestCounts> HarvestSourceAsync(
        ContentSource source,
        int limit,
        IOrchestratorProgress? progress,
        CancellationToken cancellationToken)
    {
        var counts = new HarvestCounts();
        var videos = await _lister.ListRecentAsync(source.SourceUrl, limit, ct: cancellationToken).ConfigureAwait(false);
        foreach (var video in videos)
        {
            await HarvestVideoAsync(source, video, counts, progress, cancellationToken).ConfigureAwait(false);
        }

        LogFallbackRatio(source.SourceSlug, counts);
        return counts;
    }

    private async Task HarvestVideoAsync(
        ContentSource source,
        YouTubeChannelVideo video,
        HarvestCounts counts,
        IOrchestratorProgress? progress,
        CancellationToken cancellationToken)
    {
        if (video.Duration is { } duration && duration <= DistillationValidation.ShortVideoMaxDuration)
        {
            _logger.LogInformation("skipped short {VideoId} duration_s={DurationSeconds}", video.VideoId, (int)duration.TotalSeconds);
            progress?.Report($"skipped short {video.VideoId} duration_s={(int)duration.TotalSeconds}");
            return;
        }

        if (await _blockedVideoStore.IsBlockedAsync(video.VideoId, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("skipped blocked {VideoId}", video.VideoId);
            progress?.Report($"skipped blocked {video.VideoId}");
            return;
        }

        long? contentVideoId = null;
        var mayMarkFailed = false;
        var statusPersisted = false;
        try
        {
            var resolution = await ResolveHarvestVideoIdAsync(source, video, progress, cancellationToken).ConfigureAwait(false);
            contentVideoId = resolution.VideoId;
            mayMarkFailed = resolution.MayMarkFailed;
            if (contentVideoId is null)
            {
                return;
            }

            var monthKey = _utcNow().UtcDateTime.ToString("yyyy-MM");
            var result = await _transcriptSource.FetchTranscriptAsync(video.VideoId, video.Duration, monthKey, cancellationToken).ConfigureAwait(false);
            statusPersisted = await PersistTranscriptResultAsync(contentVideoId.Value, result, monthKey, cancellationToken).ConfigureAwait(false);
            counts.Add(result.Outcome);
            LogFetch(video.VideoId, result, progress);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await MarkFailedIfPossibleAsync(contentVideoId, mayMarkFailed && !statusPersisted, cancellationToken).ConfigureAwait(false);
            _logger.LogError(exception, "harvest failed {VideoId}", video.VideoId);
            progress?.Report($"harvest failed {video.VideoId}");
        }
    }

    private async Task<HarvestVideoResolution> ResolveHarvestVideoIdAsync(
        ContentSource source,
        YouTubeChannelVideo video,
        IOrchestratorProgress? progress,
        CancellationToken cancellationToken)
    {
        var existing = await _videoStore.GetVideoByYoutubeIdAsync(source.Id, video.VideoId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (IsTerminalSuccess(existing.TranscriptStatus))
            {
                _logger.LogInformation(
                    "already harvested {VideoId} transcript_status={TranscriptStatus}",
                    video.VideoId,
                    existing.TranscriptStatus);
                progress?.Report($"already harvested {video.VideoId} transcript_status={existing.TranscriptStatus}");
                return new HarvestVideoResolution(null, MayMarkFailed: false);
            }

            _logger.LogInformation(
                "resuming harvest {VideoId} transcript_status={TranscriptStatus}",
                video.VideoId,
                existing.TranscriptStatus);
            progress?.Report($"resuming harvest {video.VideoId} transcript_status={existing.TranscriptStatus}");
            return new HarvestVideoResolution(existing.Id, existing.TranscriptStatus == TranscriptStatus.Pending);
        }

        var videoId = await _videoStore.InsertVideoAsync(
            source.Id,
            video.VideoId,
            rssGuid: null,
            video.Title,
            video.Url,
            video.PublishedUtc,
            TranscriptStatus.Pending,
            cancellationToken).ConfigureAwait(false);
        return new HarvestVideoResolution(videoId, MayMarkFailed: true);
    }

    private async Task<bool> PersistTranscriptResultAsync(
        long videoId,
        TranscriptFetchResult result,
        string monthKey,
        CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case TranscriptOutcome.Captions:
                await _videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Captions, result.Body!, cancellationToken).ConfigureAwait(false);
                await _videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Captions, cancellationToken).ConfigureAwait(false);
                return true;
            case TranscriptOutcome.Whisper:
                // Record spend first: a ledger row without a transcript is conservative;
                // a transcript/status without a ledger row under-counts the monthly cap.
                await _whisperLedger.RecordCallAsync(videoId, result.SecondsBilled!.Value, result.CostUsd!.Value, monthKey, cancellationToken).ConfigureAwait(false);
                await _videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Whisper, result.Body!, cancellationToken).ConfigureAwait(false);
                await _videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Whisper, cancellationToken).ConfigureAwait(false);
                return true;
            case TranscriptOutcome.SkippedOverCap:
                await _videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.SkippedOverCap, cancellationToken).ConfigureAwait(false);
                return true;
            case TranscriptOutcome.SkippedNoCaptions:
                await _videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.SkippedNoCaptions, cancellationToken).ConfigureAwait(false);
                return true;
            case TranscriptOutcome.Failed:
                await _videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Failed, cancellationToken).ConfigureAwait(false);
                return true;
        }

        return false;
    }

    private async Task WarnIfFfmpegUnavailableAsync(IOrchestratorProgress? progress, CancellationToken cancellationToken)
    {
        if (!await _chunker.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("ffmpeg not found on PATH - audio >24MB will be marked failed.");
            progress?.Report("ffmpeg not found on PATH - audio >24MB will be marked failed.");
        }
    }

    private async Task MarkFailedIfPossibleAsync(long? videoId, bool mayMarkFailed, CancellationToken cancellationToken)
    {
        if (videoId is not null && mayMarkFailed)
        {
            await _videoStore.UpdateTranscriptStatusAsync(videoId.Value, TranscriptStatus.Failed, cancellationToken).ConfigureAwait(false);
        }
    }

    private void LogFetch(string videoId, TranscriptFetchResult result, IOrchestratorProgress? progress)
    {
        _logger.LogInformation(
            "harvested {VideoId} transcript_source={TranscriptSource} caption_track_kind={CaptionTrackKind} outcome={Outcome}",
            videoId,
            result.Source,
            GetCaptionTrackKind(result),
            result.Outcome);
        progress?.Report(
            $"harvested {videoId} transcript_source={result.Source} caption_track_kind={GetCaptionTrackKind(result)} outcome={result.Outcome}");
    }

    private void LogFallbackRatio(string sourceSlug, HarvestCounts counts)
        => _logger.LogInformation(
            "harvest source={SourceSlug} captions={Captions} whisper={Whisper} whisper_fallback_ratio={WhisperFallbackRatio:F3}",
            sourceSlug,
            counts.Captions,
            counts.Whisper,
            counts.WhisperFallbackRatio);

    private static bool IsTerminalSuccess(string transcriptStatus)
        => transcriptStatus is TranscriptStatus.Captions or TranscriptStatus.Whisper;

    private static string? GetCaptionTrackKind(TranscriptFetchResult result)
    {
        if (result.Outcome != TranscriptOutcome.Captions || result.IsAutoGenerated is null)
        {
            return null;
        }

        return result.IsAutoGenerated.Value ? "auto_generated" : "manual";
    }

    private async Task<DistillVideoOutcome> DistillVideoAsync(
        ContentSource source,
        ContentVideo video,
        string monthKey,
        DateTimeOffset generatedUtc,
        IOrchestratorProgress? progress,
        CancellationToken cancellationToken,
        bool isSubscriptionProvider = false)
    {
        var naturalKey = GetContentNaturalKey(video);
        var sw = Stopwatch.StartNew();
        var llmCalls = 0;
        var llmSpend = 0m;
        try
        {
            var transcript = await _videoStore.GetLatestTranscriptAsync(video.Id, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Transcript missing for {naturalKey}.");
            DistillationValidation.ValidateTranscriptLength(transcript.Body);

            var classification = await _distiller.ClassifyAsync(transcript.Body, cancellationToken).ConfigureAwait(false);
            if (string.Equals(classification.Verdict, "drop", StringComparison.OrdinalIgnoreCase))
            {
                var (naturalKeyType, naturalKeyValue) = GetContentNaturalKeyInfo(video);
                await _videoStore.SetDistillStatusAsync(video.Id, DistillationValidation.DistillStatusFiltered, cancellationToken).ConfigureAwait(false);
                await _videoStore.ClearDistillOutputAsync(video.Id, cancellationToken).ConfigureAwait(false);

                var existingIndexRow = await _indexStore
                    .GetByNaturalKeyAsync(naturalKeyType, naturalKeyValue, cancellationToken)
                    .ConfigureAwait(false);
                if (existingIndexRow is not null)
                {
                    await _indexStore.DeleteByIdAsync(existingIndexRow.Id, cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation("filtered {VideoId} reason={Reason}", naturalKey, classification.Reason);
                progress?.Report($"filtered {naturalKey} reason={classification.Reason} ({sw.Elapsed.TotalSeconds:F1}s)");
                return DistillVideoOutcome.Filtered();
            }

            await _videoStore.ClearDistillOutputAsync(video.Id, cancellationToken).ConfigureAwait(false);

            if (!isSubscriptionProvider && await _llmLedger.WouldExceedCapAsync(
                DistillationValidation.ComputeProjectedCallCostUsd(transcript.Body, DistillationValidation.SummaryMaxOutputTokens),
                monthKey,
                cancellationToken).ConfigureAwait(false))
            {
                return await MarkSkippedOverCapAsync(
                    video.Id,
                    naturalKey,
                    "llm monthly cap would be exceeded before summary for " + naturalKey,
                    llmCalls,
                    llmSpend,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            var summary = await _distiller.SummarizeAsync(transcript.Body, cancellationToken).ConfigureAwait(false);
            var summaryCost = isSubscriptionProvider ? 0m : LlmSpendLedger.ComputeCostUsd(summary.Usage.InputTokens, summary.Usage.OutputTokens);
            // Why: each OpenAI call is separately billed; record its incurred cost BEFORE the next call so a later-call failure can never orphan an already-billed cost (HIGH-1/FIX-1, Phase 20 CR-01 class -- recorded spend >= incurred).
            await _llmLedger.RecordCallAsync(
                video.Id,
                summary.Usage.InputTokens,
                summary.Usage.OutputTokens,
                summaryCost,
                monthKey,
                cancellationToken).ConfigureAwait(false);
            llmCalls++;
            llmSpend += summaryCost;

            if (!isSubscriptionProvider && await _llmLedger.WouldExceedCapAsync(
                DistillationValidation.ComputeProjectedCallCostUsd(transcript.Body, DistillationValidation.ClipsMaxOutputTokens),
                monthKey,
                cancellationToken).ConfigureAwait(false))
            {
                return await MarkSkippedOverCapAsync(
                    video.Id,
                    naturalKey,
                    "llm monthly cap would be exceeded before clips for " + naturalKey,
                    llmCalls,
                    llmSpend,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            var clips = await _distiller.ExtractClipsAsync(transcript.Body, cancellationToken).ConfigureAwait(false);
            var clipsCost = isSubscriptionProvider ? 0m : LlmSpendLedger.ComputeCostUsd(clips.Usage.InputTokens, clips.Usage.OutputTokens);
            await _llmLedger.RecordCallAsync(
                video.Id,
                clips.Usage.InputTokens,
                clips.Usage.OutputTokens,
                clipsCost,
                monthKey,
                cancellationToken).ConfigureAwait(false);
            llmCalls++;
            llmSpend += clipsCost;

            if (!isSubscriptionProvider && await _llmLedger.WouldExceedCapAsync(
                DistillationValidation.ComputeProjectedCallCostUsd(transcript.Body, DistillationValidation.TagsMaxOutputTokens),
                monthKey,
                cancellationToken).ConfigureAwait(false))
            {
                return await MarkSkippedOverCapAsync(
                    video.Id,
                    naturalKey,
                    "llm monthly cap would be exceeded before tags for " + naturalKey,
                    llmCalls,
                    llmSpend,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            var tags = await _distiller.InferTagsAsync(transcript.Body, cancellationToken).ConfigureAwait(false);
            var tagsCost = isSubscriptionProvider ? 0m : LlmSpendLedger.ComputeCostUsd(tags.Usage.InputTokens, tags.Usage.OutputTokens);
            await _llmLedger.RecordCallAsync(
                video.Id,
                tags.Usage.InputTokens,
                tags.Usage.OutputTokens,
                tagsCost,
                monthKey,
                cancellationToken).ConfigureAwait(false);
            llmCalls++;
            llmSpend += tagsCost;

            DistillationValidation.ValidateSummary(summary.Summary);
            DistillationValidation.ValidateClips(clips.Clips);
            var archetypeTags = FilterTags(ContentTagDimension.Archetype, tags.Archetype);
            var bracketTags = FilterTags(ContentTagDimension.Bracket, tags.Bracket);
            var cardCategoryTags = FilterTags(ContentTagDimension.CardCategory, tags.CardCategory);

            await _videoStore.InsertSummaryAsync(video.Id, summary.Summary, cancellationToken).ConfigureAwait(false);
            var sortOrder = 0;
            foreach (var clip in clips.Clips)
            {
                // Why: 0 is a STORAGE sentinel for unknown timestamp (timestamp_s is NOT NULL); the artifact renders the [mm:ss] omission from the in-memory nullable clip, never from this row (MEDIUM-3/D-08).
                await _videoStore.InsertClipAsync(
                    video.Id,
                    clip.TimestampSeconds ?? 0,
                    clip.Excerpt,
                    sortOrder++,
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var tag in archetypeTags)
            {
                await _videoStore.InsertTagAsync(video.Id, ContentTagDimension.Archetype, tag, cancellationToken).ConfigureAwait(false);
            }

            foreach (var tag in bracketTags)
            {
                await _videoStore.InsertTagAsync(video.Id, ContentTagDimension.Bracket, tag, cancellationToken).ConfigureAwait(false);
            }

            foreach (var tag in cardCategoryTags)
            {
                await _videoStore.InsertTagAsync(video.Id, ContentTagDimension.CardCategory, tag, cancellationToken).ConfigureAwait(false);
            }

            var metadata = new ContentArtifactMetadata
            {
                Source = source.DisplayName,
                Title = video.Title,
                Url = video.VideoUrl,
                YoutubeVideoId = video.YoutubeVideoId,
                RssGuid = video.RssGuid,
                ArchetypeTags = archetypeTags,
                BracketTags = bracketTags,
                CardCategoryTags = cardCategoryTags,
                GeneratedUtc = generatedUtc,
            };
            var artifactText = ContentArtifactWriter.ToText(
                metadata,
                summary.Summary,
                clips.Clips.Select(clip => (clip.TimestampSeconds, clip.Excerpt)).ToArray());
            ContentArtifactWriter.WriteFile(_artifactRoot, source.SourceSlug, naturalKey, artifactText);
            await _indexStore.UpsertContentColumnsOnlyAsync(
                new ContentSiteIndexRow
                {
                    Id = 0,
                    Source = source.DisplayName,
                    Title = video.Title,
                    VideoUrl = video.VideoUrl,
                    ArtifactPath = ContentArtifactWriter.ComputeRelativeArtifactPath(source.SourceSlug, naturalKey),
                    PublishedUtc = video.PublishedUtc,
                    IndexedUtc = generatedUtc,
                    ArchetypeTags = archetypeTags,
                    BracketTags = bracketTags,
                    CardCategoryTags = cardCategoryTags,
                    YoutubeVideoId = video.YoutubeVideoId,
                    RssGuid = video.RssGuid,
                },
                cancellationToken).ConfigureAwait(false);
            await _videoStore.SetDistillStatusAsync(video.Id, DistillationValidation.DistillStatusDistilled, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("distilled {VideoId}", naturalKey);
            progress?.Report($"distilled {naturalKey} ({sw.Elapsed.TotalSeconds:F1}s)");
            return DistillVideoOutcome.Distilled(llmCalls, llmSpend);
        }
        catch (LlmCliConfigurationException ex)
        {
            // Why: config errors are not the video's fault; do NOT mark the video Failed.
            // Return AbortedConfig so the outer loop sets abortedReason + stopRun on the first video,
            // surfacing one clear message instead of N "distill failed" lines (quick 260615-c9e).
            _logger.LogError(ex, "distill aborted — distiller CLI not configured");
            progress?.Report($"distill aborted — distiller CLI not configured: {ex.Message}");
            return DistillVideoOutcome.AbortedConfig(llmCalls, llmSpend, $"Distiller CLI not configured: {ex.Message}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _videoStore.SetDistillStatusAsync(video.Id, DistillationValidation.DistillStatusFailed, cancellationToken).ConfigureAwait(false);
            _logger.LogError(exception, "distill failed {VideoId}", naturalKey);
            progress?.Report($"distill failed {naturalKey} ({sw.Elapsed.TotalSeconds:F1}s)");
            return DistillVideoOutcome.Failed(llmCalls, llmSpend, naturalKey);
        }
    }

    private async Task<DistillVideoOutcome> MarkSkippedOverCapAsync(
        long videoId,
        string naturalKey,
        string abortedReason,
        int llmCalls,
        decimal llmSpend,
        IOrchestratorProgress? progress,
        CancellationToken cancellationToken)
    {
        await _videoStore.SetDistillStatusAsync(videoId, DistillationValidation.DistillStatusSkippedOverCap, cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("distill skipped_over_cap {VideoId} reason={AbortedReason}", naturalKey, abortedReason);
        progress?.Report($"distill skipped_over_cap {naturalKey} reason={abortedReason}");
        return DistillVideoOutcome.SkippedOverCap(llmCalls, llmSpend, abortedReason);
    }

    private IReadOnlyList<string> FilterTags(string dimension, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var valid = new List<string>();
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag)
                && ContentTagVocabulary.IsValid(dimension, tag))
            {
                if (!valid.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    valid.Add(tag);
                }

                continue;
            }

            _logger.LogWarning("dropped out-of-vocab tag {Dimension} {Tag}", dimension, tag);
        }

        return valid;
    }

    private static string GetContentNaturalKey(ContentVideo video)
        => GetContentNaturalKeyInfo(video).NaturalKeyValue;

    private static (string NaturalKeyType, string NaturalKeyValue) GetContentNaturalKeyInfo(ContentVideo video)
    {
        var hasYoutubeVideoId = !string.IsNullOrWhiteSpace(video.YoutubeVideoId);
        var hasRssGuid = !string.IsNullOrWhiteSpace(video.RssGuid);
        if (hasYoutubeVideoId == hasRssGuid)
        {
            throw new InvalidOperationException("Exactly one content natural key is required for distillation.");
        }

        return hasYoutubeVideoId
            ? (ContentSourceType.Youtube, video.YoutubeVideoId!)
            : (ContentSourceType.Podcast, video.RssGuid!);
    }

    private static async Task<ContentSourceResult> HandleContentSourceUniqueViolationAsync(
        IContentSourceStore store,
        string slug,
        string url,
        Exception exception,
        IOrchestratorProgress? progress,
        CancellationToken cancellationToken)
    {
        var sources = await store.ListEnabledSourcesAsync(cancellationToken).ConfigureAwait(false);
        if (sources.Any(source => string.Equals(source.SourceUrl, url, StringComparison.Ordinal)))
        {
            progress?.Report("source already exists (same url)");
            return new ContentSourceResult
            {
                Success = true,
                Outcome = ContentSourceResult.ContentSourceOutcome.AlreadyExistsSameUrl,
                Slug = slug,
                Message = "source already exists (same url)",
            };
        }

        if (sources.Any(source => string.Equals(source.SourceSlug, slug, StringComparison.Ordinal))
            || ExceptionContains(exception, "source_slug"))
        {
            return new ContentSourceResult
            {
                Success = false,
                Outcome = ContentSourceResult.ContentSourceOutcome.SlugConflict,
                Slug = slug,
                Message = $"slug '{slug}' already used by a different url - pass a distinct --name",
            };
        }

        return new ContentSourceResult
        {
            Success = false,
            Outcome = ContentSourceResult.ContentSourceOutcome.Error,
            Slug = slug,
            Message = exception.Message,
        };
    }

    private static bool IsValidContentSourceType(string type)
        => type is ContentSourceType.Youtube or ContentSourceType.Podcast;

    private static bool IsContentSourceUniqueViolation(Exception exception)
        => (ExceptionContains(exception, "UNIQUE") || ExceptionContains(exception, "duplicate key"))
            && ExceptionContains(exception, "content_sources");

    private static bool ExceptionContains(Exception exception, string value)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class DistillCounts
    {
        public int SourcesProcessed { get; set; }

        public int VideosDistilled { get; private set; }

        public int VideosFiltered { get; private set; }

        public int DistillFailed { get; private set; }

        public int LlmCalls { get; private set; }

        public decimal LlmSpendUsd { get; private set; }

        public int WouldRun { get; set; }

        public decimal ProjectedSpendUsd { get; set; }

        public List<string> FailedVideoIds { get; } = [];

        public void Add(DistillVideoOutcome outcome)
        {
            LlmCalls += outcome.LlmCalls;
            LlmSpendUsd += outcome.LlmSpendUsd;
            if (outcome.IsDistilled)
            {
                VideosDistilled++;
            }

            if (outcome.IsFiltered)
            {
                VideosFiltered++;
            }

            if (outcome.FailedVideoId is not null)
            {
                DistillFailed++;
                FailedVideoIds.Add(outcome.FailedVideoId);
            }
        }
    }

    private sealed record DistillVideoOutcome(
        bool IsDistilled,
        bool IsFiltered,
        int LlmCalls,
        decimal LlmSpendUsd,
        string? FailedVideoId,
        string? AbortedReason)
    {
        public static DistillVideoOutcome Distilled(int llmCalls, decimal llmSpendUsd)
            => new(true, false, llmCalls, llmSpendUsd, FailedVideoId: null, AbortedReason: null);

        public static DistillVideoOutcome Failed(int llmCalls, decimal llmSpendUsd, string failedVideoId)
            => new(false, false, llmCalls, llmSpendUsd, failedVideoId, AbortedReason: null);

        public static DistillVideoOutcome Filtered()
            => new(false, true, 0, 0m, FailedVideoId: null, AbortedReason: null);

        public static DistillVideoOutcome SkippedOverCap(int llmCalls, decimal llmSpendUsd, string abortedReason)
            => new(false, false, llmCalls, llmSpendUsd, FailedVideoId: null, abortedReason);

        public static DistillVideoOutcome AbortedConfig(int llmCalls, decimal llmSpendUsd, string reason)
            => new(false, false, llmCalls, llmSpendUsd, FailedVideoId: null, AbortedReason: reason);
    }

    private sealed class HarvestCounts
    {
        public int Captions { get; private set; }

        public int Whisper { get; private set; }

        public int SkippedNoCaptions { get; private set; }

        public double WhisperFallbackRatio
        {
            get
            {
                var successes = Captions + Whisper;
                return successes == 0 ? 0d : (double)Whisper / successes;
            }
        }

        public void Add(TranscriptOutcome outcome)
        {
            if (outcome == TranscriptOutcome.Captions)
            {
                Captions++;
            }
            else if (outcome == TranscriptOutcome.Whisper)
            {
                Whisper++;
            }
            else if (outcome == TranscriptOutcome.SkippedNoCaptions)
            {
                SkippedNoCaptions++;
            }
        }

        public void Add(HarvestCounts counts)
        {
            Captions += counts.Captions;
            Whisper += counts.Whisper;
            SkippedNoCaptions += counts.SkippedNoCaptions;
        }
    }

    private sealed record HarvestVideoResolution(long? VideoId, bool MayMarkFailed);
}
