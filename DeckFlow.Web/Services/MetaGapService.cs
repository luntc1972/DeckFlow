using System.Text;
using System.Text.Json;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.Http;
using DeckFlow.Web.Services.Packets;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using DeckFlow.Web.Services.Scryfall;
using Polly;
using Polly.Registry;
using System.Net;

namespace DeckFlow.Web.Services;

/// <summary>
/// Builds the cEDH meta-gap prompt packet using edhtop16 reference decks.
/// </summary>
public interface IMetaGapService
{
    /// <summary>
    /// Builds the cEDH meta-gap packet for the supplied workflow request.
    /// </summary>
    /// <param name="request">cEDH meta-gap workflow request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MetaGapResult> BuildAsync(MetaGapRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to compute the packet-session cache key for the supplied cEDH meta-gap request.
    /// </summary>
    /// <param name="request">cEDH meta-gap workflow request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> TryComputeCacheKeyAsync(MetaGapRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Returns the results of a cEDH meta-gap packet build.
/// </summary>
public sealed record MetaGapResult(
    string? InputSummary,
    string? ResolvedCommanderName,
    IReadOnlyList<EdhTop16Entry> FetchedEntries,
    string? PromptText,
    string? SchemaJson,
    MetaGapResponse? AnalysisResponse,
    string? RequestContextText = null,
    string? DecklistText = null);

/// <summary>
/// Fetches top edhtop16 reference decks for the user's commander, hydrates them via Scryfall and Commander Spellbook, derives the cEDH meta-gap context (core convergence, missing staples, potential cuts), and composes the JSON-bound meta-gap prompt artifacts saved to the session zip.
/// </summary>
public sealed class MetaGapService : IMetaGapService
{
    private const int FetchCount = 48;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly string MetaGapSchemaJson = BuildSchemaJson();

    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly IEdhTop16Client _edhTop16Client;
    private readonly ICommanderSpellbookService _commanderSpellbookService;
    private readonly IScryfallCardResolver _scryfallCardResolver;
    private readonly ScryfallReferenceResolver _scryfallReferenceResolver;
    private readonly MetaGapPromptVariantRegistry _metaGapPromptRegistry;
    private readonly PacketSessionCache _packetCache;

    internal MetaGapService(
        IScryfallCardResolver scryfallCardResolver,
        IDeckEntryLoader deckEntryLoader,
        IEdhTop16Client edhTop16Client,
        ICommanderSpellbookService commanderSpellbookService,
        MetaGapPromptVariantRegistry metaGapPromptRegistry,
        PacketSessionCache packetCache)
    {
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(edhTop16Client);
        ArgumentNullException.ThrowIfNull(commanderSpellbookService);
        ArgumentNullException.ThrowIfNull(metaGapPromptRegistry);
        ArgumentNullException.ThrowIfNull(packetCache);
        _scryfallCardResolver = scryfallCardResolver;
        _scryfallReferenceResolver = new ScryfallReferenceResolver(scryfallCardResolver);
        _deckEntryLoader = deckEntryLoader;
        _edhTop16Client = edhTop16Client;
        _commanderSpellbookService = commanderSpellbookService;
        _metaGapPromptRegistry = metaGapPromptRegistry;
        _packetCache = packetCache;
    }

    /// <summary>
    /// SINGLE source of truth for the meta-gap cache-input bag. Called from BOTH
    /// <see cref="TryComputeCacheKeyAsync"/> (read side) AND the line-196 cache-write site
    /// in <see cref="BuildAsync"/> (write side). Both sides pass the SAME loadedDeck +
    /// resolvedCommanderName values — the commander resolution at lines 132-134 is deterministic
    /// from request + loadedDeck and never mutated downstream.
    /// </summary>
    private static MetaGapCacheInputs BuildMetaGapCacheInputs(
        MetaGapRequest request,
        LoadedDeck loadedDeck,
        string resolvedCommanderName)
    {
        return new MetaGapCacheInputs(
            CommanderName: resolvedCommanderName,
            NormalizedDeckSource: BuildCanonicalDeckSourceText(loadedDeck),
            TimePeriod: request.TimePeriod,
            SortBy: request.SortBy,
            MinEventSize: request.MinEventSize,
            MaxStanding: request.MaxStanding,
            SelectedReferenceIndexes: (request.SelectedReferenceIndexes ?? new List<int>())
                .OrderBy(static i => i)
                .ToArray(),
            TargetAiPlatformKey: request.TargetAiPlatform);
    }

    private static string BuildCanonicalDeckSourceText(LoadedDeck loadedDeck)
    {
        ArgumentNullException.ThrowIfNull(loadedDeck);
        var builder = new StringBuilder();
        foreach (var entry in loadedDeck.AllEntries
            .OrderBy(e => e.Board ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.SetCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.CollectorNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(entry.Board ?? string.Empty).Append('|')
                   .Append(entry.Quantity).Append('|')
                   .Append(entry.Name ?? string.Empty).Append('|')
                   .Append(entry.SetCode ?? string.Empty).Append('|')
                   .Append(entry.CollectorNumber ?? string.Empty).Append('\n');
        }
        return builder.ToString();
    }

    /// <summary>
    /// Composes the D-01 cache-input field bag for the given meta-gap request and returns the
    /// canonical PacketSessionCache key. Re-runs the same private LoadDeckAsync path BuildAsync
    /// uses and resolves the commander using the same fallback BuildAsync uses at lines 132-134.
    /// Returns null on load failure or empty deck (controller falls through to BuildAsync silently
    /// per D-11). Calls <see cref="BuildMetaGapCacheInputs"/> for write↔read parity by code locality.
    /// </summary>
    public async Task<string?> TryComputeCacheKeyAsync(MetaGapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DeckSource))
        {
            return null;
        }

        LoadedDeck loadedDeck;
        try
        {
            loadedDeck = await LoadDeckAsync(request.DeckSource, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DeckParseException or HttpRequestException)
        {
            return null;
        }

        // Mirror BuildAsync lines 132-134 exactly.
        var resolvedCommanderName = string.IsNullOrWhiteSpace(request.CommanderName)
            ? loadedDeck.CommanderName
            : request.CommanderName.Trim();

        if (string.IsNullOrWhiteSpace(resolvedCommanderName))
        {
            return null;
        }

        var inputs = BuildMetaGapCacheInputs(request, loadedDeck, resolvedCommanderName);
        return PacketSessionCache.ComputeKey(inputs);
    }

    /// <inheritdoc/>
    public async Task<MetaGapResult> BuildAsync(MetaGapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        MetaGapResponse? analysisResponse = null;
        if (request.WorkflowStep >= 3 && !string.IsNullOrWhiteSpace(request.MetaGapResponseJson))
        {
            analysisResponse = ParseResponse(request.MetaGapResponseJson);
            if (string.IsNullOrWhiteSpace(request.DeckSource) && string.IsNullOrWhiteSpace(request.CommanderName))
            {
                return new MetaGapResult(null, null, Array.Empty<EdhTop16Entry>(), null, MetaGapSchemaJson, analysisResponse, BuildRequestContextText(request));
            }
        }

        if (string.IsNullOrWhiteSpace(request.DeckSource))
        {
            throw new InvalidOperationException("Paste your deck URL or deck text before fetching EDH Top 16 reference decks.");
        }

        var loadedDeck = await LoadDeckAsync(request.DeckSource, cancellationToken).ConfigureAwait(false);
        var resolvedCommanderName = string.IsNullOrWhiteSpace(request.CommanderName)
            ? loadedDeck.CommanderName
            : request.CommanderName.Trim();

        if (string.IsNullOrWhiteSpace(resolvedCommanderName))
        {
            throw new InvalidOperationException("Could not determine a commander from the submitted deck. Enter the commander name explicitly and try again.");
        }

        var fetchedEntries = TryUseFetchedEntriesOverride(request);
        if (fetchedEntries is null)
        {
            fetchedEntries = OrderEntries(
                await _edhTop16Client.SearchCommanderEntriesAsync(
                    resolvedCommanderName,
                    request.TimePeriod,
                    request.SortBy,
                    request.MinEventSize,
                    request.MaxStanding,
                    FetchCount,
                    cancellationToken).ConfigureAwait(false),
                request.SortBy);
        }

        if (fetchedEntries.Count == 0)
        {
            throw new InvalidOperationException(
                $"No EDH Top 16 decks matched your filters for {resolvedCommanderName}. Try a longer time period, a smaller minimum event size, or a looser finish cutoff.");
        }

        var inputSummary = BuildInputSummary(loadedDeck, resolvedCommanderName, request, fetchedEntries);
        var schemaJson = MetaGapSchemaJson;
        // Skip the Step-2 prompt rebuild once we already hold a parsed analysis response
        // (Step 3 render of a restored session): the prompt is not needed to display the
        // analysis, and rebuilding it would require a reference-deck selection that the
        // restored request may not carry, failing the render the user just asked for.
        // Carry the previously generated prompt through the request so the Step-2 panel and
        // the re-download zip still expose it instead of dropping it on the Step-3 render.
        string? promptText = analysisResponse is not null && !string.IsNullOrWhiteSpace(request.MetaGapPromptText)
            ? request.MetaGapPromptText
            : null;
        if (request.WorkflowStep >= 2 && analysisResponse is null)
        {
            var selectedEntries = ResolveSelectedEntries(request.SelectedReferenceIndexes, fetchedEntries);
            var oracleNameMap = await ResolveOracleNameMapAsync(loadedDeck.PlayableEntries, selectedEntries, cancellationToken).ConfigureAwait(false);
            var normalizedMyDeckEntries = NormalizeDeckEntriesForPromptAndCombos(loadedDeck.PlayableEntries, oracleNameMap);
            var normalizedReferenceDecks = selectedEntries
                .Select(entry => BuildReferenceDeckEntries(resolvedCommanderName, entry, oracleNameMap))
                .ToList();

            var myDeckComboTask = _commanderSpellbookService.FindCombosAsync(normalizedMyDeckEntries, cancellationToken);
            var referenceComboTasks = selectedEntries
                .Select((entry, index) => _commanderSpellbookService.FindCombosAsync(
                    normalizedReferenceDecks[index],
                    cancellationToken))
                .ToList();

            await Task.WhenAll(referenceComboTasks.Prepend(myDeckComboTask)).ConfigureAwait(false);

            promptText = BuildPrompt(
                resolvedCommanderName,
                normalizedMyDeckEntries,
                myDeckComboTask.Result,
                selectedEntries,
                referenceComboTasks.Select(task => task.Result).ToList(),
                oracleNameMap,
                schemaJson,
                request.TargetAiPlatform);
        }

        var result = new MetaGapResult(
            inputSummary,
            resolvedCommanderName,
            fetchedEntries,
            promptText,
            schemaJson,
            analysisResponse,
            BuildRequestContextText(request),
            DecklistText: BuildCanonicalDecklistText(loadedDeck.AllEntries));

        // Phase 999.3 cache write. Shared BuildMetaGapCacheInputs helper (called from BOTH here AND
        // from TryComputeCacheKeyAsync) guarantees write↔read parity.
        // `loadedDeck` and `resolvedCommanderName` are already in scope at line 196 (assigned at lines 131-134).
        var cacheInputs = BuildMetaGapCacheInputs(request, loadedDeck, resolvedCommanderName);
        var cacheKey = PacketSessionCache.ComputeKey(cacheInputs);
        _packetCache.Set(cacheKey, result, PacketSizeEstimator.EstimateSizeBytes(result));

        return result;
    }

    /// <summary>
    /// Canonical Moxfield-flavored decklist text used as the zip-stored deck
    /// artifact so re-upload can restore <see cref="MetaGapRequest.DeckSource"/>.
    /// Emits Commander, Mainboard, and Possible Includes sections so optional
    /// (maybeboard/sideboard) entries the parser accepted are preserved across
    /// the round-trip.
    /// </summary>
    private static string BuildCanonicalDecklistText(IReadOnlyList<DeckEntry> allEntries)
    {
        var builder = new StringBuilder();
        var commander = allEntries
            .FirstOrDefault(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase));
        if (commander is not null)
        {
            builder.AppendLine("Commander");
            builder.AppendLine($"{commander.Quantity} {commander.Name}");
            builder.AppendLine();
        }

        builder.AppendLine("Mainboard");
        foreach (var entry in allEntries
                     .Where(entry =>
                         !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{entry.Quantity} {entry.Name}");
        }

        var optional = allEntries
            .Where(entry =>
                string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (optional.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Possible Includes");
            foreach (var entry in optional)
            {
                builder.AppendLine($"{entry.Quantity} {entry.Name}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Plain-text scalar key/value envelope round-tripped through the cEDH meta-gap zip.
    /// Mirrors <see cref="DeckAnalysisPacketService"/>'s BuildRequestContextText for Packets.
    /// </summary>
    internal static string BuildRequestContextText(MetaGapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder();
        builder.AppendLine($"workflow_step: {request.WorkflowStep}");
        PacketTextAssembler.AppendKeyValueLine(builder, "commander", request.CommanderName, string.Empty, JsonTextFormatterService.NormalizeSingleLine);
        PacketTextAssembler.AppendKeyValueLine(builder, "target_ai_platform", request.TargetAiPlatform, "ChatGPT", JsonTextFormatterService.NormalizeSingleLine);
        builder.AppendLine($"time_period: {request.TimePeriod}");
        builder.AppendLine($"sort_by: {request.SortBy}");
        builder.AppendLine($"min_event_size: {request.MinEventSize}");
        if (request.MaxStanding.HasValue)
        {
            builder.AppendLine($"max_standing: {request.MaxStanding.Value}");
        }
        if (request.SelectedReferenceIndexes is { Count: > 0 } indexes)
        {
            builder.AppendLine("selected_reference_indexes:");
            foreach (var index in indexes)
            {
                builder.AppendLine($"- {index}");
            }
        }
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <summary>
    /// Phase 10-05: when the request carries a serialized FetchedEntries payload
    /// (round-tripped from a saved zip), use it instead of re-hitting edhtop16.
    /// Returns null when the override should not be applied, so the caller falls
    /// through to the live fetch. Step 1 always re-fetches regardless.
    /// </summary>
    private static List<EdhTop16Entry>? TryUseFetchedEntriesOverride(MetaGapRequest request)
    {
        if (request.WorkflowStep < 2)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(request.FetchedEntriesJson))
        {
            return null;
        }
        try
        {
            var deserialized = JsonSerializer.Deserialize<List<EdhTop16Entry>>(
                request.FetchedEntriesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return deserialized is { Count: > 0 } ? deserialized : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<EdhTop16Entry> ResolveSelectedEntries(IReadOnlyList<int> selectedIndexes, IReadOnlyList<EdhTop16Entry> fetchedEntries)
    {
        var distinctIndexes = selectedIndexes
            .Distinct()
            .Where(index => index >= 0 && index < fetchedEntries.Count)
            .ToList();

        if (distinctIndexes.Count == 0)
        {
            throw new InvalidOperationException("Select at least 1 EDH Top 16 reference deck before generating the prompt.");
        }

        if (distinctIndexes.Count > 3)
        {
            throw new InvalidOperationException("Select no more than 3 EDH Top 16 reference decks before generating the prompt.");
        }

        return distinctIndexes.Select(index => fetchedEntries[index]).ToList();
    }

    private async Task<LoadedDeck> LoadDeckAsync(string deckSource, CancellationToken cancellationToken)
    {
        List<DeckEntry> entries;
        try
        {
            var loaded = await _deckEntryLoader.LoadFromSourceAsync(
                deckSource,
                UnrecognizedPasteBehavior.PropagateParseException,
                cancellationToken).ConfigureAwait(false);
            entries = loaded.Entries;
        }
        catch (Exception exception) when (exception is InvalidOperationException or DeckParseException or HttpRequestException)
        {
            throw new InvalidOperationException($"Deck parse failed: {exception.Message}", exception);
        }

        var playableEntries = entries
            .Where(entry =>
                !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (playableEntries.Count == 0)
        {
            throw new InvalidOperationException("Deck parse failed: the submitted deck did not contain any commander or mainboard cards.");
        }

        var hasExplicitCommander = playableEntries.Any(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase));
        var commanderName = playableEntries
            .FirstOrDefault(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            ?.Name;

        if (string.IsNullOrWhiteSpace(commanderName))
        {
            commanderName = playableEntries
                .Where(entry => entry.Quantity == 1)
                .Select(entry => entry.Name)
                .FirstOrDefault();
        }

        if (!hasExplicitCommander && !string.IsNullOrWhiteSpace(commanderName))
        {
            playableEntries = DeckEntryReflagHelper.ReflagCommanderEntry(playableEntries, commanderName);
            entries = DeckEntryReflagHelper.ReflagCommanderEntry(entries, commanderName);
        }

        return new LoadedDeck(playableEntries, commanderName ?? string.Empty, entries);
    }

    // Why: the displayed reference order must honor the user's "Sort by" choice.
    // Previously every fetch was re-sorted newest-first regardless of SortBy, so
    // the TOP/NEW dropdown changed nothing visible. TOP = best finish first
    // (lowest Standing), win-rate as tiebreak; NEW = most recent tournament first.
    private static List<EdhTop16Entry> OrderEntries(IEnumerable<EdhTop16Entry> entries, CedhMetaSortBy sortBy) =>
        sortBy == CedhMetaSortBy.TOP
            ? entries
                .OrderBy(entry => entry.Standing)
                .ThenByDescending(entry => entry.WinRate)
                .ThenByDescending(entry => entry.TournamentDate ?? DateOnly.MinValue)
                .ThenBy(entry => entry.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : entries
                .OrderByDescending(entry => entry.TournamentDate ?? DateOnly.MinValue)
                .ThenBy(entry => entry.Standing)
                .ThenBy(entry => entry.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string BuildInputSummary(
        LoadedDeck loadedDeck,
        string resolvedCommanderName,
        MetaGapRequest request,
        IReadOnlyList<EdhTop16Entry> fetchedEntries)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Commander: {resolvedCommanderName}");
        builder.AppendLine($"Submitted cards: {loadedDeck.PlayableEntries.Sum(entry => entry.Quantity)}");
        builder.AppendLine($"Time period: {request.TimePeriod}");
        builder.AppendLine($"Sort by: {request.SortBy}");
        builder.AppendLine($"Minimum event size: {(request.MinEventSize > 0 ? request.MinEventSize : "All")}");
        builder.AppendLine($"Maximum standing: {(request.MaxStanding.HasValue ? request.MaxStanding.Value : "All")}");
        builder.AppendLine($"Fetched EDH Top 16 entries: {fetchedEntries.Count}");
        builder.AppendLine();
        builder.AppendLine("Reference decks:");
        for (var index = 0; index < fetchedEntries.Count; index++)
        {
            var entry = fetchedEntries[index];
            builder.Append(index + 1);
            builder.Append(". ");
            builder.Append(string.IsNullOrWhiteSpace(entry.PlayerName) ? "Unknown player" : entry.PlayerName);
            builder.Append(" | ");
            builder.Append(string.IsNullOrWhiteSpace(entry.TournamentName) ? "Unknown tournament" : entry.TournamentName);
            builder.Append(" | Standing ");
            builder.Append(entry.Standing);
            if (entry.TournamentDate.HasValue)
            {
                builder.Append(" | ");
                builder.Append(entry.TournamentDate.Value.ToString("yyyy-MM-dd"));
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    // Internal for test access — per-AI dispatcher exercised by the AI result contract tests.
    // Phase 15-02: converted from internal static to instance method; dispatches via injected MetaGapPromptVariantRegistry.
    internal string BuildPrompt(
        string commanderName,
        IReadOnlyList<DeckEntry> myDeckEntries,
        CommanderSpellbookResult? myDeckCombos,
        IReadOnlyList<EdhTop16Entry> selectedEntries,
        IReadOnlyList<CommanderSpellbookResult?> referenceDeckCombos,
        IReadOnlyDictionary<string, string> oracleNameMap,
        string schemaJson,
        string targetAiPlatform)
    {
        return _metaGapPromptRegistry.Build(
            AiPlatform.Normalize(targetAiPlatform),
            commanderName, myDeckEntries, myDeckCombos,
            selectedEntries, referenceDeckCombos, oracleNameMap, schemaJson);
    }


    private async Task<IReadOnlyDictionary<string, string>> ResolveOracleNameMapAsync(
        IReadOnlyList<DeckEntry> myDeckEntries,
        IReadOnlyList<EdhTop16Entry> selectedEntries,
        CancellationToken cancellationToken)
    {
        var uniqueNames = myDeckEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .Select(entry => entry.Name.Trim())
            .Concat(selectedEntries
                .SelectMany(entry => entry.MainDeck)
                .Where(card => !string.IsNullOrWhiteSpace(card.Name))
                .Select(card => card.Name.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ScryfallBatchResolution batchResolution;
        try
        {
            batchResolution = await _scryfallReferenceResolver.ResolveBatchAsync(
                uniqueNames,
                (name, ct) => _scryfallCardResolver.SearchFallbackCardAsync(name, ct),
                normalizeForScryfall: false,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ScryfallReferenceCollectionException exception)
        {
            // Why: preserve the ORIGINAL cEDH-meta-gap-worded message. The shared resolver's
            // collection-CALL message contains "cards/collection", which
            // UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage matches to surface
            // "...analysis packet..." copy — wrong for a meta-gap failure. Re-wrap here so this
            // path falls back to BuildSiteSpecificMessage's generic "Scryfall returned HTTP
            // {code}..." message, matching today's behavior (the controller has no message-text
            // special-case for cEDH meta-gap, unlike Comparison's "Deck A"/"Deck B" routing).
            // Catching the narrow ScryfallReferenceCollectionException (not a plain
            // HttpRequestException) lets a per-name fallback-search failure propagate with its
            // original message, exactly as the pre-Phase-83 inline loop did (WR-01).
            throw new HttpRequestException(
                $"Scryfall card reference lookup failed while building the cEDH meta-gap prompt with HTTP {(int?)exception.StatusCode}.",
                exception,
                exception.StatusCode);
        }

        return batchResolution.OracleNameMap;
    }

    private static IReadOnlyList<DeckEntry> NormalizeDeckEntriesForPromptAndCombos(
        IReadOnlyList<DeckEntry> deckEntries,
        IReadOnlyDictionary<string, string> oracleNameMap)
    {
        return deckEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .Select(entry =>
            {
                var resolvedName = ResolvePromptCardName(entry.Name, oracleNameMap);
                return new DeckEntry
                {
                    Name = resolvedName,
                    NormalizedName = CardNormalizer.Normalize(resolvedName),
                    Quantity = entry.Quantity,
                    Board = entry.Board
                };
            })
            .ToList();
    }

    private static IReadOnlyList<DeckEntry> BuildReferenceDeckEntries(string commanderName, EdhTop16Entry entry, IReadOnlyDictionary<string, string> oracleNameMap)
    {
        var entries = new List<DeckEntry>();
        if (!string.IsNullOrWhiteSpace(commanderName))
        {
            var resolvedCommanderName = ResolvePromptCardName(commanderName, oracleNameMap);
            entries.Add(new DeckEntry
            {
                Name = resolvedCommanderName,
                NormalizedName = CardNormalizer.Normalize(resolvedCommanderName),
                Quantity = 1,
                Board = "commander"
            });
        }

        foreach (var card in entry.MainDeck.Where(card => !string.IsNullOrWhiteSpace(card.Name)))
        {
            var resolvedName = ResolvePromptCardName(card.Name, oracleNameMap);
            entries.Add(new DeckEntry
            {
                Name = resolvedName,
                NormalizedName = CardNormalizer.Normalize(resolvedName),
                Quantity = 1,
                Board = "mainboard"
            });
        }

        return entries;
    }

    // Phase 15-02: promoted from private static to internal static for use by MetaGap variant classes.
    internal static string BuildCompactDecklist(IReadOnlyList<DeckEntry> deckEntries, IReadOnlyDictionary<string, string>? oracleNameMap = null)
    {
        var builder = new StringBuilder();
        var normalizedEntries = deckEntries
            .Where(entry => !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .GroupBy(
                entry => CardNormalizer.Normalize(entry.Name),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Quantity = group.Sum(entry => entry.Quantity),
                Name = group
                    .Select(entry => ResolvePromptCardName(entry.Name, oracleNameMap))
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty
            })
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in normalizedEntries)
        {
            builder.Append(entry.Quantity);
            builder.Append(' ');
            builder.AppendLine(entry.Name);
        }

        return builder.ToString().TrimEnd();
    }

    // Phase 15-02: promoted from private static to internal static for use by MetaGap variant classes.
    internal static string BuildCompactRefDecklist(EdhTop16Entry entry, IReadOnlyDictionary<string, string>? oracleNameMap = null)
    {
        var builder = new StringBuilder();
        var normalizedCards = entry.MainDeck
            .Where(card => !string.IsNullOrWhiteSpace(card.Name))
            .GroupBy(
                card => CardNormalizer.Normalize(card.Name),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => ResolvePromptCardName(group.First().Name, oracleNameMap))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        foreach (var cardName in normalizedCards)
        {
            builder.AppendLine(cardName);
        }

        return builder.ToString().TrimEnd();
    }

    private static string GetBaseCardDisplayName(string? cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            return string.Empty;
        }

        var trimmed = cardName.Trim();
        var splitSeparators = new[] { " // ", " / " };
        foreach (var separator in splitSeparators)
        {
            var splitIndex = trimmed.IndexOf(separator, StringComparison.Ordinal);
            if (splitIndex >= 0)
            {
                return trimmed[..splitIndex].Trim();
            }
        }

        return trimmed;
    }

    private static string ResolvePromptCardName(string? cardName, IReadOnlyDictionary<string, string>? oracleNameMap)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            return string.Empty;
        }

        var trimmed = cardName.Trim();
        if (oracleNameMap is not null && oracleNameMap.TryGetValue(trimmed, out var resolvedName) && !string.IsNullOrWhiteSpace(resolvedName))
        {
            return GetBaseCardDisplayName(resolvedName);
        }

        return GetBaseCardDisplayName(trimmed);
    }

    // Phase 15-02: promoted from private static to internal static for use by MetaGap variant classes.
    internal static string BuildComboReferenceText(string label, CommanderSpellbookResult? result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Commander Spellbook combos for {label}:");

        if (result is null || (result.IncludedCombos.Count == 0 && result.AlmostIncludedCombos.Count == 0))
        {
            builder.AppendLine("(none found)");
            return builder.ToString().TrimEnd();
        }

        if (result.IncludedCombos.Count > 0)
        {
            builder.AppendLine($"Complete combos: {result.IncludedCombos.Count}");
            for (var i = 0; i < result.IncludedCombos.Count; i++)
            {
                var combo = result.IncludedCombos[i];
                builder.AppendLine($"{i + 1}. Cards: {string.Join(" + ", combo.CardNames)}");
                builder.AppendLine($"   Result: {string.Join(", ", combo.Results)}");
            }
        }
        else
        {
            builder.AppendLine("Complete combos: 0");
        }

        if (result.AlmostIncludedCombos.Count > 0)
        {
            builder.AppendLine($"Near-combos: {result.AlmostIncludedCombos.Count}");
            for (var i = 0; i < result.AlmostIncludedCombos.Count; i++)
            {
                var combo = result.AlmostIncludedCombos[i];
                builder.AppendLine($"{i + 1}. Missing: {combo.MissingCard} | Have: {string.Join(" + ", combo.CardsInDeck)}");
                builder.AppendLine($"   Result: {string.Join(", ", combo.Results)}");
            }
        }
        else
        {
            builder.AppendLine("Near-combos: 0");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSchemaJson()
    {
        var payload = new
        {
            meta_gap = new
            {
                commander = string.Empty,
                color_id = string.Empty,
                ref_deck_count = 0,
                readiness_score = 0,
                readiness_justification = string.Empty,
                win_lines = new
                {
                    my_deck = new { primary = string.Empty, backup = string.Empty },
                    ref_consensus = new { primary = string.Empty, backup = string.Empty },
                    missing_lines = new[] { string.Empty }
                },
                interaction = new
                {
                    my_count = 0,
                    ref_avg_count = 0.0,
                    verdict = string.Empty,
                    detail = string.Empty
                },
                speed = new
                {
                    my_classification = string.Empty,
                    my_avg_turn = string.Empty,
                    ref_classification = string.Empty,
                    ref_avg_turn = string.Empty,
                    detail = string.Empty
                },
                mana_efficiency = new
                {
                    my_fast_mana = 0,
                    ref_avg_fast_mana = 0.0,
                    my_avg_cmc = 0.0,
                    ref_avg_cmc = 0.0,
                    my_lands = 0,
                    ref_avg_lands = 0.0,
                    detail = string.Empty
                },
                core_convergence = new[]
                {
                    new { card = string.Empty, role = string.Empty, in_my_deck = true }
                },
                missing_staples = new[]
                {
                    new { card = string.Empty, role = string.Empty, ref_count = 0, priority = 1, why = string.Empty }
                },
                potential_cuts = new[]
                {
                    new { card = string.Empty, role = string.Empty, ref_count = 0, priority = 1, why = string.Empty }
                },
                top_10_adds = new[]
                {
                    new { card = string.Empty, replaces = string.Empty, role = string.Empty, why = string.Empty }
                },
                top_10_cuts = new[]
                {
                    new { card = string.Empty, role = string.Empty, why = string.Empty }
                },
                meta_summary = string.Empty,
                optimization_path = string.Empty
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    internal static MetaGapResponse ParseResponse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Paste the meta_gap JSON returned from your AI into Step 3.");
        }

        var json = JsonTextFormatterService.ExtractJsonPayload(input);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(ResponseParsers.TruncatedResponseMessage);
        }

        using (document)
        {
            JsonElement payload = document.RootElement;
            if (payload.ValueKind == JsonValueKind.Object
                && payload.TryGetProperty("meta_gap", out var metaGapElement))
            {
                payload = JsonSerializer.SerializeToElement(new { meta_gap = metaGapElement });
            }

            MetaGapResponse? result;
            try
            {
                result = JsonSerializer.Deserialize<MetaGapResponse>(payload.GetRawText(), JsonOptions);
            }
            catch (JsonException)
            {
                // A payload that parses as a document but maps a field to the wrong shape
                // (e.g. an object field sent as a string) must surface the friendly message
                // rather than escaping as an uncaught JsonException and 500-ing the page.
                throw new InvalidOperationException(ResponseParsers.TruncatedResponseMessage);
            }

            if (result is null || !HasMeaningfulMetaGapContent(result))
            {
                throw new InvalidOperationException("The submitted AI response did not contain a valid meta_gap payload.");
            }

            return result;
        }
    }

    private static bool HasMeaningfulMetaGapContent(MetaGapResponse response)
        => !string.IsNullOrWhiteSpace(response.MetaGap.Commander)
            || !string.IsNullOrWhiteSpace(response.MetaGap.ColorId)
            || response.MetaGap.ReadinessScore > 0
            || !string.IsNullOrWhiteSpace(response.MetaGap.ReadinessJustification)
            || response.MetaGap.WinLines is not null
            || response.MetaGap.Interaction is not null
            || response.MetaGap.Speed is not null
            || response.MetaGap.ManaEfficiency is not null
            || response.MetaGap.CoreConvergence.Count > 0
            || response.MetaGap.MissingStaples.Count > 0
            || response.MetaGap.Top10Adds.Count > 0
            || response.MetaGap.Top10Cuts.Count > 0;

    /// <summary>
    /// Parsed-deck snapshot. <c>PlayableEntries</c> drives prompt + analysis paths
    /// (maybeboard/sideboard excluded). <c>AllEntries</c> preserves the full parser
    /// output so the canonical deck artifact written to the session zip can include
    /// optional sections users typed.
    /// </summary>
    private sealed record LoadedDeck(
        IReadOnlyList<DeckEntry> PlayableEntries,
        string CommanderName,
        IReadOnlyList<DeckEntry> AllEntries);
}
