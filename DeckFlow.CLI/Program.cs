using System.CommandLine;
using System.IO;
using DeckFlow.Core.Content;
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
var manabaseCommand = new Command("manabase", "Fetch a public deck and run the Karsten §6 mana-base analysis.");
var manabaseArchidektUrlOption = new Option<string?>("--archidekt-url") { Description = "Public Archidekt deck URL." };
var manabaseMoxfieldUrlOption = new Option<string?>("--moxfield-url") { Description = "Public Moxfield deck URL." };
var manabaseModeOption = new Option<string>("--mode", () => "casual") { Description = "Analysis profile: casual | focused | cedh (focused keeps Casual surfaces with an 85% color bar; cedh lowers the land target)." };
var manabaseSwapPromptOption = new Option<bool>("--swap-prompt") { Description = "Also print a paste-ready LLM prompt asking for specific land swaps." };
var edhrecAveragesCommand = new Command("edhrec-averages", "Convert an EDHREC averages.csv dump into the bundled manabase-baseline data file.");
var edhrecAveragesCsvOption = new Option<string>("--csv") { Description = "Path to the extracted averages.csv dump.", IsRequired = true };
var edhrecAveragesDataFileOption = new Option<string>("--data-file", () => Path.Combine("DeckFlow.Web", "Data", "manabase-baseline", "latest.json")) { Description = "Path to the bundled manabase-baseline snapshot JSON." };
var edhrecDownloadCommand = new Command("edhrec-download", "Download EDHREC's published averages.tgz and/or data.tgz dumps.");
var edhrecDownloadDatasetOption = new Option<string>("--dataset", () => "all") { Description = "Dataset to download: all | averages | data." };
var edhrecDownloadOutOption = new Option<string>("--out", () => Path.Combine("artifacts", "edhrec")) { Description = "Output directory for downloaded archives and extracted CSVs." };
var edhrecDownloadExtractOption = new Option<bool>("--extract", () => true) { Description = "Extract the downloaded .tgz archive after downloading." };
var edhrecDownloadOverwriteOption = new Option<bool>("--overwrite") { Description = "Overwrite existing archives and extracted files." };
var cedhLandCalibrateCommand = new Command("cedh-land-calibrate", "Replay cached cEDH decks against the old and new land targets.");
var cedhLandCalibrateDataOption = new Option<string>("--data", () => "_calib") { Description = "Directory containing decks_all.json and cards_full.json." };
var cedhLandCalibrateBaselineOption = new Option<string>("--baseline", () => Path.Combine("DeckFlow.Web", "Data", "cedh-land-baseline", "latest.json")) { Description = "Path to the committed cEDH baseline snapshot JSON." };
var cedhLandCalibrateOutOption = new Option<string?>("--out") { Description = "Markdown report path. Defaults to <data>/cedh-calibration.md." };
var cedhLandBaselineCommand = new Command("cedh-land-baseline", "Build the monthly cEDH land baseline from cached calibration data.");
var cedhLandBaselineDataOption = new Option<string>("--data") { Description = "Directory containing decks_all.json and cards_full.json.", IsRequired = true };
var cedhLandBaselineOutOption = new Option<string>("--out", () => Path.Combine("DeckFlow.Web", "Data", "cedh-land-baseline")) { Description = "Output directory for the monthly markdown/JSON artifacts." };
var cedhLandBaselineMonthOption = new Option<string>("--month") { Description = "Month label in YYYY-MM format.", IsRequired = true };
var cedhLandBaselineThresholdsOption = new Option<string>("--thresholds", () => Path.Combine("scripts", "cedh-baseline", "drift-thresholds.json")) { Description = "Path to the committed drift-threshold configuration." };
var roleFloorResearchCommand = new Command("role-floor-research", "Reconstruct commander corpora, classify roles oracle-only, and measure role-floor divergence. The connection string may be supplied by --connection-string or by the DECKFLOW_ROLE_FLOOR_CONNECTION_STRING environment variable. Exit codes: 0 = success with at least one qualifying commander and artifacts written; 1 = bad arguments, taxonomy drift, or unhandled exception; 2 = ran successfully but zero commanders cleared the minimum deck count, so no artifact was written.");
// Why: name DECKFLOW_ROLE_FLOOR_CONNECTION_STRING here deliberately so --help tells operators how to keep the credential off the command line (plan 02-04 D-07); this is a help-text mention only, and RoleFloorResearchCommandRunner remains the single read site.
var roleFloorResearchConnectionStringOption = new Option<string?>("--connection-string") { Description = "Postgres connection string for the category-knowledge corpus. Optional on the command line; if omitted the value is read from the DECKFLOW_ROLE_FLOOR_CONNECTION_STRING environment variable. Prefer the environment variable because command-line arguments are visible in the process list. Supplying the flag overrides the environment variable.", IsRequired = false };
var roleFloorResearchMinDecksOption = new Option<int>("--min-decks", () => 40) { Description = "Minimum deduped deck count required for a commander to qualify." };
var roleFloorResearchModeOption = new Option<string>("--mode", () => "cedh") { Description = "casual | focused | cedh -- resolved via CutLabRoleAssigner.ResolveMode" };
var roleFloorResearchCardsCacheOption = new Option<string>("--cards-cache", () => Path.Combine("_role-floor-research", "cards_full.json")) { Description = "Path to the resumable Scryfall cards cache JSON." };
var roleFloorResearchLimitOption = new Option<int?>("--limit") { Description = "Cap the number of commanders loaded. Omit for the full corpus. Intended for cheap smoke runs that prove the exit-2 guard without paying the full membership load; a limited run is labelled in the artifacts and is NOT evidence." };
// Why: the Python fetcher writes the EDHREC corpus to --outdir, while the CLI reads it from
// --edhrec-data; that deliberate asymmetry matches the existing scripts/cedh-baseline pipeline,
// where fetch.py writes --outdir and the CLI reads --data.
var roleFloorResearchEdhrecDataOption = new Option<string?>("--edhrec-data") { Description = "Directory produced by scripts/edhrec-brackets/fetch.py --outdir (default _edhrec-brackets) containing manifest.json and cells/. Optional; when omitted the run uses Postgres alone and the artifacts state that no EDHREC corpus was supplied. When supplied but missing or unreadable the run fails with exit code 1." };
// Why: this workstream was renamed from the older "cutlab role floors" slug to cycle21-cut-lab and the phase was
// renumbered from 01 to 02 on 2026-07-26, so the old defaults wrote into a folder that no longer
// exists in this workstream.
var roleFloorResearchOutOption = new Option<string>("--out", () => Path.Combine(".planning", "workstreams", "cycle21-cut-lab", "phases", "02-role-floor-divergence-research", "RESEARCH-FINDINGS.md")) { Description = "Markdown findings output path." };
var roleFloorResearchOutJsonOption = new Option<string>("--out-json", () => Path.Combine(".planning", "workstreams", "cycle21-cut-lab", "phases", "02-role-floor-divergence-research", "RESEARCH-FINDINGS.json")) { Description = "Machine-readable findings output path." };
var edhrecRoleGridCommand = new Command("edhrec-role-grid", "Build the EDHREC bulk expected-role-count grid from local CSVs. --edhrec-csv and --averages are required absolute paths into the main worktree (/mnt/c/users/chrislunt/source/personal/deckflow/artifacts/edhrec/...) because artifacts/edhrec/ does not exist in this worktree. Exit codes: 0 = artifacts written; 1 = bad arguments, taxonomy drift, missing input, or unhandled exception; 2 = ran successfully but zero commanders survived the denominator gate and --min-decks, so no artifact was written.");
var edhrecRoleGridCsvOption = new Option<string>("--edhrec-csv") { Description = "Absolute path to edhrec.csv in the main worktree archive.", IsRequired = true };
var edhrecRoleGridAveragesOption = new Option<string>("--averages") { Description = "Absolute path to averages.csv in the main worktree archive.", IsRequired = true };
var edhrecRoleGridCardsCacheOption = new Option<string>("--cards-cache", () => Path.Combine("_role-floor-research", "cards_full.json")) { Description = "Path to the shared Scryfall cards cache JSON." };
var edhrecRoleGridModeOption = new Option<string>("--mode", () => "cedh") { Description = "casual | focused | cedh -- resolved via CutLabRoleAssigner.ResolveMode" };
var edhrecRoleGridMinDecksOption = new Option<int>("--min-decks", () => 0) { Description = "Minimum number_decks denominator required for a commander to survive. 0 means all." };
var edhrecRoleGridOutOption = new Option<string>("--out", () => Path.Combine(".planning", "workstreams", "cycle21-cut-lab", "phases", "02-role-floor-divergence-research", "EDHREC-ROLE-GRID.md")) { Description = "Markdown output path." };
var edhrecRoleGridOutJsonOption = new Option<string>("--out-json", () => Path.Combine(".planning", "workstreams", "cycle21-cut-lab", "phases", "02-role-floor-divergence-research", "EDHREC-ROLE-GRID.json")) { Description = "Machine-readable output path." };
var edhrecRoleGridDryRunOption = new Option<bool>("--dry-run") { Description = "Resolve paths, validate taxonomy, print the plan, and exit without reading the full archive or calling Scryfall." };
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
var contentIndexExportOutputOption = new Option<FileInfo?>("--output", () => new FileInfo(ContentKbPaths.SeedRelativePath)) { Description = "Path to the JSON seed file. Defaults to content-kb/seed/index-seed.json." };
var contentKbCheckCommand = new Command("content-kb-check", "Checks content_site_index rows against local artifact files and reports orphans (read-only; exits 1 when a published orphan exists).");
var contentKbCheckDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var contentKbCheckArtifactRootOption = new Option<DirectoryInfo?>("--artifact-root") { Description = "Artifact directory: either the data-root parent of content-kb/ or the content-kb directory itself (both are normalized). Defaults to the MTG_DATA_DIR/content-kb resolution." };

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
manabaseCommand.AddOption(manabaseArchidektUrlOption);
manabaseCommand.AddOption(manabaseMoxfieldUrlOption);
manabaseCommand.AddOption(manabaseModeOption);
manabaseCommand.AddOption(manabaseSwapPromptOption);
edhrecAveragesCommand.AddOption(edhrecAveragesCsvOption);
edhrecAveragesCommand.AddOption(edhrecAveragesDataFileOption);
edhrecDownloadCommand.AddOption(edhrecDownloadDatasetOption);
edhrecDownloadCommand.AddOption(edhrecDownloadOutOption);
edhrecDownloadCommand.AddOption(edhrecDownloadExtractOption);
edhrecDownloadCommand.AddOption(edhrecDownloadOverwriteOption);
cedhLandCalibrateCommand.AddOption(cedhLandCalibrateDataOption);
cedhLandCalibrateCommand.AddOption(cedhLandCalibrateBaselineOption);
cedhLandCalibrateCommand.AddOption(cedhLandCalibrateOutOption);
cedhLandBaselineCommand.AddOption(cedhLandBaselineDataOption);
cedhLandBaselineCommand.AddOption(cedhLandBaselineOutOption);
cedhLandBaselineCommand.AddOption(cedhLandBaselineMonthOption);
cedhLandBaselineCommand.AddOption(cedhLandBaselineThresholdsOption);
roleFloorResearchCommand.AddOption(roleFloorResearchConnectionStringOption);
roleFloorResearchCommand.AddOption(roleFloorResearchMinDecksOption);
roleFloorResearchCommand.AddOption(roleFloorResearchModeOption);
roleFloorResearchCommand.AddOption(roleFloorResearchCardsCacheOption);
roleFloorResearchCommand.AddOption(roleFloorResearchEdhrecDataOption);
roleFloorResearchCommand.AddOption(roleFloorResearchOutOption);
roleFloorResearchCommand.AddOption(roleFloorResearchOutJsonOption);
roleFloorResearchCommand.AddOption(roleFloorResearchLimitOption);
edhrecRoleGridCommand.AddOption(edhrecRoleGridCsvOption);
edhrecRoleGridCommand.AddOption(edhrecRoleGridAveragesOption);
edhrecRoleGridCommand.AddOption(edhrecRoleGridCardsCacheOption);
edhrecRoleGridCommand.AddOption(edhrecRoleGridModeOption);
edhrecRoleGridCommand.AddOption(edhrecRoleGridMinDecksOption);
edhrecRoleGridCommand.AddOption(edhrecRoleGridOutOption);
edhrecRoleGridCommand.AddOption(edhrecRoleGridOutJsonOption);
edhrecRoleGridCommand.AddOption(edhrecRoleGridDryRunOption);
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
contentKbCheckCommand.AddOption(contentKbCheckDbOption);
contentKbCheckCommand.AddOption(contentKbCheckArtifactRootOption);

compareCommand.SetHandler(context =>
{
    var parseResult = context.ParseResult;
    Environment.ExitCode = DeckCommandRunners.RunCompareAsync(
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

    var totalSeconds = DeckCommandRunners.GetCacheDurationSeconds(minutes, seconds);
    Environment.ExitCode = await DeckCommandRunners.RunArchidektCacheAsync(totalSeconds, Log.Logger);
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
rootCommand.AddCommand(manabaseCommand);
rootCommand.AddCommand(edhrecAveragesCommand);
rootCommand.AddCommand(edhrecDownloadCommand);
rootCommand.AddCommand(cedhLandCalibrateCommand);
rootCommand.AddCommand(cedhLandBaselineCommand);
rootCommand.AddCommand(roleFloorResearchCommand);
rootCommand.AddCommand(edhrecRoleGridCommand);
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
rootCommand.AddCommand(contentKbCheckCommand);

probeCommand.SetHandler((string url, FileInfo? output) =>
{
    Environment.ExitCode = DeckCommandRunners.RunProbeAsync(url, output).GetAwaiter().GetResult();
}, probeUrlOption, probeOutOption);

exportMoxfieldCommand.SetHandler((string url, FileInfo output) =>
{
    Environment.ExitCode = DeckCommandRunners.RunExportMoxfieldAsync(url, output).GetAwaiter().GetResult();
}, exportMoxfieldUrlOption, exportMoxfieldOutOption);

archidektCategoriesCommand.SetHandler((string url, FileInfo? output) =>
{
    Environment.ExitCode = DeckCommandRunners.RunArchidektCategoriesAsync(url, output).GetAwaiter().GetResult();
}, archidektCategoriesUrlOption, archidektCategoriesOutOption);

archidektCategoryCardsCommand.SetHandler((string url, string category, FileInfo? output) =>
{
    Environment.ExitCode = DeckCommandRunners.RunArchidektCategoryCardsAsync(url, category, output).GetAwaiter().GetResult();
}, archidektCategoryCardsUrlOption, archidektCategoryCardsCategoryOption, archidektCategoryCardsOutOption);

archidektHarvestRecentCommand.SetHandler((int count, FileInfo output) =>
{
    Environment.ExitCode = DeckCommandRunners.RunArchidektHarvestRecentAsync(count, output).GetAwaiter().GetResult();
}, archidektHarvestRecentCountOption, archidektHarvestRecentOutOption);

archidektCacheCommand.SetHandler((int seconds, int minutes) =>
{
    var totalSeconds = DeckCommandRunners.GetCacheDurationSeconds(minutes, seconds);
    Environment.ExitCode = DeckCommandRunners.RunArchidektCacheAsync(totalSeconds, Log.Logger).GetAwaiter().GetResult();
}, archidektCacheSecondsOption, archidektCacheMinutesOption);

categoryFindCommand.SetHandler((string cardName, int runSeconds, int timeoutSeconds) =>
{
    Environment.ExitCode = DeckCommandRunners.RunCategoryFindAsync(cardName, runSeconds, timeoutSeconds).GetAwaiter().GetResult();
}, categoryFindCardOption, categoryFindSecondsOption, categoryFindTimeoutOption);

cardLookupCommand.SetHandler((string cardName) =>
{
    Environment.ExitCode = DeckCommandRunners.RunCardLookupAsync(cardName).GetAwaiter().GetResult();
}, cardLookupNameOption);

manabaseCommand.SetHandler((string? archidektUrl, string? moxfieldUrl, string mode, bool swapPrompt) =>
{
    Environment.ExitCode = ManabaseCommandRunner.RunAsync(archidektUrl, moxfieldUrl, mode, swapPrompt).GetAwaiter().GetResult();
}, manabaseArchidektUrlOption, manabaseMoxfieldUrlOption, manabaseModeOption, manabaseSwapPromptOption);

edhrecAveragesCommand.SetHandler((string csvPath, string dataFilePath) =>
{
    Environment.ExitCode = EdhrecAveragesCommandRunner.RunEdhrecAveragesAsync(csvPath, dataFilePath).GetAwaiter().GetResult();
}, edhrecAveragesCsvOption, edhrecAveragesDataFileOption);

edhrecDownloadCommand.SetHandler((string dataset, string outputDirectory, bool extract, bool overwrite) =>
{
    Environment.ExitCode = EdhrecDataDownloadCommandRunner.RunAsync(outputDirectory, dataset, extract, overwrite).GetAwaiter().GetResult();
}, edhrecDownloadDatasetOption, edhrecDownloadOutOption, edhrecDownloadExtractOption, edhrecDownloadOverwriteOption);

cedhLandCalibrateCommand.SetHandler((string dataDirectory, string baselinePath, string? outputPath) =>
{
    Environment.ExitCode = CedhCalibrateCommandRunner.RunAsync(dataDirectory, baselinePath, outputPath).GetAwaiter().GetResult();
}, cedhLandCalibrateDataOption, cedhLandCalibrateBaselineOption, cedhLandCalibrateOutOption);

cedhLandBaselineCommand.SetHandler((string dataDirectory, string outputDirectory, string month, string thresholdsPath) =>
{
    Environment.ExitCode = CedhBaselineCommandRunner.RunAsync(dataDirectory, outputDirectory, month, thresholdsPath).GetAwaiter().GetResult();
}, cedhLandBaselineDataOption, cedhLandBaselineOutOption, cedhLandBaselineMonthOption, cedhLandBaselineThresholdsOption);

roleFloorResearchCommand.SetHandler((string? connectionString, int minDecks, string mode, string cardsCachePath, string? edhrecDataPath, string outputPath, string outputJsonPath, int? commanderLimit) =>
{
    Environment.ExitCode = RoleFloorResearchCommandRunner.RunAsync(connectionString, minDecks, mode, cardsCachePath, outputPath, outputJsonPath, edhrecDataPath, commanderLimit).GetAwaiter().GetResult();
}, roleFloorResearchConnectionStringOption, roleFloorResearchMinDecksOption, roleFloorResearchModeOption, roleFloorResearchCardsCacheOption, roleFloorResearchEdhrecDataOption, roleFloorResearchOutOption, roleFloorResearchOutJsonOption, roleFloorResearchLimitOption);

edhrecRoleGridCommand.SetHandler((string edhrecCsvPath, string averagesCsvPath, string cardsCachePath, string mode, int minDecks, string outputPath, string outputJsonPath, bool dryRun) =>
{
    Environment.ExitCode = EdhrecRoleGridCommandRunner.RunAsync(edhrecCsvPath, averagesCsvPath, cardsCachePath, mode, minDecks, outputPath, outputJsonPath, dryRun).GetAwaiter().GetResult();
}, edhrecRoleGridCsvOption, edhrecRoleGridAveragesOption, edhrecRoleGridCardsCacheOption, edhrecRoleGridModeOption, edhrecRoleGridMinDecksOption, edhrecRoleGridOutOption, edhrecRoleGridOutJsonOption, edhrecRoleGridDryRunOption);

scryfallProbeCommand.SetHandler((string endpoint, string? cardName, int repeat) =>
{
    Environment.ExitCode = DeckCommandRunners.RunScryfallProbeAsync(endpoint, cardName, repeat).GetAwaiter().GetResult();
}, scryfallProbeEndpointOption, scryfallProbeNameOption, scryfallProbeRepeatOption);

contentSourceAddCommand.SetHandler((string url, string name, string type, FileInfo? db) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunContentSourceAddAsync(url, name, type, db).GetAwaiter().GetResult();
}, contentSourceAddUrlOption, contentSourceAddNameOption, contentSourceAddTypeOption, contentSourceAddDbOption);

contentSourceSetEnabledCommand.SetHandler((long id, bool enabled, FileInfo? db) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunContentSourceSetEnabledAsync(id, enabled, db, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, contentSourceSetEnabledIdOption, contentSourceSetEnabledEnabledOption, contentSourceSetEnabledDbOption);

harvestCommand.SetHandler((FileInfo? db, int limit, bool enableWhisper, string? videoIds, long? sourceId) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunHarvestAsync(db, limit, enableWhisper, Log.Logger, CancellationToken.None, ContentKbCommandRunners.ParseVideoIds(videoIds), sourceId).GetAwaiter().GetResult();
}, harvestDbOption, harvestLimitOption, harvestEnableWhisperOption, harvestVideoIdsOption, harvestSourceIdOption);

blockVideoCommand.SetHandler((string youtubeVideoId, string? reason, FileInfo? db) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunBlockVideoAsync(db, youtubeVideoId, reason, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, blockVideoIdArgument, blockVideoReasonOption, blockVideoDbOption);

unblockVideoCommand.SetHandler((string youtubeVideoId, FileInfo? db) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunUnblockVideoAsync(db, youtubeVideoId, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, unblockVideoIdArgument, unblockVideoDbOption);

listBlockedCommand.SetHandler((FileInfo? db) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunListBlockedAsync(db, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
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

    Environment.ExitCode = ContentKbCommandRunners.RunCorpusResetAsync(db, connectionString, dryRun, Log.Logger, CancellationToken.None).GetAwaiter().GetResult();
}, corpusResetDbOption, corpusResetConnectionStringOption, corpusResetDryRunOption);

distillCommand.SetHandler((FileInfo? db, int limit, bool dryRun, string? videoIds) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunDistillAsync(db, limit, dryRun, Log.Logger, CancellationToken.None, ContentKbCommandRunners.ParseVideoIds(videoIds)).GetAwaiter().GetResult();
}, distillDbOption, distillLimitOption, distillDryRunOption, distillVideoIdsOption);

contentIndexExportCommand.SetHandler((FileInfo? db, FileInfo? output) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunContentIndexExportAsync(db, output).GetAwaiter().GetResult();
}, contentIndexExportDbOption, contentIndexExportOutputOption);

contentKbCheckCommand.SetHandler((FileInfo? db, DirectoryInfo? artifactRoot) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunContentKbCheckAsync(db, artifactRoot).GetAwaiter().GetResult();
}, contentKbCheckDbOption, contentKbCheckArtifactRootOption);

var invokeExitCode = await rootCommand.InvokeAsync(args);
return invokeExitCode == 0 ? Environment.ExitCode : invokeExitCode;
