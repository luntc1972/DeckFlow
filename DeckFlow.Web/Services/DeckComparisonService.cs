using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using DeckFlow.Core.Analysis;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using Microsoft.Extensions.Logging.Abstractions;
using DeckFlow.Web.Services.Http;
using Polly;
using Polly.Registry;
using RestSharp;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.Packets;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;
using DeckFlow.Web.Services.Scryfall;

namespace DeckFlow.Web.Services;

/// <summary>
/// Builds the deck-comparison prompt packet by hydrating two decks side-by-side.
/// </summary>
public interface IDeckComparisonService
{
    /// <summary>
    /// Builds the deck-comparison packet for the supplied two-deck request.
    /// </summary>
    /// <param name="request">Comparison workflow request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DeckComparisonResult> BuildAsync(DeckComparisonRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to compute the packet-session cache key for the supplied comparison request.
    /// </summary>
    /// <param name="request">Comparison workflow request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> TryComputeCacheKeyAsync(DeckComparisonRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Returns the results of a deck-comparison packet build.
/// </summary>
public sealed record DeckComparisonResult(
    string InputSummary,
    string DeckAListText,
    string DeckBListText,
    string DeckAComboText,
    string DeckBComboText,
    string ComparisonContextText,
    string ComparisonPromptText,
    string FollowUpPromptText,
    string ComparisonSchemaJson,
    DeckComparisonResponse? ComparisonResponse,
    string? TimingSummary,
    string? ResolvedDeckACommander = null,
    string? ResolvedDeckBCommander = null,
    string? RequestContextText = null);

/// <summary>
/// Hydrates two decks via Scryfall, queries Commander Spellbook for each, derives the side-by-side comparison context (role counts, mana curves, combo gaps), and composes the JSON-bound comparison prompt artifacts saved to the session zip.
/// </summary>
public sealed class DeckComparisonService : IDeckComparisonService
{
    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly ICommanderSpellbookService _commanderSpellbookService;
    private readonly IScryfallCardResolver _scryfallCardResolver;
    private readonly ScryfallReferenceResolver _scryfallReferenceResolver;
    private readonly ILogger<DeckComparisonService> _logger;
    private readonly ComparisonPromptVariantRegistry _comparisonPromptRegistry;
    private readonly FollowUpPromptVariantRegistry _followUpPromptRegistry;
    private readonly PacketSessionCache _packetCache;

    internal DeckComparisonService(
        IScryfallCardResolver scryfallCardResolver,
        ScryfallReferenceResolver scryfallReferenceResolver,
        IDeckEntryLoader deckEntryLoader,
        ICommanderSpellbookService commanderSpellbookService,
        ComparisonPromptVariantRegistry comparisonPromptRegistry,
        FollowUpPromptVariantRegistry followUpPromptRegistry,
        PacketSessionCache packetCache,
        ILogger<DeckComparisonService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);
        ArgumentNullException.ThrowIfNull(scryfallReferenceResolver);
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(commanderSpellbookService);
        ArgumentNullException.ThrowIfNull(comparisonPromptRegistry);
        ArgumentNullException.ThrowIfNull(followUpPromptRegistry);
        ArgumentNullException.ThrowIfNull(packetCache);
        _scryfallCardResolver = scryfallCardResolver;
        _scryfallReferenceResolver = scryfallReferenceResolver;
        _deckEntryLoader = deckEntryLoader;
        _commanderSpellbookService = commanderSpellbookService;
        _comparisonPromptRegistry = comparisonPromptRegistry;
        _followUpPromptRegistry = followUpPromptRegistry;
        _packetCache = packetCache;
        _logger = logger ?? NullLogger<DeckComparisonService>.Instance;
    }

    /// <summary>
    /// SINGLE source of truth for the deck-comparison cache-input bag. Called from BOTH
    /// <see cref="TryComputeCacheKeyAsync"/> (read side) AND the line-198 cache-write site
    /// in <see cref="BuildAsync"/> (write side). Both sides pass the SAME deckA/deckB
    /// LoadedDeck values — commander names are extracted once inside LoadDeckAsync and never
    /// mutated downstream (ValidateSameCommander at line 147 is a pure assertion, not a mutation).
    /// </summary>
    private static DeckComparisonCacheInputs BuildDeckComparisonCacheInputs(
        DeckComparisonRequest request,
        LoadedDeck deckA,
        LoadedDeck deckB)
    {
        return new DeckComparisonCacheInputs(
            NormalizedDeckASource: BuildCanonicalDeckSourceText(deckA),
            NormalizedDeckBSource: BuildCanonicalDeckSourceText(deckB),
            DeckABracket: request.DeckABracket,
            DeckBBracket: request.DeckBBracket,
            TargetAiPlatformKey: request.TargetAiPlatform);
    }

    /// <summary>
    /// Stable text representation of the LoadedDeck (D-02). Includes commander as a prefix line
    /// + all entries sorted for byte-stable output regardless of input mode (URL vs paste).
    /// </summary>
    private static string BuildCanonicalDeckSourceText(LoadedDeck loadedDeck)
    {
        ArgumentNullException.ThrowIfNull(loadedDeck);
        var builder = new StringBuilder();
        builder.Append("commander|").Append(loadedDeck.CommanderName ?? string.Empty).Append('\n');
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
    /// Composes the D-01 cache-input field bag for the given comparison request and returns the
    /// canonical PacketSessionCache key. Re-runs the same private LoadDeckAsync path BuildAsync
    /// uses for both decks. Returns null on any load failure or when either deck source is empty
    /// (controller falls through to BuildAsync silently per D-11). Calls
    /// <see cref="BuildDeckComparisonCacheInputs"/> for write↔read parity by code locality.
    /// </summary>
    public async Task<string?> TryComputeCacheKeyAsync(DeckComparisonRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DeckASource) || string.IsNullOrWhiteSpace(request.DeckBSource))
        {
            return null;
        }

        LoadedDeck deckA;
        LoadedDeck deckB;
        try
        {
            deckA = await LoadDeckAsync("Deck A", request.DeckASource, cancellationToken).ConfigureAwait(false);
            deckB = await LoadDeckAsync("Deck B", request.DeckBSource, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DeckParseException or HttpRequestException)
        {
            return null;
        }

        var inputs = BuildDeckComparisonCacheInputs(request, deckA, deckB);
        return PacketSessionCache.ComputeKey(inputs);
    }

    /// <inheritdoc/>
    public async Task<DeckComparisonResult> BuildAsync(DeckComparisonRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var overallStopwatch = Stopwatch.StartNew();
        var timings = new List<(string Label, long Ms, string? Detail)>();

        if (string.IsNullOrWhiteSpace(request.DeckASource))
        {
            throw new InvalidOperationException("Deck A URL or deck text is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DeckBSource))
        {
            throw new InvalidOperationException("Deck B URL or deck text is required.");
        }

        var deckABracket = CommanderBracketCatalog.Find(request.DeckABracket)
            ?? throw new InvalidOperationException("Choose a Commander bracket for Deck A before generating the comparison packet.");
        var deckBBracket = CommanderBracketCatalog.Find(request.DeckBBracket)
            ?? throw new InvalidOperationException("Choose a Commander bracket for Deck B before generating the comparison packet.");

        var deckALoadStopwatch = Stopwatch.StartNew();
        var deckA = await LoadDeckAsync("Deck A", request.DeckASource, cancellationToken).ConfigureAwait(false);
        timings.Add(("Deck A load", deckALoadStopwatch.ElapsedMilliseconds, $"{deckA.PlayableEntries.Sum(entry => entry.Quantity)} cards"));

        var deckBLoadStopwatch = Stopwatch.StartNew();
        var deckB = await LoadDeckAsync("Deck B", request.DeckBSource, cancellationToken).ConfigureAwait(false);
        timings.Add(("Deck B load", deckBLoadStopwatch.ElapsedMilliseconds, $"{deckB.PlayableEntries.Sum(entry => entry.Quantity)} cards"));

        ValidateSameCommander(deckA.CommanderName, deckB.CommanderName);

        var deckAName = ResolveDeckName(request.DeckAName, deckA.CommanderName, "Deck A");
        var deckBName = ResolveDeckName(request.DeckBName, deckB.CommanderName, "Deck B");

        var lookupStopwatch = Stopwatch.StartNew();
        var deckALookup = await LookupCardDetailsAsync("Deck A", deckA.PlayableEntries, cancellationToken).ConfigureAwait(false);
        var deckBLookup = await LookupCardDetailsAsync("Deck B", deckB.PlayableEntries, cancellationToken).ConfigureAwait(false);
        timings.Add(("Scryfall card lookup", lookupStopwatch.ElapsedMilliseconds, $"Deck A {deckALookup.Cards.Count} cards | Deck B {deckBLookup.Cards.Count} cards"));

        var deckACards = deckALookup.Cards;
        var deckBCards = deckBLookup.Cards;

        var deckAListText = PacketTextAssembler.BuildSectionedDecklistText(deckA.PlayableEntries, deckA.OptionalEntries, includeVersions: false, deckALookup.OracleNameMap);
        var deckBListText = PacketTextAssembler.BuildSectionedDecklistText(deckB.PlayableEntries, deckB.OptionalEntries, includeVersions: false, deckBLookup.OracleNameMap);

        var comboLookupStopwatch = Stopwatch.StartNew();
        var deckACombos = await _commanderSpellbookService.FindCombosAsync(deckA.PlayableEntries, cancellationToken).ConfigureAwait(false);
        var deckBCombos = await _commanderSpellbookService.FindCombosAsync(deckB.PlayableEntries, cancellationToken).ConfigureAwait(false);
        timings.Add((
            "Commander Spellbook",
            comboLookupStopwatch.ElapsedMilliseconds,
            $"Deck A {deckACombos?.IncludedCombos.Count ?? 0} combos | Deck B {deckBCombos?.IncludedCombos.Count ?? 0} combos"));

        var deckASummary = BuildDeckSummary(deckAName, deckA.CommanderName, deckABracket, deckA.PlayableEntries, deckACards, deckACombos);
        var deckBSummary = BuildDeckSummary(deckBName, deckB.CommanderName, deckBBracket, deckB.PlayableEntries, deckBCards, deckBCombos);
        var deckAComboText = BuildComboArtifactText(deckASummary);
        var deckBComboText = BuildComboArtifactText(deckBSummary);
        var comparisonContextText = BuildComparisonContextText(deckASummary, deckBSummary);
        var comparisonSchemaJson = BuildComparisonSchemaJson(deckAName, deckBName, deckA.CommanderName, deckB.CommanderName, deckABracket.Label, deckBBracket.Label);
        var inputSummary = BuildInputSummary(deckASummary, deckBSummary);
        var comparisonPromptText = BuildComparisonPrompt(
            deckASummary,
            deckBSummary,
            deckAListText,
            deckBListText,
            deckAComboText,
            deckBComboText,
            comparisonContextText,
            comparisonSchemaJson,
            request.TargetAiPlatform);
        var followUpPromptText = BuildFollowUpPrompt(comparisonSchemaJson, request.TargetAiPlatform);

        DeckComparisonResponse? comparisonResponse = null;
        if (request.WorkflowStep >= 3 && !string.IsNullOrWhiteSpace(request.ComparisonResponseJson))
        {
            comparisonResponse = ParseComparisonResponse(request.ComparisonResponseJson);
        }

        var timingSummary = BuildTimingSummary(timings, overallStopwatch.ElapsedMilliseconds);

        var result = new DeckComparisonResult(
            inputSummary,
            deckAListText,
            deckBListText,
            deckAComboText,
            deckBComboText,
            comparisonContextText,
            comparisonPromptText,
            followUpPromptText,
            comparisonSchemaJson,
            comparisonResponse,
            timingSummary,
            ResolvedDeckACommander: deckA.CommanderName,
            ResolvedDeckBCommander: deckB.CommanderName,
            RequestContextText: BuildRequestContextText(request));

        // Phase 999.3 cache write. Shared BuildDeckComparisonCacheInputs helper (called from BOTH
        // here AND from TryComputeCacheKeyAsync) guarantees write↔read parity.
        // `deckA` and `deckB` LoadedDeck locals are already in scope at line 198 (assigned at lines 140 + 144).
        var cacheInputs = BuildDeckComparisonCacheInputs(request, deckA, deckB);
        var cacheKey = PacketSessionCache.ComputeKey(cacheInputs);
        _packetCache.Set(cacheKey, result, PacketSizeEstimator.EstimateSizeBytes(result));

        return result;
    }

    /// <summary>
    /// Plain-text scalar key/value envelope round-tripped through the comparison zip.
    /// Mirrors <see cref="DeckAnalysisPacketService"/>'s BuildRequestContextText for Packets.
    /// Parsed via <see cref="RequestContextParser"/>; unknown keys are ignored.
    /// </summary>
    internal static string BuildRequestContextText(DeckComparisonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder();
        builder.AppendLine($"workflow_step: {request.WorkflowStep}");
        PacketTextAssembler.AppendKeyValueLine(builder, "deck_a_name", request.DeckAName, string.Empty, JsonTextFormatterService.NormalizeSingleLine);
        PacketTextAssembler.AppendKeyValueLine(builder, "deck_b_name", request.DeckBName, string.Empty, JsonTextFormatterService.NormalizeSingleLine);
        PacketTextAssembler.AppendKeyValueLine(builder, "deck_a_bracket", request.DeckABracket, string.Empty, JsonTextFormatterService.NormalizeSingleLine);
        PacketTextAssembler.AppendKeyValueLine(builder, "deck_b_bracket", request.DeckBBracket, string.Empty, JsonTextFormatterService.NormalizeSingleLine);
        PacketTextAssembler.AppendKeyValueLine(builder, "target_ai_platform", request.TargetAiPlatform, "ChatGPT", JsonTextFormatterService.NormalizeSingleLine);
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private async Task<LoadedDeck> LoadDeckAsync(string deckLabel, string deckSource, CancellationToken cancellationToken)
    {
        List<DeckEntry> entries;
        try
        {
            var loaded = await _deckEntryLoader.LoadFromSourceAsync(deckSource, cancellationToken: cancellationToken).ConfigureAwait(false);
            entries = loaded.Entries;
        }
        catch (Exception exception) when (exception is InvalidOperationException or DeckParseException or HttpRequestException)
        {
            throw new InvalidOperationException($"{deckLabel} parse failed: {exception.Message}", exception);
        }

        var playableEntries = entries
            .Where(entry =>
                !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var optionalEntries = entries
            .Where(entry =>
                string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (playableEntries.Count == 0)
        {
            throw new InvalidOperationException($"{deckLabel} parse failed: the submitted deck did not contain any commander or mainboard cards.");
        }

        var hasExplicitCommander = playableEntries.Any(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase));
        var commanderName = playableEntries
            .FirstOrDefault(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            ?.Name;

        if (string.IsNullOrWhiteSpace(commanderName))
        {
            if (!hasExplicitCommander && playableEntries.Count < 2)
            {
                throw new InvalidOperationException($"{deckLabel} parse failed: could not determine a commander from the submitted deck.");
            }

            commanderName = playableEntries
                .Where(entry => entry.Quantity == 1)
                .Select(entry => entry.Name)
                .FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(commanderName))
        {
            throw new InvalidOperationException($"{deckLabel} parse failed: could not determine a commander from the submitted deck.");
        }

        if (!hasExplicitCommander)
        {
            entries = DeckEntryReflagHelper.ReflagCommanderEntry(entries, commanderName);
            playableEntries = DeckEntryReflagHelper.ReflagCommanderEntry(playableEntries, commanderName);
        }

        return new LoadedDeck(entries, playableEntries, optionalEntries, commanderName ?? string.Empty);
    }

    private async Task<CardLookupResult> LookupCardDetailsAsync(string deckLabel, IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
    {
        var uniqueNames = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .Select(entry => entry.Name.Trim())
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
                cancellationToken,
                batchFallbackStrategy: (names, ct) => _scryfallCardResolver.SearchFallbackCardsAsync(names, ct)).ConfigureAwait(false);
        }
        catch (ScryfallReferenceCollectionException exception)
        {
            // Why: preserve the deck-labeled message the DeckPacketController's error handler relies
            // on (it routes messages containing "Deck A"/"Deck B" straight to the view, bypassing
            // UpstreamErrorMessageBuilder). Only the cards/collection-CALL failure is re-wrapped here:
            // catching the narrow ScryfallReferenceCollectionException (not a plain HttpRequestException)
            // lets a per-name fallback-SEARCH failure propagate with its ORIGINAL message, so it keeps
            // routing through UpstreamErrorMessageBuilder's friendly copy exactly as it did pre-Phase-83
            // (WR-01) rather than being mislabeled with "Deck A ... comparison packet" and rendered raw.
            throw new HttpRequestException(
                $"{deckLabel} Scryfall card reference lookup failed while building the comparison packet with HTTP {(int?)exception.StatusCode}.",
                exception,
                exception.StatusCode);
        }

        // Why (WR-02): dedup resolved cards by Name. This is an intentional, tested divergence from the
        // pre-refactor AddRange-all: it fixes a latent ArgumentException in BuildDeckSummary's
        // ToDictionary(card => card.Name) when cards/collection returns two entries with the same Name,
        // and drops orphan cards whose Name matched no request (BuildDeckSummary never looked those up,
        // so the paste artifact is unchanged). Guarded by
        // BuildAsync_CollectionReturnsDuplicateCardName_DedupsInsteadOfCrashing.
        var resolvedCards = new List<ScryfallCard>();
        var seenCardNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resolution in batchResolution.Resolutions)
        {
            if (seenCardNames.Add(resolution.Card.Name))
            {
                resolvedCards.Add(resolution.Card);
            }
        }

        return new CardLookupResult(resolvedCards, batchResolution.OracleNameMap);
    }

    private sealed record CardLookupResult(IReadOnlyList<ScryfallCard> Cards, IReadOnlyDictionary<string, string> OracleNameMap);

    private static DeckComparisonDeckSummary BuildDeckSummary(
        string deckName,
        string commanderName,
        CommanderBracketOption bracket,
        IReadOnlyList<DeckEntry> entries,
        IReadOnlyList<ScryfallCard> cards,
        CommanderSpellbookResult? comboResult)
    {
        var cardLookup = cards.ToDictionary(card => card.Name, StringComparer.OrdinalIgnoreCase);
        var mainboardEntries = entries
            .Where(entry => !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var commanderEntries = entries
            .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var totalMainboardCards = mainboardEntries.Sum(entry => entry.Quantity);
        var categories = CategoryCountReporter.CountByQuantity(mainboardEntries)
            .Take(8)
            .Select(item => $"{item.Category}: {item.Count}")
            .ToList();

        var colorIdentity = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var includedCombos = comboResult?.IncludedCombos ?? Array.Empty<SpellbookCombo>();
        var almostIncludedCombos = comboResult?.AlmostIncludedCombos ?? Array.Empty<SpellbookAlmostCombo>();

        foreach (var commanderEntry in commanderEntries)
        {
            if (cardLookup.TryGetValue(commanderEntry.Name, out var commanderCard))
            {
                foreach (var color in commanderCard.ColorIdentity ?? Array.Empty<string>())
                {
                    colorIdentity.Add(color);
                }
            }
        }

        // Build the stat inputs and gather color identity in one pass, then delegate the lands /
        // creatures / curve / average-mana-value / role tallies to DeckStatAggregator so the analysis
        // and comparison prompts share a single source of truth for those rules.
        var statInputs = new List<DeckStatCardInput>();
        foreach (var entry in mainboardEntries)
        {
            if (!cardLookup.TryGetValue(entry.Name, out var card))
            {
                continue;
            }

            foreach (var color in card.ColorIdentity ?? Array.Empty<string>())
            {
                colorIdentity.Add(color);
            }

            statInputs.Add(new DeckStatCardInput(
                entry.Quantity,
                card.TypeLine ?? string.Empty,
                NormalizeOracleText(card),
                card.ManaCost ?? string.Empty));
        }

        var stats = DeckStatAggregator.Compute(statInputs);
        // Comparison-specific: combos contribute to closing power on top of the base role tally.
        var closingPower = stats.ClosingPower + includedCombos.Count * 2 + almostIncludedCombos.Count;
        IReadOnlyList<string> sharedThemes = categories.Count == 0 ? Array.Empty<string>() : categories;
        var comboSummaries = includedCombos
            .Select(combo => $"{string.Join(" + ", combo.CardNames)} -> {string.Join(", ", combo.Results)}")
            .Take(5)
            .ToList();
        var almostComboSummaries = almostIncludedCombos
            .Select(combo => $"{combo.MissingCard} missing from {string.Join(" + ", combo.CardsInDeck)}")
            .Take(5)
            .ToList();

        return new DeckComparisonDeckSummary(
            deckName,
            commanderName,
            bracket,
            totalMainboardCards,
            stats.Lands,
            stats.Creatures,
            stats.AverageManaValue,
            stats.ManaCurve,
            colorIdentity.OrderBy(color => color, StringComparer.OrdinalIgnoreCase).ToList(),
            categories,
            stats.Ramp,
            stats.Draw,
            stats.Interaction,
            stats.Wipes,
            stats.Recursion,
            closingPower,
            sharedThemes,
            comboSummaries,
            almostComboSummaries,
            includedCombos.Count,
            almostIncludedCombos.Count);
    }

    private static string BuildInputSummary(DeckComparisonDeckSummary deckA, DeckComparisonDeckSummary deckB)
    {
        var builder = new StringBuilder();
        AppendDeckBlock(builder, "Deck A", deckA);
        builder.AppendLine();
        AppendDeckBlock(builder, "Deck B", deckB);
        return builder.ToString().TrimEnd();
    }

    private static void AppendDeckBlock(StringBuilder builder, string label, DeckComparisonDeckSummary deck)
    {
        var heading = !string.IsNullOrWhiteSpace(deck.Name)
            ? deck.Name.Trim()
            : !string.IsNullOrWhiteSpace(deck.CommanderName)
                ? deck.CommanderName.Trim()
                : label;
        builder.AppendLine(heading);
        builder.AppendLine($"Commander: {FallbackText(deck.CommanderName, "Unknown")}");
        builder.AppendLine($"Bracket: {deck.Bracket.Label}");
        builder.AppendLine($"Main deck cards: {deck.MainboardCount}");
        builder.AppendLine($"Lands: {deck.Lands}  Ramp: {deck.Ramp}  Draw: {deck.Draw}");
        builder.AppendLine($"Interaction: {deck.Interaction}  Combos: {deck.IncludedComboCount}");
    }

    private static string BuildComparisonContextText(DeckComparisonDeckSummary deckA, DeckComparisonDeckSummary deckB)
    {
        var sharedThemeNames = deckA.CategorySummaries
            .Select(item => item.Split(':', 2)[0].Trim())
            .Intersect(deckB.CategorySummaries.Select(item => item.Split(':', 2)[0].Trim()), StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("comparison_context:");
        builder.AppendLine($"generated_at_utc: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        builder.AppendLine();
        builder.AppendLine("commander_bracket_definitions:");
        foreach (var option in CommanderBracketCatalog.Options)
        {
            builder.AppendLine($"- {option.Label}: {option.Summary} {option.TurnsExpectation}");
        }
        builder.AppendLine();
        AppendDeckContext(builder, "deck_a", deckA);
        builder.AppendLine();
        AppendDeckContext(builder, "deck_b", deckB);
        builder.AppendLine();
        builder.AppendLine("comparison_signals:");
        builder.AppendLine($"shared_categories: {(sharedThemeNames.Count == 0 ? "(none)" : string.Join(", ", sharedThemeNames))}");
        builder.AppendLine($"ramp_gap: {deckA.Name} {deckA.Ramp} vs {deckB.Name} {deckB.Ramp}");
        builder.AppendLine($"draw_gap: {deckA.Name} {deckA.Draw} vs {deckB.Name} {deckB.Draw}");
        builder.AppendLine($"interaction_gap: {deckA.Name} {deckA.Interaction} vs {deckB.Name} {deckB.Interaction}");
        builder.AppendLine($"wipe_gap: {deckA.Name} {deckA.Wipes} vs {deckB.Name} {deckB.Wipes}");
        builder.AppendLine($"recursion_gap: {deckA.Name} {deckA.Recursion} vs {deckB.Name} {deckB.Recursion}");
        builder.AppendLine($"closing_power_gap: {deckA.Name} {deckA.ClosingPower} vs {deckB.Name} {deckB.ClosingPower}");
        builder.AppendLine($"combo_gap: {deckA.Name} {deckA.IncludedComboCount} complete combos vs {deckB.Name} {deckB.IncludedComboCount} complete combos");
        builder.AppendLine($"average_mana_value_gap: {deckA.Name} {deckA.AverageManaValue:0.00} vs {deckB.Name} {deckB.AverageManaValue:0.00}");
        return builder.ToString().TrimEnd();
    }

    private static void AppendDeckContext(StringBuilder builder, string label, DeckComparisonDeckSummary deck)
    {
        builder.AppendLine($"{label}:");
        builder.AppendLine($"  name: {deck.Name}");
        builder.AppendLine($"  commander: {FallbackText(deck.CommanderName, "Unknown")}");
        builder.AppendLine($"  bracket: {deck.Bracket.Label}");
        builder.AppendLine($"  bracket_summary: {deck.Bracket.Summary}");
        builder.AppendLine($"  bracket_turn_expectation: {deck.Bracket.TurnsExpectation}");
        builder.AppendLine($"  mainboard_cards: {deck.MainboardCount}");
        builder.AppendLine($"  lands: {deck.Lands}");
        builder.AppendLine($"  creatures: {deck.Creatures}");
        builder.AppendLine($"  average_mana_value: {deck.AverageManaValue:0.00}");
        builder.AppendLine($"  mana_curve: {string.Join(", ", deck.ManaCurve.Select(item => $"{item.Key}={item.Value}"))}");
        builder.AppendLine($"  color_identity: {(deck.ColorIdentity.Count == 0 ? "(unknown)" : string.Join(", ", deck.ColorIdentity))}");
        builder.AppendLine($"  categories: {(deck.CategorySummaries.Count == 0 ? "(none detected)" : string.Join(" | ", deck.CategorySummaries))}");
        builder.AppendLine($"  role_counts: ramp={deck.Ramp}, draw={deck.Draw}, interaction={deck.Interaction}, wipes={deck.Wipes}, recursion={deck.Recursion}, closing_power={deck.ClosingPower}");
        builder.AppendLine($"  combos_included: {deck.IncludedComboCount}");
        builder.AppendLine($"  combos_almost_included: {deck.AlmostIncludedComboCount}");
        builder.AppendLine($"  key_combos: {(deck.ComboSummaries.Count == 0 ? "(none found)" : string.Join(" | ", deck.ComboSummaries))}");
        builder.AppendLine($"  almost_combos: {(deck.AlmostComboSummaries.Count == 0 ? "(none found)" : string.Join(" | ", deck.AlmostComboSummaries))}");
    }

    // Internal for test access — per-AI dispatcher exercised by the AI result contract tests.
    // Phase 15-02: converted from internal static to instance method; dispatches via injected ComparisonPromptVariantRegistry.
    internal string BuildComparisonPrompt(
        DeckComparisonDeckSummary deckA,
        DeckComparisonDeckSummary deckB,
        string deckAListText,
        string deckBListText,
        string deckAComboText,
        string deckBComboText,
        string comparisonContextText,
        string comparisonSchemaJson,
        string targetAiPlatform)
    {
        return _comparisonPromptRegistry.Build(
            AiPlatform.Normalize(targetAiPlatform),
            deckA, deckB, deckAListText, deckBListText,
            deckAComboText, deckBComboText, comparisonContextText, comparisonSchemaJson);
    }


    // Phase 15-02: promoted from private static to internal static for use by Comparison/Gemini and ChatGpt variant classes.
    internal static void AppendPromptDeckSection(
        StringBuilder builder,
        DeckComparisonDeckSummary deck,
        string deckListText,
        string comboText)
    {
        builder.AppendLine($"Name: {deck.Name}");
        builder.AppendLine($"Commander: {FallbackText(deck.CommanderName, "Unknown")}");
        builder.AppendLine($"Bracket: {deck.Bracket.Label}");
        builder.AppendLine($"Bracket summary: {deck.Bracket.Summary}");
        builder.AppendLine($"Bracket turn expectation: {deck.Bracket.TurnsExpectation}");
        builder.AppendLine("Normalized decklist:");
        builder.AppendLine(deckListText);
        builder.AppendLine();
        builder.AppendLine("Combo summary:");
        builder.AppendLine(comboText);
    }

    // Phase 15-02: promoted from private static to internal static for use by Comparison/Claude variant class.
    internal static void AppendComparisonPromptDeckXml(
        StringBuilder builder,
        string tagName,
        DeckComparisonDeckSummary deck,
        string deckListText,
        string comboText)
    {
        builder.AppendLine($"<{tagName}>");
        builder.AppendLine($"  <name>{deck.Name}</name>");
        builder.AppendLine($"  <commander>{FallbackText(deck.CommanderName, "Unknown")}</commander>");
        builder.AppendLine("  <bracket>");
        builder.AppendLine($"    <label>{deck.Bracket.Label}</label>");
        builder.AppendLine($"    <summary>{deck.Bracket.Summary}</summary>");
        builder.AppendLine($"    <turn_expectation>{deck.Bracket.TurnsExpectation}</turn_expectation>");
        builder.AppendLine("  </bracket>");
        builder.AppendLine("  <list>");
        builder.AppendLine(deckListText);
        builder.AppendLine("  </list>");
        builder.AppendLine("  <combos>");
        builder.AppendLine(comboText);
        builder.AppendLine("  </combos>");
        builder.AppendLine($"</{tagName}>");
    }

    // Phase 15-02: converted from internal static to instance method; dispatches via injected FollowUpPromptVariantRegistry.
    internal string BuildFollowUpPrompt(string comparisonSchemaJson, string targetAiPlatform)
    {
        return _followUpPromptRegistry.Build(
            AiPlatform.Normalize(targetAiPlatform),
            comparisonSchemaJson);
    }


    private static string BuildComparisonSchemaJson(string deckAName, string deckBName, string deckACommander, string deckBCommander, string deckABracket, string deckBBracket)
    {
        var payload = new
        {
            deck_a_name = deckAName,
            deck_b_name = deckBName,
            deck_a_commander = deckACommander,
            deck_b_commander = deckBCommander,
            deck_a_gameplan = string.Empty,
            deck_b_gameplan = string.Empty,
            deck_a_bracket = deckABracket,
            deck_b_bracket = deckBBracket,
            shared_themes = Array.Empty<string>(),
            major_differences = Array.Empty<string>(),
            deck_a_strengths = Array.Empty<string>(),
            deck_b_strengths = Array.Empty<string>(),
            deck_a_weaknesses = Array.Empty<string>(),
            deck_b_weaknesses = Array.Empty<string>(),
            speed_comparison = string.Empty,
            resilience_comparison = string.Empty,
            interaction_comparison = string.Empty,
            mana_consistency_comparison = string.Empty,
            closing_power_comparison = string.Empty,
            combo_comparison = string.Empty,
            overall_verdict = string.Empty,
            key_gap_cards_or_packages = Array.Empty<string>(),
            deck_a_key_combos = Array.Empty<string>(),
            deck_b_key_combos = Array.Empty<string>(),
            recommended_for = new
            {
                deck_a = Array.Empty<string>(),
                deck_b = Array.Empty<string>()
            },
            confidence_notes = Array.Empty<string>()
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    internal static DeckComparisonResponse ParseComparisonResponse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Paste the deck_comparison JSON returned from your AI into Step 3.");
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
                && payload.TryGetProperty("deck_comparison", out var comparisonElement))
            {
                payload = comparisonElement;
            }

            var result = JsonSerializer.Deserialize<DeckComparisonResponse>(payload.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result is null || !HasMeaningfulComparisonContent(result))
            {
                throw new InvalidOperationException("The submitted AI response did not contain a valid deck_comparison payload.");
            }

            return result;
        }
    }

    private static bool HasMeaningfulComparisonContent(DeckComparisonResponse response)
        => !string.IsNullOrWhiteSpace(response.DeckAName)
            || !string.IsNullOrWhiteSpace(response.DeckBName)
            || !string.IsNullOrWhiteSpace(response.DeckACommander)
            || !string.IsNullOrWhiteSpace(response.DeckBCommander)
            || !string.IsNullOrWhiteSpace(response.DeckAGameplan)
            || !string.IsNullOrWhiteSpace(response.DeckBGameplan)
            || response.SharedThemes.Count > 0
            || response.MajorDifferences.Count > 0
            || response.DeckAStrengths.Count > 0
            || response.DeckAWeaknesses.Count > 0
            || response.DeckBStrengths.Count > 0
            || response.DeckBWeaknesses.Count > 0
            || !string.IsNullOrWhiteSpace(response.OverallVerdict)
            || response.RecommendedFor.DeckA.Count > 0
            || response.RecommendedFor.DeckB.Count > 0;

    private static string BuildTimingSummary(IReadOnlyList<(string Label, long Ms, string? Detail)> timings, long totalMs)
    {
        var builder = new StringBuilder();
        foreach (var timing in timings)
        {
            builder.Append("- ");
            builder.Append(timing.Label);
            builder.Append(": ");
            builder.Append(timing.Ms);
            builder.Append(" ms");
            if (!string.IsNullOrWhiteSpace(timing.Detail))
            {
                builder.Append(" (");
                builder.Append(timing.Detail);
                builder.Append(')');
            }

            builder.AppendLine();
        }

        builder.Append("Total: ");
        builder.Append(totalMs);
        builder.Append(" ms");
        return builder.ToString();
    }

    private static readonly HashSet<string> SectionKeywordsThatAreNotCommanders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mainboard",
        "Sideboard",
        "Maybeboard",
        "Deck",
        "Commander",
        "Companion"
    };

    private static void ValidateSameCommander(string? deckACommander, string? deckBCommander)
    {
        ValidateCommanderIsRealCardName(deckACommander, "Deck A");
        ValidateCommanderIsRealCardName(deckBCommander, "Deck B");
        if (!string.Equals(deckACommander!.Trim(), deckBCommander!.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Both decks must share the same commander to be compared. Deck A: \"{deckACommander.Trim()}\". Deck B: \"{deckBCommander.Trim()}\". To compare different commanders, use the Deck Analysis tool on each deck individually.");
        }
    }

    private static void ValidateCommanderIsRealCardName(string? commander, string deckLabel)
    {
        if (string.IsNullOrWhiteSpace(commander))
        {
            throw new InvalidOperationException($"{deckLabel}'s commander could not be identified. Use a deck format that marks the commander (Archidekt/Moxfield URL with the commander assigned, or a decklist with a `Commander` section header above the commander card).");
        }
        if (SectionKeywordsThatAreNotCommanders.Contains(commander.Trim()))
        {
            throw new InvalidOperationException($"{deckLabel}'s commander parsed as the section keyword \"{commander.Trim()}\" — this means the deck list was pasted without a `Commander` section header. Re-paste the deck with a `Commander` line above the commander card, or use an Archidekt/Moxfield URL where the commander is explicitly assigned.");
        }
    }

    private static string ResolveDeckName(string requestedName, string commanderName, string fallback)
        => string.IsNullOrWhiteSpace(requestedName)
            ? FallbackText(commanderName, fallback)
            : requestedName.Trim();

    // Phase 15-02: promoted from private static to internal static for use by promoted helpers AppendPromptDeckSection / AppendComparisonPromptDeckXml.
    internal static string FallbackText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeOracleText(ScryfallCard card)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(card.OracleText))
        {
            parts.Add(CollapseWhitespace(card.OracleText));
        }

        foreach (var face in card.CardFaces ?? Array.Empty<ScryfallCardFace>())
        {
            if (!string.IsNullOrWhiteSpace(face.OracleText))
            {
                parts.Add(CollapseWhitespace(face.OracleText));
            }
        }

        return string.Join(" ", parts);
    }

    private static string CollapseWhitespace(string value)
        => string.Join(" ", value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    // Phase 15-02: promoted from private static to internal static for use by Comparison and FollowUp variant classes.
    internal static string IndentJson(string json, int indentSize)
    {
        var indent = new string(' ', indentSize);
        var lines = json.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line => indent + line));
    }

    private sealed record LoadedDeck(
        IReadOnlyList<DeckEntry> AllEntries,
        IReadOnlyList<DeckEntry> PlayableEntries,
        IReadOnlyList<DeckEntry> OptionalEntries,
        string CommanderName);

    // Internal for test construction — exercised by the AI result contract tests.
    internal sealed record DeckComparisonDeckSummary(
        string Name,
        string CommanderName,
        CommanderBracketOption Bracket,
        int MainboardCount,
        int Lands,
        int Creatures,
        decimal AverageManaValue,
        IReadOnlyDictionary<string, int> ManaCurve,
        IReadOnlyList<string> ColorIdentity,
        IReadOnlyList<string> CategorySummaries,
        int Ramp,
        int Draw,
        int Interaction,
        int Wipes,
        int Recursion,
        int ClosingPower,
        IReadOnlyList<string> SharedThemes,
        IReadOnlyList<string> ComboSummaries,
        IReadOnlyList<string> AlmostComboSummaries,
        int IncludedComboCount,
        int AlmostIncludedComboCount);

    private static string BuildComboArtifactText(DeckComparisonDeckSummary deck)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{deck.Name} combos");
        builder.AppendLine($"Commander bracket: {deck.Bracket.Label}");
        builder.AppendLine($"Complete combos: {deck.IncludedComboCount}");
        builder.AppendLine($"Near-combos: {deck.AlmostIncludedComboCount}");
        builder.AppendLine();
        builder.AppendLine("Key combos:");
        if (deck.ComboSummaries.Count == 0)
        {
            builder.AppendLine("(none found)");
        }
        else
        {
            foreach (var combo in deck.ComboSummaries)
            {
                builder.AppendLine($"- {combo}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Near-combos:");
        if (deck.AlmostComboSummaries.Count == 0)
        {
            builder.AppendLine("(none found)");
        }
        else
        {
            foreach (var combo in deck.AlmostComboSummaries)
            {
                builder.AppendLine($"- {combo}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
