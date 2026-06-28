using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Storage;
using Serilog;

namespace DeckFlow.CLI;

internal static class ContentKbCommandRunners
{
    public static async Task<int> RunContentSourceAddAsync(string url, string name, string type, FileInfo? db)
    {
        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var artifactRoot = ContentKbCliPaths.ResolveArtifactRoot(db);
            var orchestrator = CreateSqliteOrchestrator(
                dbPath,
                artifactRoot,
                distiller: new ThrowingLlmDistillationService(),
                lister: new ThrowingYouTubeChannelVideoLister(),
                transcriptSource: new ThrowingTranscriptSource(),
                chunker: new ThrowingFfmpegAudioChunker());
            var result = await orchestrator
                .AddSourceAsync(url, name, type, new ConsoleOrchestratorProgress())
                .ConfigureAwait(false);

            return result.Outcome switch
            {
                ContentSourceResult.ContentSourceOutcome.Added => 0,
                ContentSourceResult.ContentSourceOutcome.AlreadyExistsSameUrl => 0,
                ContentSourceResult.ContentSourceOutcome.InvalidType => WriteErrorAndReturn(result.Message, 2),
                ContentSourceResult.ContentSourceOutcome.SlugConflict => WriteErrorAndReturn(result.Message, 3),
                _ => WriteErrorAndReturn(result.Message, 1),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static async Task<int> RunContentSourceSetEnabledAsync(
        long id,
        bool enabled,
        FileInfo? db,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var artifactRoot = ContentKbCliPaths.ResolveArtifactRoot(db);
            var orchestrator = CreateSqliteOrchestrator(
                dbPath,
                artifactRoot,
                distiller: new ThrowingLlmDistillationService(),
                lister: new ThrowingYouTubeChannelVideoLister(),
                transcriptSource: new ThrowingTranscriptSource(),
                chunker: new ThrowingFfmpegAudioChunker());
            var result = await orchestrator
                .SetSourceEnabledAsync(id, enabled, new ConsoleOrchestratorProgress(), ct)
                .ConfigureAwait(false);

            return result.Success ? 0 : WriteErrorAndReturn(result.Message, 1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Content source set-enabled failed {SourceId}", id);
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static async Task<int> RunDistillAsync(
        FileInfo? db,
        int limit,
        bool dryRun,
        Serilog.ILogger logger,
        CancellationToken ct,
        IReadOnlyList<string>? videoIds = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var artifactRoot = ContentKbCliPaths.ResolveArtifactRoot(db);
            using var llmHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            var providerEnv = Environment.GetEnvironmentVariable(LlmDistillationProviderFactory.EnvironmentVariableName);
            var isSubscriptionProvider = LlmDistillationProviderFactory.IsSubscriptionProvider(providerEnv);
            var orchestrator = CreateSqliteOrchestrator(
                dbPath,
                artifactRoot,
                distiller: LlmDistillationProviderFactory.Resolve(providerEnv, llmHttpClient),
                lister: new ThrowingYouTubeChannelVideoLister(),
                transcriptSource: new ThrowingTranscriptSource(),
                chunker: new ThrowingFfmpegAudioChunker());
            var result = await orchestrator
                .DistillAsync(limit, dryRun, isSubscriptionProvider, videoIds: videoIds, progress: new ConsoleOrchestratorProgress(), cancellationToken: ct)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                Console.Error.WriteLine(result.AbortedReason);
                return 1;
            }

            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Content KB distill failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Blocks a harvested YouTube video id and hard-deletes existing local KB rows for it.
    /// </summary>
    /// <param name="db">Optional path to the content KB database.</param>
    /// <param name="youtubeVideoId">YouTube video identifier to block.</param>
    /// <param name="reason">Optional operator-supplied reason.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunBlockVideoAsync(
        FileInfo? db,
        string youtubeVideoId,
        string? reason,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var artifactRoot = ContentKbCliPaths.ResolveArtifactRoot(db);
            var orchestrator = CreateSqliteOrchestrator(
                dbPath,
                artifactRoot,
                distiller: new ThrowingLlmDistillationService(),
                lister: new ThrowingYouTubeChannelVideoLister(),
                transcriptSource: new ThrowingTranscriptSource(),
                chunker: new ThrowingFfmpegAudioChunker());
            var result = await orchestrator
                .BlockVideoAsync(youtubeVideoId, reason, new ConsoleOrchestratorProgress(), ct)
                .ConfigureAwait(false);

            return result.Success ? 0 : WriteErrorAndReturn(result.Message, 1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Block video failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Deletes all content video and site-index rows while preserving source config and blocked videos.
    /// </summary>
    /// <param name="db">Optional SQLite path to the content KB database.</param>
    /// <param name="connectionString">Optional Postgres connection string for a non-SQLite reset target.</param>
    /// <param name="dryRun">When true, report without deleting.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunCorpusResetAsync(
        FileInfo? db,
        string? connectionString,
        bool dryRun,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            ContentKbOrchestrator orchestrator;
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                var connection = new RelationalDatabaseConnection(
                    RelationalDatabaseProvider.Postgres,
                    PostgresConnectionStringNormalizer.Normalize(connectionString));
                orchestrator = CreateConnectionOrchestrator(
                    connection,
                    ContentKbCliPaths.ResolveArtifactRoot(db),
                    distiller: new ThrowingLlmDistillationService(),
                    lister: new ThrowingYouTubeChannelVideoLister(),
                    transcriptSource: new ThrowingTranscriptSource(),
                    chunker: new ThrowingFfmpegAudioChunker());
            }
            else
            {
                var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
                orchestrator = CreateSqliteOrchestrator(
                    dbPath,
                    ContentKbCliPaths.ResolveArtifactRoot(db),
                    distiller: new ThrowingLlmDistillationService(),
                    lister: new ThrowingYouTubeChannelVideoLister(),
                    transcriptSource: new ThrowingTranscriptSource(),
                    chunker: new ThrowingFfmpegAudioChunker());
            }

            var result = await orchestrator
                .ResetCorpusAsync(dryRun, new ConsoleOrchestratorProgress(), ct)
                .ConfigureAwait(false);

            return result.Success ? 0 : WriteErrorAndReturn(result.Message, 1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Corpus reset failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Unblocks a harvested YouTube video id so later harvest runs may re-ingest it.
    /// </summary>
    /// <param name="db">Optional path to the content KB database.</param>
    /// <param name="youtubeVideoId">YouTube video identifier to unblock.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunUnblockVideoAsync(
        FileInfo? db,
        string youtubeVideoId,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var artifactRoot = ContentKbCliPaths.ResolveArtifactRoot(db);
            var orchestrator = CreateSqliteOrchestrator(
                dbPath,
                artifactRoot,
                distiller: new ThrowingLlmDistillationService(),
                lister: new ThrowingYouTubeChannelVideoLister(),
                transcriptSource: new ThrowingTranscriptSource(),
                chunker: new ThrowingFfmpegAudioChunker());
            var result = await orchestrator
                .UnblockVideoAsync(youtubeVideoId, new ConsoleOrchestratorProgress(), ct)
                .ConfigureAwait(false);

            return result.Success ? 0 : WriteErrorAndReturn(result.Message, 1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Unblock video failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Lists blocked harvested YouTube video ids.
    /// </summary>
    /// <param name="db">Optional path to the content KB database.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunListBlockedAsync(
        FileInfo? db,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var artifactRoot = ContentKbCliPaths.ResolveArtifactRoot(db);
            var orchestrator = CreateSqliteOrchestrator(
                dbPath,
                artifactRoot,
                distiller: new ThrowingLlmDistillationService(),
                lister: new ThrowingYouTubeChannelVideoLister(),
                transcriptSource: new ThrowingTranscriptSource(),
                chunker: new ThrowingFfmpegAudioChunker());
            var result = await orchestrator
                .ListBlockedAsync(cancellationToken: ct)
                .ConfigureAwait(false);

            foreach (var item in result.Items)
            {
                Console.Out.WriteLine($"{item.YoutubeVideoId}\t{item.BlockedUtc:O}\t{item.Reason ?? string.Empty}");
            }

            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "List blocked videos failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Exports the local Content KB site index to a JSON seed file.
    /// </summary>
    /// <param name="db">Optional path to the content KB database.</param>
    /// <param name="output">Optional destination path for the seed file.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunContentIndexExportAsync(FileInfo? db, FileInfo? output)
    {
        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var artifactRoot = ContentKbCliPaths.ResolveArtifactRoot(db);
            var orchestrator = CreateSqliteOrchestrator(
                dbPath,
                artifactRoot,
                distiller: new ThrowingLlmDistillationService(),
                lister: new ThrowingYouTubeChannelVideoLister(),
                transcriptSource: new ThrowingTranscriptSource(),
                chunker: new ThrowingFfmpegAudioChunker());
            var result = await orchestrator.ExportIndexAsync().ConfigureAwait(false);
            if (!result.Success)
            {
                Console.Error.WriteLine(result.Message);
                return 1;
            }

            var outputPath = output?.FullName ?? Path.Combine("content-kb", "seed", "index-seed.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
            await File.WriteAllTextAsync(outputPath, SerializeContentIndexExportRows(result.Rows)).ConfigureAwait(false);
            Console.WriteLine($"Exported {result.RowCount} rows to {outputPath}");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Read-only consistency check: classifies each local <c>content_site_index</c> row against the
    /// artifact files on disk and reports OK / published-orphan / hidden-orphan, exiting 1 when any
    /// PUBLISHED orphan (visible row missing its artifact) exists.
    /// </summary>
    /// <param name="db">Optional path to the LOCAL content KB database (never prod).</param>
    /// <param name="artifactRoot">
    /// Optional artifact directory. Accepts EITHER the data-root parent of <c>content-kb/</c> OR the
    /// <c>content-kb</c> directory itself — the handler normalizes both to a content base.
    /// </param>
    /// <returns>Process exit code: 1 when a published orphan exists, else 0.</returns>
    public static async Task<int> RunContentKbCheckAsync(FileInfo? db, DirectoryInfo? artifactRoot)
    {
        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var rawRoot = artifactRoot?.FullName ?? ContentKbCliPaths.ResolveArtifactRoot(db);
            var contentBase = NormalizeToContentBase(rawRoot);

            var store = new ContentSiteIndexStore(dbPath);
            var rows = await store.GetAllRowsAsync().ConfigureAwait(false);
            var result = ContentKbOrphanScanner.Scan(rows, contentBase);

            foreach (var check in result.Rows)
            {
                var marker = check.Exists ? "OK     " : "MISSING";
                var visibility = check.IsVisible ? "visible" : "not visible";
                Console.WriteLine($"  {marker}  {check.ArtifactPath} ({visibility}, approval={check.ApprovalStatus})");
            }

            Console.WriteLine();
            Console.WriteLine($"Total rows: {result.TotalRows}");
            Console.WriteLine($"Rows with artifact: {result.RowsWithArtifact}");
            Console.WriteLine($"Missing artifacts: {result.MissingCount}");
            Console.WriteLine($"  Published (missing): {result.PublishedOrphanCount}");
            Console.WriteLine($"  Unpublished (missing): {result.HiddenOrphanCount}");

            return result.PublishedOrphanCount > 0 ? 1 : 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Normalizes a supplied artifact directory to a content base (the parent that CONTAINS
    /// <c>content-kb/</c>), mirroring <c>ContentKbArtifactPathResolver.ContentBase</c>. When the
    /// directory's final segment is <c>content-kb</c>, its parent is returned; otherwise the
    /// directory is used as-is. This lets both conventions resolve to identical artifact paths.
    /// </summary>
    /// <param name="rawRoot">The supplied or default artifact directory.</param>
    /// <returns>The resolved content base directory.</returns>
    private static string NormalizeToContentBase(string rawRoot)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rawRoot));
        if (string.Equals(Path.GetFileName(trimmed), "content-kb", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(trimmed) ?? trimmed;
        }

        return trimmed;
    }

    /// <summary>
    /// Parses a comma-separated --video-ids option value into a trimmed id list.
    /// </summary>
    /// <param name="videoIds">Raw option value; null/blank yields null (option not used).</param>
    /// <returns>Distinct trimmed ids in input order, or null when the option was not supplied.</returns>
    internal static IReadOnlyList<string>? ParseVideoIds(string? videoIds)
    {
        if (string.IsNullOrWhiteSpace(videoIds))
        {
            return null;
        }

        var parsed = videoIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return parsed.Count > 0 ? parsed : null;
    }

    public static async Task<int> RunHarvestAsync(
        FileInfo? db,
        int limit,
        bool enableWhisper,
        Serilog.ILogger logger,
        CancellationToken ct,
        IReadOnlyList<string>? videoIds = null,
        long? sourceId = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var artifactRoot = ContentKbCliPaths.ResolveArtifactRoot(db);
            using var youtubeHttpClient = new HttpClient();
            using var whisperHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            var transcriptFetcher = TranscriptProviderFactory.Resolve(
                Environment.GetEnvironmentVariable(TranscriptProviderFactory.EnvironmentVariableName),
                youtubeHttpClient);
            var whisperLedger = new WhisperSpendLedger(dbPath);
            var chunker = new FfmpegAudioChunker();
            var whisper = new WhisperTranscriptionService(whisperLedger, chunker, whisperHttpClient);
            var transcriptSource = new YouTubeTranscriptSource(
                transcriptFetcher,
                new YouTubeAudioSource(youtubeHttpClient),
                whisper,
                enableWhisper);
            var orchestrator = CreateSqliteOrchestrator(
                dbPath,
                artifactRoot,
                distiller: new ThrowingLlmDistillationService(),
                lister: new YouTubeChannelVideoLister(youtubeHttpClient),
                transcriptSource: transcriptSource,
                chunker: chunker);
            var result = await orchestrator
                .HarvestAsync(limit, videoIds, sourceId, new ConsoleOrchestratorProgress(), ct)
                .ConfigureAwait(false);

            return result.Success ? 0 : WriteErrorAndReturn(result.Message, 1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Content KB harvest failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    internal static string SerializeContentIndexExportRows(IReadOnlyList<ContentIndexExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var json = JsonSerializer.Serialize(
            rows,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            });
        return json + "\n";
    }

    private static ContentKbOrchestrator CreateSqliteOrchestrator(
        string dbPath,
        string artifactRoot,
        ILlmDistillationService distiller,
        IYouTubeChannelVideoLister lister,
        ITranscriptSource transcriptSource,
        IFfmpegAudioChunker chunker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ArgumentNullException.ThrowIfNull(distiller);
        ArgumentNullException.ThrowIfNull(lister);
        ArgumentNullException.ThrowIfNull(transcriptSource);
        ArgumentNullException.ThrowIfNull(chunker);

        return new ContentKbOrchestrator(
            new ContentSourceStore(dbPath),
            new ContentVideoStore(dbPath),
            new ContentSiteIndexStore(dbPath),
            new BlockedVideoStore(dbPath),
            new ContentHarvestRunStore(dbPath),
            new LlmSpendLedger(dbPath),
            new WhisperSpendLedger(dbPath),
            distiller,
            lister,
            transcriptSource,
            chunker,
            () => DateTimeOffset.UtcNow,
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = artifactRoot,
            });
    }

    private static ContentKbOrchestrator CreateConnectionOrchestrator(
        RelationalDatabaseConnection connection,
        string artifactRoot,
        ILlmDistillationService distiller,
        IYouTubeChannelVideoLister lister,
        ITranscriptSource transcriptSource,
        IFfmpegAudioChunker chunker)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ArgumentNullException.ThrowIfNull(distiller);
        ArgumentNullException.ThrowIfNull(lister);
        ArgumentNullException.ThrowIfNull(transcriptSource);
        ArgumentNullException.ThrowIfNull(chunker);

        return new ContentKbOrchestrator(
            new ContentSourceStore(connection),
            new ContentVideoStore(connection),
            new ContentSiteIndexStore(connection),
            new BlockedVideoStore(connection),
            new ContentHarvestRunStore(connection),
            new LlmSpendLedger(connection),
            new WhisperSpendLedger(connection),
            distiller,
            lister,
            transcriptSource,
            chunker,
            () => DateTimeOffset.UtcNow,
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = artifactRoot,
            });
    }

    private static int WriteErrorAndReturn(string? message, int exitCode)
    {
        Console.Error.WriteLine(message);
        return exitCode;
    }

    private sealed class ConsoleOrchestratorProgress : IOrchestratorProgress
    {
        public void Report(string message)
        {
            Console.WriteLine(message);
        }
    }

    private sealed class ThrowingLlmDistillationService : ILlmDistillationService
    {
        public Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingLlmDistillationService)}.{nameof(SummarizeAsync)} must not be called by this CLI path");

        public Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingLlmDistillationService)}.{nameof(ClassifyAsync)} must not be called by this CLI path");

        public Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingLlmDistillationService)}.{nameof(ExtractClipsAsync)} must not be called by this CLI path");

        public Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingLlmDistillationService)}.{nameof(InferTagsAsync)} must not be called by this CLI path");
    }

    private sealed class ThrowingYouTubeChannelVideoLister : IYouTubeChannelVideoLister
    {
        public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(string channelUrl, int limit, int skip = 0, CancellationToken ct = default)
            => throw new InvalidOperationException($"{nameof(ThrowingYouTubeChannelVideoLister)}.{nameof(ListRecentAsync)} must not be called by this CLI path");

        public Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(IReadOnlyList<string> videoIds, CancellationToken ct = default)
            => throw new InvalidOperationException($"{nameof(ThrowingYouTubeChannelVideoLister)}.{nameof(GetByIdsAsync)} must not be called by this CLI path");
    }

    private sealed class ThrowingTranscriptSource : ITranscriptSource
    {
        public string SourceType
            => throw new InvalidOperationException($"{nameof(ThrowingTranscriptSource)}.{nameof(SourceType)} must not be called by this CLI path");

        public Task<TranscriptFetchResult> FetchTranscriptAsync(string naturalKey, TimeSpan? knownDuration, string monthKey, CancellationToken ct = default)
            => throw new InvalidOperationException($"{nameof(ThrowingTranscriptSource)}.{nameof(FetchTranscriptAsync)} must not be called by this CLI path");
    }

    private sealed class ThrowingFfmpegAudioChunker : IFfmpegAudioChunker
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
            => throw new InvalidOperationException($"{nameof(ThrowingFfmpegAudioChunker)}.{nameof(IsAvailableAsync)} must not be called by this CLI path");

        public Task<IReadOnlyList<string>> ChunkAsync(string inputPath, string outputDirectory, int segmentSeconds = 300, CancellationToken ct = default)
            => throw new InvalidOperationException($"{nameof(ThrowingFfmpegAudioChunker)}.{nameof(ChunkAsync)} must not be called by this CLI path");
    }
}
