using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeckFlow.Core.Content;
using DeckFlow.Core.Diffing;
using DeckFlow.Core.Exporting;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Serilog;

namespace DeckFlow.CLI;

internal static class CommandRunners
{
    /// <summary>
    /// Compares two decks and writes the resulting import artifacts.
    /// </summary>
    /// <param name="moxfield">Optional local file for the left-side deck input.</param>
    /// <param name="moxfieldUrl">Optional URL for the left-side deck input.</param>
    /// <param name="archidekt">Optional local file for the right-side deck input.</param>
    /// <param name="archidektUrl">Optional URL for the right-side deck input.</param>
    /// <param name="output">Destination path for the delta import file.</param>
    /// <param name="mode">Diff matching mode.</param>
    /// <param name="direction">Source and target direction for the compare.</param>
    /// <param name="dryRun">Whether output should be printed instead of written.</param>
    /// <param name="resolveConflicts">Whether printing conflicts should be resolved interactively.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunCompareAsync(FileInfo? moxfield, string? moxfieldUrl, FileInfo? archidekt, string? archidektUrl, FileInfo output, MatchMode mode, SyncDirection direction, bool dryRun, bool resolveConflicts)
    {
        try
        {
            var deckEntryLoader = CreateDeckEntryLoader();
            var moxfieldEntries = await LoadMoxfieldEntriesAsync(moxfield, moxfieldUrl, deckEntryLoader);
            var archidektEntries = await LoadArchidektEntriesAsync(archidekt, archidektUrl, deckEntryLoader);
            deckEntryLoader.ValidateCommanderDeckSize("Moxfield", moxfieldEntries);
            deckEntryLoader.ValidateCommanderDeckSize("Archidekt", archidektEntries);
            var sourceEntries = direction == SyncDirection.MoxfieldToArchidekt ? moxfieldEntries : archidektEntries;
            var targetEntries = direction == SyncDirection.MoxfieldToArchidekt ? archidektEntries : moxfieldEntries;
            var sourceSystem = direction == SyncDirection.MoxfieldToArchidekt ? "Moxfield" : "Archidekt";
            var targetSystem = direction == SyncDirection.MoxfieldToArchidekt ? "Archidekt" : "Moxfield";
            var diff = new DiffEngine(mode).Compare(sourceEntries, targetEntries);

            Console.WriteLine(ReconciliationReporter.ToText(diff, sourceSystem, targetSystem));
            Console.WriteLine();

            if (resolveConflicts)
            {
                diff = diff with
                {
                    PrintingConflicts = ResolveConflicts(diff.PrintingConflicts, sourceSystem, targetSystem).ToList(),
                };

                var checklist = ReconciliationReporter.GenerateSwapChecklist(diff.PrintingConflicts.ToList(), targetSystem);
                if (!string.IsNullOrWhiteSpace(checklist))
                {
                    Console.WriteLine(checklist);
                    Console.WriteLine();
                }
            }

            if (dryRun)
            {
                Console.WriteLine("(Dry run - no output file written. Run without --dry-run to generate file.)");
                Console.WriteLine();
                Console.WriteLine(DeltaExporter.ToText(diff.ToAdd.ToList(), targetSystem));
                Console.WriteLine();
                Console.WriteLine("--- Full Import ---");
                Console.WriteLine(FullImportExporter.ToText(sourceEntries, targetEntries, mode, targetSystem, diff.PrintingConflicts));
                return 0;
            }

            DeltaExporter.WriteFile(diff.ToAdd.ToList(), output.FullName, targetSystem);
            var fullImportPath = Path.Combine(
                output.DirectoryName ?? Directory.GetCurrentDirectory(),
                $"{Path.GetFileNameWithoutExtension(output.Name)}.full{output.Extension}");
            FullImportExporter.WriteFile(sourceEntries, targetEntries, mode, fullImportPath, targetSystem, diff.PrintingConflicts);
            Console.WriteLine($"Wrote delta file: {output.FullName}");
            Console.WriteLine($"Wrote full import file: {fullImportPath}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DeckParseException or InvalidOperationException or HttpRequestException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Loads Moxfield entries from either a local file or a public URL.
    /// </summary>
    /// <param name="file">Optional local deck file.</param>
    /// <param name="url">Optional public Moxfield URL.</param>
    /// <param name="deckEntryLoader">Shared deck loader.</param>
    /// <returns>The loaded deck entries.</returns>
    public static async Task<List<DeckEntry>> LoadMoxfieldEntriesAsync(FileInfo? file, string? url, IDeckEntryLoader? deckEntryLoader = null)
    {
        deckEntryLoader ??= CreateDeckEntryLoader();

        if (file is not null && !string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Specify either --moxfield or --moxfield-url, not both.");
        }

        if (file is not null)
        {
            return await deckEntryLoader.LoadAsync(
                new DeckLoadRequest(DeckPlatform.Moxfield, DeckInputKind.FilePath, file.FullName, ExcludeMaybeboard: true));
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            return await deckEntryLoader.LoadAsync(
                new DeckLoadRequest(DeckPlatform.Moxfield, DeckInputKind.PublicUrl, url, ExcludeMaybeboard: true));
        }

        throw new InvalidOperationException("Either --moxfield or --moxfield-url is required.");
    }

    /// <summary>
    /// Loads Archidekt entries from either a local file or a public URL.
    /// </summary>
    /// <param name="file">Optional local deck file.</param>
    /// <param name="url">Optional public Archidekt URL.</param>
    /// <param name="deckEntryLoader">Shared deck loader.</param>
    /// <returns>The loaded deck entries.</returns>
    public static async Task<List<DeckEntry>> LoadArchidektEntriesAsync(FileInfo? file, string? url, IDeckEntryLoader? deckEntryLoader = null)
    {
        deckEntryLoader ??= CreateDeckEntryLoader();

        if (file is not null && !string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Specify either --archidekt or --archidekt-url, not both.");
        }

        if (file is not null)
        {
            return await deckEntryLoader.LoadAsync(
                new DeckLoadRequest(DeckPlatform.Archidekt, DeckInputKind.FilePath, file.FullName));
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            return await deckEntryLoader.LoadAsync(
                new DeckLoadRequest(DeckPlatform.Archidekt, DeckInputKind.PublicUrl, url));
        }

        throw new InvalidOperationException("Either --archidekt or --archidekt-url is required.");
    }

    /// <summary>
    /// Validates that a deck contains the expected Commander card count.
    /// </summary>
    /// <param name="systemName">Display name used in error messages.</param>
    /// <param name="entries">Deck entries to validate.</param>
    public static void ValidateDeckSize(string systemName, IReadOnlyList<DeckEntry> entries)
    {
        CreateDeckEntryLoader().ValidateCommanderDeckSize(systemName, entries);
    }

    public static IEnumerable<PrintingConflict> ResolveConflicts(IReadOnlyList<PrintingConflict> conflicts, string sourceSystem, string targetSystem)
    {
        foreach (var conflict in conflicts)
        {
            Console.WriteLine($"Printing conflict: {conflict.CardName}");
            Console.WriteLine($"  [A] Keep {targetSystem} version: ({conflict.ArchidektVersion.SetCode}) {conflict.ArchidektVersion.CollectorNumber}  [category: {conflict.ArchidektVersion.Category ?? "none"}]");
            Console.WriteLine($"  [M] Use {sourceSystem} version:   ({conflict.MoxfieldVersion.SetCode}) {conflict.MoxfieldVersion.CollectorNumber}");
            Console.Write("Choice (A/M, default A): ");

            var input = Console.ReadLine()?.Trim();
            var resolution = string.Equals(input, "M", StringComparison.OrdinalIgnoreCase)
                ? PrintingChoice.UseMoxfield
                : PrintingChoice.KeepArchidekt;

            yield return conflict with { Resolution = resolution };
            Console.WriteLine();
        }
    }

    public static async Task<int> RunProbeAsync(string url, FileInfo? output)
    {
        if (!MoxfieldApiUrl.TryGetDeckId(url, out var deckId))
        {
            Console.Error.WriteLine($"Unable to determine Moxfield deck id from: {url}");
            return 1;
        }

        var apiUri = MoxfieldApiUrl.BuildDeckApiUri(deckId);
        Console.WriteLine($"Deck id: {deckId}");
        Console.WriteLine($"API URL: {apiUri}");

        var client = new RestClient(new RestClientOptions
        {
            ThrowOnAnyError = false,
        });

        var request = new RestRequest(apiUri, Method.Get);
        request.AddHeader("User-Agent", "Mozilla/5.0 DeckFlow/1.0");
        request.AddHeader("Accept", "application/json, text/plain, */*");
        request.AddHeader("Referer", "https://moxfield.com/");

        var response = await client.ExecuteAsync(request);
        var body = response.Content ?? string.Empty;

        Console.WriteLine($"HTTP {(int)response.StatusCode} {response.StatusDescription}");
        Console.WriteLine($"Content-Type: {response.ContentType}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine();
            Console.WriteLine(body[..Math.Min(body.Length, 400)]);
            return 1;
        }

        if (output is not null)
        {
            Directory.CreateDirectory(output.DirectoryName ?? Directory.GetCurrentDirectory());
            await File.WriteAllTextAsync(output.FullName, body);
            Console.WriteLine($"Saved raw JSON to: {output.FullName}");
        }

        using var json = System.Text.Json.JsonDocument.Parse(body);
        var root = json.RootElement;

        Console.WriteLine();
        Console.WriteLine("Top-level properties:");
        foreach (var property in root.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {property}");
        }

        Console.WriteLine();
        Console.WriteLine("Tag-like paths:");
        var paths = new List<string>();
        CollectInterestingPaths(root, "$", paths);
        if (paths.Count == 0)
        {
            Console.WriteLine("  none found");
        }
        else
        {
            foreach (var path in paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  {path}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Exports a public Moxfield deck as text.
    /// </summary>
    /// <param name="url">Public Moxfield deck URL.</param>
    /// <param name="output">Destination text file.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunExportMoxfieldAsync(string url, FileInfo output)
    {
        try
        {
            var entries = await CreateDeckEntryLoader().LoadAsync(
                new DeckLoadRequest(DeckPlatform.Moxfield, DeckInputKind.PublicUrl, url, ExcludeMaybeboard: true));
            Directory.CreateDirectory(output.DirectoryName ?? Directory.GetCurrentDirectory());
            MoxfieldTextExporter.WriteFile(entries, output.FullName);
            Console.WriteLine($"Wrote Moxfield deck file: {output.FullName}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Fetches an Archidekt deck and prints category totals by quantity.
    /// </summary>
    /// <param name="url">Public Archidekt deck URL.</param>
    /// <param name="output">Optional destination file for the report.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunArchidektCategoriesAsync(string url, FileInfo? output)
    {
        try
        {
            var entries = await CreateDeckEntryLoader().LoadAsync(
                new DeckLoadRequest(DeckPlatform.Archidekt, DeckInputKind.PublicUrl, url));
            var text = CategoryCountReporter.ToText(entries);

            Console.WriteLine(text);
            if (output is not null)
            {
                Directory.CreateDirectory(output.DirectoryName ?? Directory.GetCurrentDirectory());
                await File.WriteAllTextAsync(output.FullName, text);
                Console.WriteLine();
                Console.WriteLine($"Wrote category counts: {output.FullName}");
            }

            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Fetches an Archidekt deck and prints the cards found in a specific category.
    /// </summary>
    /// <param name="url">Public Archidekt deck URL.</param>
    /// <param name="category">Category to filter by.</param>
    /// <param name="output">Optional destination file for the report.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunArchidektCategoryCardsAsync(string url, string category, FileInfo? output)
    {
        try
        {
            var entries = await CreateDeckEntryLoader().LoadAsync(
                new DeckLoadRequest(DeckPlatform.Archidekt, DeckInputKind.PublicUrl, url));
            var text = CategoryCardReporter.ToText(entries, category);

            Console.WriteLine(text);
            if (output is not null)
            {
                Directory.CreateDirectory(output.DirectoryName ?? Directory.GetCurrentDirectory());
                await File.WriteAllTextAsync(output.FullName, text);
                Console.WriteLine();
                Console.WriteLine($"Wrote category card list: {output.FullName}");
            }

            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static async Task<int> RunArchidektHarvestRecentAsync(int count, FileInfo output)
    {
        try
        {
            var recentDeckIds = await new ArchidektRecentDecksImporter().ImportRecentDeckIdsAsync(count);
            var importer = new ArchidektApiDeckImporter();
            var entries = new List<DeckEntry>();

            foreach (var deckId in recentDeckIds)
            {
                var deckEntries = await importer.ImportAsync(deckId);
                entries.AddRange(deckEntries);
            }

            var text = CategoryKnowledgeReporter.ToText(entries, recentDeckIds.Count);
            Directory.CreateDirectory(output.DirectoryName ?? Directory.GetCurrentDirectory());
            await File.WriteAllTextAsync(output.FullName, text);

            Console.WriteLine($"Harvested {recentDeckIds.Count} decks.");
            Console.WriteLine($"Wrote category knowledge file: {output.FullName}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static async Task<int> RunArchidektCacheAsync(int seconds, Serilog.ILogger? logger = null)
    {
        logger ??= Log.Logger;
        try
        {
            var durationSeconds = Math.Max(5, seconds);
            var artifactsPath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
            var repository = new CategoryKnowledgeRepository(Path.Combine(artifactsPath, "category-knowledge.db"));
            var duration = TimeSpan.FromSeconds(durationSeconds);

            logger.Information("Starting Archidekt cache sweep for {DurationSeconds}s (artifacts: {ArtifactsPath}).", durationSeconds, artifactsPath);

            var session = new ArchidektDeckCacheSession(
                repository,
                new ArchidektApiDeckImporter(),
                new ArchidektRecentDecksImporter(),
                NullLogger<ArchidektDeckCacheSession>.Instance);

            var result = await session.RunAsync(duration, queueBatchSize: 5, fetchBatchSize: 10);

            logger.Information(
                "Cache run stopped after {ElapsedSeconds:F1}s. New decks: {DecksAdded}; updated decks: {DecksUpdated}; skipped: {DecksSkipped}.",
                result.Duration.TotalSeconds,
                result.DecksAdded,
                result.DecksUpdated,
                result.DecksSkipped);
            Console.WriteLine($"Cache run stopped after {result.Duration.TotalSeconds:F1} seconds.");
            Console.WriteLine($"New decks cached: {result.DecksAdded}");
            Console.WriteLine($"Existing decks updated: {result.DecksUpdated}");
            Console.WriteLine($"Decks skipped: {result.DecksSkipped}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException)
        {
            logger.Error(exception, "Archidekt cache sweep failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static async Task<int> RunContentSourceAddAsync(string url, string name, string type, FileInfo? db)
    {
        if (!IsValidContentSourceType(type))
        {
            Console.Error.WriteLine($"Unsupported content source type '{type}'. Use youtube_channel or podcast_rss.");
            return 2;
        }

        var dbPath = ResolveContentKbDatabasePath(db);
        var slug = SlugifySourceName.Slugify(name);
        var store = new ContentSourceStore(dbPath);
        Console.WriteLine($"Computed slug: {slug}");

        try
        {
            var id = await store.InsertSourceAsync(slug, name, type, url);
            Console.WriteLine($"Added content source {id}: {slug}");
            return 0;
        }
        catch (Exception exception) when (IsContentSourceUniqueViolation(exception))
        {
            return await HandleContentSourceUniqueViolationAsync(store, slug, url, exception);
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
            var dbPath = ResolveContentKbDatabasePath(db);
            var store = new ContentSourceStore(dbPath);
            await store.SetEnabledAsync(id, enabled, ct).ConfigureAwait(false);
            Console.WriteLine($"Source {id} enabled={enabled}.");
            return 0;
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
            var dbPath = ResolveContentKbDatabasePath(db);
            var artifactRoot = ResolveContentKbArtifactRoot(db);
            using var llmHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            var sourceStore = new ContentSourceStore(dbPath);
            var videoStore = new ContentVideoStore(dbPath);
            var indexStore = new ContentSiteIndexStore(dbPath);
            var runStore = new ContentHarvestRunStore(dbPath);
            var ledger = new LlmSpendLedger(dbPath);
            var providerEnv = Environment.GetEnvironmentVariable(LlmDistillationProviderFactory.EnvironmentVariableName);
            var isSubscriptionProvider = !string.IsNullOrWhiteSpace(providerEnv)
                && !string.Equals(providerEnv.Trim(), "openai", StringComparison.OrdinalIgnoreCase);
            var distiller = LlmDistillationProviderFactory.Resolve(providerEnv, llmHttpClient);

            return await RunDistillAsync(
                sourceStore,
                videoStore,
                indexStore,
                runStore,
                ledger,
                distiller,
                artifactRoot,
                limit,
                dryRun,
                logger,
                () => DateTimeOffset.UtcNow,
                ct,
                isSubscriptionProvider,
                videoIds).ConfigureAwait(false);
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
            var dbPath = ResolveContentKbDatabasePath(db);
            var blockedStore = new BlockedVideoStore(dbPath);
            var videoStore = new ContentVideoStore(dbPath);
            var siteIndexStore = new ContentSiteIndexStore(ResolveContentKbDatabasePath(db));

            return await RunBlockVideoAsync(youtubeVideoId, reason, blockedStore, videoStore, siteIndexStore, logger, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Block video failed.");
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
            var blockedStore = new BlockedVideoStore(ResolveContentKbDatabasePath(db));
            return await RunUnblockVideoAsync(youtubeVideoId, blockedStore, logger, ct).ConfigureAwait(false);
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
            var blockedStore = new BlockedVideoStore(ResolveContentKbDatabasePath(db));
            return await RunListBlockedAsync(blockedStore, Console.Out, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "List blocked videos failed.");
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
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);
        ArgumentNullException.ThrowIfNull(blockedStore);
        ArgumentNullException.ThrowIfNull(videoStore);
        ArgumentNullException.ThrowIfNull(siteIndexStore);
        ArgumentNullException.ThrowIfNull(logger);

        // Why: writing the block row first ensures a partial failure cannot leave the
        // video deleted-but-reharvestable across the separate content/site-index stores.
        await blockedStore.AddBlockAsync(youtubeVideoId, reason, ct).ConfigureAwait(false);
        var deletedRows = await videoStore.DeleteVideoByYoutubeIdAsync(youtubeVideoId, ct).ConfigureAwait(false);
        var row = await siteIndexStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, youtubeVideoId, ct).ConfigureAwait(false);
        var deletedSiteIndexRows = 0;
        if (row is not null)
        {
            deletedSiteIndexRows = await siteIndexStore.DeleteByIdAsync(row.Id, ct).ConfigureAwait(false);
        }

        logger.Information(
            "blocked video {VideoId} content_rows_deleted={DeletedRows} site_index_rows_deleted={SiteIndexDeletedRows}",
            youtubeVideoId,
            deletedRows,
            deletedSiteIndexRows);
        return 0;
    }

    internal static async Task<int> RunUnblockVideoAsync(
        string youtubeVideoId,
        IBlockedVideoStore blockedStore,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);
        ArgumentNullException.ThrowIfNull(blockedStore);
        ArgumentNullException.ThrowIfNull(logger);

        var removed = await blockedStore.RemoveBlockAsync(youtubeVideoId, ct).ConfigureAwait(false);
        if (!removed)
        {
            logger.Information("unblocked video {VideoId}; no row removed", youtubeVideoId);
            return 0;
        }

        logger.Information("unblocked video {VideoId}", youtubeVideoId);
        return 0;
    }

    internal static async Task<int> RunListBlockedAsync(
        IBlockedVideoStore blockedStore,
        TextWriter writer,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(blockedStore);
        ArgumentNullException.ThrowIfNull(writer);

        var blocked = await blockedStore.ListBlockedAsync(ct).ConfigureAwait(false);
        foreach (var row in blocked)
        {
            await writer.WriteLineAsync($"{row.YoutubeVideoId}\t{row.BlockedUtc:O}\t{row.Reason ?? string.Empty}").ConfigureAwait(false);
        }

        return 0;
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
            var dbPath = ResolveContentKbDatabasePath(db);
            var indexStore = new ContentSiteIndexStore(dbPath);
            await indexStore.EnsureSchemaAsync().ConfigureAwait(false);
            var rows = await indexStore.GetAllRowsAsync().ConfigureAwait(false);
            var exportRows = rows.Select(ContentIndexExportRow.From).ToList();
            var outputPath = output?.FullName ?? Path.Combine("content-kb", "seed", "index-seed.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());

            var json = JsonSerializer.Serialize(
                exportRows,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                });
            await File.WriteAllTextAsync(outputPath, json + "\n").ConfigureAwait(false);
            Console.WriteLine($"Exported {exportRows.Count} rows to {outputPath}");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
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

        // Why: explicit --video-ids means "exactly these", so the recent-N clip is bypassed.
        var requestedKeys = videoIds is { Count: > 0 }
            ? new HashSet<string>(videoIds, StringComparer.Ordinal)
            : null;
        var maxVideosPerSource = requestedKeys?.Count ?? Math.Max(1, limit);
        var monthKey = utcNow().UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var generatedUtc = utcNow().ToUniversalTime();
        var counts = new DistillCounts();
        string? abortedReason = null;
        var stopRun = false;
        var runId = dryRun ? 0 : await runStore.StartRunAsync(ct).ConfigureAwait(false);

        try
        {
            var sources = await sourceStore.ListEnabledSourcesAsync(ct).ConfigureAwait(false);
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
                    pendingVideos = await videoStore
                        .ListVideosPendingDistillAsync(source.Id, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.Error(exception, "distill source failed {SourceSlug}", source.SourceSlug);
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
                    var status = await videoStore.GetDistillStatusAsync(video.Id, ct).ConfigureAwait(false);
                    if (string.Equals(status, DistillStatusDistilled, StringComparison.Ordinal))
                    {
                        logger.Information("already distilled {VideoId}", naturalKey);
                        continue;
                    }

                    attemptedForSource++;
                    if (dryRun)
                    {
                        var transcript = await videoStore.GetLatestTranscriptAsync(video.Id, ct).ConfigureAwait(false);
                        if (transcript is null)
                        {
                            logger.Warning("WOULD skip {VideoId} because transcript is missing", naturalKey);
                            Console.WriteLine($"WOULD skip {naturalKey} (missing transcript)");
                            continue;
                        }

                        var projectedCost = isSubscriptionProvider ? 0m : ComputeProjectedVideoCostUsd(transcript.Body);
                        var wouldSkip = !isSubscriptionProvider
                            && await ledger.WouldExceedCapAsync(projectedCost, monthKey, ct).ConfigureAwait(false);
                        var disposition = wouldSkip ? "WOULD skip over cap"
                            : isSubscriptionProvider ? "WOULD distill ($0, subscription)"
                            : "WOULD distill";
                        logger.Information(
                            "{Disposition} {VideoId} (~${ProjectedCostUsd:F4})",
                            disposition,
                            naturalKey,
                            projectedCost);
                        Console.WriteLine($"{disposition} {naturalKey} (~${projectedCost:F4})");
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
                        videoStore,
                        indexStore,
                        ledger,
                        distiller,
                        artifactRoot,
                        monthKey,
                        generatedUtc,
                        logger,
                        ct,
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
                await runStore.CompleteRunAsync(
                    runId,
                    counts.SourcesProcessed,
                    counts.VideosDistilled,
                    transcriptsFetched: 0,
                    whisperCalls: counts.LlmCalls,
                    spendUsd: counts.LlmSpendUsd,
                    abortedReason,
                    ct).ConfigureAwait(false);
            }
        }

        if (dryRun)
        {
            logger.Information(
                "dry-run distill complete would_run={WouldRun} projected_spend_usd={ProjectedSpendUsd:F6}",
                counts.WouldRun,
                counts.ProjectedSpendUsd);
            var spendDisplay = isSubscriptionProvider ? "$0 (subscription)" : $"${counts.ProjectedSpendUsd:F6}";
            Console.WriteLine($"Dry run complete. Would distill {counts.WouldRun} videos; projected spend {spendDisplay}.");
            return 0;
        }

        logger.Information(
            "distill complete sources={SourcesProcessed} videos_distilled={VideosDistilled} llm_calls={LlmCalls} spend_usd={SpendUsd:F6} distill_failed={DistillFailed} failed_video_ids={FailedVideoIds}",
            counts.SourcesProcessed,
            counts.VideosDistilled,
            counts.LlmCalls,
            counts.LlmSpendUsd,
            counts.DistillFailed,
            string.Join(",", counts.FailedVideoIds));
        return 0;
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
            var dbPath = ResolveContentKbDatabasePath(db);
            using var youtubeHttpClient = new HttpClient();
            using var whisperHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            var sourceStore = new ContentSourceStore(dbPath);
            var videoStore = new ContentVideoStore(dbPath);
            var blockedVideoStore = new BlockedVideoStore(dbPath);
            var ledger = new WhisperSpendLedger(dbPath);
            var chunker = new FfmpegAudioChunker();
            var transcriptFetcher = TranscriptProviderFactory.Resolve(
                Environment.GetEnvironmentVariable(TranscriptProviderFactory.EnvironmentVariableName),
                youtubeHttpClient);
            var whisper = new WhisperTranscriptionService(ledger, chunker, whisperHttpClient);
            var transcriptSource = new YouTubeTranscriptSource(
                transcriptFetcher,
                new YouTubeAudioSource(youtubeHttpClient),
                whisper,
                enableWhisper);

            return await RunHarvestAsync(
                sourceStore,
                videoStore,
                blockedVideoStore,
                ledger,
                new YouTubeChannelVideoLister(youtubeHttpClient),
                transcriptSource,
                chunker,
                limit,
                logger,
                () => DateTimeOffset.UtcNow,
                ct,
                videoIds,
                sourceId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Content KB harvest failed.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
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
        await WarnIfFfmpegUnavailableAsync(chunker, logger, ct);
        var sources = await sourceStore.ListEnabledSourcesAsync(ct);

        if (videoIds is { Count: > 0 })
        {
            return await HarvestExplicitVideoIdsAsync(
                sources,
                videoIds,
                sourceId,
                videoStore,
                blockedVideoStore,
                ledger,
                lister,
                transcriptSource,
                logger,
                utcNow,
                ct);
        }

        var aggregate = new HarvestCounts();
        foreach (var source in sources.Where(source => source.SourceType == ContentSourceType.Youtube))
        {
            try
            {
                var sourceCounts = await HarvestSourceAsync(
                    source,
                    videoStore,
                    blockedVideoStore,
                    ledger,
                    lister,
                    transcriptSource,
                    Math.Max(1, limit),
                    logger,
                    utcNow,
                    ct);
                aggregate.Add(sourceCounts);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.Error(exception, "harvest source failed {SourceSlug}", source.SourceSlug);
                continue;
            }
        }

        LogFallbackRatio(logger, "aggregate", aggregate);
        // Phase 21 owns distillation, artifact emit, slim-index rows, and run records.
        return 0;
    }

    // Why: --video-ids bypasses the most-recent walk so the operator can pick exact
    // videos; a single target source keeps slug attribution unambiguous.
    private static async Task<int> HarvestExplicitVideoIdsAsync(
        IReadOnlyList<ContentSource> sources,
        IReadOnlyList<string> videoIds,
        long? sourceId,
        IContentVideoStore videoStore,
        IBlockedVideoStore blockedVideoStore,
        IWhisperSpendLedger ledger,
        IYouTubeChannelVideoLister lister,
        ITranscriptSource transcriptSource,
        Serilog.ILogger logger,
        Func<DateTimeOffset> utcNow,
        CancellationToken ct)
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
                Console.Error.WriteLine($"--source-id {id} does not match an enabled YouTube source.");
                return 1;
            }
        }
        else if (youtubeSources.Count == 1)
        {
            target = youtubeSources[0];
        }
        else
        {
            Console.Error.WriteLine(
                $"--video-ids needs a single target source but {youtubeSources.Count} YouTube sources are enabled; pass --source-id.");
            return 1;
        }

        var videos = await lister.GetByIdsAsync(videoIds, ct);
        if (videos.Count < videoIds.Count)
        {
            var resolved = videos.Select(video => video.VideoId).ToHashSet(StringComparer.Ordinal);
            foreach (var missing in videoIds.Where(requested => !resolved.Contains(requested)))
            {
                logger.Warning("requested video id did not resolve {VideoId}", missing);
            }
        }

        var counts = new HarvestCounts();
        foreach (var video in videos)
        {
            await HarvestVideoAsync(target, video, videoStore, blockedVideoStore, ledger, transcriptSource, counts, logger, utcNow, ct);
        }

        LogFallbackRatio(logger, target.SourceSlug, counts);
        return 0;
    }

    private static async Task<HarvestCounts> HarvestSourceAsync(
        ContentSource source,
        IContentVideoStore videoStore,
        IBlockedVideoStore blockedVideoStore,
        IWhisperSpendLedger ledger,
        IYouTubeChannelVideoLister lister,
        ITranscriptSource transcriptSource,
        int limit,
        Serilog.ILogger logger,
        Func<DateTimeOffset> utcNow,
        CancellationToken ct)
    {
        var counts = new HarvestCounts();
        var videos = await lister.ListRecentAsync(source.SourceUrl, limit, ct);
        foreach (var video in videos)
        {
            await HarvestVideoAsync(source, video, videoStore, blockedVideoStore, ledger, transcriptSource, counts, logger, utcNow, ct);
        }

        LogFallbackRatio(logger, source.SourceSlug, counts);
        return counts;
    }

    private static async Task HarvestVideoAsync(
        ContentSource source,
        YouTubeChannelVideo video,
        IContentVideoStore videoStore,
        IBlockedVideoStore blockedVideoStore,
        IWhisperSpendLedger ledger,
        ITranscriptSource transcriptSource,
        HarvestCounts counts,
        Serilog.ILogger logger,
        Func<DateTimeOffset> utcNow,
        CancellationToken ct)
    {
        if (video.Duration is { } duration && duration <= ShortVideoMaxDuration)
        {
            logger.Information("skipped short {VideoId} duration_s={DurationSeconds}", video.VideoId, (int)duration.TotalSeconds);
            return;
        }

        if (await blockedVideoStore.IsBlockedAsync(video.VideoId, ct))
        {
            logger.Information("skipped blocked {VideoId}", video.VideoId);
            return;
        }

        long? contentVideoId = null;
        var mayMarkFailed = false;
        var statusPersisted = false;
        try
        {
            var resolution = await ResolveHarvestVideoIdAsync(source, video, videoStore, logger, ct);
            contentVideoId = resolution.VideoId;
            mayMarkFailed = resolution.MayMarkFailed;
            if (contentVideoId is null)
            {
                return;
            }

            var monthKey = utcNow().UtcDateTime.ToString("yyyy-MM");
            var result = await transcriptSource.FetchTranscriptAsync(video.VideoId, video.Duration, monthKey, ct);
            statusPersisted = await PersistTranscriptResultAsync(videoStore, ledger, contentVideoId.Value, result, monthKey, ct);
            counts.Add(result.Outcome);
            LogFetch(logger, video.VideoId, result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await MarkFailedIfPossibleAsync(videoStore, contentVideoId, mayMarkFailed && !statusPersisted, ct);
            logger.Error(exception, "harvest failed {VideoId}", video.VideoId);
        }
    }

    private static async Task<HarvestVideoResolution> ResolveHarvestVideoIdAsync(
        ContentSource source,
        YouTubeChannelVideo video,
        IContentVideoStore videoStore,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        var existing = await videoStore.GetVideoByYoutubeIdAsync(source.Id, video.VideoId, ct);
        if (existing is not null)
        {
            if (IsTerminalSuccess(existing.TranscriptStatus))
            {
                logger.Information(
                    "already harvested {VideoId} transcript_status={TranscriptStatus}",
                    video.VideoId,
                    existing.TranscriptStatus);
                return new HarvestVideoResolution(null, MayMarkFailed: false);
            }

            logger.Information(
                "resuming harvest {VideoId} transcript_status={TranscriptStatus}",
                video.VideoId,
                existing.TranscriptStatus);
            return new HarvestVideoResolution(existing.Id, existing.TranscriptStatus == TranscriptStatus.Pending);
        }

        var videoId = await videoStore.InsertVideoAsync(
            source.Id,
            video.VideoId,
            rssGuid: null,
            video.Title,
            video.Url,
            video.PublishedUtc,
            TranscriptStatus.Pending,
            ct);
        return new HarvestVideoResolution(videoId, MayMarkFailed: true);
    }

    private static async Task<bool> PersistTranscriptResultAsync(
        IContentVideoStore videoStore,
        IWhisperSpendLedger ledger,
        long videoId,
        TranscriptFetchResult result,
        string monthKey,
        CancellationToken ct)
    {
        switch (result.Outcome)
        {
            case TranscriptOutcome.Captions:
                await videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Captions, result.Body!, ct);
                await videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Captions, ct);
                return true;
            case TranscriptOutcome.Whisper:
                // Record spend first: a ledger row without a transcript is conservative;
                // a transcript/status without a ledger row under-counts the monthly cap.
                await ledger.RecordCallAsync(videoId, result.SecondsBilled!.Value, result.CostUsd!.Value, monthKey, ct);
                await videoStore.InsertTranscriptAsync(videoId, TranscriptSource.Whisper, result.Body!, ct);
                await videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Whisper, ct);
                return true;
            case TranscriptOutcome.SkippedOverCap:
                await videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.SkippedOverCap, ct);
                return true;
            case TranscriptOutcome.SkippedNoCaptions:
                await videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.SkippedNoCaptions, ct);
                return true;
            case TranscriptOutcome.Failed:
                await videoStore.UpdateTranscriptStatusAsync(videoId, TranscriptStatus.Failed, ct);
                return true;
        }

        return false;
    }

    private static async Task WarnIfFfmpegUnavailableAsync(
        IFfmpegAudioChunker chunker,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        if (!await chunker.IsAvailableAsync(ct))
        {
            logger.Warning("ffmpeg not found on PATH - audio >24MB will be marked failed.");
        }
    }

    private static async Task MarkFailedIfPossibleAsync(
        IContentVideoStore videoStore,
        long? videoId,
        bool mayMarkFailed,
        CancellationToken ct)
    {
        if (videoId is not null && mayMarkFailed)
        {
            await videoStore.UpdateTranscriptStatusAsync(videoId.Value, TranscriptStatus.Failed, ct);
        }
    }

    private sealed record HarvestVideoResolution(long? VideoId, bool MayMarkFailed);

    private static void LogFetch(Serilog.ILogger logger, string videoId, TranscriptFetchResult result)
        => logger.Information(
            "harvested {VideoId} transcript_source={TranscriptSource} caption_track_kind={CaptionTrackKind} outcome={Outcome}",
            videoId,
            result.Source,
            GetCaptionTrackKind(result),
            result.Outcome);

    private static void LogFallbackRatio(Serilog.ILogger logger, string sourceSlug, HarvestCounts counts)
        => logger.Information(
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

    private static async Task<DistillVideoOutcome> DistillVideoAsync(
        ContentSource source,
        ContentVideo video,
        IContentVideoStore videoStore,
        IContentSiteIndexStore indexStore,
        ILlmSpendLedger ledger,
        ILlmDistillationService distiller,
        string artifactRoot,
        string monthKey,
        DateTimeOffset generatedUtc,
        Serilog.ILogger logger,
        CancellationToken ct,
        bool isSubscriptionProvider = false)
    {
        var naturalKey = GetContentNaturalKey(video);
        var llmCalls = 0;
        var llmSpend = 0m;
        try
        {
            var transcript = await videoStore.GetLatestTranscriptAsync(video.Id, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Transcript missing for {naturalKey}.");
            ValidateTranscriptLength(transcript.Body);

            var projectedVideoCost = isSubscriptionProvider ? 0m : ComputeProjectedVideoCostUsd(transcript.Body);
            if (!isSubscriptionProvider && await ledger.WouldExceedCapAsync(projectedVideoCost, monthKey, ct).ConfigureAwait(false))
            {
                return await MarkSkippedOverCapAsync(
                    videoStore,
                    video.Id,
                    naturalKey,
                    "llm monthly cap would be exceeded before distilling " + naturalKey,
                    llmCalls,
                    llmSpend,
                    logger,
                    ct).ConfigureAwait(false);
            }

            await videoStore.ClearDistillOutputAsync(video.Id, ct).ConfigureAwait(false);

            if (!isSubscriptionProvider && await ledger.WouldExceedCapAsync(
                ComputeProjectedCallCostUsd(transcript.Body, SummaryMaxOutputTokens),
                monthKey,
                ct).ConfigureAwait(false))
            {
                return await MarkSkippedOverCapAsync(
                    videoStore,
                    video.Id,
                    naturalKey,
                    "llm monthly cap would be exceeded before summary for " + naturalKey,
                    llmCalls,
                    llmSpend,
                    logger,
                    ct).ConfigureAwait(false);
            }

            var summary = await distiller.SummarizeAsync(transcript.Body, ct).ConfigureAwait(false);
            var summaryCost = isSubscriptionProvider ? 0m : LlmSpendLedger.ComputeCostUsd(summary.Usage.InputTokens, summary.Usage.OutputTokens);
            // Why: each OpenAI call is separately billed; record its incurred cost BEFORE the next call so a later-call failure can never orphan an already-billed cost (HIGH-1/FIX-1, Phase 20 CR-01 class -- recorded spend >= incurred).
            await ledger.RecordCallAsync(
                video.Id,
                summary.Usage.InputTokens,
                summary.Usage.OutputTokens,
                summaryCost,
                monthKey,
                ct).ConfigureAwait(false);
            llmCalls++;
            llmSpend += summaryCost;

            if (!isSubscriptionProvider && await ledger.WouldExceedCapAsync(
                ComputeProjectedCallCostUsd(transcript.Body, ClipsMaxOutputTokens),
                monthKey,
                ct).ConfigureAwait(false))
            {
                return await MarkSkippedOverCapAsync(
                    videoStore,
                    video.Id,
                    naturalKey,
                    "llm monthly cap would be exceeded before clips for " + naturalKey,
                    llmCalls,
                    llmSpend,
                    logger,
                    ct).ConfigureAwait(false);
            }

            var clips = await distiller.ExtractClipsAsync(transcript.Body, ct).ConfigureAwait(false);
            var clipsCost = isSubscriptionProvider ? 0m : LlmSpendLedger.ComputeCostUsd(clips.Usage.InputTokens, clips.Usage.OutputTokens);
            await ledger.RecordCallAsync(
                video.Id,
                clips.Usage.InputTokens,
                clips.Usage.OutputTokens,
                clipsCost,
                monthKey,
                ct).ConfigureAwait(false);
            llmCalls++;
            llmSpend += clipsCost;

            if (!isSubscriptionProvider && await ledger.WouldExceedCapAsync(
                ComputeProjectedCallCostUsd(transcript.Body, TagsMaxOutputTokens),
                monthKey,
                ct).ConfigureAwait(false))
            {
                return await MarkSkippedOverCapAsync(
                    videoStore,
                    video.Id,
                    naturalKey,
                    "llm monthly cap would be exceeded before tags for " + naturalKey,
                    llmCalls,
                    llmSpend,
                    logger,
                    ct).ConfigureAwait(false);
            }

            var tags = await distiller.InferTagsAsync(transcript.Body, ct).ConfigureAwait(false);
            var tagsCost = isSubscriptionProvider ? 0m : LlmSpendLedger.ComputeCostUsd(tags.Usage.InputTokens, tags.Usage.OutputTokens);
            await ledger.RecordCallAsync(
                video.Id,
                tags.Usage.InputTokens,
                tags.Usage.OutputTokens,
                tagsCost,
                monthKey,
                ct).ConfigureAwait(false);
            llmCalls++;
            llmSpend += tagsCost;

            ValidateSummary(summary.Summary);
            ValidateClips(clips.Clips);
            var archetypeTags = FilterTags(ContentTagDimension.Archetype, tags.Archetype, logger);
            var bracketTags = FilterTags(ContentTagDimension.Bracket, tags.Bracket, logger);
            var cardCategoryTags = FilterTags(ContentTagDimension.CardCategory, tags.CardCategory, logger);

            await videoStore.InsertSummaryAsync(video.Id, summary.Summary, ct).ConfigureAwait(false);
            var sortOrder = 0;
            foreach (var clip in clips.Clips)
            {
                // Why: 0 is a STORAGE sentinel for unknown timestamp (timestamp_s is NOT NULL); the artifact renders the [mm:ss] omission from the in-memory nullable clip, never from this row (MEDIUM-3/D-08).
                await videoStore.InsertClipAsync(
                    video.Id,
                    clip.TimestampSeconds ?? 0,
                    clip.Excerpt,
                    sortOrder++,
                    ct).ConfigureAwait(false);
            }

            foreach (var tag in archetypeTags)
            {
                await videoStore.InsertTagAsync(video.Id, ContentTagDimension.Archetype, tag, ct).ConfigureAwait(false);
            }

            foreach (var tag in bracketTags)
            {
                await videoStore.InsertTagAsync(video.Id, ContentTagDimension.Bracket, tag, ct).ConfigureAwait(false);
            }

            foreach (var tag in cardCategoryTags)
            {
                await videoStore.InsertTagAsync(video.Id, ContentTagDimension.CardCategory, tag, ct).ConfigureAwait(false);
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
            ContentArtifactWriter.WriteFile(artifactRoot, source.SourceSlug, naturalKey, artifactText);
            await indexStore.UpsertRowAsync(
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
                ct).ConfigureAwait(false);
            await videoStore.SetDistillStatusAsync(video.Id, DistillStatusDistilled, ct).ConfigureAwait(false);
            logger.Information("distilled {VideoId}", naturalKey);
            return DistillVideoOutcome.Distilled(llmCalls, llmSpend);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await videoStore.SetDistillStatusAsync(video.Id, DistillStatusFailed, ct).ConfigureAwait(false);
            logger.Error(exception, "distill failed {VideoId}", naturalKey);
            return DistillVideoOutcome.Failed(llmCalls, llmSpend, naturalKey);
        }
    }

    private static async Task<DistillVideoOutcome> MarkSkippedOverCapAsync(
        IContentVideoStore videoStore,
        long videoId,
        string naturalKey,
        string abortedReason,
        int llmCalls,
        decimal llmSpend,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        await videoStore.SetDistillStatusAsync(videoId, DistillStatusSkippedOverCap, ct).ConfigureAwait(false);
        logger.Warning("distill skipped_over_cap {VideoId} reason={AbortedReason}", naturalKey, abortedReason);
        return DistillVideoOutcome.SkippedOverCap(llmCalls, llmSpend, abortedReason);
    }

    private static IReadOnlyList<string> FilterTags(
        string dimension,
        IReadOnlyList<string> tags,
        Serilog.ILogger logger)
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

            logger.Warning("dropped out-of-vocab tag {Dimension} {Tag}", dimension, tag);
        }

        return valid;
    }

    private static string GetContentNaturalKey(ContentVideo video)
    {
        var hasYoutubeVideoId = !string.IsNullOrWhiteSpace(video.YoutubeVideoId);
        var hasRssGuid = !string.IsNullOrWhiteSpace(video.RssGuid);
        if (hasYoutubeVideoId == hasRssGuid)
        {
            throw new InvalidOperationException("Exactly one content natural key is required for distillation.");
        }

        return hasYoutubeVideoId ? video.YoutubeVideoId! : video.RssGuid!;
    }

    private static void ValidateTranscriptLength(string transcript)
    {
        if (EstimateTokenCount(transcript) > MaxTranscriptInputTokens)
        {
            throw new InvalidOperationException("Transcript too long for the distillation context window.");
        }
    }

    private static void ValidateSummary(string summary)
    {
        if (CountWords(summary) > SummaryMaxWords)
        {
            throw new InvalidOperationException("Summary exceeded the 200-word limit.");
        }
    }

    private static void ValidateClips(IReadOnlyList<ClipItem> clips)
    {
        if (clips.Count is < MinClipCount or > MaxClipCount)
        {
            throw new InvalidOperationException("Clip extraction must return 3 to 8 clips.");
        }

        if (clips.Any(clip => clip.TimestampSeconds < 0))
        {
            throw new InvalidOperationException("Clip timestamps cannot be negative.");
        }

        if (clips.All(clip => (clip.TimestampSeconds ?? 0) == 0))
        {
            throw new InvalidOperationException("Clip extraction cannot return every clip with timestamp 0.");
        }
    }

    private static int CountWords(string text)
        => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static decimal ComputeProjectedVideoCostUsd(string transcript)
        => LlmSpendLedger.ComputeCostUsd(
            EstimateTokenCount(transcript) * DistillationCallCount,
            SummaryMaxOutputTokens + ClipsMaxOutputTokens + TagsMaxOutputTokens);

    private static decimal ComputeProjectedCallCostUsd(string transcript, int maxOutputTokens)
        => LlmSpendLedger.ComputeCostUsd(EstimateTokenCount(transcript), maxOutputTokens);

    private static int EstimateTokenCount(string transcript)
        => Math.Max(1, (int)Math.Ceiling(transcript.Length / 4m));

    public static async Task<int> RunCategoryFindAsync(string cardName, int cacheSeconds, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            Console.Error.WriteLine("Card name is required.");
            return 1;
        }

        var artifactsPath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
        var repository = new CategoryKnowledgeRepository(Path.Combine(artifactsPath, "category-knowledge.db"));
        var perRun = GetCacheDurationSeconds(0, cacheSeconds);
        var totalTimeout = TimeSpan.FromSeconds(Math.Max(perRun, timeoutSeconds));
        using var cancellation = new CancellationTokenSource(totalTimeout);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < totalTimeout)
        {
            await repository.EnsureSchemaAsync(cancellation.Token);
            var categories = await repository.GetCategoriesAsync(cardName, cancellation.Token);
            if (categories.Count > 0)
            {
                Console.WriteLine($"Found {cardName} with categories: {string.Join(", ", categories)}");
                return 0;
            }

            var remainingSeconds = Math.Min(perRun, (int)(totalTimeout - stopwatch.Elapsed).TotalSeconds);
            if (remainingSeconds <= 0)
            {
                break;
            }

            await RunArchidektCacheAsync(remainingSeconds, Log.Logger);
        }

        Console.WriteLine($"Timed out after {totalTimeout.TotalSeconds:F0}s without finding {cardName}.");
        return 2;
    }

    public static void CollectInterestingPaths(System.Text.Json.JsonElement element, string path, List<string> paths)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = $"{path}.{property.Name}";
                    if (property.Name.Contains("tag", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("category", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("board", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("annotation", StringComparison.OrdinalIgnoreCase))
                    {
                        paths.Add(propertyPath);
                    }

                    CollectInterestingPaths(property.Value, propertyPath, paths);
                }

                break;
            case System.Text.Json.JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (index >= 3)
                    {
                        paths.Add($"{path}[...]");
                        break;
                    }

                    CollectInterestingPaths(item, $"{path}[{index}]", paths);
                    index++;
                }

                break;
        }
    }

    public static int GetCacheDurationSeconds(int minutes, int seconds)
        => Math.Max(5, minutes > 0 ? minutes * 60 : seconds);

    public static async Task<int> RunCardLookupAsync(string cardName)
    {
        try
        {
            var client = new RestClient(new RestClientOptions
            {
                BaseUrl = new Uri("https://api.scryfall.com"),
                ThrowOnAnyError = false,
            });

            client.AddDefaultHeader("User-Agent", "DeckFlow.CLI/1.0 (+https://github.com/luntc1972/DeckFlow)");
            client.AddDefaultHeader("Accept", "application/json;q=0.9,*/*;q=0.8");

            var request = new RestRequest("cards/named", Method.Get);
            request.AddQueryParameter("exact", cardName);

            var response = await client.ExecuteAsync<ScryfallCardDto>(request);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Card not found: {cardName}");
                return 2;
            }

            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                Console.Error.WriteLine($"Scryfall returned HTTP {(int)response.StatusCode}.");
                return 1;
            }

            PrintCard(response.Data);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static async Task<int> RunScryfallProbeAsync(string endpoint, string? cardName, int repeat)
    {
        try
        {
            var client = new RestClient(new RestClientOptions
            {
                BaseUrl = new Uri("https://api.scryfall.com"),
                ThrowOnAnyError = false,
            });

            client.AddDefaultHeader("User-Agent", "DeckFlow.CLI/1.0 (+https://github.com/luntc1972/DeckFlow)");
            client.AddDefaultHeader("Accept", "application/json;q=0.9,*/*;q=0.8");

            var iterations = Math.Max(1, repeat);
            for (var index = 0; index < iterations; index++)
            {
                var request = BuildProbeRequest(endpoint, cardName);
                var start = DateTime.UtcNow;
                var response = await client.ExecuteAsync(request);
                var elapsed = DateTime.UtcNow - start;

                Console.WriteLine($"=== Probe #{index + 1}/{iterations} ===");
                Console.WriteLine($"URL: {client.Options.BaseUrl}{request.Resource}");
                Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"Elapsed: {elapsed.TotalMilliseconds:F0} ms");

                Console.WriteLine("Response headers:");
                if (response.Headers is not null)
                {
                    foreach (var header in response.Headers.OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"  {header.Name}: {header.Value}");
                    }
                }

                Console.WriteLine("Rate-limit-relevant headers (extracted):");
                PrintHeaderIfPresent(response, "Retry-After");
                PrintHeaderIfPresent(response, "X-RateLimit-Limit");
                PrintHeaderIfPresent(response, "X-RateLimit-Remaining");
                PrintHeaderIfPresent(response, "X-RateLimit-Reset");
                PrintHeaderIfPresent(response, "RateLimit-Limit");
                PrintHeaderIfPresent(response, "RateLimit-Remaining");
                PrintHeaderIfPresent(response, "RateLimit-Reset");

                var body = response.Content ?? string.Empty;
                var bodyPreview = body.Length > 800 ? body[..800] + $"... [truncated, total {body.Length} chars]" : body;
                Console.WriteLine("Body:");
                Console.WriteLine(bodyPreview);
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static RestRequest BuildProbeRequest(string endpoint, string? cardName)
    {
        var mode = endpoint.ToLowerInvariant();
        switch (mode)
        {
            case "named":
            {
                var request = new RestRequest("cards/named", Method.Get);
                request.AddQueryParameter("exact", cardName ?? "Sol Ring");
                return request;
            }
            case "search":
            {
                var request = new RestRequest("cards/search", Method.Get);
                request.AddQueryParameter("q", cardName ?? "Sol Ring");
                return request;
            }
            case "random":
            {
                return new RestRequest("cards/random", Method.Get);
            }
            default:
            {
                var request = new RestRequest("cards/named", Method.Get);
                request.AddQueryParameter("exact", cardName ?? "Sol Ring");
                return request;
            }
        }
    }

    private static void PrintHeaderIfPresent(RestResponse response, string headerName)
    {
        var value = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, headerName, StringComparison.OrdinalIgnoreCase))?.Value;
        if (value is not null)
        {
            Console.WriteLine($"  {headerName}: {value}");
        }
    }

    private static async Task<int> HandleContentSourceUniqueViolationAsync(
        ContentSourceStore store,
        string slug,
        string url,
        Exception exception)
    {
        var sources = await store.ListEnabledSourcesAsync();
        if (sources.Any(source => string.Equals(source.SourceUrl, url, StringComparison.Ordinal)))
        {
            Console.WriteLine("source already exists (same url)");
            return 0;
        }

        if (sources.Any(source => string.Equals(source.SourceSlug, slug, StringComparison.Ordinal))
            || ExceptionContains(exception, "source_slug"))
        {
            Console.Error.WriteLine($"slug '{slug}' already used by a different url - pass a distinct --name");
            return 3;
        }

        Console.Error.WriteLine(exception.Message);
        return 1;
    }

    private static string ResolveContentKbDatabasePath(FileInfo? db)
        => db?.FullName ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "content-kb.db");

    private static string ResolveContentKbArtifactRoot(FileInfo? db)
    {
        var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDir))
        {
            return Path.GetFullPath(Path.Combine(dataDir, "content-kb"));
        }

        // Why: D-11 / HSK-04 collapses the dual artifact tree to the repo-root content-kb so drift is impossible.
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "content-kb"));
    }

    private sealed record ContentIndexExportRow
    {
        public required string NaturalKeyType { get; init; }

        public required string NaturalKeyValue { get; init; }

        public required string Source { get; init; }

        public required string Title { get; init; }

        public required string VideoUrl { get; init; }

        public required string ArtifactPath { get; init; }

        public DateTimeOffset? PublishedUtc { get; init; }

        public required DateTimeOffset IndexedUtc { get; init; }

        public required IReadOnlyList<string> ArchetypeTags { get; init; }

        public required IReadOnlyList<string> BracketTags { get; init; }

        public required IReadOnlyList<string> CardCategoryTags { get; init; }

        public static ContentIndexExportRow From(ContentSiteIndexRow row)
        {
            var (naturalKeyType, naturalKeyValue) = GetNaturalKey(row);

            return new ContentIndexExportRow
            {
                NaturalKeyType = naturalKeyType,
                NaturalKeyValue = naturalKeyValue,
                Source = row.Source,
                Title = row.Title,
                VideoUrl = row.VideoUrl,
                ArtifactPath = row.ArtifactPath,
                PublishedUtc = row.PublishedUtc,
                IndexedUtc = row.IndexedUtc,
                ArchetypeTags = row.ArchetypeTags,
                BracketTags = row.BracketTags,
                CardCategoryTags = row.CardCategoryTags,
            };
        }

        private static (string NaturalKeyType, string NaturalKeyValue) GetNaturalKey(ContentSiteIndexRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.YoutubeVideoId))
            {
                return (ContentSourceType.Youtube, row.YoutubeVideoId);
            }

            if (!string.IsNullOrWhiteSpace(row.RssGuid))
            {
                return (ContentSourceType.Podcast, row.RssGuid);
            }

            throw new InvalidOperationException($"Content site-index row {row.Id} has no natural key.");
        }
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

    // Why: 60s is the conservative YouTube Shorts cutoff - long enough to exclude Shorts,
    // short enough to keep legitimate brief MTG videos.
    private static readonly TimeSpan ShortVideoMaxDuration = TimeSpan.FromSeconds(60);
    private const int SummaryMaxOutputTokens = 400;
    private const int ClipsMaxOutputTokens = 1200;
    private const int TagsMaxOutputTokens = 200;
    private const int SummaryMaxWords = 200;
    private const int MinClipCount = 3;
    private const int MaxClipCount = 8;
    private const int MaxTranscriptInputTokens = 120_000;
    private const int DistillationCallCount = 3;
    private const string DistillStatusDistilled = "distilled";
    private const string DistillStatusSkippedOverCap = "skipped_over_cap";
    private const string DistillStatusFailed = "failed";

    private sealed class DistillCounts
    {
        public int SourcesProcessed { get; set; }

        public int VideosDistilled { get; private set; }

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

            if (outcome.FailedVideoId is not null)
            {
                DistillFailed++;
                FailedVideoIds.Add(outcome.FailedVideoId);
            }
        }
    }

    private sealed record DistillVideoOutcome(
        bool IsDistilled,
        int LlmCalls,
        decimal LlmSpendUsd,
        string? FailedVideoId,
        string? AbortedReason)
    {
        public static DistillVideoOutcome Distilled(int llmCalls, decimal llmSpendUsd)
            => new(true, llmCalls, llmSpendUsd, FailedVideoId: null, AbortedReason: null);

        public static DistillVideoOutcome Failed(int llmCalls, decimal llmSpendUsd, string failedVideoId)
            => new(false, llmCalls, llmSpendUsd, failedVideoId, AbortedReason: null);

        public static DistillVideoOutcome SkippedOverCap(int llmCalls, decimal llmSpendUsd, string abortedReason)
            => new(false, llmCalls, llmSpendUsd, FailedVideoId: null, abortedReason);
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

    private static void PrintCard(ScryfallCardDto card)
    {
        Console.WriteLine(card.Name);
        if (!string.IsNullOrWhiteSpace(card.ManaCost))
        {
            Console.WriteLine(card.ManaCost);
        }

        Console.WriteLine(card.TypeLine);
        if (!string.IsNullOrWhiteSpace(card.OracleText))
        {
            Console.WriteLine();
            Console.WriteLine(card.OracleText);
        }

        if (!string.IsNullOrWhiteSpace(card.Power) && !string.IsNullOrWhiteSpace(card.Toughness))
        {
            Console.WriteLine();
            Console.WriteLine($"{card.Power}/{card.Toughness}");
        }
    }

    /// <summary>
    /// Creates the shared deck loader used by the CLI commands.
    /// </summary>
    /// <returns>A deck loader configured with the CLI's concrete importers and parsers.</returns>
    private static IDeckEntryLoader CreateDeckEntryLoader()
        => new DeckEntryLoader(
            new MoxfieldApiDeckImporter(),
            new ArchidektApiDeckImporter(),
            new MoxfieldParser(),
            new ArchidektParser());

    private record ScryfallCardDto(
        string Name,
        [property: JsonPropertyName("mana_cost")] string? ManaCost,
        [property: JsonPropertyName("type_line")] string TypeLine,
        [property: JsonPropertyName("oracle_text")] string? OracleText,
        [property: JsonPropertyName("power")] string? Power,
        [property: JsonPropertyName("toughness")] string? Toughness);
}
