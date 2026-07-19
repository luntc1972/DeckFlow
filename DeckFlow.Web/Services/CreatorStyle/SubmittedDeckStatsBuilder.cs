using System.Net;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Knowledge.CreatorStyleRubric;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Builds submitted-deck statistics and deck-context inputs for creator-style evaluation.
/// </summary>
public interface ISubmittedDeckStatsBuilder
{
    /// <summary>
    /// Loads and analyzes a submitted deck source.
    /// </summary>
    /// <param name="deckSource">Deck URL or pasted export text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The submitted-deck analysis result.</returns>
    Task<SubmittedDeckAnalysis> BuildAsync(string deckSource, CancellationToken cancellationToken = default);
}

/// <summary>
/// Carries the submitted-deck statistics, grounding context, and normalized load result.
/// </summary>
public sealed record SubmittedDeckAnalysis
{
    /// <summary>
    /// Gets the submitted-deck statistics keyed by canonical measured metric strings.
    /// </summary>
    public required SubmittedDeckStats Stats { get; init; }

    /// <summary>
    /// Gets the deck-context inputs needed for card-grounding and whitelist checks.
    /// </summary>
    public required CardGroundingDeckContext DeckContext { get; init; }

    /// <summary>
    /// Gets the loaded deck entries after commander inference has been applied.
    /// </summary>
    public required IReadOnlyList<DeckEntry> Entries { get; init; }

    /// <summary>
    /// Gets the resolved commander name when card resolution succeeds.
    /// </summary>
    public string? ResolvedCommanderName { get; init; }

    /// <summary>
    /// Gets the loader-provided import notice, if any.
    /// </summary>
    public string? ImportNotice { get; init; }
}

/// <summary>
/// Produces submitted-deck metrics using the same category, combo, and Karsten pipelines as creator profiles.
/// </summary>
public sealed class SubmittedDeckStatsBuilder : ISubmittedDeckStatsBuilder
{
    private const int ScryfallBatchSize = 75;
    private static readonly HashSet<string> AnalyzedBoards = new(StringComparer.OrdinalIgnoreCase)
    {
        "mainboard",
        "commander"
    };

    private readonly IDeckEntryLoader? _deckEntryLoader;
    private readonly CategoryKnowledgeRepository? _categoryKnowledgeRepository;
    private readonly ICommanderSpellbookService? _commanderSpellbookService;
    private readonly IScryfallCardResolver? _scryfallCardResolver;
    private readonly ILogger<SubmittedDeckStatsBuilder> _logger;
    private readonly Func<string, CancellationToken, Task<DeckSourceLoadResult>>? _loadDeckAsyncOverride;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<string>>>? _getCategoriesAsyncOverride;
    private readonly Func<IReadOnlyList<DeckEntry>, CancellationToken, Task<CommanderSpellbookResult?>>? _findCombosAsyncOverride;
    private readonly Func<IReadOnlyList<DeckEntry>, CancellationToken, Task<SubmittedDeckResolution>>? _analyzeSubmittedDeckAsyncOverride;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? _executeCollectionAsyncOverride;
    private readonly Func<string, CancellationToken, Task<ScryfallCard?>>? _searchFallbackCardAsyncOverride;

    /// <summary>
    /// Creates a submitted-deck stats builder using the production dependencies.
    /// </summary>
    public SubmittedDeckStatsBuilder(
        IDeckEntryLoader deckEntryLoader,
        CategoryKnowledgeRepository categoryKnowledgeRepository,
        ICommanderSpellbookService commanderSpellbookService,
        IScryfallCardResolver scryfallCardResolver,
        ILogger<SubmittedDeckStatsBuilder>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(categoryKnowledgeRepository);
        ArgumentNullException.ThrowIfNull(commanderSpellbookService);
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);

        _deckEntryLoader = deckEntryLoader;
        _categoryKnowledgeRepository = categoryKnowledgeRepository;
        _commanderSpellbookService = commanderSpellbookService;
        _scryfallCardResolver = scryfallCardResolver;
        _logger = logger ?? NullLogger<SubmittedDeckStatsBuilder>.Instance;
    }

    internal SubmittedDeckStatsBuilder(
        Func<string, CancellationToken, Task<DeckSourceLoadResult>>? loadDeckAsyncOverride = null,
        Func<string, CancellationToken, Task<IReadOnlyList<string>>>? getCategoriesAsyncOverride = null,
        Func<IReadOnlyList<DeckEntry>, CancellationToken, Task<CommanderSpellbookResult?>>? findCombosAsyncOverride = null,
        Func<IReadOnlyList<DeckEntry>, CancellationToken, Task<SubmittedDeckResolution>>? analyzeSubmittedDeckAsyncOverride = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsyncOverride = null,
        Func<string, CancellationToken, Task<ScryfallCard?>>? searchFallbackCardAsyncOverride = null,
        ILogger<SubmittedDeckStatsBuilder>? logger = null)
    {
        _logger = logger ?? NullLogger<SubmittedDeckStatsBuilder>.Instance;
        _loadDeckAsyncOverride = loadDeckAsyncOverride;
        _getCategoriesAsyncOverride = getCategoriesAsyncOverride;
        _findCombosAsyncOverride = findCombosAsyncOverride;
        _analyzeSubmittedDeckAsyncOverride = analyzeSubmittedDeckAsyncOverride;
        _executeCollectionAsyncOverride = executeCollectionAsyncOverride;
        _searchFallbackCardAsyncOverride = searchFallbackCardAsyncOverride;
    }

    /// <inheritdoc />
    public async Task<SubmittedDeckAnalysis> BuildAsync(string deckSource, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckSource);

        DeckSourceLoadResult loaded = await LoadDeckAsync(deckSource, cancellationToken).ConfigureAwait(false);
        List<DeckEntry> flaggedEntries = ReflagInferredCommanders(loaded.Entries.ToList());
        List<DeckEntry> analyzedEntries = flaggedEntries
            .Where(entry => AnalyzedBoards.Contains(entry.Board))
            .ToList();

        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories =
            await ResolveCategoriesAsync(analyzedEntries, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, int> categoryCounts = CountCategories(analyzedEntries, cardCategories);
        double comboCount = await ResolveComboCountAsync(analyzedEntries, cancellationToken).ConfigureAwait(false);
        SubmittedDeckResolution resolution = await ResolveSubmittedDeckAsync(analyzedEntries, cancellationToken).ConfigureAwait(false);

        var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (string category in ContentTagVocabulary.CardCategories)
        {
            metrics[$"category_ratio:{category}"] = categoryCounts.TryGetValue(category, out int count)
                ? count
                : 0d;
        }

        metrics["combo_density:included_per_deck"] = comboCount;
        metrics["karsten:land_delta"] = resolution.Report.LandDelta;
        metrics["karsten:target_lands"] = resolution.Report.TargetLands;
        metrics["karsten:health_score"] = resolution.HasResolvedDeck
            ? ToHealthScore(resolution.Report.Health)
            : 0d;

        return new SubmittedDeckAnalysis
        {
            Stats = new SubmittedDeckStats
            {
                Metrics = metrics,
                DeckSize = analyzedEntries.Sum(entry => entry.Quantity),
                CommanderCount = analyzedEntries
                    .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
                    .Sum(entry => entry.Quantity)
            },
            DeckContext = resolution.DeckContext,
            Entries = flaggedEntries,
            ResolvedCommanderName = resolution.ResolvedCommanderName,
            ImportNotice = loaded.FallbackNotice
        };
    }

    private async Task<DeckSourceLoadResult> LoadDeckAsync(string deckSource, CancellationToken cancellationToken)
    {
        if (_loadDeckAsyncOverride is not null)
        {
            return await _loadDeckAsyncOverride(deckSource, cancellationToken).ConfigureAwait(false);
        }

        return await _deckEntryLoader!
            .LoadFromSourceAsync(deckSource, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ResolveCategoriesAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken)
    {
        var categoryMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string cardName in entries
                     .Select(entry => entry.Name)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            IReadOnlyList<string> categories = await GetCategoriesAsync(cardName, cancellationToken).ConfigureAwait(false);
            categoryMap[cardName] = categories;
        }

        return categoryMap;
    }

    private async Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken)
    {
        if (_getCategoriesAsyncOverride is not null)
        {
            return await _getCategoriesAsyncOverride(cardName, cancellationToken).ConfigureAwait(false);
        }

        return await _categoryKnowledgeRepository!
            .GetCategoriesAsync(cardName, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, int> CountCategories(
        IReadOnlyList<DeckEntry> entries,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories)
    {
        if (entries.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var sample = new CreatorDeckSample
        {
            DeckId = "submitted-deck",
            Entries = entries,
            CardCount = entries.Sum(entry => entry.Quantity),
            ConfidenceMarker = string.Empty
        };

        return CategoryCounter.CountPerDeck(sample, cardCategories);
    }

    private async Task<double> ResolveComboCountAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
    {
        try
        {
            CommanderSpellbookResult? result = _findCombosAsyncOverride is not null
                ? await _findCombosAsyncOverride(entries, cancellationToken).ConfigureAwait(false)
                : await _commanderSpellbookService!
                    .FindCombosAsync(entries, cancellationToken)
                    .ConfigureAwait(false);

            return result?.IncludedCombos.Count ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Commander Spellbook lookup failed; continuing without combo density.");
            return 0;
        }
    }

    private async Task<SubmittedDeckResolution> ResolveSubmittedDeckAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken)
    {
        if (_analyzeSubmittedDeckAsyncOverride is not null)
        {
            return await _analyzeSubmittedDeckAsyncOverride(entries, cancellationToken).ConfigureAwait(false);
        }

        if (entries.Count == 0)
        {
            return EmptyResolution();
        }

        return await AnalyzeSubmittedDeckAsync(entries, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SubmittedDeckResolution> AnalyzeSubmittedDeckAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken)
    {
        ResolvedScryfallCardIndex index = await ResolveCardsAsync(entries, cancellationToken).ConfigureAwait(false);
        var deckEntries = new List<DeckCardEntry>(entries.Count);
        ScryfallCard? resolvedCommanderCard = null;

        foreach (DeckEntry entry in entries)
        {
            ResolvedScryfallCard? resolvedCard;
            if (!index.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out resolvedCard))
            {
                ScryfallCard? fallback = await SearchFallbackCardAsync(entry.Name, cancellationToken).ConfigureAwait(false);
                if (fallback is not null)
                {
                    resolvedCard = new ResolvedScryfallCard(fallback, ScryfallCardDataMapper.ToCardData(fallback));
                    index.Add(resolvedCard);
                }
            }

            if (resolvedCard is null)
            {
                _logger.LogDebug("Skipping unresolved submitted-deck manabase card {CardName}.", entry.Name);
                continue;
            }

            bool isCommander = string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase);
            if (isCommander && resolvedCommanderCard is null)
            {
                resolvedCommanderCard = resolvedCard.Card;
            }

            deckEntries.Add(new DeckCardEntry
            {
                Card = resolvedCard.Data,
                Quantity = entry.Quantity,
                IsCommander = isCommander
            });
        }

        if (deckEntries.Count == 0)
        {
            return EmptyResolution();
        }

        IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        // Why: this must match MeasuredStyleProfileBuilder's isSingleton:true + Casual path exactly
        // so submitted-deck karsten metrics stay apples-to-apples with the fused creator targets.
        ManabaseReport report = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);

        return new SubmittedDeckResolution
        {
            Report = report,
            DeckContext = new CardGroundingDeckContext
            {
                CommanderColorIdentity = resolvedCommanderCard?.ColorIdentity?
                    .Where(IsWubrgSymbol)
                    .ToHashSet(StringComparer.Ordinal)
                    ?? new HashSet<string>(StringComparer.Ordinal),
                DeckProducedColors = deck.Sources
                    .SelectMany(source => source.Produces)
                    .Select(ToWubrgChar)
                    .Where(color => color != '\0')
                    .ToHashSet(),
                DeckCardNames = entries
                    .Select(entry => CardNormalizer.Normalize(entry.Name))
                    .ToHashSet(StringComparer.Ordinal)
            },
            ResolvedCommanderName = resolvedCommanderCard?.Name,
            HasResolvedDeck = true
        };
    }

    private async Task<ResolvedScryfallCardIndex> ResolveCardsAsync(
        IReadOnlyList<DeckEntry> deckCards,
        CancellationToken cancellationToken)
    {
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

        var index = new ResolvedScryfallCardIndex();
        for (int offset = 0; offset < identifiers.Count; offset += ScryfallBatchSize)
        {
            object[] batch = identifiers.Skip(offset).Take(ScryfallBatchSize).ToArray();
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new { identifiers = batch });

            RestResponse<ScryfallCollectionResponse> response = await ExecuteCollectionAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during submitted-deck manabase analysis.",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            foreach (ScryfallCard card in response.Data.Data)
            {
                index.Add(new ResolvedScryfallCard(card, ScryfallCardDataMapper.ToCardData(card)));
            }
        }

        return index;
    }

    private async Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(
        RestRequest request,
        CancellationToken cancellationToken)
    {
        if (_executeCollectionAsyncOverride is not null)
        {
            return await _executeCollectionAsyncOverride(request, cancellationToken).ConfigureAwait(false);
        }

        return await _scryfallCardResolver!
            .ExecuteCollectionAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
    {
        if (_searchFallbackCardAsyncOverride is not null)
        {
            return await _searchFallbackCardAsyncOverride(cardName, cancellationToken).ConfigureAwait(false);
        }

        return await _scryfallCardResolver!
            .SearchFallbackCardAsync(cardName, cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<DeckEntry> ReflagInferredCommanders(List<DeckEntry> entries)
    {
        IReadOnlyList<string> commanderNames = CommanderInference.InferLeadingCommanderNames(entries);
        if (commanderNames.Count == 0)
        {
            return entries;
        }

        HashSet<string> commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return entries
            .Select(entry => commanderNameSet.Contains(entry.Name)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                ? entry with { Board = "commander" }
                : entry)
            .ToList();
    }

    private static double ToHealthScore(ManabaseHealth health)
    {
        return health switch
        {
            ManabaseHealth.Healthy => 3,
            ManabaseHealth.Functional => 2,
            ManabaseHealth.Workable => 1,
            _ => 0
        };
    }

    private static bool IsWubrgSymbol(string symbol)
        => symbol is "W" or "U" or "B" or "R" or "G";

    private static char ToWubrgChar(ManaColor color)
    {
        return color switch
        {
            ManaColor.White => 'W',
            ManaColor.Blue => 'U',
            ManaColor.Black => 'B',
            ManaColor.Red => 'R',
            ManaColor.Green => 'G',
            _ => '\0'
        };
    }

    private static SubmittedDeckResolution EmptyResolution()
    {
        return new SubmittedDeckResolution
        {
            Report = EmptyReport(),
            DeckContext = new CardGroundingDeckContext
            {
                CommanderColorIdentity = new HashSet<string>(StringComparer.Ordinal),
                DeckProducedColors = new HashSet<char>(),
                DeckCardNames = new HashSet<string>(StringComparer.Ordinal)
            },
            ResolvedCommanderName = null,
            HasResolvedDeck = false
        };
    }

    private static ManabaseReport EmptyReport()
        => new()
        {
            ActualLands = 0,
            TargetLands = 0,
            ColorFindings = Array.Empty<ColorSourceFinding>(),
            Mode = ManabaseMode.Casual,
            Castability = Array.Empty<CardCastability>(),
            ColorSpellCounts = new Dictionary<ManaColor, int>(),
            CommanderColors = Array.Empty<ManaColor>(),
            LandTarget = null,
            TapAnalysis = null,
            MulliganEvaluation = null,
            DemandingCards = Array.Empty<DemandingCard>(),
            RampSourceNames = Array.Empty<string>(),
            RampAndDrawNames = Array.Empty<string>(),
            UnsupportedInteractions = Array.Empty<UnsupportedInteraction>(),
            Summary = string.Empty
        };

    private sealed class ResolvedScryfallCardIndex
    {
        private readonly ScryfallCardNameIndex _index = new();
        private readonly Dictionary<string, ResolvedScryfallCard> _byName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ResolvedScryfallCard> _byFrontFace = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ResolvedScryfallCard> _byPrinting = new(StringComparer.Ordinal);

        public void Add(ResolvedScryfallCard card)
        {
            ArgumentNullException.ThrowIfNull(card);

            _index.Add(card.Data);

            string? printing = ScryfallCardNameIndex.PrintingKey(card.Card.SetCode, card.Card.CollectorNumber);
            if (printing is not null)
            {
                _byPrinting[printing] = card;
            }

            _byName[Normalize(card.Card.Name)] = card;
            string? frontFace = FrontFace(card.Card.Name);
            if (frontFace is not null)
            {
                _byFrontFace[Normalize(frontFace)] = card;
            }
        }

        public bool TryResolve(string name, string? setCode, string? collectorNumber, out ResolvedScryfallCard? card)
        {
            ArgumentNullException.ThrowIfNull(name);

            string? printing = ScryfallCardNameIndex.PrintingKey(setCode, collectorNumber);
            if (printing is not null && _byPrinting.TryGetValue(printing, out ResolvedScryfallCard? printingHit))
            {
                card = printingHit;
                return true;
            }

            string normalized = Normalize(name);
            string? frontFace = FrontFace(name);
            if (_byName.TryGetValue(normalized, out ResolvedScryfallCard? exactHit)
                || (frontFace is not null && _byName.TryGetValue(Normalize(frontFace), out exactHit))
                || _byFrontFace.TryGetValue(normalized, out exactHit)
                || (frontFace is not null && _byFrontFace.TryGetValue(Normalize(frontFace), out exactHit)))
            {
                card = exactHit;
                return true;
            }

            card = null;
            return false;
        }

        private static string Normalize(string value) => value.Trim().ToLowerInvariant();

        private static string? FrontFace(string name)
        {
            const string faceSeparator = "//";
            int split = name.IndexOf(faceSeparator, StringComparison.Ordinal);
            return split > 0 ? name[..split] : null;
        }
    }
}

internal sealed record SubmittedDeckResolution
{
    public required ManabaseReport Report { get; init; }

    public required CardGroundingDeckContext DeckContext { get; init; }

    public string? ResolvedCommanderName { get; init; }

    public required bool HasResolvedDeck { get; init; }
}

internal sealed record ResolvedScryfallCard(ScryfallCard Card, ScryfallCardData Data);
