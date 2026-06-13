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
            var isSubscriptionProvider = !string.IsNullOrWhiteSpace(providerEnv)
                && !string.Equals(providerEnv.Trim(), "openai", StringComparison.OrdinalIgnoreCase);
            var orchestrator = CreateSqliteOrchestrator(
                dbPath,
                artifactRoot,
                distiller: LlmDistillationProviderFactory.Resolve(providerEnv, llmHttpClient),
                lister: new ThrowingYouTubeChannelVideoLister(),
                transcriptSource: new ThrowingTranscriptSource(),
                chunker: new ThrowingFfmpegAudioChunker());
            var result = await orchestrator
                .DistillAsync(limit, dryRun, isSubscriptionProvider, videoIds, new ConsoleOrchestratorProgress(), ct)
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

    internal static async Task<int> RunBlockVideoAsync(
        string youtubeVideoId,
        string? reason,
        IBlockedVideoStore blockedStore,
        IContentVideoStore videoStore,
        IContentSiteIndexStore siteIndexStore,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(blockedStore);
        ArgumentNullException.ThrowIfNull(videoStore);
        ArgumentNullException.ThrowIfNull(siteIndexStore);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var orchestrator = CreateOrchestrator(
                new ThrowingContentSourceStore(),
                videoStore,
                siteIndexStore,
                blockedStore,
                new ThrowingContentHarvestRunStore(),
                new ThrowingLlmSpendLedger(),
                new ThrowingWhisperSpendLedger(),
                new ThrowingLlmDistillationService(),
                new ThrowingYouTubeChannelVideoLister(),
                new ThrowingTranscriptSource(),
                new ThrowingFfmpegAudioChunker(),
                Directory.GetCurrentDirectory(),
                () => DateTimeOffset.UtcNow);
            var result = await orchestrator.BlockVideoAsync(youtubeVideoId, reason, cancellationToken: ct).ConfigureAwait(false);
            return result.Success ? 0 : WriteErrorAndReturn(result.Message, 1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Block video failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    internal static async Task<int> RunUnblockVideoAsync(
        string youtubeVideoId,
        IBlockedVideoStore blockedStore,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(blockedStore);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var orchestrator = CreateOrchestrator(
                new ThrowingContentSourceStore(),
                new ThrowingContentVideoStore(),
                new ThrowingContentSiteIndexStore(),
                blockedStore,
                new ThrowingContentHarvestRunStore(),
                new ThrowingLlmSpendLedger(),
                new ThrowingWhisperSpendLedger(),
                new ThrowingLlmDistillationService(),
                new ThrowingYouTubeChannelVideoLister(),
                new ThrowingTranscriptSource(),
                new ThrowingFfmpegAudioChunker(),
                Directory.GetCurrentDirectory(),
                () => DateTimeOffset.UtcNow);
            var result = await orchestrator.UnblockVideoAsync(youtubeVideoId, cancellationToken: ct).ConfigureAwait(false);
            return result.Success ? 0 : WriteErrorAndReturn(result.Message, 1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Unblock video failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    internal static async Task<int> RunCorpusResetAsync(
        IContentVideoStore videoStore,
        IContentSiteIndexStore siteIndexStore,
        bool dryRun,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(videoStore);
        ArgumentNullException.ThrowIfNull(siteIndexStore);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var orchestrator = CreateOrchestrator(
                new ThrowingContentSourceStore(),
                videoStore,
                siteIndexStore,
                new ThrowingBlockedVideoStore(),
                new ThrowingContentHarvestRunStore(),
                new ThrowingLlmSpendLedger(),
                new ThrowingWhisperSpendLedger(),
                new ThrowingLlmDistillationService(),
                new ThrowingYouTubeChannelVideoLister(),
                new ThrowingTranscriptSource(),
                new ThrowingFfmpegAudioChunker(),
                Directory.GetCurrentDirectory(),
                () => DateTimeOffset.UtcNow);
            var result = await orchestrator.ResetCorpusAsync(dryRun, cancellationToken: ct).ConfigureAwait(false);
            return result.Success ? 0 : WriteErrorAndReturn(result.Message, 1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Corpus reset failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    internal static async Task<int> RunListBlockedAsync(
        IBlockedVideoStore blockedStore,
        TextWriter writer,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(blockedStore);
        ArgumentNullException.ThrowIfNull(writer);

        var orchestrator = CreateOrchestrator(
            new ThrowingContentSourceStore(),
            new ThrowingContentVideoStore(),
            new ThrowingContentSiteIndexStore(),
            blockedStore,
            new ThrowingContentHarvestRunStore(),
            new ThrowingLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            new ThrowingLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            Directory.GetCurrentDirectory(),
            () => DateTimeOffset.UtcNow);
        var result = await orchestrator.ListBlockedAsync(cancellationToken: ct).ConfigureAwait(false);
        foreach (var item in result.Items)
        {
            await writer.WriteLineAsync($"{item.YoutubeVideoId}\t{item.BlockedUtc:O}\t{item.Reason ?? string.Empty}").ConfigureAwait(false);
        }

        return 0;
    }

    internal static async Task<int> RunHarvestAsync(
        IContentSourceStore sourceStore,
        IContentVideoStore videoStore,
        IBlockedVideoStore blockedVideoStore,
        IWhisperSpendLedger ledger,
        IYouTubeChannelVideoLister lister,
        ITranscriptSource transcriptSource,
        IFfmpegAudioChunker chunker,
        int limit,
        Serilog.ILogger logger,
        Func<DateTimeOffset> utcNow,
        CancellationToken ct,
        IReadOnlyList<string>? videoIds = null,
        long? sourceId = null)
    {
        ArgumentNullException.ThrowIfNull(sourceStore);
        ArgumentNullException.ThrowIfNull(videoStore);
        ArgumentNullException.ThrowIfNull(blockedVideoStore);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(lister);
        ArgumentNullException.ThrowIfNull(transcriptSource);
        ArgumentNullException.ThrowIfNull(chunker);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(utcNow);

        try
        {
            var orchestrator = CreateOrchestrator(
                sourceStore,
                videoStore,
                new ThrowingContentSiteIndexStore(),
                blockedVideoStore,
                new ThrowingContentHarvestRunStore(),
                new ThrowingLlmSpendLedger(),
                ledger,
                new ThrowingLlmDistillationService(),
                lister,
                transcriptSource,
                chunker,
                Directory.GetCurrentDirectory(),
                utcNow);
            var result = await orchestrator.HarvestAsync(limit, videoIds, sourceId, cancellationToken: ct).ConfigureAwait(false);
            return result.Success ? 0 : WriteErrorAndReturn(result.Message, 1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Content KB harvest failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    internal static async Task<int> RunDistillAsync(
        IContentSourceStore sourceStore,
        IContentVideoStore videoStore,
        IContentSiteIndexStore indexStore,
        IContentHarvestRunStore runStore,
        ILlmSpendLedger ledger,
        ILlmDistillationService distiller,
        string artifactRoot,
        int limit,
        bool dryRun,
        Serilog.ILogger logger,
        Func<DateTimeOffset> utcNow,
        CancellationToken ct,
        bool isSubscriptionProvider = false,
        IReadOnlyList<string>? videoIds = null)
    {
        ArgumentNullException.ThrowIfNull(sourceStore);
        ArgumentNullException.ThrowIfNull(videoStore);
        ArgumentNullException.ThrowIfNull(indexStore);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(distiller);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(utcNow);

        try
        {
            var orchestrator = CreateOrchestrator(
                sourceStore,
                videoStore,
                indexStore,
                new ThrowingBlockedVideoStore(),
                runStore,
                ledger,
                new ThrowingWhisperSpendLedger(),
                distiller,
                new ThrowingYouTubeChannelVideoLister(),
                new ThrowingTranscriptSource(),
                new ThrowingFfmpegAudioChunker(),
                artifactRoot,
                utcNow);
            var result = await orchestrator
                .DistillAsync(limit, dryRun, isSubscriptionProvider, videoIds, cancellationToken: ct)
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

    private static ContentKbOrchestrator CreateOrchestrator(
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
        string artifactRoot,
        Func<DateTimeOffset> utcNow)
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
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ArgumentNullException.ThrowIfNull(utcNow);

        return new ContentKbOrchestrator(
            sourceStore,
            videoStore,
            indexStore,
            blockedVideoStore,
            runStore,
            llmLedger,
            whisperLedger,
            distiller,
            lister,
            transcriptSource,
            chunker,
            utcNow,
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

    private sealed class ThrowingContentSourceStore : IContentSourceStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSourceStore)}.{nameof(EnsureSchemaAsync)} must not be called by this CLI path");

        public Task<long> InsertSourceAsync(string sourceSlug, string displayName, string sourceType, string sourceUrl, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSourceStore)}.{nameof(InsertSourceAsync)} must not be called by this CLI path");

        public Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSourceStore)}.{nameof(GetSourceAsync)} must not be called by this CLI path");

        public Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSourceStore)}.{nameof(ListEnabledSourcesAsync)} must not be called by this CLI path");
    }

    private sealed class ThrowingContentVideoStore : IContentVideoStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(EnsureSchemaAsync)} must not be called by this CLI path");

        public Task<long> InsertVideoAsync(long sourceId, string? youtubeVideoId, string? rssGuid, string title, string videoUrl, DateTimeOffset? publishedUtc, string transcriptStatus, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertVideoAsync)} must not be called by this CLI path");

        public Task<ContentVideo?> GetVideoByYoutubeIdAsync(long sourceId, string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(GetVideoByYoutubeIdAsync)} must not be called by this CLI path");

        public Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(long sourceId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(ListVideosPendingDistillAsync)} must not be called by this CLI path");

        public Task UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(UpdateTranscriptStatusAsync)} must not be called by this CLI path");

        public Task<long> InsertTranscriptAsync(long videoId, string source, string body, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertTranscriptAsync)} must not be called by this CLI path");

        public Task<ContentTranscriptBody?> GetLatestTranscriptAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(GetLatestTranscriptAsync)} must not be called by this CLI path");

        public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertSummaryAsync)} must not be called by this CLI path");

        public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertClipAsync)} must not be called by this CLI path");

        public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(InsertTagAsync)} must not be called by this CLI path");

        public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(DeleteVideoAsync)} must not be called by this CLI path");

        public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(DeleteVideoByYoutubeIdAsync)} must not be called by this CLI path");

        public Task<int> DeleteAllVideosAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(DeleteAllVideosAsync)} must not be called by this CLI path");

        public Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(ClearDistillOutputAsync)} must not be called by this CLI path");

        public Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(GetDistillStatusAsync)} must not be called by this CLI path");

        public Task SetDistillStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(SetDistillStatusAsync)} must not be called by this CLI path");

        public Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(CountTranscriptsByVideoAsync)} must not be called by this CLI path");

        public Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(CountSummariesByVideoAsync)} must not be called by this CLI path");

        public Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(CountClipsByVideoAsync)} must not be called by this CLI path");

        public Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentVideoStore)}.{nameof(CountTagsByVideoAsync)} must not be called by this CLI path");
    }

    private sealed class ThrowingContentSiteIndexStore : IContentSiteIndexStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(EnsureSchemaAsync)} must not be called by this CLI path");

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(UpsertRowAsync)} must not be called by this CLI path");

        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(UpsertRowPreservingVisibilityAsync)} must not be called by this CLI path");

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetByNaturalKeyAsync)} must not be called by this CLI path");

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetPublishedRowsAsync)} must not be called by this CLI path");

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetAllRowsAsync)} must not be called by this CLI path");

        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(GetByIdAsync)} must not be called by this CLI path");

        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetVisibilityAsync)} must not be called by this CLI path");

        public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetHiddenAsync)} must not be called by this CLI path");

        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(DeleteByIdAsync)} must not be called by this CLI path");

        public Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(DeleteAllRowsAsync)} must not be called by this CLI path");

        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetEvergreenAsync)} must not be called by this CLI path");

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetVisibilityBySourceAsync)} must not be called by this CLI path");

        public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentSiteIndexStore)}.{nameof(SetHiddenBySourceAsync)} must not be called by this CLI path");
    }

    private sealed class ThrowingContentHarvestRunStore : IContentHarvestRunStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentHarvestRunStore)}.{nameof(EnsureSchemaAsync)} must not be called by this CLI path");

        public Task<long> StartRunAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentHarvestRunStore)}.{nameof(StartRunAsync)} must not be called by this CLI path");

        public Task CompleteRunAsync(long runId, int sourcesProcessed, int videosProcessed, int transcriptsFetched, int whisperCalls, decimal spendUsd, string? abortedReason, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentHarvestRunStore)}.{nameof(CompleteRunAsync)} must not be called by this CLI path");

        public Task<ContentHarvestRun?> GetRunAsync(long runId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingContentHarvestRunStore)}.{nameof(GetRunAsync)} must not be called by this CLI path");
    }

    private sealed class ThrowingBlockedVideoStore : IBlockedVideoStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(EnsureSchemaAsync)} must not be called by this CLI path");

        public Task AddBlockAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(AddBlockAsync)} must not be called by this CLI path");

        public Task<bool> RemoveBlockAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(RemoveBlockAsync)} must not be called by this CLI path");

        public Task<bool> IsBlockedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(IsBlockedAsync)} must not be called by this CLI path");

        public Task<IReadOnlyList<BlockedVideo>> ListBlockedAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingBlockedVideoStore)}.{nameof(ListBlockedAsync)} must not be called by this CLI path");
    }

    private sealed class ThrowingLlmSpendLedger : ILlmSpendLedger
    {
        public Task RecordCallAsync(long videoId, int inputTokens, int outputTokens, decimal costUsd, string monthKey, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingLlmSpendLedger)}.{nameof(RecordCallAsync)} must not be called by this CLI path");

        public Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingLlmSpendLedger)}.{nameof(GetMonthlyTotalAsync)} must not be called by this CLI path");

        public Task<bool> WouldExceedCapAsync(decimal projectedCallCostUsd, string monthKey, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingLlmSpendLedger)}.{nameof(WouldExceedCapAsync)} must not be called by this CLI path");
    }

    private sealed class ThrowingWhisperSpendLedger : IWhisperSpendLedger
    {
        public Task RecordCallAsync(long videoId, int secondsBilled, decimal costUsd, string monthKey, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingWhisperSpendLedger)}.{nameof(RecordCallAsync)} must not be called by this CLI path");

        public Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingWhisperSpendLedger)}.{nameof(GetMonthlyTotalAsync)} must not be called by this CLI path");

        public Task<bool> WouldExceedCapAsync(decimal projectedCallCostUsd, string monthKey, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"{nameof(ThrowingWhisperSpendLedger)}.{nameof(WouldExceedCapAsync)} must not be called by this CLI path");
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
        public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(string channelUrl, int limit, CancellationToken ct = default)
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
