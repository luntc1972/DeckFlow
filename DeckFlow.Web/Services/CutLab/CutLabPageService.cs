using DeckFlow.Core.Analysis;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Processes Cut Lab intake requests into a rendered working-session result.</summary>
public interface ICutLabPageService
{
    /// <summary>Loads, validates, resolves, and serializes a Cut Lab working session.</summary>
    /// <param name="request">Current Cut Lab request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Result of Cut Lab page processing.</summary>
public sealed record CutLabProcessResult
{
    /// <summary>The resolved working-session state for the page.</summary>
    public CutLabState? State { get; init; }

    /// <summary>The serialized hidden-field JSON for the working session.</summary>
    public string? SerializedStateJson { get; init; }

    /// <summary>Non-commander pool count used by the 101-150 validation gate.</summary>
    public int CardCount { get; init; }

    /// <summary>Mainboard-only quantity loaded from the source before any Cut Lab board filtering.</summary>
    public int MainboardCardCount { get; init; }

    /// <summary>Sideboard quantity loaded from the source before any Cut Lab board filtering.</summary>
    public int SideboardCardCount { get; init; }

    /// <summary>Considering or maybeboard quantity loaded from the source before any Cut Lab board filtering.</summary>
    public int MaybeboardCardCount { get; init; }

    /// <summary>Per-board counts used for shared breakdown rendering and validation.</summary>
    public BoardCounts BoardCounts { get; init; } = new();

    /// <summary>Commander-banlist card names present in the rendered pool.</summary>
    public IReadOnlyList<string> BannedCardsPresent { get; init; } = [];

    /// <summary>True when the current pool has no banned cards.</summary>
    public bool IsLegal { get; init; }

    /// <summary>True when a commander must be selected manually before proceeding.</summary>
    public bool CommanderSelectionRequired { get; init; }

    /// <summary>Commander-eligible card choices shown when manual selection is required.</summary>
    public IReadOnlyList<string> CommanderChoices { get; init; } = [];

    /// <summary>Non-blocking warnings collected during processing.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>True when combo lookup completed successfully for this result.</summary>
    public bool ComboDataAvailable { get; init; }

    /// <summary>True when category lookup completed successfully for this result.</summary>
    public bool CategoryDataAvailable { get; init; }

    /// <summary>Per-card structural role assignments keyed by card name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> RoleAssignmentsByCardName { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-card text and printing details keyed by rendered card name.</summary>
    public IReadOnlyDictionary<string, CutLabCardTextView> CardTextByCardName { get; init; } =
        new Dictionary<string, CutLabCardTextView>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-card combo badge state and disclosure context keyed by normalized card name.</summary>
    public IReadOnlyDictionary<string, CutLabComboBadgeView> ComboBadgeByCardName { get; init; } =
        new Dictionary<string, CutLabComboBadgeView>(CutLabCardNames.Comparer);

    /// <summary>Resolved role-floor rows, including default provenance and user overrides.</summary>
    public IReadOnlyList<CutLabResolvedFloor> ResolvedFloors { get; init; } = [];

    /// <summary>Computed structural findings for the current pool.</summary>
    public CutLabStructuralFindingsResult Findings { get; init; } =
        new([], ComboDataAvailable: false, CategoryDataAvailable: false);

    /// <summary>The current derived working-list round plan for the rendered state.</summary>
    public CutLabRoundPlan? RoundPlan { get; init; }

    /// <summary>The server-computed deltas for the next proposal on the rendered state.</summary>
    public CutLabProposalDeltas? InitialProposalDeltas { get; init; }

    /// <summary>The server-computed metric snapshot for the current derived working list.</summary>
    public CutLabMetricSnapshot? CurrentSnapshot { get; init; }

    /// <summary>Actual lands in the current working-pool simulation, when available.</summary>
    public int? CurrentActualLands { get; init; }

    /// <summary>Target lands in the current working-pool simulation, when available.</summary>
    public double? CurrentTargetLands { get; init; }

    /// <summary>True when the commander-aware floor defaults layer is enabled for this render.</summary>
    public bool CommanderFloorsEnabled { get; init; }

    /// <summary>User-facing error for a hard failure, null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when a result state is available for rendering.</summary>
    public bool HasResult { get; init; }

    /// <summary>
    /// All known EDHREC commander themes for the resolved commander, ordered by deck count
    /// descending, for the plan panel's full checkbox list (not just the checked subset carried in
    /// <see cref="CutLabState.Intent"/>'s <c>PlanProfile</c>).
    /// </summary>
    public IReadOnlyList<CutLabCommanderTheme> AvailableCommanderThemes { get; init; } = [];

    /// <summary>True when the EDHREC commander-theme lookup for the plan panel was unavailable.</summary>
    public bool CommanderThemesUnavailable { get; init; }
}

/// <summary>Default Cut Lab page-service orchestrator.</summary>
internal sealed class CutLabPageService : ICutLabPageService
{
    /// <summary>
    /// Commander-aware role-floor defaults flag key: seeded OFF. When enabled, Cut Lab resolves
    /// the Phase 3 commander baseline layer and shows the Bracket/Commander floor columns; off =
    /// byte-identical to the pre-Phase-3 bracket-only surface.
    /// </summary>
    public const string CommanderFloorsFlagKey = "analysis.cut-lab.commander-floors";

    private static readonly HashSet<string> AnalyzedBoards =
        new(StringComparer.OrdinalIgnoreCase) { "mainboard", "commander" };

    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly IScryfallCardResolver _cardResolver;
    private readonly ICommanderBanListService _banListService;
    private readonly IManabaseBaselineProvider? _manabaseBaseline;
    private readonly ICedhLandBaselineProvider? _cedhBaseline;
    private readonly IRoleFloorBaselineProvider? _roleFloorBaseline;
    private readonly ICutLabAnalysisContextBuilder _analysisContextBuilder;
    private readonly ICutLabSimulationService _simulationService;
    private readonly ICutLabFloorResolver _floorResolver;
    private readonly IFeatureFlagCache? _featureFlags;
    private readonly ILogger<CutLabPageService> _logger;
    private readonly ICutLabPlanAffinityFactory _planAffinityFactory;
    private readonly IEdhrecCommanderThemeService _themeService;

    /// <summary>Creates the Cut Lab page service.</summary>
    /// <param name="deckEntryLoader">Deck loader for URL/paste imports.</param>
    /// <param name="cardResolver">Scryfall resolver for type-line lookup.</param>
    /// <param name="banListService">Commander banlist service.</param>
    /// <param name="manabaseBaseline">Optional bracket baseline dependency for structural analysis.</param>
    /// <param name="cedhBaseline">Optional cEDH commander baseline dependency for structural analysis.</param>
    /// <param name="roleFloorBaseline">Optional commander role-floor baseline dependency for structural analysis.</param>
    /// <param name="analysisContextBuilder">Optional shared builder for resolved-card, classification, and role-assignment analysis.</param>
    /// <param name="simulationService">Optional simulation service for baseline, current snapshot, and proposal-delta computation.</param>
    /// <param name="logger">Optional logger for non-blocking diagnostics.</param>
    /// <param name="featureFlags">Optional feature-flag cache for dark-launching commander-aware floor defaults.</param>
    /// <param name="floorResolver">Optional shared floor resolver used to re-derive defaults per request.</param>
    /// <param name="planAffinityFactory">Optional shared plan-affinity factory used to resolve the checked plan profile against the pool.</param>
    /// <param name="themeService">Optional EDHREC commander-theme source used to build the plan panel's theme list and resolve checked theme slugs.</param>
    public CutLabPageService(
        IDeckEntryLoader deckEntryLoader,
        IScryfallCardResolver cardResolver,
        ICommanderBanListService banListService,
        IManabaseBaselineProvider? manabaseBaseline = null,
        ICedhLandBaselineProvider? cedhBaseline = null,
        IRoleFloorBaselineProvider? roleFloorBaseline = null,
        ICutLabAnalysisContextBuilder? analysisContextBuilder = null,
        ICutLabSimulationService? simulationService = null,
        ILogger<CutLabPageService>? logger = null,
        IFeatureFlagCache? featureFlags = null,
        ICutLabFloorResolver? floorResolver = null,
        ICutLabPlanAffinityFactory? planAffinityFactory = null,
        IEdhrecCommanderThemeService? themeService = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(cardResolver);
        ArgumentNullException.ThrowIfNull(banListService);

        _deckEntryLoader = deckEntryLoader;
        _cardResolver = cardResolver;
        _banListService = banListService;
        _manabaseBaseline = manabaseBaseline;
        _cedhBaseline = cedhBaseline;
        _roleFloorBaseline = roleFloorBaseline;
        CutLabResolvedCardCache sharedResolvedCardCache = new();
        _analysisContextBuilder = analysisContextBuilder
            // Why: test-only fallback; the DI path always injects the shared ScryfallReferenceResolver,
            // so this instance gets a private collection cache rather than the shared singleton.
            ?? new CutLabAnalysisContextBuilder(
                cardResolver,
                sharedResolvedCardCache,
                new ScryfallReferenceResolver(cardResolver, new ScryfallCollectionCardCache()));
        _simulationService = simulationService
            ?? NoOpCutLabSimulationService.Instance;
        _featureFlags = featureFlags;
        _logger = logger ?? NullLogger<CutLabPageService>.Instance;
        _floorResolver = floorResolver
            ?? new CutLabFloorResolver(_manabaseBaseline, _cedhBaseline, _roleFloorBaseline, _featureFlags);
        _planAffinityFactory = planAffinityFactory ?? NullCutLabPlanAffinityFactory.Instance;
        _themeService = themeService ?? NullEdhrecCommanderThemeService.Instance;
    }

    /// <summary>
    /// Test-only probe for the DI guard that verifies the optional structural-analysis services are
    /// actually registered in the production container shape.
    /// </summary>
    internal bool HasStructuralAnalysisDependencies =>
        _manabaseBaseline is not null
        && _cedhBaseline is not null
        // Why: the provider itself is fail-open, but its absence from the container is a wiring defect,
        // not a degraded-data case, and the guard must keep those distinct.
        && _roleFloorBaseline is not null
        && !ReferenceEquals(_simulationService, NoOpCutLabSimulationService.Instance);

    /// <inheritdoc />
    public async Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<string>();
        var (_, _, _, deckSource) = DeckInputReconciler.Reconcile(
            request.DeckInputSource,
            request.DeckUrl,
            request.DeckText,
            request.DeckSource);

        if (string.IsNullOrWhiteSpace(deckSource))
        {
            return new CutLabProcessResult();
        }

        try
        {
            CutLabPoolValidator.ValidateSourceLength(deckSource.Length);
        }
        catch (InvalidOperationException exception)
        {
            return Error(exception.Message, warnings);
        }

        DeckSourceLoadResult load;
        try
        {
            load = await _deckEntryLoader.LoadFromSourceAsync(deckSource, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DeckParseException or InvalidOperationException)
        {
            return Error(exception.Message, warnings);
        }
        catch (HttpRequestException exception)
        {
            return Error(UpstreamErrorMessageBuilder.BuildScryfallMessage(exception), warnings);
        }

        if (!string.IsNullOrWhiteSpace(load.FallbackNotice))
        {
            warnings.Add(load.FallbackNotice);
        }

        var entries = ReflagInferredCommanders(load.Entries);
        IReadOnlySet<string> analyzedBoards = BuildAnalyzedBoards(request);
        EntryAnalysis analysis = AnalyzeEntries(entries, analyzedBoards);
        BoardCounts boardCounts = analysis.BoardCounts;
        List<DeckEntry> analyzedEntries = analysis.AnalyzedEntries;

        if (analyzedEntries.Count == 0)
        {
            return Error("No mainboard or commander cards were found in that deck.", warnings);
        }

        List<ResolvedCutLabEntry> resolvedEntries;
        try
        {
            resolvedEntries = await ResolveEntriesAsync(analyzedEntries, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return Error(UpstreamErrorMessageBuilder.BuildScryfallMessage(exception), warnings);
        }

        IReadOnlyList<CutLabPoolCard> resolutionPool = analyzedEntries
            .Select(entry => new CutLabPoolCard { Name = entry.Name, Quantity = entry.Quantity })
            .ToArray();
        if (_analysisContextBuilder.HasUnattemptedCards(resolutionPool))
        {
            // The builder records only names skipped after a swallowed transient failure. Re-resolve
            // the full pool so its known-missing cache continues to suppress confirmed typos.
            try
            {
                IReadOnlyList<ResolvedCutLabEntry> retriedEntries = await ResolveEntriesAsync(
                    analyzedEntries,
                    cancellationToken).ConfigureAwait(false);
                resolvedEntries = ReconcileResolvedEntries(resolvedEntries, retriedEntries.Select(entry => entry.Card));
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Cut Lab: recovery lookup failed; continuing with the initial resolution snapshot.");
            }
        }

        bool upperBoundValidationDeferred = _analysisContextBuilder.HasUnattemptedCards(resolutionPool);
        var commanderResolution = ResolveCommanderSelection(resolvedEntries, request.SelectedCommander);

        int nonCommanderCardCount = CountNonCommanderCards(analyzedEntries, commanderResolution.CommanderNames);
        try
        {
            CutLabPoolValidator.ValidateCardCount(
                nonCommanderCardCount,
                boardCounts,
                validateMaximum: !upperBoundValidationDeferred);
        }
        catch (InvalidOperationException exception)
        {
            return Error(exception.Message, warnings);
        }

        IReadOnlyList<string> bannedCardsPresent;
        try
        {
            bannedCardsPresent = await ResolveBannedCardsPresentAsync(resolvedEntries, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Cut Lab: banlist fetch failed; continuing without legality check.");
            bannedCardsPresent = [];
            warnings.Add("Banned-card check unavailable right now - legality was not verified for this import.");
        }

        var priorState = CutLabStateSerializer.Deserialize(request.CutLabStateJson, request.Bracket);
        EdhrecThemeResult planThemeResult = await FetchPlanThemeResultAsync(commanderResolution.CommanderNames, cancellationToken).ConfigureAwait(false);
        var preAnalysisState = CutLabLockRules.EnforceCommanderLock(
            BuildState(priorState, resolvedEntries, commanderResolution.CommanderNames, request, [], planThemeResult));
        IReadOnlyList<CutLabPoolCard> derivedWorkingList = CutLabWorkingList.Derive(preAnalysisState.Pool, preAnalysisState.Decisions, preAnalysisState.QuantityAdjustments);
        IReadOnlyList<ScryfallCardData> preResolvedCards = resolvedEntries
            .Select(entry => entry.Card)
            .Where(card => card is not null)
            .Cast<ScryfallCardData>()
            .ToArray();
        IReadOnlyList<string> unresolvedEntryNames = resolvedEntries
            .Where(entry => entry.Card is null)
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _analysisContextBuilder.PrimeResolvedCardsCache(
            derivedWorkingList,
            preResolvedCards,
            unresolvedEntryNames,
            resolutionPool);

        CutLabAnalysisContext analysisContext = await _analysisContextBuilder.BuildAsync(
            derivedWorkingList,
            request.PlayExperience,
            commanderResolution.CommanderNames,
            preAnalysisState.Decisions.Count == 0 ? null : preResolvedCards,
            poolKey: null,
            cancellationToken).ConfigureAwait(false);

        // Why the union, and why this sits AFTER BuildAsync rather than before it:
        // B-1 deliberately leaves a swallowed 429's casualties re-attemptable, so BuildAsync retries
        // them inside this same request -- and at the 500ms pacing floor a transient 429 has usually
        // cleared by then, making recovery the EXPECTED path rather than a corner case. Deriving the
        // warning and the card text from the first-pass snapshot alone therefore tells the user a
        // card could not be looked up, and drops its card text, for a card that resolved seconds ago.
        // The union (not a swap) is required because BuildAsync resolves derivedWorkingList, which
        // excludes already-cut cards, while the card-text map covers the FULL pool -- swapping would
        // lose text for every cut card. ToLastWinsDictionary gives the fresher entry precedence.
        IReadOnlyList<ScryfallCardData> finalResolvedCards = preResolvedCards
            .Concat(analysisContext.ResolvedCards)
            .ToArray();
        HashSet<string> resolvedAfterBuild = analysisContext.ResolvedCards
            .Select(card => CutLabCardNames.Normalize(card.Name))
            .ToHashSet(CutLabCardNames.Comparer);
        IReadOnlyList<string> stillUnresolvedNames = unresolvedEntryNames
            .Where(name => !resolvedAfterBuild.Contains(CutLabCardNames.Normalize(name)))
            .ToArray();

        List<ResolvedCutLabEntry> finalResolvedEntries = ReconcileResolvedEntries(resolvedEntries, finalResolvedCards);
        bool recoveredDuringBuild = resolvedEntries.Zip(finalResolvedEntries, (before, after) => before.Card is null && after.Card is not null).Any(static recovered => recovered);
        if (upperBoundValidationDeferred || recoveredDuringBuild)
        {
            commanderResolution = ResolveCommanderSelection(finalResolvedEntries, request.SelectedCommander);
            nonCommanderCardCount = CountNonCommanderCards(analyzedEntries, commanderResolution.CommanderNames);
            try
            {
                CutLabPoolValidator.ValidateCardCount(
                    nonCommanderCardCount,
                    boardCounts);
            }
            catch (InvalidOperationException exception)
            {
                return Error(exception.Message, warnings);
            }
        }

        if (recoveredDuringBuild)
        {
            // BuildAsync can recover cards after both intake passes have failed. Rebuild every
            // commander-dependent value from that final snapshot so locks, counts, roles, floors,
            // and the picker cannot retain facts from an earlier resolution attempt.
            resolvedEntries = finalResolvedEntries;

            preAnalysisState = CutLabLockRules.EnforceCommanderLock(
                BuildState(priorState, resolvedEntries, commanderResolution.CommanderNames, request, [], planThemeResult));
            derivedWorkingList = CutLabWorkingList.Derive(preAnalysisState.Pool, preAnalysisState.Decisions, preAnalysisState.QuantityAdjustments);
            preResolvedCards = resolvedEntries
                .Select(entry => entry.Card)
                .Where(card => card is not null)
                .Cast<ScryfallCardData>()
                .ToArray();
            unresolvedEntryNames = resolvedEntries
                .Where(entry => entry.Card is null)
                .Select(entry => entry.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _analysisContextBuilder.PrimeResolvedCardsCache(
                derivedWorkingList,
                preResolvedCards,
                unresolvedEntryNames,
                resolutionPool);
            analysisContext = await _analysisContextBuilder.BuildAsync(
                derivedWorkingList,
                request.PlayExperience,
                commanderResolution.CommanderNames,
                preAnalysisState.Decisions.Count == 0 ? null : preResolvedCards,
                poolKey: null,
                cancellationToken).ConfigureAwait(false);
            finalResolvedCards = preResolvedCards
                .Concat(analysisContext.ResolvedCards)
                .ToArray();
            resolvedAfterBuild = analysisContext.ResolvedCards
                .Select(card => CutLabCardNames.Normalize(card.Name))
                .ToHashSet(CutLabCardNames.Comparer);
            stillUnresolvedNames = unresolvedEntryNames
                .Where(name => !resolvedAfterBuild.Contains(CutLabCardNames.Normalize(name)))
                .ToArray();
        }

        // W-1 (round-1 review): a swallowed transient lookup failure leaves a card with no facts --
        // its land counts, role assignment, and commander detection can silently be wrong. Surface
        // that through the same user-visible warnings channel the banlist failure already uses,
        // rather than only a server-side LogWarning.
        if (stillUnresolvedNames.Count > 0)
        {
            warnings.Add(BuildUnresolvedCardsWarning(stillUnresolvedNames));
        }

        bool commanderFloorsEnabled = IsFlagOn(CommanderFloorsFlagKey);
        CutLabState floorResolutionState = CutLabFloorRules.ClampFloors(
            preAnalysisState with { RoleFloors = priorState.RoleFloors },
            preAnalysisState.Intent.Bracket);
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors = _floorResolver.Resolve(
            floorResolutionState,
            analysisContext.CommanderManaValue,
            commanderResolution.CommanderNames);
        IReadOnlyDictionary<string, int> floorByRole = resolvedFloors.ToDictionary(
            floor => floor.Role,
            floor => floor.Floor,
            StringComparer.OrdinalIgnoreCase);
        var state = preAnalysisState with
        {
            RoleFloors = resolvedFloors
                .Where(floor => floor.IsUserSet)
                .Select(floor => new CutLabRoleFloor
                {
                    Role = floor.Role,
                    Floor = floor.Floor,
                    IsUserSet = true,
                })
                .ToArray(),
        };

        if (state.OriginalEntries.Count == 0)
        {
            state = state with
            {
                OriginalEntries = analyzedEntries
                    .Select(static entry => new CutLabOriginalEntry
                    {
                        Name = entry.Name,
                        Quantity = entry.Quantity,
                        Board = entry.Board,
                        SetCode = entry.SetCode,
                        CollectorNumber = entry.CollectorNumber,
                        Category = entry.Category,
                    })
                    .ToArray(),
            };
        }

        if (state.BaselineSnapshot is null)
        {
            try
            {
                CutLabSimulationResult baselineResult = await _simulationService.BuildSnapshotResult(
                    state.Pool,
                    request.PlayExperience,
                    trialsOverride: null,
                    poolKey: null,
                    goals: state.Goals,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                state = state with
                {
                    BaselineSnapshot = baselineResult.Snapshot,
                    BaselineActualLands = baselineResult.ActualLands,
                    BaselineTargetLands = baselineResult.TargetLands,
                    Pool = CutLabSimulationResult.ApplySimulationCardData(state.Pool, baselineResult.CastabilityByCardName),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Cut Lab: baseline snapshot failed; continuing without D-12 baseline.");
                warnings.Add("Baseline snapshot unavailable right now - continuing without original-pool metrics.");
            }
        }

        IReadOnlyDictionary<string, CutLabPlanAffinity>? planAffinities = await _planAffinityFactory.BuildAsync(
            state.Intent.PlanProfile,
            analysisContext.AnalyzedCards,
            commanderResolution.CommanderNames,
            cancellationToken).ConfigureAwait(false);
        (CutLabStructuralFindingsResult findings, CutLabRoundPlan roundPlan) = CutLabCutRoundEngine.BuildFindingsAndRoundPlan(
            derivedWorkingList,
            analysisContext,
            floorByRole,
            state.Decisions,
            IsFlagOn(CutLabStructuralFindings.FunctionalTwinsFlagKey),
            planAffinities: planAffinities);

        CutLabMetricSnapshot? currentSnapshot = null;
        int? currentActualLands = null;
        double? currentTargetLands = null;
        if (state.Decisions.Count == 0 && state.BaselineSnapshot is not null)
        {
            currentSnapshot = state.BaselineSnapshot;
            currentActualLands = state.BaselineActualLands;
            currentTargetLands = state.BaselineTargetLands;
        }
        else
        {
            try
            {
                CutLabSimulationResult currentResult = await _simulationService.BuildSnapshotResult(
                    derivedWorkingList,
                    request.PlayExperience,
                    trialsOverride: ICutLabSimulationService.InLoopTrials,
                    poolKey: null,
                    goals: state.Goals,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                currentSnapshot = currentResult.Snapshot;
                currentActualLands = currentResult.ActualLands;
                currentTargetLands = currentResult.TargetLands;
                state = state with
                {
                    Pool = CutLabSimulationResult.ApplySimulationCardData(state.Pool, currentResult.CastabilityByCardName),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Cut Lab: current working snapshot failed; continuing without current metrics.");
                warnings.Add("Current working snapshot unavailable right now - continuing without live metrics.");
            }
        }

        CutLabProposalDeltas? initialProposalDeltas = null;
        if (roundPlan.NextProposal is not null)
        {
            try
            {
                initialProposalDeltas = await _simulationService.ComputeProposalDeltas(
                    derivedWorkingList,
                    roundPlan.NextProposal.CardName,
                    request.PlayExperience,
                    trialsOverride: ICutLabSimulationService.InLoopTrials,
                    poolKey: null,
                    goals: state.Goals,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Cut Lab: proposal deltas failed for {CardName}; continuing without deltas.", roundPlan.NextProposal.CardName);
                warnings.Add("Proposal delta preview unavailable right now - continuing without cut-impact metrics.");
            }
        }

        string serializedStateJson;
        try
        {
            serializedStateJson = CutLabStateSerializer.Serialize(state);
        }
        catch (InvalidOperationException exception)
        {
            return Error(exception.Message, warnings);
        }

        IReadOnlyDictionary<string, CutLabCardTextView> cardTextByCardName = BuildCardTextByCardName(state.Pool, finalResolvedCards);
        IReadOnlyDictionary<string, CutLabComboBadgeView> comboBadgeByCardName = BuildComboBadgeByCardName(
            analysisContext.Classification.CardComboMembership);

        return new CutLabProcessResult
        {
            State = state,
            SerializedStateJson = serializedStateJson,
            CardCount = nonCommanderCardCount,
            MainboardCardCount = boardCounts.MainboardCount,
            SideboardCardCount = boardCounts.SideboardCount,
            MaybeboardCardCount = boardCounts.MaybeboardCount,
            BoardCounts = boardCounts,
            BannedCardsPresent = bannedCardsPresent,
            IsLegal = bannedCardsPresent.Count == 0,
            CommanderSelectionRequired = commanderResolution.SelectionRequired,
            CommanderChoices = commanderResolution.CommanderChoices,
            Warnings = warnings,
            ComboDataAvailable = analysisContext.Classification.ComboDataAvailable,
            CategoryDataAvailable = analysisContext.Classification.CategoryDataAvailable,
            RoleAssignmentsByCardName = analysisContext.RolesByCardName,
            CardTextByCardName = cardTextByCardName,
            ComboBadgeByCardName = comboBadgeByCardName,
            ResolvedFloors = resolvedFloors,
            Findings = findings,
            RoundPlan = roundPlan,
            InitialProposalDeltas = initialProposalDeltas,
            CurrentSnapshot = currentSnapshot,
            CurrentActualLands = currentActualLands,
            CurrentTargetLands = currentTargetLands,
            CommanderFloorsEnabled = commanderFloorsEnabled,
            HasResult = true,
            AvailableCommanderThemes = planThemeResult.Themes,
            CommanderThemesUnavailable = planThemeResult.IsUnavailable,
        };
    }

    // Why: the plan panel needs the full known-theme list (for its checkbox rows) on every render,
    // not only when a theme is checked, so this fetch runs unconditionally rather than being gated
    // behind a non-empty PlanProfile like the plan-affinity factory's fetch is.
    private async Task<EdhrecThemeResult> FetchPlanThemeResultAsync(IReadOnlyList<string> commanderNames, CancellationToken cancellationToken)
    {
        string? commanderName = commanderNames.Count > 0 ? commanderNames[0] : null;
        if (string.IsNullOrWhiteSpace(commanderName))
        {
            return new EdhrecThemeResult([], true);
        }

        try
        {
            return await _themeService.GetCommanderThemesAsync(commanderName, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cut Lab: EDHREC commander theme list fetch failed for plan panel ({CommanderName})", commanderName);
            return new EdhrecThemeResult([], true);
        }
    }

    // True only when the named flag exists in the snapshot AND is enabled. Fail-safe OFF: a missing
    // key returns false (unlike IFeatureFlagCache.IsEnabled, which defaults missing keys ON).
    private bool IsFlagOn(string key)
        => _featureFlags is { } flags
            && flags.Snapshot().TryGetValue(key, out bool enabled)
            && enabled;

    private async Task<List<ResolvedCutLabEntry>> ResolveEntriesAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CutLabPoolCard> cacheLookupPool = entries
            .Select(entry => new CutLabPoolCard
            {
                Name = entry.Name,
                Quantity = entry.Quantity,
            })
            .ToArray();

        // Why (B-1, round-1 review): ResolvePoolCardsAsync reads the same CutLabResolvedCardCache
        // internally and only issues Scryfall calls for names in neither the cached cards nor the
        // cached known-missing set, so this costs no extra network work on a fully-cached pool --
        // while the removed TryGetCachedResolvedCards branch returned ANY cached entry, including
        // one whose gaps were never attempted, which is what made a rate-limited import permanent
        // for 30 minutes instead of retryable.
        IReadOnlyList<ScryfallCardData> resolvedCards = await _analysisContextBuilder.ResolvePoolCardsAsync(
            cacheLookupPool,
            failOpenOnLookupErrors: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<string, ScryfallCardData> cachedCardsByName = CutLabCardNames.ToLastWinsDictionary(
            resolvedCards,
            card => card.Name,
            card => card);

        var resolvedEntries = new List<ResolvedCutLabEntry>(entries.Count);

        foreach (DeckEntry entry in entries)
        {
            ScryfallCardData? card = null;
            string normalizedName = CutLabCardNames.Normalize(entry.Name);
            cachedCardsByName.TryGetValue(normalizedName, out card);

            resolvedEntries.Add(new ResolvedCutLabEntry(
                entry.Name,
                entry.Quantity,
                card?.TypeLine ?? string.Empty,
                string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase),
                card));
        }

        return resolvedEntries;
    }

    private static List<ResolvedCutLabEntry> ReconcileResolvedEntries(
        IReadOnlyList<ResolvedCutLabEntry> entries,
        IEnumerable<ScryfallCardData?> recoveredCards)
    {
        IReadOnlyDictionary<string, ScryfallCardData> recoveredCardsByName = CutLabCardNames.ToLastWinsDictionary(
            recoveredCards.Where(card => card is not null).Cast<ScryfallCardData>(),
            card => card.Name,
            card => card);
        return entries
            .Select(entry => recoveredCardsByName.TryGetValue(CutLabCardNames.Normalize(entry.Name), out ScryfallCardData? card)
                ? entry with { TypeLine = card.TypeLine ?? string.Empty, Card = card }
                : entry)
            .ToList();
    }

    /// <summary>
    /// Builds the user-facing degraded-import warning (W-1, round-1 review) for cards whose facts
    /// could not be looked up. Names up to five affected cards and rolls up the remainder as "and N
    /// more" so the string cannot grow unbounded. Deliberately plain: no HTTP status codes, no
    /// Scryfall internals, no mention of rate limiting -- only the consequence in the user's terms.
    /// </summary>
    private static string BuildUnresolvedCardsWarning(IReadOnlyList<string> unresolvedEntryNames)
    {
        const int MaxNamedCards = 5;
        List<string> namedCards = unresolvedEntryNames.Take(MaxNamedCards).ToList();
        int remainder = unresolvedEntryNames.Count - namedCards.Count;
        if (remainder > 0)
        {
            namedCards.Add($"and {remainder} more");
        }

        string cardList = string.Join(", ", namedCards);

        // Why: vary only the words that actually differ. Spelling the whole sentence out per branch
        // means a copy-edit can land on one arm and silently ship two different messages by count.
        bool singular = unresolvedEntryNames.Count == 1;
        string subject = singular ? "1 card" : $"{unresolvedEntryNames.Count} cards";
        string pronoun = singular ? "it" : "them";
        return $"{subject} could not be looked up ({cardList}) - land counts, role assignments, and commander detection may be wrong for {pronoun}.";
    }

    private static List<DeckEntry> ReflagInferredCommanders(List<DeckEntry> entries)
    {
        IReadOnlyList<string> commanderNames = CommanderInference.InferLeadingCommanderNames(entries);
        if (commanderNames.Count == 0)
        {
            return entries;
        }

        var commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return entries
            .Select(entry => commanderNameSet.Contains(entry.Name)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                ? entry with { Board = "commander" }
                : entry)
            .ToList();
    }

    private static CommanderResolution ResolveCommanderSelection(
        IReadOnlyList<ResolvedCutLabEntry> entries,
        string? selectedCommander)
    {
        var commanderChoices = new List<string>();
        var seenChoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool selectedCommanderSupplied = !string.IsNullOrWhiteSpace(selectedCommander);
        string? normalizedSelectedCommander = selectedCommanderSupplied ? selectedCommander!.Trim() : null;

        foreach (ResolvedCutLabEntry entry in entries)
        {
            bool eligible = CommanderEligibility.IsEligible(entry.TypeLine, oracleText: null);
            if (eligible && seenChoices.Add(entry.Name))
            {
                commanderChoices.Add(entry.Name);
            }
        }

        if (selectedCommanderSupplied)
        {
            var selected = entries.FirstOrDefault(entry =>
                string.Equals(entry.Name, normalizedSelectedCommander, StringComparison.OrdinalIgnoreCase)
                && CommanderEligibility.IsEligible(entry.TypeLine, oracleText: null));
            if (selected is not null)
            {
                return new CommanderResolution([selected.Name], commanderChoices, false);
            }
        }

        var validatedFlaggedCommanders = entries
            .Where(entry => entry.IsCommander && CommanderEligibility.IsEligible(entry.TypeLine, oracleText: null))
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (validatedFlaggedCommanders.Length > 0)
        {
            return new CommanderResolution(validatedFlaggedCommanders, commanderChoices, false);
        }

        if (commanderChoices.Count > 0)
        {
            return new CommanderResolution([], commanderChoices, true);
        }

        var fallbackChoices = entries
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new CommanderResolution([], fallbackChoices, fallbackChoices.Length > 0);
    }

    private static int CountNonCommanderCards(
        IReadOnlyList<DeckEntry> entries,
        IReadOnlyList<string> commanderNames)
    {
        var commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return entries
            .Where(entry => !commanderNameSet.Contains(entry.Name))
            .Sum(entry => entry.Quantity);
    }

    private static IReadOnlySet<string> BuildAnalyzedBoards(CutLabRequest request)
    {
        if (!request.IncludeSideboard && !request.IncludeMaybeboard)
        {
            return AnalyzedBoards;
        }

        var boards = new HashSet<string>(AnalyzedBoards, StringComparer.OrdinalIgnoreCase);
        if (request.IncludeSideboard)
        {
            boards.Add("sideboard");
        }

        if (request.IncludeMaybeboard)
        {
            boards.Add("maybeboard");
        }

        return boards;
    }

    private static EntryAnalysis AnalyzeEntries(IReadOnlyList<DeckEntry> entries, IReadOnlySet<string> analyzedBoards)
    {
        int mainboardCount = 0;
        int sideboardCount = 0;
        int maybeboardCount = 0;
        var analyzedEntries = new List<DeckEntry>(entries.Count);

        foreach (DeckEntry entry in entries)
        {
            if (analyzedBoards.Contains(entry.Board))
            {
                analyzedEntries.Add(entry);
            }

            if (string.Equals(entry.Board, "mainboard", StringComparison.OrdinalIgnoreCase))
            {
                mainboardCount += entry.Quantity;
                continue;
            }

            if (AnalyzedBoards.Contains(entry.Board))
            {
                continue;
            }

            if (string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            {
                sideboardCount += entry.Quantity;
                continue;
            }

            if (string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase))
            {
                maybeboardCount += entry.Quantity;
            }
        }

        return new EntryAnalysis
        {
            BoardCounts = new BoardCounts
            {
                MainboardCount = mainboardCount,
                SideboardCount = sideboardCount,
                MaybeboardCount = maybeboardCount,
            },
            AnalyzedEntries = analyzedEntries,
        };
    }

    private static CutLabState BuildState(
        CutLabState priorState,
        IReadOnlyList<ResolvedCutLabEntry> resolvedEntries,
        IReadOnlyList<string> commanderNames,
        CutLabRequest request,
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors,
        EdhrecThemeResult planThemeResult)
    {
        var priorCards = priorState.Pool
            .GroupBy(card => CutLabCardNames.Normalize(card.Name), CutLabCardNames.Comparer)
            .ToDictionary(group => group.Key, group => group.Last(), CutLabCardNames.Comparer);
        var commanderNameSet = commanderNames
            .Select(CutLabCardNames.Normalize)
            .ToHashSet(CutLabCardNames.Comparer);

        var pool = resolvedEntries
            .Select(entry =>
            {
                string normalizedName = CutLabCardNames.Normalize(entry.Name);
                bool isCommander = commanderNameSet.Contains(normalizedName);
                priorCards.TryGetValue(normalizedName, out CutLabPoolCard? priorCard);
                return new CutLabPoolCard
                {
                    Name = entry.Name,
                    Quantity = entry.Quantity,
                    TypeLine = entry.TypeLine,
                    IsCommander = isCommander,
                    IsLocked = !isCommander && priorCard is not null ? priorCard.IsLocked : isCommander,
                    PackageId = !isCommander && priorCard is not null ? priorCard.PackageId : null,
                    LastKnownCmc = priorCard?.LastKnownCmc,
                    LastKnownCastPercent = priorCard?.LastKnownCastPercent,
                };
            })
            .ToArray();

        return new CutLabState
        {
            Commander = commanderNames.Count == 0 ? string.Empty : commanderNames[0],
            Pool = pool,
            Packages = priorState.Packages,
            Decisions = priorState.Decisions,
            QuantityAdjustments = priorState.QuantityAdjustments,
            OriginalEntries = priorState.OriginalEntries,
            Goals = priorState.Goals,
            BaselineSnapshot = priorState.BaselineSnapshot,
            BaselineActualLands = priorState.BaselineActualLands,
            BaselineTargetLands = priorState.BaselineTargetLands,
            RoleFloors = resolvedFloors
                .Where(floor => floor.IsUserSet)
                .Select(floor => new CutLabRoleFloor
                {
                    Role = floor.Role,
                    Floor = floor.Floor,
                    IsUserSet = true,
                })
                .ToArray(),
            Intent = new CutLabIntent
            {
                PrimaryPlan = request.PrimaryPlan,
                SecondaryPlan = string.IsNullOrWhiteSpace(request.SecondaryPlan) ? null : request.SecondaryPlan,
                PlanProfile = BuildPlanProfile(request, priorState.Intent.PlanProfile, planThemeResult),
                Bracket = request.Bracket,
                PlayExperience = request.PlayExperience,
                IncludeSideboard = request.IncludeSideboard,
                IncludeMaybeboard = request.IncludeMaybeboard,
            },
        };
    }

    // Why: PlanProfile == null means "the plan panel has never been presented for this session" and
    // is the only signal that authorizes the top-three default preselection (PLPR-02). Once a
    // profile exists — even an intentionally empty one — a request that carries no PlanThemes means
    // the user cleared every box, not that defaults should be re-applied (D-1 in the plan doc).
    private static CutLabPlanProfile BuildPlanProfile(
        CutLabRequest request,
        CutLabPlanProfile? priorProfile,
        EdhrecThemeResult planThemeResult)
        => BuildPlanProfile(request.PlanStrategies ?? [], request.PlanThemes ?? [], priorProfile, planThemeResult);

    /// <summary>
    /// Filters posted strategy and theme slugs into a validated <see cref="CutLabPlanProfile"/> — shared by
    /// the intake form projection above and the <c>/api/cut-lab/plan-apply</c> round trip, which posts the
    /// checked slugs already embedded in the client-carried session state rather than in a
    /// <see cref="CutLabRequest"/>. Strategy slugs are dropped unless they resolve against
    /// <c>DeckPlanStrategyCatalog</c>; theme slugs are dropped unless they resolve against the commander's
    /// EDHREC-fetched theme list, which also supplies the authoritative display name and deck count for any
    /// slug that survives (T-08-07-01).
    /// </summary>
    internal static CutLabPlanProfile BuildPlanProfile(
        IReadOnlyList<string> requestedStrategySlugs,
        IReadOnlyList<string> requestedThemeSlugs,
        CutLabPlanProfile? priorProfile,
        EdhrecThemeResult planThemeResult)
    {
        IReadOnlyList<string> resolvedStrategies = requestedStrategySlugs
            .Where(slug => DeckPlanStrategyCatalog.TryGetBySlug(slug, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        bool isFirstPresentation = priorProfile is null;

        IReadOnlyList<CutLabCommanderTheme> checkedThemes;
        if (planThemeResult.IsUnavailable)
        {
            checkedThemes = priorProfile?.CommanderThemes ?? [];
        }
        else if (isFirstPresentation && requestedThemeSlugs.Count == 0)
        {
            checkedThemes = EdhrecCommanderThemeService.SelectDefaultThemes(planThemeResult.Themes);
        }
        else
        {
            Dictionary<string, CutLabCommanderTheme> knownThemesBySlug = planThemeResult.Themes
                .ToDictionary(theme => theme.Slug, StringComparer.OrdinalIgnoreCase);
            checkedThemes = requestedThemeSlugs
                .Where(slug => knownThemesBySlug.ContainsKey(slug))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(slug => knownThemesBySlug[slug])
                .ToArray();
        }

        return new CutLabPlanProfile
        {
            GenericStrategies = resolvedStrategies,
            CommanderThemes = checkedThemes,
            CommanderThemesUnavailable = planThemeResult.IsUnavailable,
        };
    }

    private static IReadOnlyDictionary<string, CutLabCardTextView> BuildCardTextByCardName(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyList<ScryfallCardData> preResolvedCards)
    {
        IReadOnlyDictionary<string, ScryfallCardData> resolvedByName = CutLabCardNames.ToLastWinsDictionary(
            preResolvedCards,
            card => card.Name,
            card => card);
        Dictionary<string, CutLabCardTextView> cardTextByCardName = new(StringComparer.OrdinalIgnoreCase);

        foreach (CutLabPoolCard card in pool)
        {
            if (resolvedByName.TryGetValue(CutLabCardNames.Normalize(card.Name), out ScryfallCardData? resolvedCard))
            {
                (string? power, string? toughness) = ResolvePowerAndToughness(resolvedCard);
                cardTextByCardName[card.Name] = new CutLabCardTextView
                {
                    TypeLine = resolvedCard.TypeLine,
                    ManaCost = resolvedCard.ManaCost,
                    SetCode = resolvedCard.Set,
                    CollectorNumber = resolvedCard.CollectorNumber,
                    OracleText = ResolveOracleText(resolvedCard),
                    Power = power,
                    Toughness = toughness,
                    Cmc = card.LastKnownCmc,
                    CastPercent = card.LastKnownCastPercent,
                };
            }
        }

        return cardTextByCardName;
    }

    private static string? ResolveOracleText(ScryfallCardData card)
    {
        if (!string.IsNullOrWhiteSpace(card.OracleText))
        {
            return card.OracleText;
        }

        if (card.CardFaces is not { Count: > 0 } faces)
        {
            return card.OracleText;
        }

        string joined = string.Join(
            "\n\n//\n\n",
            faces
                .Where(face => !string.IsNullOrWhiteSpace(face.OracleText))
                .Select(face => string.IsNullOrWhiteSpace(face.Name)
                    ? face.OracleText!.Trim()
                    : $"{face.Name}\n{face.OracleText!.Trim()}"));

        return joined.Length > 0 ? joined : card.OracleText;
    }

    private static (string? Power, string? Toughness) ResolvePowerAndToughness(ScryfallCardData card)
    {
        var power = card.Power;
        var toughness = card.Toughness;
        if (string.IsNullOrWhiteSpace(card.OracleText)
            && (string.IsNullOrWhiteSpace(power) || string.IsNullOrWhiteSpace(toughness))
            && card.CardFaces is { Count: > 0 })
        {
            power = card.CardFaces[0].Power;
            toughness = card.CardFaces[0].Toughness;
        }

        return !string.IsNullOrWhiteSpace(power) && !string.IsNullOrWhiteSpace(toughness)
            ? (power, toughness)
            : (null, null);
    }

    private static IReadOnlyDictionary<string, CutLabComboBadgeView> BuildComboBadgeByCardName(
        IReadOnlyDictionary<string, CutLabCardComboMembership> cardComboMembership)
    {
        Dictionary<string, CutLabComboBadgeView> comboBadgeByCardName = new(CutLabCardNames.Comparer);

        foreach ((string normalizedCardName, CutLabCardComboMembership membership) in cardComboMembership)
        {
            if (membership.CompleteCombos.Count > 0)
            {
                comboBadgeByCardName[normalizedCardName] = new CutLabComboBadgeView
                {
                    BadgeState = ComboBadgeState.CompletePiece,
                    Context = JoinCardNames(
                        membership.CompleteCombos
                            .SelectMany(combo => combo.Results)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(result => result, StringComparer.OrdinalIgnoreCase)
                            .ToArray()),
                };
                continue;
            }

            if (membership.NearCombos.Count > 0)
            {
                comboBadgeByCardName[normalizedCardName] = new CutLabComboBadgeView
                {
                    BadgeState = ComboBadgeState.NeedsPartner,
                    Context = $"Needs {JoinCardNames(membership.NearCombos.Select(combo => combo.MissingCard).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(cardName => cardName, StringComparer.OrdinalIgnoreCase).ToArray())}",
                };
            }
        }

        return comboBadgeByCardName;
    }

    private static string JoinCardNames(IReadOnlyList<string> cardNames)
        => cardNames.Count switch
        {
            0 => string.Empty,
            1 => cardNames[0],
            2 => $"{cardNames[0]} and {cardNames[1]}",
            _ => $"{string.Join(", ", cardNames.Take(cardNames.Count - 1))} and {cardNames[^1]}",
        };

    private async Task<IReadOnlyList<string>> ResolveBannedCardsPresentAsync(
        IReadOnlyList<ResolvedCutLabEntry> entries,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> bannedCards = await _banListService.GetBannedCardsAsync(cancellationToken).ConfigureAwait(false);
        var bannedSet = bannedCards.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var present = entries
            .Select(entry => entry.Name)
            .Where(name => bannedSet.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (present.Length > 0)
        {
            _logger.LogInformation("Cut Lab intake found {BannedCardCount} banned card(s) in the submitted pool.", present.Length);
        }

        return present;
    }

    private static CutLabProcessResult Error(string message, IReadOnlyList<string>? warnings = null) =>
        new()
        {
            ErrorMessage = message,
            Warnings = warnings ?? [],
        };

    private sealed record ResolvedCutLabEntry(
        string Name,
        int Quantity,
        string TypeLine,
        bool IsCommander,
        ScryfallCardData? Card);

    private sealed record CommanderResolution(
        IReadOnlyList<string> CommanderNames,
        IReadOnlyList<string> CommanderChoices,
        bool SelectionRequired);

    private sealed record EntryAnalysis
    {
        public required BoardCounts BoardCounts { get; init; }

        public required List<DeckEntry> AnalyzedEntries { get; init; }
    }

    private sealed class NoOpCutLabSimulationService : ICutLabSimulationService
    {
        public static NoOpCutLabSimulationService Instance { get; } = new();

        public Task<CutLabSimulationResult> BuildSnapshotResult(
            IReadOnlyList<CutLabPoolCard> workingList,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabSimulationResult());

        public Task<CutLabMetricSnapshot> BuildSnapshot(
            IReadOnlyList<CutLabPoolCard> workingList,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabMetricSnapshot());

        public Task<CutLabProposalDeltas> ComputeProposalDeltas(
            IReadOnlyList<CutLabPoolCard> currentWorkingList,
            string candidateCardName,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabProposalDeltas
            {
                CardName = candidateCardName,
            });
    }
}
