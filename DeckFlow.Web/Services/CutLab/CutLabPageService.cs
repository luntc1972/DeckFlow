using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
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

    /// <summary>User-facing error for a hard failure, null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when a result state is available for rendering.</summary>
    public bool HasResult { get; init; }
}

/// <summary>Default Cut Lab page-service orchestrator.</summary>
internal sealed class CutLabPageService : ICutLabPageService
{
    private static readonly HashSet<string> AnalyzedBoards =
        new(StringComparer.OrdinalIgnoreCase) { "mainboard", "commander" };

    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly IScryfallCardResolver _cardResolver;
    private readonly ICommanderBanListService _banListService;
    private readonly IManabaseBaselineProvider? _manabaseBaseline;
    private readonly ICedhLandBaselineProvider? _cedhBaseline;
    private readonly ICutLabAnalysisContextBuilder _analysisContextBuilder;
    private readonly ICutLabSimulationService _simulationService;
    private readonly ILogger<CutLabPageService> _logger;

    /// <summary>Creates the Cut Lab page service.</summary>
    /// <param name="deckEntryLoader">Deck loader for URL/paste imports.</param>
    /// <param name="cardResolver">Scryfall resolver for type-line lookup.</param>
    /// <param name="banListService">Commander banlist service.</param>
    /// <param name="manabaseBaseline">Optional bracket baseline dependency for structural analysis.</param>
    /// <param name="cedhBaseline">Optional cEDH commander baseline dependency for structural analysis.</param>
    /// <param name="analysisContextBuilder">Optional shared builder for resolved-card, classification, and role-assignment analysis.</param>
    /// <param name="simulationService">Optional simulation service for baseline, current snapshot, and proposal-delta computation.</param>
    /// <param name="logger">Optional logger for non-blocking diagnostics.</param>
    public CutLabPageService(
        IDeckEntryLoader deckEntryLoader,
        IScryfallCardResolver cardResolver,
        ICommanderBanListService banListService,
        IManabaseBaselineProvider? manabaseBaseline = null,
        ICedhLandBaselineProvider? cedhBaseline = null,
        ICutLabAnalysisContextBuilder? analysisContextBuilder = null,
        ICutLabSimulationService? simulationService = null,
        ILogger<CutLabPageService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(cardResolver);
        ArgumentNullException.ThrowIfNull(banListService);

        _deckEntryLoader = deckEntryLoader;
        _cardResolver = cardResolver;
        _banListService = banListService;
        _manabaseBaseline = manabaseBaseline;
        _cedhBaseline = cedhBaseline;
        CutLabResolvedCardCache sharedResolvedCardCache = new();
        _analysisContextBuilder = analysisContextBuilder
            ?? new CutLabAnalysisContextBuilder(cardResolver, sharedResolvedCardCache);
        _simulationService = simulationService
            ?? NoOpCutLabSimulationService.Instance;
        _logger = logger ?? NullLogger<CutLabPageService>.Instance;
    }

    /// <summary>
    /// Test-only probe for the DI guard that verifies the optional structural-analysis services are
    /// actually registered in the production container shape.
    /// </summary>
    internal bool HasStructuralAnalysisDependencies =>
        _manabaseBaseline is not null
        && _cedhBaseline is not null
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

        var commanderResolution = ResolveCommanderSelection(resolvedEntries, request.SelectedCommander);

        int nonCommanderCardCount = CountNonCommanderCards(analyzedEntries, commanderResolution.CommanderNames);
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

        var priorState = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
        var preAnalysisState = CutLabLockRules.EnforceCommanderLock(
            BuildState(priorState, resolvedEntries, commanderResolution.CommanderNames, request, []));
        IReadOnlyList<CutLabPoolCard> derivedWorkingList = CutLabWorkingList.Derive(preAnalysisState.Pool, preAnalysisState.Decisions, preAnalysisState.QuantityAdjustments);
        IReadOnlyList<ScryfallCardData> preResolvedCards = resolvedEntries
            .Select(entry => entry.Card)
            .Where(card => card is not null)
            .Cast<ScryfallCardData>()
            .ToArray();
        _analysisContextBuilder.PrimeResolvedCardsCache(
            preAnalysisState.Pool,
            preResolvedCards,
            resolvedEntries
                .Where(entry => entry.Card is null)
                .Select(entry => entry.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());

        CutLabAnalysisContext analysisContext = await _analysisContextBuilder.BuildAsync(
            derivedWorkingList,
            request.PlayExperience,
            commanderResolution.CommanderNames,
            preAnalysisState.Decisions.Count == 0 ? null : preResolvedCards,
            poolKey: null,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors = CutLabFloorDefaults.ResolveDefaults(
            request.Bracket,
            request.PlayExperience,
            analysisContext.CommanderManaValue,
            commanderResolution.CommanderNames,
            _manabaseBaseline,
            _cedhBaseline,
            priorState.RoleFloors);
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
                CutLabMetricSnapshot baselineSnapshot = await _simulationService.BuildSnapshot(
                    state.Pool,
                    request.PlayExperience,
                    trialsOverride: null,
                    poolKey: null,
                    goals: state.Goals,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                state = state with { BaselineSnapshot = baselineSnapshot };
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

        (CutLabStructuralFindingsResult findings, CutLabRoundPlan roundPlan) = CutLabCutRoundEngine.BuildFindingsAndRoundPlan(
            derivedWorkingList,
            analysisContext,
            floorByRole,
            state.Decisions);

        CutLabMetricSnapshot? currentSnapshot = null;
        if (state.Decisions.Count == 0 && state.BaselineSnapshot is not null)
        {
            currentSnapshot = state.BaselineSnapshot;
        }
        else
        {
            try
            {
                currentSnapshot = await _simulationService.BuildSnapshot(
                    derivedWorkingList,
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

        IReadOnlyDictionary<string, CutLabCardTextView> cardTextByCardName = BuildCardTextByCardName(state.Pool, preResolvedCards);
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
            HasResult = true,
        };
    }

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

        IReadOnlyList<ScryfallCardData>? resolvedCards = null;
        if (_analysisContextBuilder.TryGetCachedResolvedCards(cacheLookupPool, out IReadOnlyList<ScryfallCardData>? cachedCards)
            && cachedCards is not null)
        {
            resolvedCards = cachedCards;
        }
        else
        {
            resolvedCards = await _analysisContextBuilder.ResolvePoolCardsAsync(
                cacheLookupPool,
                failOpenOnLookupErrors: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

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
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors)
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
                Bracket = request.Bracket,
                PlayExperience = request.PlayExperience,
                IncludeSideboard = request.IncludeSideboard,
                IncludeMaybeboard = request.IncludeMaybeboard,
            },
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
                cardTextByCardName[card.Name] = new CutLabCardTextView
                {
                    TypeLine = resolvedCard.TypeLine,
                    ManaCost = resolvedCard.ManaCost,
                    SetCode = resolvedCard.Set,
                    CollectorNumber = resolvedCard.CollectorNumber,
                    OracleText = ResolveOracleText(resolvedCard),
                    Power = ResolvePower(resolvedCard),
                    Toughness = ResolveToughness(resolvedCard),
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

    private static string? ResolvePower(ScryfallCardData card)
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
            ? power
            : null;
    }

    private static string? ResolveToughness(ScryfallCardData card)
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
            ? toughness
            : null;
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
