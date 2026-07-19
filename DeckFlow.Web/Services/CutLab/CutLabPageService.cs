using System.Net;
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
using RestSharp;

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

    /// <summary>Resolved role-floor rows, including default provenance and user overrides.</summary>
    public IReadOnlyList<CutLabResolvedFloor> ResolvedFloors { get; init; } = [];

    /// <summary>Computed structural findings for the current pool.</summary>
    public CutLabStructuralFindingsResult Findings { get; init; } =
        new([], ComboDataAvailable: false, CategoryDataAvailable: false);

    /// <summary>User-facing error for a hard failure, null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when a result state is available for rendering.</summary>
    public bool HasResult { get; init; }
}

/// <summary>Default Cut Lab page-service orchestrator.</summary>
internal sealed class CutLabPageService : ICutLabPageService
{
    private const int ScryfallBatchSize = 75;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyCategories =
        new Dictionary<string, IReadOnlyList<string>>();

    private static readonly HashSet<string> AnalyzedBoards =
        new(StringComparer.OrdinalIgnoreCase) { "mainboard", "commander" };

    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly IScryfallCardResolver _cardResolver;
    private readonly ICommanderBanListService _banListService;
    private readonly ICategoryKnowledgeStore? _categoryKnowledge;
    private readonly ICommanderSpellbookService? _spellbook;
    private readonly IManabaseBaselineProvider? _manabaseBaseline;
    private readonly ICedhLandBaselineProvider? _cedhBaseline;
    private readonly ILogger<CutLabPageService> _logger;

    /// <summary>Creates the Cut Lab page service.</summary>
    /// <param name="deckEntryLoader">Deck loader for URL/paste imports.</param>
    /// <param name="cardResolver">Scryfall resolver for type-line lookup.</param>
    /// <param name="banListService">Commander banlist service.</param>
    /// <param name="categoryKnowledge">Optional batched category lookup dependency for structural analysis.</param>
    /// <param name="spellbook">Optional combo lookup dependency for structural analysis.</param>
    /// <param name="manabaseBaseline">Optional bracket baseline dependency for structural analysis.</param>
    /// <param name="cedhBaseline">Optional cEDH commander baseline dependency for structural analysis.</param>
    /// <param name="logger">Optional logger for non-blocking diagnostics.</param>
    public CutLabPageService(
        IDeckEntryLoader deckEntryLoader,
        IScryfallCardResolver cardResolver,
        ICommanderBanListService banListService,
        ICategoryKnowledgeStore? categoryKnowledge = null,
        ICommanderSpellbookService? spellbook = null,
        IManabaseBaselineProvider? manabaseBaseline = null,
        ICedhLandBaselineProvider? cedhBaseline = null,
        ILogger<CutLabPageService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(cardResolver);
        ArgumentNullException.ThrowIfNull(banListService);

        _deckEntryLoader = deckEntryLoader;
        _cardResolver = cardResolver;
        _banListService = banListService;
        _categoryKnowledge = categoryKnowledge;
        _spellbook = spellbook;
        _manabaseBaseline = manabaseBaseline;
        _cedhBaseline = cedhBaseline;
        _logger = logger ?? NullLogger<CutLabPageService>.Instance;
    }

    /// <summary>
    /// Test-only probe for the DI guard that verifies the optional structural-analysis services are
    /// actually registered in the production container shape.
    /// </summary>
    internal bool HasStructuralAnalysisDependencies =>
        _categoryKnowledge is not null
        && _spellbook is not null
        && _manabaseBaseline is not null
        && _cedhBaseline is not null;

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
        var analyzedEntries = entries
            .Where(entry => AnalyzedBoards.Contains(entry.Board))
            .ToList();

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
        ClassificationInputs classification = await LoadClassificationInputsAsync(
            resolvedEntries,
            commanderResolution.CommanderNames,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName = BuildRoleAssignments(
            resolvedEntries,
            commanderResolution.CommanderNames,
            request.PlayExperience,
            classification,
            out IReadOnlyList<CutLabAnalyzedCard> analyzedCards,
            out double commanderManaValue);

        int nonCommanderCardCount = CountNonCommanderCards(analyzedEntries, commanderResolution.CommanderNames);
        try
        {
            CutLabPoolValidator.ValidateCardCount(nonCommanderCardCount);
        }
        catch (InvalidOperationException exception)
        {
            return Error(exception.Message, warnings);
        }

        var bannedCardsPresent = await ResolveBannedCardsPresentAsync(resolvedEntries, cancellationToken).ConfigureAwait(false);
        var priorState = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors = CutLabFloorDefaults.ResolveDefaults(
            request.Bracket,
            request.PlayExperience,
            commanderManaValue,
            commanderResolution.CommanderNames,
            _manabaseBaseline,
            _cedhBaseline,
            priorState.RoleFloors);
        IReadOnlyDictionary<string, int> floorByRole = resolvedFloors.ToDictionary(
            floor => floor.Role,
            floor => floor.Floor,
            StringComparer.OrdinalIgnoreCase);
        CutLabStructuralFindingsResult findings = CutLabStructuralFindings.Compute(
            analyzedCards,
            classification.AlmostIncludedCombos,
            floorByRole,
            classification.ComboDataAvailable,
            classification.CategoryDataAvailable);
        var state = BuildState(priorState, resolvedEntries, commanderResolution.CommanderNames, request, resolvedFloors);
        state = CutLabLockRules.EnforceCommanderLock(state);

        string serializedStateJson;
        try
        {
            serializedStateJson = CutLabStateSerializer.Serialize(state);
        }
        catch (InvalidOperationException exception)
        {
            return Error(exception.Message, warnings);
        }

        return new CutLabProcessResult
        {
            State = state,
            SerializedStateJson = serializedStateJson,
            CardCount = nonCommanderCardCount,
            BannedCardsPresent = bannedCardsPresent,
            IsLegal = bannedCardsPresent.Count == 0,
            CommanderSelectionRequired = commanderResolution.SelectionRequired,
            CommanderChoices = commanderResolution.CommanderChoices,
            Warnings = warnings,
            ComboDataAvailable = classification.ComboDataAvailable,
            CategoryDataAvailable = classification.CategoryDataAvailable,
            RoleAssignmentsByCardName = roleAssignmentsByCardName,
            ResolvedFloors = resolvedFloors,
            Findings = findings,
            HasResult = true,
        };
    }

    private async Task<List<ResolvedCutLabEntry>> ResolveEntriesAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken)
    {
        var index = await ResolveCardsAsync(entries, cancellationToken).ConfigureAwait(false);
        var resolvedEntries = new List<ResolvedCutLabEntry>(entries.Count);

        foreach (DeckEntry entry in entries)
        {
            if (!index.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out ScryfallCardData? card))
            {
                ScryfallCard? fallback = await _cardResolver.SearchFallbackCardAsync(entry.Name, cancellationToken).ConfigureAwait(false);
                if (fallback is not null)
                {
                    card = ScryfallCardDataMapper.ToCardData(fallback);
                    index.Add(card);
                }
            }

            resolvedEntries.Add(new ResolvedCutLabEntry(
                entry.Name,
                entry.Quantity,
                card?.TypeLine ?? string.Empty,
                string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase),
                card));
        }

        return resolvedEntries;
    }

    private async Task<ScryfallCardNameIndex> ResolveCardsAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken)
    {
        var identifiers = new List<object>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DeckEntry entry in entries)
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
                await _cardResolver.ExecuteCollectionAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during cut-lab intake.",
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

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildRoleAssignments(
        IReadOnlyList<ResolvedCutLabEntry> resolvedEntries,
        IReadOnlyList<string> commanderNames,
        string playExperience,
        ClassificationInputs classification,
        out IReadOnlyList<CutLabAnalyzedCard> analyzedCards,
        out double commanderManaValue)
    {
        ArgumentNullException.ThrowIfNull(resolvedEntries);
        ArgumentNullException.ThrowIfNull(commanderNames);
        ArgumentNullException.ThrowIfNull(playExperience);
        ArgumentNullException.ThrowIfNull(classification);

        HashSet<string> commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ManabaseMode mode = CutLabRoleAssigner.ResolveMode(playExperience);
        Dictionary<string, IReadOnlyList<string>> rolesByCardName = new(StringComparer.OrdinalIgnoreCase);
        List<CutLabAnalyzedCard> analyzed = new(resolvedEntries.Count);
        commanderManaValue = 0;
        bool commanderManaValueResolved = false;

        foreach (ResolvedCutLabEntry entry in resolvedEntries)
        {
            IReadOnlyList<string> categories = classification.CategoriesByName.TryGetValue(entry.Name, out IReadOnlyList<string>? hit)
                ? hit
                : Array.Empty<string>();
            IReadOnlyList<string> roles = [];
            double manaValue = 0;

            if (entry.Card is not null)
            {
                CardFact fact = ScryfallCardFactMapper.ToCardFact(
                    entry.Card,
                    entry.Quantity,
                    commanderNameSet.Contains(entry.Name));
                roles = CutLabRoleAssigner.AssignRoles(
                    fact,
                    categories,
                    classification.ComboNames.Contains(entry.Name),
                    mode);
                manaValue = fact.ManaValue;

                if (!commanderManaValueResolved && commanderNameSet.Contains(entry.Name))
                {
                    commanderManaValue = fact.ManaValue;
                    commanderManaValueResolved = true;
                }
            }

            rolesByCardName[entry.Name] = roles;
            analyzed.Add(new CutLabAnalyzedCard(
                entry.Name,
                manaValue,
                roles.Contains("lands", StringComparer.Ordinal),
                roles,
                categories)
            {
                Quantity = entry.Quantity,
            });
        }

        analyzedCards = analyzed;
        return rolesByCardName;
    }

    private async Task<ClassificationInputs> LoadClassificationInputsAsync(
        IReadOnlyList<ResolvedCutLabEntry> resolvedEntries,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken)
    {
        HashSet<string> comboNames = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<SpellbookAlmostCombo> almostIncludedCombos = [];
        bool comboDataAvailable = false;

        if (_spellbook is not null)
        {
            try
            {
                CommanderSpellbookResult? combos =
                    await _spellbook.FindCombosAsync(
                        BuildSpellbookEntries(resolvedEntries, commanderNames),
                        cancellationToken).ConfigureAwait(false);
                comboDataAvailable = combos is not null;
                if (combos is not null)
                {
                    almostIncludedCombos = combos.AlmostIncludedCombos;
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
                _logger.LogWarning(exception, "Cut Lab: Commander Spellbook fetch failed; continuing without combo roles.");
            }
        }

        // ONE batched lookup for the whole pool. A per-card loop here previously caused ~65
        // sequential queries and pushed request time toward ~20 seconds, so this must stay batched.
        CategoryLookupResult categories = await GetCategoriesFailOpenAsync(
            resolvedEntries
                .Select(entry => entry.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cancellationToken).ConfigureAwait(false);

        return new ClassificationInputs(
            comboNames,
            almostIncludedCombos,
            categories.CategoriesByName,
            comboDataAvailable,
            categories.CategoryDataAvailable);
    }

    private async Task<CategoryLookupResult> GetCategoriesFailOpenAsync(
        IReadOnlyCollection<string> cardNames,
        CancellationToken cancellationToken)
    {
        if (_categoryKnowledge is null || cardNames.Count == 0)
        {
            return new CategoryLookupResult(EmptyCategories, false);
        }

        try
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> categories =
                await _categoryKnowledge.GetCategoriesForNamesAsync(cardNames, cancellationToken).ConfigureAwait(false);
            return new CategoryLookupResult(categories, true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cut Lab: batch category lookup failed; using heuristics only.");
            return new CategoryLookupResult(EmptyCategories, false);
        }
    }

    private static List<DeckEntry> BuildSpellbookEntries(
        IReadOnlyList<ResolvedCutLabEntry> resolvedEntries,
        IReadOnlyList<string> commanderNames)
    {
        HashSet<string> commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<DeckEntry> spellbookEntries = new(resolvedEntries.Count);

        foreach (ResolvedCutLabEntry entry in resolvedEntries)
        {
            spellbookEntries.Add(new DeckEntry
            {
                Name = entry.Name,
                NormalizedName = entry.Name.ToLowerInvariant(),
                Quantity = entry.Quantity,
                Board = commanderNameSet.Contains(entry.Name) ? "commander" : "mainboard",
            });
        }

        return spellbookEntries;
    }

    private static CutLabState BuildState(
        CutLabState priorState,
        IReadOnlyList<ResolvedCutLabEntry> resolvedEntries,
        IReadOnlyList<string> commanderNames,
        CutLabRequest request,
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors)
    {
        var priorCards = priorState.Pool
            .GroupBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pool = resolvedEntries
            .Select(entry =>
            {
                bool isCommander = commanderNameSet.Contains(entry.Name);
                priorCards.TryGetValue(entry.Name, out CutLabPoolCard? priorCard);
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
            },
        };
    }

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

    private sealed record ClassificationInputs(
        IReadOnlySet<string> ComboNames,
        IReadOnlyList<SpellbookAlmostCombo> AlmostIncludedCombos,
        IReadOnlyDictionary<string, IReadOnlyList<string>> CategoriesByName,
        bool ComboDataAvailable,
        bool CategoryDataAvailable);

    private sealed record CategoryLookupResult(
        IReadOnlyDictionary<string, IReadOnlyList<string>> CategoriesByName,
        bool CategoryDataAvailable);

    private sealed record CommanderResolution(
        IReadOnlyList<string> CommanderNames,
        IReadOnlyList<string> CommanderChoices,
        bool SelectionRequired);
}
