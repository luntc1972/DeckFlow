using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
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

    /// <summary>Optional user-supplied companion designator; blank/null means "no manual override".</summary>
    public string? CompanionDesignator { get; init; }
}

/// <summary>The outcome of a mana-base analysis: the report plus presentation context.</summary>
/// <param name="Report">The computed Karsten §6 report.</param>
/// <param name="InputSummary">Short human summary of what was analyzed.</param>
/// <param name="Unresolved">Card names Scryfall could not resolve (excluded from the math).</param>
/// <param name="ImportWarning">Optional notice from the deck importer (e.g. a fallback path).</param>
/// <param name="PromptSwapPrompt">Paste-ready prompt asking an LLM for specific land swaps.</param>
/// <param name="Suggestions">Auto-detected alt/reduced-cost suggestions to pre-populate the override box.</param>
/// <param name="Verdict">Optional synthesized plain-language verdict (Casual only when the flag is on).</param>
/// <param name="Budget">Optional ramp/draw slot-budget advisory (Casual only when the flag is on).</param>
/// <param name="ShowPlainLanguage">Whether the UI should surface the plain-language glosses/verdict gate.</param>
public sealed record ManabaseAnalysisResult(
    ManabaseReport Report,
    string InputSummary,
    IReadOnlyList<string> Unresolved,
    string? ImportWarning,
    string PromptSwapPrompt,
    IReadOnlyList<CostSuggestion> Suggestions,
    ManabaseVerdict? Verdict,
    ManabaseRampDrawBudget? Budget,
    bool ShowPlainLanguage)
{
    /// <summary>Whether the command-zone castability affordances were enabled for this result.</summary>
    public bool CommanderCastabilityEnabled { get; init; }

    /// <summary>Whether the tap-analyzer card and paste-artifact section were enabled for this result.</summary>
    public bool ShowTapAnalyzer { get; init; }

    /// <summary>Whether the opening-hand / mulligan-evaluator block was enabled for this result.</summary>
    public bool ShowMulliganEval { get; init; }

    /// <summary>Whether the plan-presence opener stat was enabled (flag on) for this result.</summary>
    public bool ShowPlanPresence { get; init; }

    /// <summary>Optional companion castability row modeled outside the analyzed 99.</summary>
    public CardCastability? CompanionRow { get; init; }

    /// <summary>
    /// Override card names that matched no card in the analyzed deck (typo or not-in-deck), so their
    /// line was silently dropped. Surfaced to the user as "not applied" feedback. Empty when every
    /// override bound to a spell.
    /// </summary>
    public IReadOnlyList<string> UnmatchedOverrideNames { get; init; } = Array.Empty<string>();
}

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
    private const int MaxCompanionNameLength = 200;

    // Only these boards make up the deck under analysis; a sideboard/maybeboard would skew the
    // land target.
    private static readonly HashSet<string> AnalyzedBoards =
        new(StringComparer.OrdinalIgnoreCase) { "mainboard", "commander" };

    /// <summary>
    /// Flag key: bundles the settled sim-accuracy knobs (mana quantity, ramp-credit-v2,
    /// color-aware mulligan, land-ramp sim, health-band headline floor, pay-life untapped, and MDFC
    /// land backs modeled as real lands.
    /// Seeded ON.
    /// </summary>
    public const string AccuracyFlagKey = "analysis.manabase.accuracy";

    /// <summary>
    /// MQ-health-band flag key: when enabled, the composite-weakest color's worst-spell cast %
    /// feeds the health-band verdict (Functional→Workable when below the mode's support threshold).
    /// Seeded OFF — promoted to ON after a full 9-deck calibration regression guard passes.
    /// </summary>
    public const string HealthBandCastabilityFlagKey = "analysis.manabase.health-band-castability";

    /// <summary>
    /// Phase-71 flag key: when enabled, Casual mode computes a deterministic plain-language verdict
    /// plus ramp/draw budget advisory; cEDH uses the same gate for UI glosses only. Seeded OFF.
    /// </summary>
    public const string PlainLanguageVerdictFlagKey = "analysis.manabase.plain-language-verdict";

    /// <summary>
    /// Phase-72 flag key: seeded OFF; gates the command-zone castability callout plus companion
    /// modeling in Casual mode.
    /// </summary>
    public const string CommanderCastabilityFlagKey = "analysis.manabase.commander-castability";

    /// <summary>
    /// Phase-75 flag key: seeded OFF; gates the tap-analyzer card on the mana base page plus the
    /// "Untapped Sources:" block in the paste artifact. Read fail-safe OFF; off = byte-identical output.
    /// </summary>
    public const string TapAnalyzerFlagKey = "analysis.manabase.tap-analyzer";

    /// <summary>
    /// Phase-81 flag key: seeded OFF; gates the opening-hand / mulligan-evaluator block on the mana
    /// base page plus the "Opening Hand (mulligan)" block in the paste artifact. Read fail-safe OFF;
    /// off = byte-identical output.
    /// </summary>
    public const string MulliganEvalFlagKey = "analysis.manabase.mulligan-eval";

    /// <summary>
    /// Plan-presence flag key: seeded OFF. Gates the "with a plan" opener stat AND the only new I/O it
    /// needs — the per-card category lookup and the Commander Spellbook combo fetch. Read fail-safe OFF,
    /// so the default manabase path takes on no extra cost until an admin enables the beta stat.
    /// </summary>
    public const string PlanPresenceFlagKey = "analysis.manabase.plan-presence";

    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly IScryfallCardResolver _scryfallCardResolver;
    private readonly IFeatureFlagCache? _featureFlags;
    private readonly ICategoryKnowledgeStore? _categoryKnowledge;
    private readonly ICommanderSpellbookService? _spellbook;
    private readonly ILogger<ManabaseAnalysisService> _logger;

    /// <summary>Creates the analysis service.</summary>
    public ManabaseAnalysisService(
        IDeckEntryLoader deckEntryLoader,
        IScryfallCardResolver scryfallCardResolver,
        IFeatureFlagCache? featureFlags = null,
        ICategoryKnowledgeStore? categoryKnowledge = null,
        ICommanderSpellbookService? spellbook = null,
        ILogger<ManabaseAnalysisService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);

        _deckEntryLoader = deckEntryLoader;
        _scryfallCardResolver = scryfallCardResolver;
        _featureFlags = featureFlags;
        _categoryKnowledge = categoryKnowledge;
        _spellbook = spellbook;
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

        // Read the bundled manabase accuracy flag BEFORE classification — the ramp/draw land-target
        // credit, land-ramp sim source, and pay-life untapped land handling are all built before the
        // analyzer path runs, so reading this after Resolve would be too late.
        bool accuracy = IsFlagOn(AccuracyFlagKey);
        bool rampCreditV2 = accuracy;
        bool landRampSim = accuracy;
        bool payLifeUntapped = accuracy;
        bool mdfcAsLand = accuracy;
        bool checkLandUntapped = accuracy;
        bool commanderCastability = IsFlagOn(CommanderCastabilityFlagKey);
        bool showTapAnalyzer = IsFlagOn(TapAnalyzerFlagKey);
        bool showMulliganEval = IsFlagOn(MulliganEvalFlagKey);
        // Read BEFORE resolve: the flag gates the plan-role tagging (and its category + Spellbook I/O)
        // done during classification. Off = no extra I/O and PlanRoles stay None (byte-identical path).
        // ALSO require the opening-hand block (mulligan-eval): the "With a plan" line renders only inside
        // that block, so enabling plan-presence alone must not do the extra I/O + sim for a line that can
        // never show (Codex MED). Both flags on = the stat runs and surfaces.
        bool showPlanPresence = IsFlagOn(PlanPresenceFlagKey) && showMulliganEval;

        ResolvedManabaseDeck resolved = await ResolveAndClassifyAsync(
                deckSource,
                rampCreditV2,
                landRampSim,
                payLifeUntapped,
                mdfcAsLand,
                checkLandUntapped,
                commanderCastability,
                classifyPlanRoles: showPlanPresence,
                options.Mode,
                options.CompanionDesignator,
                cancellationToken)
            .ConfigureAwait(false);

        // Fan the bundled accuracy flag out to the existing Core bools so the internal analyzer/classifier
        // plumbing stays stable. Fail-safe OFF still comes from IsFlagOn above.
        bool useManaQuantity = accuracy;
        bool colorAwareMulligan = accuracy;

        // P4 gated-ramp is always on (efficacy R2 M3): before the sim credits a ramp piece's mana it
        // verifies the ramp's OWN colored cost is payable from the current board (mirrors 17Lands),
        // otherwise a {G} dork gets deployed from a green-less hand and its mana inflates cast %. This
        // was previously coupled to the land-ramp-sim flag, but rocks and dorks are modeled in the sim
        // unconditionally — land-ramp-sim only adds land-ramp SPELLS (Cultivate) — so the gate is
        // relevant whenever the deck runs any ramp, not only when land-ramp spells are simulated.
        // Decoupled and hardcoded on: it is pure correctness, was already live in prod (land-ramp-sim
        // enabled), and the 9-deck calibration guard confirms a <=1pt delta with no band change.
        // MQ-health-band: couple the verdict tier to the sim's composite-worst-color cast %. Fail-safe
        // OFF — seeded OFF; promoted to ON once the 9-deck calibration regression guard confirms no
        // Solid/Excellent deck regresses.
        bool useHealthBandCastability = IsFlagOn(HealthBandCastabilityFlagKey);
        bool useHealthBandHeadlineFloor = accuracy;
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            resolved.Deck, options.Mode, options.CommanderImportance, options.CostOverrides,
            useManaQuantity, colorAwareMulligan, gateRampOnCastable: true,
            useHealthBandCastability: useHealthBandCastability,
            useHealthBandHeadlineFloor: useHealthBandHeadlineFloor);

        bool plainLanguage = IsFlagOn(PlainLanguageVerdictFlagKey);
        ManabaseRampDrawBudget? budget = null;
        ManabaseVerdict? verdict = null;
        CardCastability? companionRow = null;
        string swapPrompt;

        if (commanderCastability && resolved.CompanionCard is not null)
        {
            ParsedManaCost printedCost = ManaCostParser.Parse(resolved.CompanionCard.ManaCost);
            SpellRequirement companionRequirement = ManabaseAnalyzer.BuildCompanionSpell(
                resolved.CompanionCard.Name, printedCost, resolved.CompanionCard.Cmc);
            companionRow = ManabaseAnalyzer.SimulateCompanion(
                resolved.Deck,
                companionRequirement,
                useManaQuantity,
                colorAwareMulligan,
                gateRampOnCastable: true); // always on — see the report Analyze call above (R2 M3)
        }

        if (plainLanguage)
        {
            if (options.Mode == ManabaseMode.Casual)
            {
                budget = ManabaseRampDrawBudgetCalculator.Calculate(resolved.Deck);
                verdict = ManabaseVerdictSynthesizer.Synthesize(report, options.Mode, budget);
            }

            swapPrompt = ManabaseSwapPromptBuilder.Build(
                report, deckName, resolved.DecklistText, options.Mode, verdict, budget, commanderCastability, companionRow);
        }
        else
        {
            swapPrompt = ManabaseSwapPromptBuilder.Build(
                report, deckName, resolved.DecklistText, options.Mode, null, null, commanderCastability, companionRow);
        }

        return new ManabaseAnalysisResult(
            report, resolved.InputSummary, resolved.Unresolved, resolved.FallbackNotice,
            swapPrompt, resolved.Deck.CostSuggestions, verdict, budget, plainLanguage)
        {
            CommanderCastabilityEnabled = commanderCastability,
            CompanionRow = companionRow,
            ShowTapAnalyzer = showTapAnalyzer,
            ShowMulliganEval = showMulliganEval,
            ShowPlanPresence = showPlanPresence,
            UnmatchedOverrideNames = report.UnmatchedOverrideNames,
        };
    }

    /// <inheritdoc />
    public async Task<ManabaseLoadResult> LoadAsync(
        string deckSource,
        CancellationToken cancellationToken = default)
    {
        // Load surfaces cost suggestions only; neither the ramp-credit land target nor the land-ramp sim
        // source is used here, so the flag values are immaterial — pass false.
        ResolvedManabaseDeck resolved = await ResolveAndClassifyAsync(
                deckSource,
                rampCreditV2: false,
                landRampSim: false,
                payLifeUntapped: false,
                mdfcAsLand: false,
                checkLandUntapped: false,
                commanderCastability: false,
                classifyPlanRoles: false,
                mode: ManabaseMode.Casual,
                companionDesignator: null,
                cancellationToken)
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
        bool payLifeUntapped,
        bool mdfcAsLand,
        bool checkLandUntapped,
        bool commanderCastability,
        bool classifyPlanRoles,
        ManabaseMode mode,
        string? companionDesignator,
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

        // Moxfield plaintext exports carry no "Commander" section header — the commander is
        // simply the leading card. Reflag inferred commander(s) to the commander board so the
        // analyzer weights their colors and the callout names them, matching the deck-analysis
        // tool's behavior. No-op when the source already tagged a commander board.
        var entries = ReflagInferredCommanders(load.Entries);

        var deckCards = entries
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

        string? companionName = commanderCastability
            ? ResolveCompanionName(companionDesignator, load.DetectedCompanionName)
            : null;
        string? normalizedCompanionName = companionName is null ? null : CardNormalizer.Normalize(companionName);
        DeckEntry? excludedCompanionEntry = normalizedCompanionName is null
            ? null
            : deckCards.FirstOrDefault(entry =>
                string.Equals(entry.Board, "mainboard", StringComparison.OrdinalIgnoreCase)
                && string.Equals(CardNormalizer.Normalize(entry.Name), normalizedCompanionName, StringComparison.Ordinal));

        ScryfallCardNameIndex index = await ResolveCardsAsync(deckCards, cancellationToken).ConfigureAwait(false);
        ScryfallCardData? companionCard = null;
        if (commanderCastability && companionName is not null)
        {
            companionCard = excludedCompanionEntry is not null
                ? await ResolveCompanionFromDeckEntryAsync(index, excludedCompanionEntry, cancellationToken).ConfigureAwait(false)
                : await ResolveSingleCardAsync(companionName, cancellationToken).ConfigureAwait(false);
        }

        var deckEntries = new List<DeckCardEntry>();
        var unresolved = new List<string>();
        foreach (DeckEntry entry in deckCards)
        {
            if (excludedCompanionEntry is not null && ReferenceEquals(entry, excludedCompanionEntry))
            {
                continue;
            }

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
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true, rampCreditV2: rampCreditV2, landRampSim: landRampSim, payLifeUntapped: payLifeUntapped, mdfcAsLand: mdfcAsLand, checkLandUntapped: checkLandUntapped);

        if (classifyPlanRoles)
        {
            deck = await TagPlanRolesAsync(deck, facts, deckCards, mode, cancellationToken).ConfigureAwait(false);
        }

        string decklistText = string.Join(
            "\n",
            deckCards.Select(e => $"{e.Quantity} {e.Name}"));

        // Land count matches ManabaseReport.ActualLands (the analyzer counts IsLand sources the
        // same way), so the loaded summary reads identically to the analyzed one.
        int landCount = deck.Sources.Count(s => s.IsLand);
        int cardCount = deckCards.Sum(e => e.Quantity);
        string inputSummary = $"{cardCount} cards · {landCount} lands"
            + (unresolved.Count > 0 ? $" · {unresolved.Count} unresolved" : string.Empty);

        return new ResolvedManabaseDeck(deck, unresolved, load.FallbackNotice, decklistText, inputSummary, companionCard);
    }

    /// <summary>
    /// Tag each spell with its win-directed <see cref="PlanRole"/>s for the plan-presence stat. Fetches
    /// the deck's Commander Spellbook combo pieces once and each spell's crowd categories, both
    /// fail-open (a network/DB error yields no roles for that card, never a failed analysis). Only
    /// called when the plan-presence flag is on, so this extra I/O never touches the default path.
    /// </summary>
    private async Task<ManabaseDeck> TagPlanRolesAsync(
        ManabaseDeck deck,
        IReadOnlyList<CardFact> facts,
        IReadOnlyList<DeckEntry> deckCards,
        ManabaseMode mode,
        CancellationToken cancellationToken)
    {
        // Source 2 (combo pieces), fetched once. Fail-open: a Spellbook outage leaves the set empty.
        var comboNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_spellbook is not null)
        {
            try
            {
                CommanderSpellbookResult? combos =
                    await _spellbook.FindCombosAsync(deckCards, cancellationToken).ConfigureAwait(false);
                if (combos is not null)
                {
                    foreach (SpellbookCombo combo in combos.IncludedCombos)
                    {
                        foreach (string cardName in combo.CardNames)
                        {
                            comboNames.Add(cardName);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Plan-presence: Commander Spellbook fetch failed; continuing without combo roles.");
            }
        }

        var factByName = new Dictionary<string, CardFact>(StringComparer.OrdinalIgnoreCase);
        foreach (CardFact fact in facts)
        {
            factByName[fact.Name] = fact;
        }

        // Source 1 (crowd categories): ONE batched lookup for the spells we will actually classify (those
        // with a resolved fact). A per-card loop here issued one DB query per non-land card, which serially
        // exhausted the request timeout on a full decklist (~65 sequential Postgres round-trips ~= 20s).
        // Batching collapses it to a single query.
        IReadOnlyDictionary<string, IReadOnlyList<string>> categoriesByName =
            await GetCategoriesFailOpenAsync(
                deck.Spells.Where(s => factByName.ContainsKey(s.Name)).Select(s => s.Name).ToList(),
                cancellationToken).ConfigureAwait(false);

        var tagged = new List<SpellRequirement>(deck.Spells.Count);
        foreach (SpellRequirement spell in deck.Spells)
        {
            PlanRole roles = PlanRole.None;
            if (factByName.TryGetValue(spell.Name, out CardFact? fact))
            {
                IReadOnlyList<string> categories = categoriesByName.TryGetValue(spell.Name, out IReadOnlyList<string>? hit)
                    ? hit
                    : Array.Empty<string>();
                roles = PlanRoleClassifier.Classify(fact, categories, comboNames.Contains(spell.Name), mode);
            }

            tagged.Add(spell with { PlanRoles = roles });
        }

        return deck with { Spells = tagged };
    }

    // Source 1 (crowd categories), fail-open for the whole batch so a DB hiccup drops every card to the
    // heuristic tier rather than failing the analysis. One query, never one-per-card.
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesFailOpenAsync(
        IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken)
    {
        if (_categoryKnowledge is null || cardNames.Count == 0)
        {
            return EmptyCategories;
        }

        try
        {
            return await _categoryKnowledge.GetCategoriesForNamesAsync(cardNames, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Plan-presence: batch category lookup failed; using heuristics only.");
            return EmptyCategories;
        }
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyCategories =
        new Dictionary<string, IReadOnlyList<string>>();

    // Reflags the leading Moxfield-ordering commander(s) to the commander board when the
    // source carried no explicit commander tag. Returns the input unchanged when a commander
    // board already exists or none can be inferred.
    private static List<DeckEntry> ReflagInferredCommanders(List<DeckEntry> entries)
    {
        IReadOnlyList<string> commanderNames = CommanderInference.InferLeadingCommanderNames(entries);
        if (commanderNames.Count == 0)
        {
            return entries;
        }

        var commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Only reflag the analyzed boards. The inferred commander is always a leading mainboard
        // entry, so restricting the promotion here keeps a same-named sideboard/maybeboard copy
        // from being pulled into the analyzed set as a second "commander".
        return entries
            .Select(entry => commanderNameSet.Contains(entry.Name)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                ? entry with { Board = "commander" }
                : entry)
            .ToList();
    }

    // Internal carrier for the shared resolve+classify stage (no report yet).
    private sealed record ResolvedManabaseDeck(
        ManabaseDeck Deck,
        IReadOnlyList<string> Unresolved,
        string? FallbackNotice,
        string DecklistText,
        string InputSummary,
        ScryfallCardData? CompanionCard);

    private static string? ResolveCompanionName(string? designator, string? detected)
        => BoundCompanionName(designator) ?? BoundCompanionName(detected);

    private static string? BoundCompanionName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string trimmed = name.Trim();
        return trimmed.Length <= MaxCompanionNameLength
            ? trimmed
            : trimmed[..MaxCompanionNameLength];
    }

    private async Task<ScryfallCardData?> ResolveCompanionFromDeckEntryAsync(
        ScryfallCardNameIndex index,
        DeckEntry companionEntry,
        CancellationToken cancellationToken)
    {
        if (index.TryResolve(companionEntry.Name, companionEntry.SetCode, companionEntry.CollectorNumber, out ScryfallCardData? hit))
        {
            return hit;
        }

        ScryfallCard? fallback = await _scryfallCardResolver
            .SearchFallbackCardAsync(companionEntry.Name, cancellationToken).ConfigureAwait(false);
        if (fallback is null)
        {
            return null;
        }

        ScryfallCardData data = ScryfallCardDataMapper.ToCardData(fallback);
        index.Add(data);
        return data;
    }

    private async Task<ScryfallCardData?> ResolveSingleCardAsync(string cardName, CancellationToken cancellationToken)
    {
        // The single-name Scryfall resolve (collection lookup + exact-name fallback) lives on the
        // resolver; this service only maps the result into its ScryfallCardData shape.
        ScryfallCard? card = await _scryfallCardResolver.ResolveSingleAsync(cardName, cancellationToken).ConfigureAwait(false);
        return card is null ? null : ScryfallCardDataMapper.ToCardData(card);
    }

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
