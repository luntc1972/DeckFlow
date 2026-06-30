using DeckFlow.Core.Bracket;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.Bracket;

/// <summary>
/// Orchestrates the bracket classification pipeline: load a deck from a URL or pasted
/// text, detect two-card combos via Commander Spellbook (null result = unavailable, not
/// zero combos — BRACKET-03), classify via <see cref="BracketClassifier"/>, and build
/// the paste artifact via <see cref="BracketPromptVariantRegistry"/>.
/// </summary>
public sealed class BracketClassificationService : IBracketClassificationService
{
    // Mirrors ManabaseAnalysisService.MaxDeckSourceChars — same abuse-cap rationale.
    private const int MaxDeckSourceChars = 100_000;

    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly ICommanderSpellbookService _spellbookService;
    private readonly IGameChangerCatalogService _catalogService;
    private readonly BracketPromptVariantRegistry _registry;
    private readonly ILogger<BracketClassificationService> _logger;

    /// <summary>Creates the classification service.</summary>
    internal BracketClassificationService(
        IDeckEntryLoader deckEntryLoader,
        ICommanderSpellbookService spellbookService,
        IGameChangerCatalogService catalogService,
        BracketPromptVariantRegistry registry,
        ILogger<BracketClassificationService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(spellbookService);
        ArgumentNullException.ThrowIfNull(catalogService);
        ArgumentNullException.ThrowIfNull(registry);

        _deckEntryLoader = deckEntryLoader;
        _spellbookService = spellbookService;
        _catalogService = catalogService;
        _registry = registry;
        _logger = logger ?? NullLogger<BracketClassificationService>.Instance;
    }

    /// <inheritdoc />
    public async Task<BracketClassificationResult> ClassifyAsync(
        string deckSource,
        int? targetBracketNumber,
        string platform,
        string? deckName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deckSource))
        {
            throw new InvalidOperationException("Enter a deck URL or paste a deck list.");
        }

        if (deckSource.Length > MaxDeckSourceChars)
        {
            throw new InvalidOperationException("That deck input is too large to classify.");
        }

        DeckSourceLoadResult load;
        try
        {
            load = await _deckEntryLoader
                .LoadFromSourceAsync(deckSource, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DeckParseException exception)
        {
            // Surface a parse failure as a user-facing validation error, not a 500.
            throw new InvalidOperationException(exception.Message, exception);
        }

        if (load.Entries.Count == 0)
        {
            throw new InvalidOperationException("That deck looks empty.");
        }

        _logger.LogInformation(
            "BracketClassify: loaded {CardCount} entries from source.", load.Entries.Count);

        // Null from FindCombosAsync means the API was unavailable — NEVER treat as zero combos.
        // A null twoCardCombos passed to BracketClassifier sets ComboDetectionAvailable=false and
        // suppresses the two-card combo B4 gate (BRACKET-03 / Pitfall 1).
        CommanderSpellbookResult? comboResult = await _spellbookService
            .FindCombosAsync(load.Entries, cancellationToken)
            .ConfigureAwait(false);

        // Why: map only two-card combos from the Spellbook result; 3+-card combos do not trigger
        // the two-card B4 gate per the WotC rubric. If comboResult is null (API unavailable) we
        // preserve null rather than falling back to an empty list so the classifier knows combo
        // detection was unavailable (not that the deck has zero combos).
        IReadOnlyList<TwoCardCombo>? twoCardCombos = comboResult is null
            ? null
            : comboResult.IncludedCombos
                .Where(c => c.CardNames.Count == 2)
                .Select(c => new TwoCardCombo(c.CardNames, c.Results))
                .ToList();

        GameChangerCatalog catalog = _catalogService.GetCatalog();

        BracketClassification classification = BracketClassifier.Classify(
            load.Entries, catalog, twoCardCombos);

        _logger.LogInformation(
            "BracketClassify: deck classified as B{BracketNumber}; " +
            "ComboDetectionAvailable={ComboAvailable}; " +
            "TwoCardCombos={TwoCardComboCount}.",
            classification.BracketNumber,
            classification.ComboDetectionAvailable,
            classification.TwoCardCombos?.Count);

        string artifact = _registry.Build(
            AiPlatform.Normalize(platform),
            classification,
            targetBracketNumber,
            deckName,
            catalog.Tiers,
            catalog,
            cancellationToken);

        return new BracketClassificationResult(
            classification,
            catalog.Tiers,
            artifact,
            targetBracketNumber,
            load.FallbackNotice);
    }
}
