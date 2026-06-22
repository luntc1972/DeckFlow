using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Services;
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
public sealed record ManabaseAnalysisResult(
    ManabaseReport Report,
    string InputSummary,
    IReadOnlyList<string> Unresolved,
    string? ImportWarning,
    string ChatGptSwapPrompt,
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

    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly IScryfallCardResolver _scryfallCardResolver;
    private readonly ILogger<ManabaseAnalysisService> _logger;

    /// <summary>Creates the analysis service.</summary>
    public ManabaseAnalysisService(
        IDeckEntryLoader deckEntryLoader,
        IScryfallCardResolver scryfallCardResolver,
        ILogger<ManabaseAnalysisService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);

        _deckEntryLoader = deckEntryLoader;
        _scryfallCardResolver = scryfallCardResolver;
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
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck, options.Mode, options.CommanderImportance, options.CostOverrides);

        string decklistText = string.Join(
            "\n",
            deckCards.Select(e => $"{e.Quantity} {e.Name}"));

        int landCount = report.ActualLands;
        int cardCount = deckCards.Sum(e => e.Quantity);
        string inputSummary = $"{cardCount} cards · {landCount} lands"
            + (unresolved.Count > 0 ? $" · {unresolved.Count} unresolved" : string.Empty);

        string swapPrompt = ManabaseSwapPromptBuilder.Build(report, deckName, decklistText, options.Mode);

        return new ManabaseAnalysisResult(
            report, inputSummary, unresolved, load.FallbackNotice, swapPrompt, deck.CostSuggestions);
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
