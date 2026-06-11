using System.CommandLine;
using System.IO;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;
using DeckFlow.CLI;
using Serilog;
using Serilog.Events;

var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
Directory.CreateDirectory(logDirectory);
var logPath = Path.Combine(logDirectory, "cli-.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

AppDomain.CurrentDomain.ProcessExit += (_, _) => Log.CloseAndFlush();

var compareCommand = new Command("compare", "Compare Moxfield and Archidekt exports.");
var moxfieldOption = new Option<FileInfo?>("--moxfield");
var moxfieldUrlOption = new Option<string?>("--moxfield-url");
var archidektOption = new Option<FileInfo?>("--archidekt");
var archidektUrlOption = new Option<string?>("--archidekt-url");
var outOption = new Option<FileInfo>("--out") { IsRequired = true };
var modeOption = new Option<MatchMode>("--mode", () => MatchMode.Loose);
var directionOption = new Option<SyncDirection>("--direction", () => SyncDirection.MoxfieldToArchidekt);
var dryRunOption = new Option<bool>("--dry-run");
var resolveConflictsOption = new Option<bool>("--resolve-conflicts");
var probeCommand = new Command("probe-moxfield", "Fetch a public Moxfield deck JSON payload and inspect tag-like fields.");
var probeUrlOption = new Option<string>("--url") { IsRequired = true };
var probeOutOption = new Option<FileInfo?>("--out");
var exportMoxfieldCommand = new Command("export-moxfield", "Fetch a public Moxfield deck through the API and save it as deck text.");
var exportMoxfieldUrlOption = new Option<string>("--url") { IsRequired = true };
var exportMoxfieldOutOption = new Option<FileInfo>("--out") { IsRequired = true };
var archidektCategoriesCommand = new Command("archidekt-categories", "Fetch a public Archidekt deck and print category counts by card quantity.");
var archidektCategoriesUrlOption = new Option<string>("--url") { IsRequired = true };
var archidektCategoriesOutOption = new Option<FileInfo?>("--out");
var archidektCategoryCardsCommand = new Command("archidekt-category-cards", "Fetch a public Archidekt deck and list cards in a specific category.");
var archidektCategoryCardsUrlOption = new Option<string>("--url") { IsRequired = true };
var archidektCategoryCardsCategoryOption = new Option<string>("--category") { IsRequired = true };
var archidektCategoryCardsOutOption = new Option<FileInfo?>("--out");
var archidektHarvestRecentCommand = new Command("archidekt-harvest-recent", "Fetch recent public Archidekt decks and aggregate cards by category.");
var archidektHarvestRecentCountOption = new Option<int>("--count", () => 20);
var archidektHarvestRecentOutOption = new Option<FileInfo>("--out") { IsRequired = true };
var archidektCacheCommand = new Command("archidekt-cache", "Run an incremental Archidekt category cache job for the requested duration.");
var archidektCacheSecondsOption = new Option<int>("--seconds", () => 20);
var archidektCacheMinutesOption = new Option<int>("--minutes", () => 0);
var categoryFindCommand = new Command("category-find", "Keep running the cache job until a card is observed in the knowledge DB.");
var categoryFindCardOption = new Option<string>("--card") { IsRequired = true };
var categoryFindSecondsOption = new Option<int>("--cache-seconds", () => 20);
var categoryFindTimeoutOption = new Option<int>("--timeout", () => 600);
var cardLookupCommand = new Command("card-lookup", "Lookup a single card via Scryfall and show the printed text.");
var cardLookupNameOption = new Option<string>("--name") { IsRequired = true };
var scryfallProbeCommand = new Command("scryfall-probe", "Hit Scryfall once (or many times) and log the full response including headers.");
var scryfallProbeEndpointOption = new Option<string>("--endpoint", () => "named") { Description = "named | search | random" };
var scryfallProbeNameOption = new Option<string?>("--name") { Description = "Card name for named/search. Defaults to Sol Ring." };
var scryfallProbeRepeatOption = new Option<int>("--repeat", () => 1) { Description = "How many times to call the endpoint back-to-back (use to force 429)." };
var contentSourceAddCommand = new Command("content-source-add", "Add a content source for the Content KB harvester.");
var contentSourceAddUrlOption = new Option<string>("--url") { IsRequired = true };
var contentSourceAddNameOption = new Option<string>("--name") { IsRequired = true };
var contentSourceAddTypeOption = new Option<string>("--type", () => ContentSourceType.Youtube) { Description = "youtube_channel | podcast_rss" };
var contentSourceAddDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var contentSourceSetEnabledCommand = new Command("content-source-set-enabled", "Enable or disable a Content KB source.");
var contentSourceSetEnabledIdOption = new Option<long>("--id") { IsRequired = true };
var contentSourceSetEnabledEnabledOption = new Option<bool>("--enabled") { IsRequired = true };
var contentSourceSetEnabledDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var harvestCommand = new Command("harvest", "Fetch transcripts for enabled Content KB sources.");
var harvestDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var harvestLimitOption = new Option<int>("--limit", () => 5) { Description = "Recent videos per enabled source." };
var harvestEnableWhisperOption = new Option<bool>("--enable-whisper", () => false) { Description = "Enable Whisper audio-transcription fallback when captions are unavailable (off by default; captions-only)." };
var harvestVideoIdsOption = new Option<string?>("--video-ids") { Description = "Comma-separated YouTube video ids to harvest instead of the most-recent walk; --limit is ignored. Ids must belong to the target source's channel." };
var harvestSourceIdOption = new Option<long?>("--source-id") { Description = "Content source id the --video-ids belong to. Required when more than one YouTube source is enabled." };
var blockVideoCommand = new Command("block-video", "Block a YouTube video id and hard-delete existing local Content KB rows.");
var blockVideoIdArgument = new Argument<string>("youtube-id");
var blockVideoReasonOption = new Option<string?>("--reason") { Description = "Optional operator-supplied reason for the block." };
var blockVideoDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var unblockVideoCommand = new Command("unblock-video", "Remove a YouTube video id from the harvest block list.");
var unblockVideoIdArgument = new Argument<string>("youtube-id");
var unblockVideoDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var listBlockedCommand = new Command("list-blocked", "List blocked YouTube video ids.");
var listBlockedDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var corpusResetCommand = new Command("corpus-reset", "Delete all Content KB corpus rows while preserving blocked_videos and content_sources.");
var corpusResetDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var corpusResetConnectionStringOption = new Option<string?>("--connection-string") { Description = "Explicit Postgres connection string for resetting a non-SQLite Content KB database." };
var corpusResetDryRunOption = new Option<bool>("--dry-run", () => false) { Description = "Report the reset target without deleting anything." };
var distillCommand = new Command("distill", "Distill harvested transcripts into Content KB artifacts.");
var distillDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var distillLimitOption = new Option<int>("--limit", () => 5) { Description = "Videos to distill per enabled source." };
var distillDryRunOption = new Option<bool>("--dry-run", () => false) { Description = "Estimate projected spend over pending videos and process nothing." };
var distillVideoIdsOption = new Option<string?>("--video-ids") { Description = "Comma-separated natural keys (YouTube video ids or RSS guids) to distill; other pending videos are skipped and --limit is ignored." };
var contentIndexExportCommand = new Command("content-index-export", "Exports the local content_site_index to a tracked JSON seed file for commit-then-deploy.");
var contentIndexExportDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var contentIndexExportOutputOption = new Option<FileInfo?>("--output", () => new FileInfo(Path.Combine("content-kb", "seed", "index-seed.json"))) { Description = "Path to the JSON seed file. Defaults to content-kb/seed/index-seed.json." };

compareCommand.AddOption(moxfieldOption);
compareCommand.AddOption(moxfieldUrlOption);
compareCommand.AddOption(archidektOption);
compareCommand.AddOption(archidektUrlOption);
compareCommand.AddOption(outOption);
compareCommand.AddOption(modeOption);
compareCommand.AddOption(directionOption);
compareCommand.AddOption(dryRunOption);
compareCommand.AddOption(resolveConflictsOption);
probeCommand.AddOption(probeUrlOption);
probeCommand.AddOption(probeOutOption);
exportMoxfieldCommand.AddOption(exportMoxfieldUrlOption);
exportMoxfieldCommand.AddOption(exportMoxfieldOutOption);
archidektCategoriesCommand.AddOption(archidektCategoriesUrlOption);
archidektCategoriesCommand.AddOption(archidektCategoriesOutOption);
archidektCategoryCardsCommand.AddOption(archidektCategoryCardsUrlOption);
archidektCategoryCardsCommand.AddOption(archidektCategoryCardsCategoryOption);
archidektCategoryCardsCommand.AddOption(archidektCategoryCardsOutOption);
archidektHarvestRecentCommand.AddOption(archidektHarvestRecentCountOption);
archidektHarvestRecentCommand.AddOption(archidektHarvestRecentOutOption);
archidektCacheCommand.AddOption(archidektCacheSecondsOption);
archidektCacheCommand.AddOption(archidektCacheMinutesOption);
categoryFindCommand.AddOption(categoryFindCardOption);
categoryFindCommand.AddOption(categoryFindSecondsOption);
categoryFindCommand.AddOption(categoryFindTimeoutOption);
cardLookupCommand.AddOption(cardLookupNameOption);
scryfallProbeCommand.AddOption(scryfallProbeEndpointOption);
scryfallProbeCommand.AddOption(scryfallProbeNameOption);
scryfallProbeCommand.AddOption(scryfallProbeRepeatOption);
contentSourceAddCommand.AddOption(contentSourceAddUrlOption);
contentSourceAddCommand.AddOption(contentSourceAddNameOption);
contentSourceAddCommand.AddOption(contentSourceAddTypeOption);
contentSourceAddCommand.AddOption(contentSourceAddDbOption);
contentSourceSetEnabledCommand.AddOption(contentSourceSetEnabledIdOption);
contentSourceSetEnabledCommand.AddOption(contentSourceSetEnabledEnabledOption);
contentSourceSetEnabledCommand.AddOption(contentSourceSetEnabledDbOption);
harvestCommand.AddOption(harvestDbOption);
harvestCommand.AddOption(harvestLimitOption);
harvestCommand.AddOption(harvestEnableWhisperOption);
harvestCommand.AddOption(harvestVideoIdsOption);
harvestCommand.AddOption(harvestSourceIdOption);
blockVideoCommand.AddArgument(blockVideoIdArgument);
blockVideoCommand.AddOption(blockVideoReasonOption);
blockVideoCommand.AddOption(blockVideoDbOption);
unblockVideoCommand.AddArgument(unblockVideoIdArgument);
unblockVideoCommand.AddOption(unblockVideoDbOption);
listBlockedCommand.AddOption(listBlockedDbOption);
corpusResetCommand.AddOption(corpusResetDbOption);
corpusResetCommand.AddOption(corpusResetConnectionStringOption);
corpusResetCommand.AddOption(corpusResetDryRunOption);
distillCommand.AddOption(distillDbOption);
distillCommand.AddOption(distillLimitOption);
distillCommand.AddOption(distillDryRunOption);
distillCommand.AddOption(distillVideoIdsOption);
contentIndexExportCommand.AddOption(contentIndexExportDbOption);
contentIndexExportCommand.AddOption(contentIndexExportOutputOption);

compareCommand.SetHandler(context =>
{
    var parseResult = context.ParseResult;
    Environment.ExitCode = CommandRunners.RunCompareAsync(
        parseResult.GetValueForOption(moxfieldOption),
        parseResult.GetValueForOption(moxfieldUrlOption),
        parseResult.GetValueForOption(archidektOption),
        parseResult.GetValueForOption(archidektUrlOption),
        parseResult.GetValueForOption(outOption)!,
        parseResult.GetValueForOption(modeOption),
        parseResult.GetValueForOption(directionOption),
        parseResult.GetValueForOption(dryRunOption),
        parseResult.GetValueForOption(resolveConflictsOption)).GetAwaiter().GetResult();
});

var rootCommand = new RootCommand("DeckFlow");
var cacheFlagOption = new Option<bool>("--archidekt-cache", "Run an Archidekt category cache sweep for the requested duration.");
var cacheMinutesOption = new Option<int>("--minutes", () => 0) { Description = "Duration in minutes when using --archidekt-cache." };
var cacheSecondsOption = new Option<int>("--seconds", () => 0) { Description = "Duration in seconds when using --archidekt-cache." };
rootCommand.AddOption(cacheFlagOption);
rootCommand.AddOption(cacheMinutesOption);
rootCommand.AddOption(cacheSecondsOption);
rootCommand.SetHandler(async (bool runCache, int minutes, int seconds) =>
{
    if (!runCache)
    {
        Console.WriteLine("DeckFlow CLI. Use --help to see available commands or specify --archidekt-cache.");
        Environment.ExitCode = 0;
        return;
    }

    var totalSeconds = CommandRunners.GetCacheDurationSeconds(minutes, seconds);
    Environment.ExitCode = await CommandRunners.RunArchidektCacheAsync(totalSeconds, Log.Logger);
}, cacheFlagOption, cacheMinutesOption, cacheSecondsOption);

rootCommand.AddCommand(compareCommand);
rootCommand.AddCommand(probeCommand);
rootCommand.AddCommand(exportMoxfieldCommand);
rootCommand.AddCommand(archidektCategoriesCommand);
rootCommand.AddCommand(archidektCategoryCardsCommand);
rootCommand.AddCommand(archidektHarvestRecentCommand);
rootCommand.AddCommand(archidektCacheCommand);
rootCommand.AddCommand(categoryFindCommand);
rootCommand.AddCommand(cardLookupCommand);
rootCommand.AddCommand(scryfallProbeCommand);
rootCommand.AddCommand(contentSourceAddCommand);
rootCommand.AddCommand(contentSourceSetEnabledCommand);
rootCommand.AddCommand(harvestCommand);
rootCommand.AddCommand(blockVideoCommand);
rootCommand.AddCommand(unblockVideoCommand);
rootCommand.AddCommand(listBlockedCommand);
rootCommand.AddCommand(corpusResetCommand);
rootCommand.AddCommand(distillCommand);
rootCommand.AddCommand(contentIndexExportCommand);

probeCommand.SetHandler((string url, FileInfo? output) =>
{
    Environment.ExitCode = CommandRunners.RunProbeAsync(url, output).GetAwaiter().GetResult();
}, probeUrlOption, probeOutOption);

exportMoxfieldCommand.SetHandler((string url, FileInfo output) =>
{
    Environment.ExitCode = CommandRunners.RunExportMoxfieldAsync(url, output).GetAwaiter().GetResult();
}, exportMoxfieldUrlOption, exportMoxfieldOutOption);

archidektCategoriesCommand.SetHandler((string url, FileInfo? output) =>
{
    Environment.ExitCode = CommandRunners.RunArchidektCategoriesAsync(url, output).GetAwaiter().GetResult();
}, archidektCategoriesUrlOption, archidektCategoriesOutOption);

archidektCategoryCardsCommand.SetHandler((string url, string category, FileInfo? output) =>
{
    Environment.ExitCode = CommandRunners.RunArchidektCategoryCardsAsync(url, category, output).GetAwaiter().GetResult();
}, archidektCategoryCardsUrlOption, archidektCategoryCardsCategoryOption, archidektCategoryCardsOutOption);

archidektHarvestRecentCommand.SetHandler((int count, FileInfo output) =>
{
    Environment.ExitCode = CommandRunners.RunArchidektHarvestRecentAsync(count, output).GetAwaiter().GetResult();
}, archidektHarvestRecentCountOption, archidektHarvestRecentOutOption);

archidektCacheCommand.SetHandler((int seconds, int minutes) =>
{
    var totalSeconds = CommandRunners.GetCacheDurationSeconds(minutes, seconds);
    Environment.ExitCode = CommandRunners.RunArchidektCacheAsync(totalSeconds, Log.Logger).GetAwaiter().GetResult();
}, archidektCacheSecondsOption, archidektCacheMinutesOption);

categoryFindCommand.SetHandler((string cardName, int runSeconds, int timeoutSeconds) =>
{
    Environment.ExitCode = CommandRunners.RunCategoryFindAsync(cardName, runSeconds, timeoutSeconds).GetAwaiter().GetResult();
}, categoryFindCardOption, categoryFindSecondsOption, categoryFindTimeoutOption);

cardLookupCommand.SetHandler((string cardName) =>
{
    Environment.ExitCode = CommandRunners.RunCardLookupAsync(cardName).GetAwaiter().GetResult();
}, cardLookupNameOption);

scryfallProbeCommand.SetHandler((string endpoint, string? cardName, int repeat) =>
{
    Environment.ExitCode = CommandRunners.RunScryfallProbeAsync(endpoint, cardName, repeat).GetAwaiter().GetResult();
}, scryfallProbeEndpointOption, scryfallProbeNameOption, scryfallProbeRepeatOption);

contentSourceAddCommand.SetHandler((string url, string name, string type, FileInfo? db) =>
{
    Environment.ExitCode = CommandRunners.RunContentSourceAddAsync(url, name, type, db).GetAwaiter().GetResult();
}, contentSourceAddUrlOption, contentSourceAddNameOption, contentSourceAddTypeOption, contentSourceAddDbOption);

contentSourceSetEnabledCommand.SetHandler((long id, bool enabled, FileInfo? db) =>
{
    Environment.ExitCode = CommandRunners.RunContentSourceSetEnabledAsync(id, enabled, db, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, contentSourceSetEnabledIdOption, contentSourceSetEnabledEnabledOption, contentSourceSetEnabledDbOption);

harvestCommand.SetHandler((FileInfo? db, int limit, bool enableWhisper, string? videoIds, long? sourceId) =>
{
    Environment.ExitCode = CommandRunners.RunHarvestAsync(db, limit, enableWhisper, Log.Logger, CancellationToken.None, CommandRunners.ParseVideoIds(videoIds), sourceId).GetAwaiter().GetResult();
}, harvestDbOption, harvestLimitOption, harvestEnableWhisperOption, harvestVideoIdsOption, harvestSourceIdOption);

blockVideoCommand.SetHandler((string youtubeVideoId, string? reason, FileInfo? db) =>
{
    Environment.ExitCode = CommandRunners.RunBlockVideoAsync(db, youtubeVideoId, reason, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, blockVideoIdArgument, blockVideoReasonOption, blockVideoDbOption);

unblockVideoCommand.SetHandler((string youtubeVideoId, FileInfo? db) =>
{
    Environment.ExitCode = CommandRunners.RunUnblockVideoAsync(db, youtubeVideoId, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, unblockVideoIdArgument, unblockVideoDbOption);

listBlockedCommand.SetHandler((FileInfo? db) =>
{
    Environment.ExitCode = CommandRunners.RunListBlockedAsync(db, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, listBlockedDbOption);

corpusResetCommand.SetHandler((FileInfo? db, string? connectionString, bool dryRun) =>
{
    if (db is not null && !string.IsNullOrWhiteSpace(connectionString))
    {
        throw new ArgumentException("Specify either --db or --connection-string, not both.");
    }

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        _ = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString);
    }

    Environment.ExitCode = CommandRunners.RunCorpusResetAsync(db, connectionString, dryRun, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, corpusResetDbOption, corpusResetConnectionStringOption, corpusResetDryRunOption);

distillCommand.SetHandler((FileInfo? db, int limit, bool dryRun, string? videoIds) =>
{
    Environment.ExitCode = CommandRunners.RunDistillAsync(db, limit, dryRun, Log.Logger, CancellationToken.None, CommandRunners.ParseVideoIds(videoIds)).GetAwaiter().GetResult();
}, distillDbOption, distillLimitOption, distillDryRunOption, distillVideoIdsOption);

contentIndexExportCommand.SetHandler((FileInfo? db, FileInfo? output) =>
{
    Environment.ExitCode = CommandRunners.RunContentIndexExportAsync(db, output).GetAwaiter().GetResult();
}, contentIndexExportDbOption, contentIndexExportOutputOption);

var invokeExitCode = await rootCommand.InvokeAsync(args);
return invokeExitCode == 0 ? Environment.ExitCode : invokeExitCode;
