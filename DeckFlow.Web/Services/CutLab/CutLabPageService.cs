using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
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

    /// <summary>User-facing error for a hard failure, null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when a result state is available for rendering.</summary>
    public bool HasResult { get; init; }
}

/// <summary>Default Cut Lab page-service orchestrator.</summary>
internal sealed class CutLabPageService : ICutLabPageService
{
    private const int ScryfallBatchSize = 75;

    private static readonly HashSet<string> AnalyzedBoards =
        new(StringComparer.OrdinalIgnoreCase) { "mainboard", "commander" };

    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly IScryfallCardResolver _cardResolver;
    private readonly ICommanderBanListService _banListService;
    private readonly ILogger<CutLabPageService> _logger;

    /// <summary>Creates the Cut Lab page service.</summary>
    /// <param name="deckEntryLoader">Deck loader for URL/paste imports.</param>
    /// <param name="cardResolver">Scryfall resolver for type-line lookup.</param>
    /// <param name="banListService">Commander banlist service.</param>
    /// <param name="logger">Optional logger for non-blocking diagnostics.</param>
    public CutLabPageService(
        IDeckEntryLoader deckEntryLoader,
        IScryfallCardResolver cardResolver,
        ICommanderBanListService banListService,
        ILogger<CutLabPageService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(cardResolver);
        ArgumentNullException.ThrowIfNull(banListService);

        _deckEntryLoader = deckEntryLoader;
        _cardResolver = cardResolver;
        _banListService = banListService;
        _logger = logger ?? NullLogger<CutLabPageService>.Instance;
    }

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

        var nonCommanderCardCount = analyzedEntries
            .Where(entry => !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Quantity);

        try
        {
            CutLabPoolValidator.ValidateCardCount(nonCommanderCardCount);
        }
        catch (InvalidOperationException exception)
        {
            return Error(exception.Message, warnings);
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
        var bannedCardsPresent = await ResolveBannedCardsPresentAsync(resolvedEntries, cancellationToken).ConfigureAwait(false);
        var priorState = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
        var state = BuildState(priorState, resolvedEntries, commanderResolution.CommanderNames, request);
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
                string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)));
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

        bool hadFlaggedCommander = entries.Any(entry => entry.IsCommander);
        bool selectionRequired = commanderChoices.Count > 0 && (selectedCommanderSupplied || hadFlaggedCommander);
        return new CommanderResolution([], commanderChoices, selectionRequired);
    }

    private static CutLabState BuildState(
        CutLabState priorState,
        IReadOnlyList<ResolvedCutLabEntry> resolvedEntries,
        IReadOnlyList<string> commanderNames,
        CutLabRequest request)
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
        bool IsCommander);

    private sealed record CommanderResolution(
        IReadOnlyList<string> CommanderNames,
        IReadOnlyList<string> CommanderChoices,
        bool SelectionRequired);
}
