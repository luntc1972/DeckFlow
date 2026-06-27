using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;

namespace DeckFlow.Web.Services.Manabase;

/// <summary>
/// Loads a deck, resolves its cards through Scryfall, and runs the Core mana-base analyzer,
/// returning a <see cref="ManabaseAnalysisResult"/>. All HTTP stays here (via
/// <see cref="IScryfallCardResolver"/>); the Core pipeline stays pure.
/// </summary>
public interface IManabaseAnalysisService
{
    /// <summary>Analyze the mana base of the deck identified by <paramref name="deckSource"/>.</summary>
    /// <param name="deckSource">A public deck URL or pasted decklist text.</param>
    /// <param name="deckName">Optional display name for the deck (used in the ChatGPT prompt).</param>
    /// <param name="options">Mode + commander-importance knobs; defaults to Casual / Standard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ManabaseAnalysisResult> AnalyzeAsync(
        string deckSource,
        string? deckName,
        ManabaseAnalysisOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve the deck and detect its reduced/alternative-cost suggestions WITHOUT running the
    /// (expensive) castability simulation. Backs the "Load deck" step so the user can review and
    /// edit the detected overrides before analysis.
    /// </summary>
    /// <param name="deckSource">A public deck URL or pasted decklist text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ManabaseLoadResult> LoadAsync(
        string deckSource,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The user-selected analysis knobs threaded from the form into the Core analyzer. Bundled into
/// one object so the parameter list does not telescope as more modes are added. Defaults keep the
/// historic Casual / Standard behavior for any caller that omits them.
/// </summary>
public sealed class ManabaseAnalysisOptions
{
    /// <summary>The analysis profile (Casual default, cEDH lowers the land target).</summary>
    public ManabaseMode Mode { get; init; } = ManabaseMode.Casual;

    /// <summary>How heavily to weight the commander's colors (Standard default).</summary>
    public CommanderImportance CommanderImportance { get; init; } = CommanderImportance.Standard;

    /// <summary>
    /// Optional per-card effective-cost overrides (card name → canonical braced cost). Replaces the
    /// printed cost in the castability math for alt/reduced-cost cards. Empty/null = no overrides.
    /// </summary>
    public IReadOnlyDictionary<string, string>? CostOverrides { get; init; }
}

/// <summary>The outcome of a mana-base analysis: the report plus presentation context.</summary>
/// <param name="Report">The computed Karsten §6 report.</param>
/// <param name="InputSummary">Short human summary of what was analyzed.</param>
/// <param name="Unresolved">Card names Scryfall could not resolve (excluded from the math).</param>
/// <param name="ImportWarning">Optional notice from the deck importer (e.g. a fallback path).</param>
/// <param name="ChatGptSwapPrompt">Paste-ready prompt asking an LLM for specific land swaps.</param>
/// <param name="Suggestions">Auto-detected alt/reduced-cost suggestions to pre-populate the override box.</param>
/// <param name="Verdict">Optional synthesized plain-language verdict (Casual only when the flag is on).</param>
/// <param name="Budget">Optional ramp/draw slot-budget advisory (Casual only when the flag is on).</param>
/// <param name="ShowPlainLanguage">Whether the UI should surface the plain-language glosses/verdict gate.</param>
public sealed record ManabaseAnalysisResult(
    ManabaseReport Report,
    string InputSummary,
    IReadOnlyList<string> Unresolved,
    string? ImportWarning,
    string ChatGptSwapPrompt,
    IReadOnlyList<CostSuggestion> Suggestions,
    ManabaseVerdict? Verdict,
    ManabaseRampDrawBudget? Budget,
    bool ShowPlainLanguage);

/// <summary>
/// The outcome of the cheap "Load deck" step: the deck resolved and classified, with its detected
/// cost suggestions, but no simulation/report. Feeds the review-and-edit-then-analyze flow.
/// </summary>
/// <param name="InputSummary">Short human summary (card/land counts) of what was loaded.</param>
/// <param name="Unresolved">Card names Scryfall could not resolve (excluded from the math).</param>
/// <param name="ImportWarning">Optional notice from the deck importer (e.g. a fallback path).</param>
/// <param name="Suggestions">Auto-detected alt/reduced-cost suggestions to pre-populate the override box.</param>
public sealed record ManabaseLoadResult(
    string InputSummary,
    IReadOnlyList<string> Unresolved,
    string? ImportWarning,
    IReadOnlyList<CostSuggestion> Suggestions);

/// <inheritdoc />
public sealed class ManabaseAnalysisService : IManabaseAnalysisService
{
    // Scryfall's collection endpoint accepts at most 75 identifiers per request.
    private const int ScryfallBatchSize = 75;

    // Abuse caps for this anonymous public endpoint: bound the pasted payload and the number
    // of cards so one request can't force unbounded allocations or upstream Scryfall calls.
    // A Commander deck is ~100 cards; these leave generous headroom while rejecting abuse.
    private const int MaxDeckSourceChars = 100_000;
    private const int MaxDeckCards = 500;

    // Only these boards make up the deck under analysis; a sideboard/maybeboard would skew the
    // land target.
    private static readonly HashSet<string> AnalyzedBoards =
        new(StringComparer.OrdinalIgnoreCase) { "mainboard", "commander" };

    /// <summary>
    /// MQ-02 flag key: when enabled, the castability rows credit each source its full mana amount
    /// (Sol Ring = 2, etc.). Seeded ON after the Phase-70 flag baseline (8 decks, no verdict flips).
    /// </summary>
    public const string ManaQuantityFlagKey = "analysis.manabase.source-mana-quantity";

    /// <summary>
    /// MQ-03 flag key: when enabled, the Karsten ramp/draw land-target credit is narrowed to
    /// repeatable ramp + true draw (one-shot rituals / Treasure-makers no longer lower the target).
    /// Seeded ON after the Phase-70 flag baseline. Read BEFORE classification (the credit is computed
    /// in the classifier, not the analyzer).
    /// </summary>
    public const string RampCreditV2FlagKey = "analysis.manabase.ramp-credit-v2";

    /// <summary>
    /// MQ-05 flag key: when enabled, the castability rows' London mulligan keeps multi-color hands
    /// only when the opening lands show enough distinct colors (count-only otherwise). Cast%-affecting
    /// on 2+ color decks; mono decks are unchanged. Seeded ON after the Phase-70 flag baseline.
    /// </summary>
    public const string ColorAwareMulliganFlagKey = "analysis.manabase.color-aware-mulligan";

    /// <summary>
    /// MQ-03 70-03b flag key: when enabled, repeatable land-ramp spells (Cultivate / Rampant Growth)
    /// are modeled in the castability simulator as colorless ramp sources so the fetched land's mana is
    /// credited (closing the sim ↔ regression gap). Seeded ON after the Phase-70 land-ramp baseline.
    /// </summary>
    public const string LandRampSimFlagKey = "analysis.manabase.land-ramp-sim";

    /// <summary>
    /// MQ-health-band flag key: when enabled, the composite-weakest color's worst-spell cast %
    /// feeds the health-band verdict (Functional→Workable when below the mode's support threshold).
    /// Seeded OFF — promoted to ON after a full 9-deck calibration regression guard passes.
    /// </summary>
    public const string HealthBandCastabilityFlagKey = "analysis.manabase.health-band-castability";

    /// <summary>
    /// MQ-health-band headline-floor flag key: when enabled, a strong headline castability result
    /// can narrowly promote a land-short NeedsWork verdict to Workable. Seeded OFF.
    /// </summary>
    public const string HealthBandHeadlineFloorFlagKey = "analysis.manabase.health-band-headline-floor";

    /// <summary>
    /// Phase-71 flag key: when enabled, Casual mode computes a deterministic plain-language verdict
    /// plus ramp/draw budget advisory; cEDH uses the same gate for UI glosses only. Seeded OFF.
    /// </summary>
    public const string PlainLanguageVerdictFlagKey = "manabase.plain-language-verdict";

    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly IScryfallCardResolver _scryfallCardResolver;
    private readonly IFeatureFlagCache? _featureFlags;
    private readonly ILogger<ManabaseAnalysisService> _logger;

    /// <summary>Creates the analysis service.</summary>
    public ManabaseAnalysisService(
        IDeckEntryLoader deckEntryLoader,
        IScryfallCardResolver scryfallCardResolver,
        IFeatureFlagCache? featureFlags = null,
        ILogger<ManabaseAnalysisService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);

        _deckEntryLoader = deckEntryLoader;
        _scryfallCardResolver = scryfallCardResolver;
        _featureFlags = featureFlags;
        _logger = logger ?? NullLogger<ManabaseAnalysisService>.Instance;
    }

    /// <inheritdoc />
    public async Task<ManabaseAnalysisResult> AnalyzeAsync(
        string deckSource,
        string? deckName,
        ManabaseAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ManabaseAnalysisOptions();

        // MQ-03: read BEFORE classification — the ramp/draw land-target credit AND the 70-03b land-ramp
        // sim source are both built in the classifier, so reading them after Resolve would be too late.
        bool rampCreditV2 = IsFlagOn(RampCreditV2FlagKey);
        bool landRampSim = IsFlagOn(LandRampSimFlagKey);

        ResolvedManabaseDeck resolved = await ResolveAndClassifyAsync(deckSource, rampCreditV2, landRampSim, cancellationToken)
            .ConfigureAwait(false);

        // MQ-02: read the flag and pass it down, so the simulator stays a pure function of its
        // arguments. Both flags read fail-safe OFF (a missing/unseeded key must NOT turn a
        // safety-gated experiment on — IFeatureFlagCache.IsEnabled defaults missing keys ON, so use
        // Snapshot().TryGetValue via IsFlagOn instead).
        bool useManaQuantity = IsFlagOn(ManaQuantityFlagKey);

        // MQ-05: read the color-aware-mulligan flag and pass it down. Fail-safe OFF, same as the others.
        bool colorAwareMulligan = IsFlagOn(ColorAwareMulliganFlagKey);

        // P4 gated-ramp shares the land-ramp-sim flag: when ramp is modeled in the sim, also gate its
        // credit on the ramp's own colored cost being payable (mirrors 17Lands; corrects the optimism).
        // MQ-health-band: couple the verdict tier to the sim's composite-worst-color cast %. Fail-safe
        // OFF — seeded OFF; promoted to ON once the 9-deck calibration regression guard confirms no
        // Solid/Excellent deck regresses.
        bool useHealthBandCastability = IsFlagOn(HealthBandCastabilityFlagKey);
        bool useHealthBandHeadlineFloor = IsFlagOn(HealthBandHeadlineFloorFlagKey);
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            resolved.Deck, options.Mode, options.CommanderImportance, options.CostOverrides,
            useManaQuantity, colorAwareMulligan, gateRampOnCastable: landRampSim,
            useHealthBandCastability: useHealthBandCastability,
            useHealthBandHeadlineFloor: useHealthBandHeadlineFloor);

        bool plainLanguage = IsFlagOn(PlainLanguageVerdictFlagKey);
        ManabaseRampDrawBudget? budget = null;
        ManabaseVerdict? verdict = null;
        string swapPrompt;

        if (plainLanguage)
        {
            if (options.Mode == ManabaseMode.Casual)
            {
                budget = ManabaseRampDrawBudgetCalculator.Calculate(resolved.Deck);
                verdict = ManabaseVerdictSynthesizer.Synthesize(report, options.Mode, budget);
            }

            swapPrompt = ManabaseSwapPromptBuilder.Build(
                report, deckName, resolved.DecklistText, options.Mode, verdict, budget);
        }
        else
        {
            swapPrompt = ManabaseSwapPromptBuilder.Build(report, deckName, resolved.DecklistText, options.Mode);
        }

        return new ManabaseAnalysisResult(
            report, resolved.InputSummary, resolved.Unresolved, resolved.FallbackNotice,
            swapPrompt, resolved.Deck.CostSuggestions, verdict, budget, plainLanguage);
    }

    /// <inheritdoc />
    public async Task<ManabaseLoadResult> LoadAsync(
        string deckSource,
        CancellationToken cancellationToken = default)
    {
        // Load surfaces cost suggestions only; neither the ramp-credit land target nor the land-ramp sim
        // source is used here, so the flag values are immaterial — pass false.
        ResolvedManabaseDeck resolved = await ResolveAndClassifyAsync(deckSource, rampCreditV2: false, landRampSim: false, cancellationToken)
            .ConfigureAwait(false);

        // No simulation here — Load just surfaces the detected cost suggestions for review/edit.
        return new ManabaseLoadResult(
            resolved.InputSummary, resolved.Unresolved, resolved.FallbackNotice, resolved.Deck.CostSuggestions);
    }

    // Shared front half of both entry points: validate input, load + board-filter the deck, resolve
    // every card through Scryfall, and classify it into a ManabaseDeck (which carries the detected
    // cost suggestions). Stops short of the castability simulation so Load can reuse it cheaply.
    // True only when the named flag exists in the snapshot AND is enabled. Fail-safe OFF: a missing
    // key returns false (unlike IFeatureFlagCache.IsEnabled, which defaults missing keys ON).
    private bool IsFlagOn(string key)
        => _featureFlags is { } flags
            && flags.Snapshot().TryGetValue(key, out bool enabled)
            && enabled;

    private async Task<ResolvedManabaseDeck> ResolveAndClassifyAsync(
        string deckSource,
        bool rampCreditV2,
        bool landRampSim,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deckSource))
        {
            throw new InvalidOperationException("Provide a public deck URL or paste a decklist.");
        }

        if (deckSource.Length > MaxDeckSourceChars)
        {
            throw new InvalidOperationException("That deck input is too large to analyze.");
        }

        DeckSourceLoadResult load;
        try
        {
            load = await _deckEntryLoader.LoadFromSourceAsync(deckSource, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DeckParseException exception)
        {
            // Surface a parse failure as a user-facing validation error, not a 500.
            throw new InvalidOperationException(exception.Message, exception);
        }

        var deckCards = load.Entries
            .Where(e => AnalyzedBoards.Contains(e.Board))
            .ToList();

        if (deckCards.Count == 0)
        {
            throw new InvalidOperationException("No mainboard or commander cards were found in that deck.");
        }

        if (deckCards.Count > MaxDeckCards)
        {
            throw new InvalidOperationException($"That deck has too many cards to analyze (limit {MaxDeckCards}).");
        }

        ScryfallCardNameIndex index = await ResolveCardsAsync(deckCards, cancellationToken).ConfigureAwait(false);

        var deckEntries = new List<DeckCardEntry>();
        var unresolved = new List<string>();
        foreach (DeckEntry entry in deckCards)
        {
            ScryfallCardData? card;
            if (!index.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out card))
            {
                // The batch lookup missed this card — typically an exact printing Scryfall has no
                // record of (e.g. an etched/promo collector number). Reuse the shared exact-name
                // fallback that the comparison/analysis paths already use, then cache it in the
                // index so duplicate entries don't re-query.
                ScryfallCard? fallback = await _scryfallCardResolver
                    .SearchFallbackCardAsync(entry.Name, cancellationToken).ConfigureAwait(false);
                if (fallback is not null)
                {
                    card = ScryfallCardDataMapper.ToCardData(fallback);
                    index.Add(card);
                }
            }

            if (card is not null)
            {
                deckEntries.Add(new DeckCardEntry
                {
                    Card = card,
                    Quantity = entry.Quantity,
                    IsCommander = string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase),
                });
            }
            else
            {
                unresolved.Add(entry.Name);
            }
        }

        if (deckEntries.Count == 0)
        {
            throw new InvalidOperationException("Scryfall could not resolve any of the deck's cards; try again shortly.");
        }

        IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true, rampCreditV2: rampCreditV2, landRampSim: landRampSim);

        string decklistText = string.Join(
            "\n",
            deckCards.Select(e => $"{e.Quantity} {e.Name}"));

        // Land count matches ManabaseReport.ActualLands (the analyzer counts IsLand sources the
        // same way), so the loaded summary reads identically to the analyzed one.
        int landCount = deck.Sources.Count(s => s.IsLand);
        int cardCount = deckCards.Sum(e => e.Quantity);
        string inputSummary = $"{cardCount} cards · {landCount} lands"
            + (unresolved.Count > 0 ? $" · {unresolved.Count} unresolved" : string.Empty);

        return new ResolvedManabaseDeck(deck, unresolved, load.FallbackNotice, decklistText, inputSummary);
    }

    // Internal carrier for the shared resolve+classify stage (no report yet).
    private sealed record ResolvedManabaseDeck(
        ManabaseDeck Deck,
        IReadOnlyList<string> Unresolved,
        string? FallbackNotice,
        string DecklistText,
        string InputSummary);

    // Batch-resolve the deck's cards through Scryfall's collection endpoint, preferring an exact
    // printing (set + collector number) so alternate / flavor names still resolve.
    private async Task<ScryfallCardNameIndex> ResolveCardsAsync(
        IReadOnlyList<DeckEntry> deckCards,
        CancellationToken cancellationToken)
    {
        // Distinct identifiers: printing key when known, else a name key.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var identifiers = new List<object>();
        foreach (DeckEntry entry in deckCards)
        {
            string? printing = ScryfallCardNameIndex.PrintingKey(entry.SetCode, entry.CollectorNumber);
            string key = printing ?? $"name:{entry.Name}";
            if (!seen.Add(key))
            {
                continue;
            }

            identifiers.Add(printing is not null
                ? new { set = entry.SetCode, collector_number = entry.CollectorNumber }
                : (object)new { name = entry.Name });
        }

        var index = new ScryfallCardNameIndex();
        for (int offset = 0; offset < identifiers.Count; offset += ScryfallBatchSize)
        {
            var batch = identifiers.Skip(offset).Take(ScryfallBatchSize).ToArray();
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new { identifiers = batch });

            RestResponse<ScryfallCollectionResponse> response =
                await _scryfallCardResolver.ExecuteCollectionAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during mana-base analysis.",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            foreach (ScryfallCard card in response.Data.Data)
            {
                index.Add(ScryfallCardDataMapper.ToCardData(card));
            }
        }

        return index;
    }
}
