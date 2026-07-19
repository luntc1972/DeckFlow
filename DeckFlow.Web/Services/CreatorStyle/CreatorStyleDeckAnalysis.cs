using System.Net;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using RestSharp;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Shared creator-style Scryfall resolution and manabase analysis helper.
/// </summary>
internal static class CreatorStyleDeckAnalysis
{
    internal static async Task<SubmittedDeckResolution> AnalyzeSubmittedDeckAsync(
        IReadOnlyList<DeckEntry> entries,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> executeCollectionAsync,
        Func<string, CancellationToken, Task<ScryfallCard?>> searchFallbackCardAsync,
        Action<string> unresolvedCardLogger,
        string errorMessageSuffix,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(executeCollectionAsync);
        ArgumentNullException.ThrowIfNull(searchFallbackCardAsync);
        ArgumentNullException.ThrowIfNull(unresolvedCardLogger);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessageSuffix);

        ResolvedScryfallCards resolvedCards = await ResolveCardsAsync(entries, executeCollectionAsync, errorMessageSuffix, cancellationToken).ConfigureAwait(false);
        var deckEntries = new List<DeckCardEntry>(entries.Count);
        ScryfallCard? resolvedCommanderCard = null;

        foreach (DeckEntry entry in entries)
        {
            if (!resolvedCards.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out ScryfallCardData? resolvedCardData))
            {
                ScryfallCard? fallback = await searchFallbackCardAsync(entry.Name, cancellationToken).ConfigureAwait(false);
                if (fallback is not null)
                {
                    resolvedCards.Add(fallback);
                    resolvedCards.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out resolvedCardData);
                }
            }

            if (resolvedCardData is null)
            {
                unresolvedCardLogger(entry.Name);
                continue;
            }

            bool isCommander = string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase);
            if (isCommander && resolvedCommanderCard is null)
            {
                resolvedCommanderCard = resolvedCards.GetRawCard(resolvedCardData);
            }

            deckEntries.Add(new DeckCardEntry
            {
                Card = resolvedCardData,
                Quantity = entry.Quantity,
                IsCommander = isCommander
            });
        }

        if (deckEntries.Count == 0)
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

        ManabaseDeck deck = Classify(deckEntries);
        ManabaseReport report = Analyze(deck);

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

    internal static async Task<ManabaseReport> AnalyzeDeckAsync(
        IReadOnlyList<DeckEntry> entries,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> executeCollectionAsync,
        Func<string, CancellationToken, Task<ScryfallCard?>> searchFallbackCardAsync,
        Action<string> unresolvedCardLogger,
        string errorMessageSuffix,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(executeCollectionAsync);
        ArgumentNullException.ThrowIfNull(searchFallbackCardAsync);
        ArgumentNullException.ThrowIfNull(unresolvedCardLogger);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessageSuffix);

        ResolvedScryfallCards resolvedCards = await ResolveCardsAsync(entries, executeCollectionAsync, errorMessageSuffix, cancellationToken).ConfigureAwait(false);
        var deckEntries = new List<DeckCardEntry>(entries.Count);

        foreach (DeckEntry entry in entries)
        {
            if (!resolvedCards.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out ScryfallCardData? card))
            {
                ScryfallCard? fallback = await searchFallbackCardAsync(entry.Name, cancellationToken).ConfigureAwait(false);
                if (fallback is not null)
                {
                    resolvedCards.Add(fallback);
                    resolvedCards.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out card);
                }
            }

            if (card is null)
            {
                unresolvedCardLogger(entry.Name);
                continue;
            }

            deckEntries.Add(new DeckCardEntry
            {
                Card = card,
                Quantity = entry.Quantity,
                IsCommander = string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)
            });
        }

        if (deckEntries.Count == 0)
        {
            return EmptyReport();
        }

        return Analyze(Classify(deckEntries));
    }

    internal static async Task<ResolvedScryfallCards> ResolveCardsAsync(
        IReadOnlyList<DeckEntry> deckCards,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> executeCollectionAsync,
        string errorMessageSuffix,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deckCards);
        ArgumentNullException.ThrowIfNull(executeCollectionAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessageSuffix);

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

        var resolvedCards = new ResolvedScryfallCards();
        for (int offset = 0; offset < identifiers.Count; offset += ScryfallLimits.CollectionBatchSize)
        {
            object[] batch = identifiers.Skip(offset).Take(ScryfallLimits.CollectionBatchSize).ToArray();
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new { identifiers = batch });

            RestResponse<ScryfallCollectionResponse> response = await executeCollectionAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during {errorMessageSuffix}",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            foreach (ScryfallCard card in response.Data.Data)
            {
                resolvedCards.Add(card);
            }
        }

        return resolvedCards;
    }

    internal static double ToHealthScore(ManabaseHealth health)
    {
        return health switch
        {
            ManabaseHealth.Healthy => 3,
            ManabaseHealth.Functional => 2,
            ManabaseHealth.Workable => 1,
            _ => 0
        };
    }

    internal static ManabaseReport EmptyReport()
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

    internal static ManabaseDeck Classify(IReadOnlyList<DeckCardEntry> deckEntries)
        => ManabaseClassifier.Classify(ScryfallCardFactMapper.ToCardFacts(deckEntries), isSingleton: true);

    internal static ManabaseReport Analyze(ManabaseDeck deck)
        => ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);

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

    internal sealed class ResolvedScryfallCards
    {
        private readonly ScryfallCardNameIndex _nameIndex = new();
        private readonly Dictionary<ScryfallCardData, ScryfallCard> _rawCardsByData = new(ReferenceEqualityComparer.Instance);

        public void Add(ScryfallCard card)
        {
            ArgumentNullException.ThrowIfNull(card);

            ScryfallCardData cardData = ScryfallCardDataMapper.ToCardData(card);
            _nameIndex.Add(cardData);
            _rawCardsByData[cardData] = card;
        }

        public bool TryResolve(string name, string? setCode, string? collectorNumber, out ScryfallCardData? card)
        {
            ArgumentNullException.ThrowIfNull(name);

            return _nameIndex.TryResolve(name, setCode, collectorNumber, out card);
        }

        public ScryfallCard GetRawCard(ScryfallCardData cardData)
        {
            if (_rawCardsByData.TryGetValue(cardData, out ScryfallCard? rawCard))
            {
                return rawCard;
            }

            throw new InvalidOperationException("Resolved creator-style card data did not have a matching raw Scryfall card.");
        }
    }
}
