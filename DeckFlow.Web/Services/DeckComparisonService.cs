using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using Microsoft.Extensions.Logging.Abstractions;
using DeckFlow.Web.Services.Http;
using Polly;
using Polly.Registry;
using RestSharp;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;

namespace DeckFlow.Web.Services;

/// <summary>
/// Builds the deck-comparison prompt packet by hydrating two decks side-by-side.
/// </summary>
public interface IDeckComparisonService
{
    /// <summary>
    /// Builds the deck-comparison packet for the supplied two-deck request.
    /// </summary>
    /// <param name="request">Comparison workflow request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DeckComparisonResult> BuildAsync(DeckComparisonRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Returns the results of a deck-comparison packet build.
/// </summary>
public sealed record DeckComparisonResult(
    string InputSummary,
    string DeckAListText,
    string DeckBListText,
    string DeckAComboText,
    string DeckBComboText,
    string ComparisonContextText,
    string ComparisonPromptText,
    string FollowUpPromptText,
    string ComparisonSchemaJson,
    DeckComparisonResponse? ComparisonResponse,
    string? TimingSummary,
    string? ResolvedDeckACommander = null,
    string? ResolvedDeckBCommander = null,
    string? RequestContextText = null);

/// <summary>
/// Hydrates two decks via Scryfall, queries Commander Spellbook for each, derives the side-by-side comparison context (role counts, mana curves, combo gaps), and composes the JSON-bound comparison prompt artifacts saved to the session zip.
/// </summary>
public sealed class DeckComparisonService : IDeckComparisonService
{
    private const int ScryfallBatchSize = 75;

    private readonly IMoxfieldDeckImporter _moxfieldDeckImporter;
    private readonly IArchidektDeckImporter _archidektDeckImporter;
    private readonly MoxfieldParser _moxfieldParser;
    private readonly ArchidektParser _archidektParser;
    private readonly ICommanderSpellbookService _commanderSpellbookService;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> _executeCollectionAsync;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>> _executeSearchAsync;
    private readonly ILogger<DeckComparisonService> _logger;
    private readonly ComparisonPromptVariantRegistry _comparisonPromptRegistry;
    private readonly FollowUpPromptVariantRegistry _followUpPromptRegistry;

    internal DeckComparisonService(
        IScryfallRestClientFactory scryfallRestClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser,
        ICommanderSpellbookService commanderSpellbookService,
        ComparisonPromptVariantRegistry comparisonPromptRegistry,
        FollowUpPromptVariantRegistry followUpPromptRegistry,
        ILogger<DeckComparisonService>? logger = null,
        RestClient? restClientOverride = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsyncOverride = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsyncOverride = null)
    {
        ArgumentNullException.ThrowIfNull(scryfallRestClientFactory);
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(moxfieldDeckImporter);
        ArgumentNullException.ThrowIfNull(archidektDeckImporter);
        ArgumentNullException.ThrowIfNull(moxfieldParser);
        ArgumentNullException.ThrowIfNull(archidektParser);
        ArgumentNullException.ThrowIfNull(commanderSpellbookService);
        ArgumentNullException.ThrowIfNull(comparisonPromptRegistry);
        ArgumentNullException.ThrowIfNull(followUpPromptRegistry);
        var pipeline = pipelineProvider.GetPipeline<RestResponse>("scryfall") ?? ResiliencePipeline<RestResponse>.Empty;
        _moxfieldDeckImporter = moxfieldDeckImporter;
        _archidektDeckImporter = archidektDeckImporter;
        _moxfieldParser = moxfieldParser;
        _archidektParser = archidektParser;
        _commanderSpellbookService = commanderSpellbookService;
        _comparisonPromptRegistry = comparisonPromptRegistry;
        _followUpPromptRegistry = followUpPromptRegistry;
        _logger = logger ?? NullLogger<DeckComparisonService>.Instance;
        var client = restClientOverride ?? scryfallRestClientFactory.Create();
        _executeCollectionAsync = executeCollectionAsyncOverride ?? ((request, cancellationToken) =>
            ScryfallThrottle.ExecuteAsync(
                token => pipeline.ExecuteAsync(
                    async pollyCt => await client.ExecuteAsync<ScryfallCollectionResponse>(request, pollyCt).ConfigureAwait(false),
                    token).AsTask(),
                cancellationToken));
        _executeSearchAsync = executeSearchAsyncOverride ?? ((request, cancellationToken) =>
            ScryfallThrottle.ExecuteAsync(
                token => pipeline.ExecuteAsync(
                    async pollyCt => await client.ExecuteAsync<ScryfallSearchResponse>(request, pollyCt).ConfigureAwait(false),
                    token).AsTask(),
                cancellationToken));
    }

    public async Task<DeckComparisonResult> BuildAsync(DeckComparisonRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var overallStopwatch = Stopwatch.StartNew();
        var timings = new List<(string Label, long Ms, string? Detail)>();

        if (string.IsNullOrWhiteSpace(request.DeckASource))
        {
            throw new InvalidOperationException("Deck A URL or deck text is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DeckBSource))
        {
            throw new InvalidOperationException("Deck B URL or deck text is required.");
        }

        var deckABracket = CommanderBracketCatalog.Find(request.DeckABracket)
            ?? throw new InvalidOperationException("Choose a Commander bracket for Deck A before generating the comparison packet.");
        var deckBBracket = CommanderBracketCatalog.Find(request.DeckBBracket)
            ?? throw new InvalidOperationException("Choose a Commander bracket for Deck B before generating the comparison packet.");

        var deckALoadStopwatch = Stopwatch.StartNew();
        var deckA = await LoadDeckAsync("Deck A", request.DeckASource, cancellationToken).ConfigureAwait(false);
        timings.Add(("Deck A load", deckALoadStopwatch.ElapsedMilliseconds, $"{deckA.PlayableEntries.Sum(entry => entry.Quantity)} cards"));

        var deckBLoadStopwatch = Stopwatch.StartNew();
        var deckB = await LoadDeckAsync("Deck B", request.DeckBSource, cancellationToken).ConfigureAwait(false);
        timings.Add(("Deck B load", deckBLoadStopwatch.ElapsedMilliseconds, $"{deckB.PlayableEntries.Sum(entry => entry.Quantity)} cards"));

        ValidateSameCommander(deckA.CommanderName, deckB.CommanderName);

        var deckAName = ResolveDeckName(request.DeckAName, deckA.CommanderName, "Deck A");
        var deckBName = ResolveDeckName(request.DeckBName, deckB.CommanderName, "Deck B");

        var lookupStopwatch = Stopwatch.StartNew();
        var deckALookup = await LookupCardDetailsAsync("Deck A", deckA.PlayableEntries, cancellationToken).ConfigureAwait(false);
        var deckBLookup = await LookupCardDetailsAsync("Deck B", deckB.PlayableEntries, cancellationToken).ConfigureAwait(false);
        timings.Add(("Scryfall card lookup", lookupStopwatch.ElapsedMilliseconds, $"Deck A {deckALookup.Cards.Count} cards | Deck B {deckBLookup.Cards.Count} cards"));

        var deckACards = deckALookup.Cards;
        var deckBCards = deckBLookup.Cards;

        var deckAListText = BuildDecklistText(deckA.PlayableEntries, deckA.OptionalEntries, deckALookup.OracleNameMap);
        var deckBListText = BuildDecklistText(deckB.PlayableEntries, deckB.OptionalEntries, deckBLookup.OracleNameMap);

        var comboLookupStopwatch = Stopwatch.StartNew();
        var deckACombos = await _commanderSpellbookService.FindCombosAsync(deckA.PlayableEntries, cancellationToken).ConfigureAwait(false);
        var deckBCombos = await _commanderSpellbookService.FindCombosAsync(deckB.PlayableEntries, cancellationToken).ConfigureAwait(false);
        timings.Add((
            "Commander Spellbook",
            comboLookupStopwatch.ElapsedMilliseconds,
            $"Deck A {deckACombos?.IncludedCombos.Count ?? 0} combos | Deck B {deckBCombos?.IncludedCombos.Count ?? 0} combos"));

        var deckASummary = BuildDeckSummary(deckAName, deckA.CommanderName, deckABracket, deckA.PlayableEntries, deckACards, deckACombos);
        var deckBSummary = BuildDeckSummary(deckBName, deckB.CommanderName, deckBBracket, deckB.PlayableEntries, deckBCards, deckBCombos);
        var deckAComboText = BuildComboArtifactText(deckASummary);
        var deckBComboText = BuildComboArtifactText(deckBSummary);
        var comparisonContextText = BuildComparisonContextText(deckASummary, deckBSummary);
        var comparisonSchemaJson = BuildComparisonSchemaJson(deckAName, deckBName, deckA.CommanderName, deckB.CommanderName, deckABracket.Label, deckBBracket.Label);
        var inputSummary = BuildInputSummary(deckASummary, deckBSummary);
        var comparisonPromptText = BuildComparisonPrompt(
            deckASummary,
            deckBSummary,
            deckAListText,
            deckBListText,
            deckAComboText,
            deckBComboText,
            comparisonContextText,
            comparisonSchemaJson,
            request.TargetAiPlatform);
        var followUpPromptText = BuildFollowUpPrompt(comparisonSchemaJson, request.TargetAiPlatform);

        DeckComparisonResponse? comparisonResponse = null;
        if (request.WorkflowStep >= 3 && !string.IsNullOrWhiteSpace(request.ComparisonResponseJson))
        {
            comparisonResponse = ParseComparisonResponse(request.ComparisonResponseJson);
        }

        var timingSummary = BuildTimingSummary(timings, overallStopwatch.ElapsedMilliseconds);

        return new DeckComparisonResult(
            inputSummary,
            deckAListText,
            deckBListText,
            deckAComboText,
            deckBComboText,
            comparisonContextText,
            comparisonPromptText,
            followUpPromptText,
            comparisonSchemaJson,
            comparisonResponse,
            timingSummary,
            ResolvedDeckACommander: deckA.CommanderName,
            ResolvedDeckBCommander: deckB.CommanderName,
            RequestContextText: BuildRequestContextText(request));
    }

    /// <summary>
    /// Plain-text scalar key/value envelope round-tripped through the comparison zip.
    /// Mirrors <see cref="DeckAnalysisPacketService"/>'s BuildRequestContextText for Packets.
    /// Parsed via <see cref="RequestContextParser"/>; unknown keys are ignored.
    /// </summary>
    internal static string BuildRequestContextText(DeckComparisonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder();
        builder.AppendLine($"workflow_step: {request.WorkflowStep}");
        builder.AppendLine($"deck_a_name: {JsonTextFormatterService.NormalizeSingleLine(request.DeckAName, string.Empty)}");
        builder.AppendLine($"deck_b_name: {JsonTextFormatterService.NormalizeSingleLine(request.DeckBName, string.Empty)}");
        builder.AppendLine($"deck_a_bracket: {JsonTextFormatterService.NormalizeSingleLine(request.DeckABracket, string.Empty)}");
        builder.AppendLine($"deck_b_bracket: {JsonTextFormatterService.NormalizeSingleLine(request.DeckBBracket, string.Empty)}");
        builder.AppendLine($"target_ai_platform: {JsonTextFormatterService.NormalizeSingleLine(request.TargetAiPlatform, "ChatGPT")}");
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private async Task<LoadedDeck> LoadDeckAsync(string deckLabel, string deckSource, CancellationToken cancellationToken)
    {
        List<DeckEntry> entries;
        try
        {
            entries = await LoadDeckEntriesAsync(deckSource, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DeckParseException or HttpRequestException)
        {
            throw new InvalidOperationException($"{deckLabel} parse failed: {exception.Message}", exception);
        }

        var playableEntries = entries
            .Where(entry =>
                !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var optionalEntries = entries
            .Where(entry =>
                string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (playableEntries.Count == 0)
        {
            throw new InvalidOperationException($"{deckLabel} parse failed: the submitted deck did not contain any commander or mainboard cards.");
        }

        var hasExplicitCommander = playableEntries.Any(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase));
        var commanderName = playableEntries
            .FirstOrDefault(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            ?.Name;

        if (string.IsNullOrWhiteSpace(commanderName))
        {
            if (!hasExplicitCommander && playableEntries.Count < 2)
            {
                throw new InvalidOperationException($"{deckLabel} parse failed: could not determine a commander from the submitted deck.");
            }

            commanderName = playableEntries
                .Where(entry => entry.Quantity == 1)
                .Select(entry => entry.Name)
                .FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(commanderName))
        {
            throw new InvalidOperationException($"{deckLabel} parse failed: could not determine a commander from the submitted deck.");
        }

        if (!hasExplicitCommander)
        {
            entries = ReflagCommanderEntry(entries, commanderName);
            playableEntries = ReflagCommanderEntry(playableEntries, commanderName);
        }

        return new LoadedDeck(entries, playableEntries, optionalEntries, commanderName ?? string.Empty);
    }

    private static List<DeckEntry> ReflagCommanderEntry(List<DeckEntry> source, string commanderName)
    {
        var matched = false;
        var result = new List<DeckEntry>(source.Count);
        foreach (var entry in source)
        {
            if (!matched
                && entry.Quantity == 1
                && string.Equals(entry.Name, commanderName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(entry with { Board = "commander" });
                matched = true;
            }
            else
            {
                result.Add(entry);
            }
        }
        return result;
    }

    private async Task<List<DeckEntry>> LoadDeckEntriesAsync(string deckSource, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(deckSource.Trim(), UriKind.Absolute, out var uri))
        {
            if (uri.Host.Contains("moxfield.com", StringComparison.OrdinalIgnoreCase))
            {
                return await _moxfieldDeckImporter.ImportAsync(deckSource, cancellationToken).ConfigureAwait(false);
            }

            if (uri.Host.Contains("archidekt.com", StringComparison.OrdinalIgnoreCase))
            {
                return await _archidektDeckImporter.ImportAsync(deckSource, cancellationToken).ConfigureAwait(false);
            }
        }

        try
        {
            return _moxfieldParser.ParseText(deckSource);
        }
        catch (DeckParseException)
        {
        }

        try
        {
            return _archidektParser.ParseText(deckSource);
        }
        catch (DeckParseException)
        {
        }

        throw new InvalidOperationException("The submitted deck was not recognized as a Moxfield URL, Archidekt URL, Moxfield export, or Archidekt export.");
    }

    private async Task<CardLookupResult> LookupCardDetailsAsync(string deckLabel, IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
    {
        var uniqueNames = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .Select(entry => entry.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resolvedCards = new List<ScryfallCard>();
        var oracleNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in Chunk(uniqueNames, ScryfallBatchSize))
        {
            var request = new RestRequest("cards/collection", Method.Post)
                .AddJsonBody(new
                {
                    identifiers = chunk.Select(name => new { name }).ToArray()
                });

            var response = await _executeCollectionAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                throw new HttpRequestException(
                    $"{deckLabel} Scryfall card reference lookup failed while building the comparison packet with HTTP {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }

            foreach (var card in response.Data.Data)
            {
                var submittedName = chunk.FirstOrDefault(name => string.Equals(name, card.Name, StringComparison.OrdinalIgnoreCase));
                if (submittedName is not null)
                {
                    oracleNameMap[submittedName] = card.Name;
                }
            }

            resolvedCards.AddRange(response.Data.Data);

            var unresolvedNames = chunk
                .Where(name => !oracleNameMap.ContainsKey(name))
                .ToList();

            foreach (var unresolvedName in unresolvedNames)
            {
                var fallbackCard = await SearchFallbackCardAsync(unresolvedName, cancellationToken).ConfigureAwait(false);
                if (fallbackCard is null)
                {
                    continue;
                }

                oracleNameMap[unresolvedName] = fallbackCard.Name;
                if (!resolvedCards.Any(card => string.Equals(card.Name, fallbackCard.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    resolvedCards.Add(fallbackCard);
                }
            }
        }

        return new CardLookupResult(resolvedCards, oracleNameMap);
    }

    private async Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
    {
        var normalizedName = cardName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        var request = new RestRequest("cards/search", Method.Get);
        request.AddQueryParameter("q", $"!\"{normalizedName}\"");
        request.AddQueryParameter("unique", "cards");
        request.AddQueryParameter("order", "name");

        var response = await _executeSearchAsync(request, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
        {
            return response.Data?.Data.FirstOrDefault();
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        throw new HttpRequestException(
            $"Scryfall fallback lookup failed while resolving {cardName} with HTTP {(int)response.StatusCode}.",
            null,
            response.StatusCode);
    }

    private sealed record CardLookupResult(IReadOnlyList<ScryfallCard> Cards, IReadOnlyDictionary<string, string> OracleNameMap);

    private static DeckComparisonDeckSummary BuildDeckSummary(
        string deckName,
        string commanderName,
        CommanderBracketOption bracket,
        IReadOnlyList<DeckEntry> entries,
        IReadOnlyList<ScryfallCard> cards,
        CommanderSpellbookResult? comboResult)
    {
        var cardLookup = cards.ToDictionary(card => card.Name, StringComparer.OrdinalIgnoreCase);
        var mainboardEntries = entries
            .Where(entry => !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var commanderEntries = entries
            .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var totalMainboardCards = mainboardEntries.Sum(entry => entry.Quantity);
        var categories = CategoryCountReporter.CountByQuantity(mainboardEntries)
            .Take(8)
            .Select(item => $"{item.Category}: {item.Count}")
            .ToList();

        var curveBuckets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["0-1"] = 0,
            ["2"] = 0,
            ["3"] = 0,
            ["4"] = 0,
            ["5+"] = 0
        };

        var nonlandCardCount = 0;
        var manaValueTotal = 0m;
        var colorIdentity = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lands = 0;
        var creatures = 0;
        var ramp = 0;
        var draw = 0;
        var interaction = 0;
        var wipes = 0;
        var recursion = 0;
        var closingPower = 0;
        var includedCombos = comboResult?.IncludedCombos ?? Array.Empty<SpellbookCombo>();
        var almostIncludedCombos = comboResult?.AlmostIncludedCombos ?? Array.Empty<SpellbookAlmostCombo>();

        foreach (var commanderEntry in commanderEntries)
        {
            if (cardLookup.TryGetValue(commanderEntry.Name, out var commanderCard))
            {
                foreach (var color in commanderCard.ColorIdentity ?? Array.Empty<string>())
                {
                    colorIdentity.Add(color);
                }
            }
        }

        foreach (var entry in mainboardEntries)
        {
            if (!cardLookup.TryGetValue(entry.Name, out var card))
            {
                continue;
            }

            var typeLine = card.TypeLine ?? string.Empty;
            var oracleText = NormalizeOracleText(card);
            var quantity = entry.Quantity;
            var manaValue = EstimateManaValue(card.ManaCost);

            foreach (var color in card.ColorIdentity ?? Array.Empty<string>())
            {
                colorIdentity.Add(color);
            }

            if (typeLine.Contains("Land", StringComparison.OrdinalIgnoreCase))
            {
                lands += quantity;
                curveBuckets["0-1"] += quantity;
                continue;
            }

            nonlandCardCount += quantity;
            manaValueTotal += manaValue * quantity;

            if (manaValue <= 1)
            {
                curveBuckets["0-1"] += quantity;
            }
            else if (manaValue == 2)
            {
                curveBuckets["2"] += quantity;
            }
            else if (manaValue == 3)
            {
                curveBuckets["3"] += quantity;
            }
            else if (manaValue == 4)
            {
                curveBuckets["4"] += quantity;
            }
            else
            {
                curveBuckets["5+"] += quantity;
            }

            if (typeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase))
            {
                creatures += quantity;
            }

            if (IsRampCard(typeLine, oracleText))
            {
                ramp += quantity;
            }

            if (IsDrawCard(oracleText))
            {
                draw += quantity;
            }

            if (IsInteractionCard(typeLine, oracleText))
            {
                interaction += quantity;
            }

            if (IsBoardWipeCard(oracleText))
            {
                wipes += quantity;
            }

            if (IsRecursionCard(oracleText))
            {
                recursion += quantity;
            }

            if (IsClosingPowerCard(typeLine, oracleText))
            {
                closingPower += quantity;
            }
        }

        closingPower += includedCombos.Count * 2 + almostIncludedCombos.Count;
        IReadOnlyList<string> sharedThemes = categories.Count == 0 ? Array.Empty<string>() : categories;
        var averageManaValue = nonlandCardCount == 0 ? 0m : Math.Round(manaValueTotal / nonlandCardCount, 2);
        var comboSummaries = includedCombos
            .Select(combo => $"{string.Join(" + ", combo.CardNames)} -> {string.Join(", ", combo.Results)}")
            .Take(5)
            .ToList();
        var almostComboSummaries = almostIncludedCombos
            .Select(combo => $"{combo.MissingCard} missing from {string.Join(" + ", combo.CardsInDeck)}")
            .Take(5)
            .ToList();

        return new DeckComparisonDeckSummary(
            deckName,
            commanderName,
            bracket,
            totalMainboardCards,
            lands,
            creatures,
            averageManaValue,
            curveBuckets,
            colorIdentity.OrderBy(color => color, StringComparer.OrdinalIgnoreCase).ToList(),
            categories,
            ramp,
            draw,
            interaction,
            wipes,
            recursion,
            closingPower,
            sharedThemes,
            comboSummaries,
            almostComboSummaries,
            includedCombos.Count,
            almostIncludedCombos.Count);
    }

    private static string BuildInputSummary(DeckComparisonDeckSummary deckA, DeckComparisonDeckSummary deckB)
    {
        var builder = new StringBuilder();
        AppendDeckBlock(builder, "Deck A", deckA);
        builder.AppendLine();
        AppendDeckBlock(builder, "Deck B", deckB);
        return builder.ToString().TrimEnd();
    }

    private static void AppendDeckBlock(StringBuilder builder, string label, DeckComparisonDeckSummary deck)
    {
        var heading = string.IsNullOrWhiteSpace(deck.Name) ? label : $"{label} — {deck.Name}";
        builder.AppendLine(heading);
        builder.AppendLine($"Commander: {FallbackText(deck.CommanderName, "Unknown")}");
        builder.AppendLine($"Bracket: {deck.Bracket.Label}");
        builder.AppendLine($"Main deck cards: {deck.MainboardCount}");
        builder.AppendLine($"Lands: {deck.Lands}  Ramp: {deck.Ramp}  Draw: {deck.Draw}");
        builder.AppendLine($"Interaction: {deck.Interaction}  Combos: {deck.IncludedComboCount}");
    }

    private static string BuildComparisonContextText(DeckComparisonDeckSummary deckA, DeckComparisonDeckSummary deckB)
    {
        var sharedThemeNames = deckA.CategorySummaries
            .Select(item => item.Split(':', 2)[0].Trim())
            .Intersect(deckB.CategorySummaries.Select(item => item.Split(':', 2)[0].Trim()), StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("comparison_context:");
        builder.AppendLine($"generated_at_utc: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        builder.AppendLine();
        builder.AppendLine("commander_bracket_definitions:");
        foreach (var option in CommanderBracketCatalog.Options)
        {
            builder.AppendLine($"- {option.Label}: {option.Summary} {option.TurnsExpectation}");
        }
        builder.AppendLine();
        AppendDeckContext(builder, "deck_a", deckA);
        builder.AppendLine();
        AppendDeckContext(builder, "deck_b", deckB);
        builder.AppendLine();
        builder.AppendLine("comparison_signals:");
        builder.AppendLine($"shared_categories: {(sharedThemeNames.Count == 0 ? "(none)" : string.Join(", ", sharedThemeNames))}");
        builder.AppendLine($"ramp_gap: {deckA.Name} {deckA.Ramp} vs {deckB.Name} {deckB.Ramp}");
        builder.AppendLine($"draw_gap: {deckA.Name} {deckA.Draw} vs {deckB.Name} {deckB.Draw}");
        builder.AppendLine($"interaction_gap: {deckA.Name} {deckA.Interaction} vs {deckB.Name} {deckB.Interaction}");
        builder.AppendLine($"wipe_gap: {deckA.Name} {deckA.Wipes} vs {deckB.Name} {deckB.Wipes}");
        builder.AppendLine($"recursion_gap: {deckA.Name} {deckA.Recursion} vs {deckB.Name} {deckB.Recursion}");
        builder.AppendLine($"closing_power_gap: {deckA.Name} {deckA.ClosingPower} vs {deckB.Name} {deckB.ClosingPower}");
        builder.AppendLine($"combo_gap: {deckA.Name} {deckA.IncludedComboCount} complete combos vs {deckB.Name} {deckB.IncludedComboCount} complete combos");
        builder.AppendLine($"average_mana_value_gap: {deckA.Name} {deckA.AverageManaValue:0.00} vs {deckB.Name} {deckB.AverageManaValue:0.00}");
        return builder.ToString().TrimEnd();
    }

    private static void AppendDeckContext(StringBuilder builder, string label, DeckComparisonDeckSummary deck)
    {
        builder.AppendLine($"{label}:");
        builder.AppendLine($"  name: {deck.Name}");
        builder.AppendLine($"  commander: {FallbackText(deck.CommanderName, "Unknown")}");
        builder.AppendLine($"  bracket: {deck.Bracket.Label}");
        builder.AppendLine($"  bracket_summary: {deck.Bracket.Summary}");
        builder.AppendLine($"  bracket_turn_expectation: {deck.Bracket.TurnsExpectation}");
        builder.AppendLine($"  mainboard_cards: {deck.MainboardCount}");
        builder.AppendLine($"  lands: {deck.Lands}");
        builder.AppendLine($"  creatures: {deck.Creatures}");
        builder.AppendLine($"  average_mana_value: {deck.AverageManaValue:0.00}");
        builder.AppendLine($"  mana_curve: {string.Join(", ", deck.ManaCurve.Select(item => $"{item.Key}={item.Value}"))}");
        builder.AppendLine($"  color_identity: {(deck.ColorIdentity.Count == 0 ? "(unknown)" : string.Join(", ", deck.ColorIdentity))}");
        builder.AppendLine($"  categories: {(deck.CategorySummaries.Count == 0 ? "(none detected)" : string.Join(" | ", deck.CategorySummaries))}");
        builder.AppendLine($"  role_counts: ramp={deck.Ramp}, draw={deck.Draw}, interaction={deck.Interaction}, wipes={deck.Wipes}, recursion={deck.Recursion}, closing_power={deck.ClosingPower}");
        builder.AppendLine($"  combos_included: {deck.IncludedComboCount}");
        builder.AppendLine($"  combos_almost_included: {deck.AlmostIncludedComboCount}");
        builder.AppendLine($"  key_combos: {(deck.ComboSummaries.Count == 0 ? "(none found)" : string.Join(" | ", deck.ComboSummaries))}");
        builder.AppendLine($"  almost_combos: {(deck.AlmostComboSummaries.Count == 0 ? "(none found)" : string.Join(" | ", deck.AlmostComboSummaries))}");
    }

    // Internal for test access — per-AI dispatcher exercised by the AI result contract tests.
    // Phase 15-02: converted from internal static to instance method; dispatches via injected ComparisonPromptVariantRegistry.
    internal string BuildComparisonPrompt(
        DeckComparisonDeckSummary deckA,
        DeckComparisonDeckSummary deckB,
        string deckAListText,
        string deckBListText,
        string deckAComboText,
        string deckBComboText,
        string comparisonContextText,
        string comparisonSchemaJson,
        string targetAiPlatform)
    {
        return _comparisonPromptRegistry.Build(
            AiPlatform.Normalize(targetAiPlatform),
            deckA, deckB, deckAListText, deckBListText,
            deckAComboText, deckBComboText, comparisonContextText, comparisonSchemaJson);
    }


    // Phase 15-02: promoted from private static to internal static for use by Comparison/Gemini and ChatGpt variant classes.
    internal static void AppendPromptDeckSection(
        StringBuilder builder,
        DeckComparisonDeckSummary deck,
        string deckListText,
        string comboText)
    {
        builder.AppendLine($"Name: {deck.Name}");
        builder.AppendLine($"Commander: {FallbackText(deck.CommanderName, "Unknown")}");
        builder.AppendLine($"Bracket: {deck.Bracket.Label}");
        builder.AppendLine($"Bracket summary: {deck.Bracket.Summary}");
        builder.AppendLine($"Bracket turn expectation: {deck.Bracket.TurnsExpectation}");
        builder.AppendLine("Normalized decklist:");
        builder.AppendLine(deckListText);
        builder.AppendLine();
        builder.AppendLine("Combo summary:");
        builder.AppendLine(comboText);
    }

    // Phase 15-02: promoted from private static to internal static for use by Comparison/Claude variant class.
    internal static void AppendComparisonPromptDeckXml(
        StringBuilder builder,
        string tagName,
        DeckComparisonDeckSummary deck,
        string deckListText,
        string comboText)
    {
        builder.AppendLine($"<{tagName}>");
        builder.AppendLine($"  <name>{deck.Name}</name>");
        builder.AppendLine($"  <commander>{FallbackText(deck.CommanderName, "Unknown")}</commander>");
        builder.AppendLine("  <bracket>");
        builder.AppendLine($"    <label>{deck.Bracket.Label}</label>");
        builder.AppendLine($"    <summary>{deck.Bracket.Summary}</summary>");
        builder.AppendLine($"    <turn_expectation>{deck.Bracket.TurnsExpectation}</turn_expectation>");
        builder.AppendLine("  </bracket>");
        builder.AppendLine("  <list>");
        builder.AppendLine(deckListText);
        builder.AppendLine("  </list>");
        builder.AppendLine("  <combos>");
        builder.AppendLine(comboText);
        builder.AppendLine("  </combos>");
        builder.AppendLine($"</{tagName}>");
    }

    // Phase 15-02: converted from internal static to instance method; dispatches via injected FollowUpPromptVariantRegistry.
    internal string BuildFollowUpPrompt(string comparisonSchemaJson, string targetAiPlatform)
    {
        return _followUpPromptRegistry.Build(
            AiPlatform.Normalize(targetAiPlatform),
            comparisonSchemaJson);
    }


    private static string BuildComparisonSchemaJson(string deckAName, string deckBName, string deckACommander, string deckBCommander, string deckABracket, string deckBBracket)
    {
        var payload = new
        {
            deck_a_name = deckAName,
            deck_b_name = deckBName,
            deck_a_commander = deckACommander,
            deck_b_commander = deckBCommander,
            deck_a_gameplan = string.Empty,
            deck_b_gameplan = string.Empty,
            deck_a_bracket = deckABracket,
            deck_b_bracket = deckBBracket,
            shared_themes = Array.Empty<string>(),
            major_differences = Array.Empty<string>(),
            deck_a_strengths = Array.Empty<string>(),
            deck_b_strengths = Array.Empty<string>(),
            deck_a_weaknesses = Array.Empty<string>(),
            deck_b_weaknesses = Array.Empty<string>(),
            speed_comparison = string.Empty,
            resilience_comparison = string.Empty,
            interaction_comparison = string.Empty,
            mana_consistency_comparison = string.Empty,
            closing_power_comparison = string.Empty,
            combo_comparison = string.Empty,
            overall_verdict = string.Empty,
            key_gap_cards_or_packages = Array.Empty<string>(),
            deck_a_key_combos = Array.Empty<string>(),
            deck_b_key_combos = Array.Empty<string>(),
            recommended_for = new
            {
                deck_a = Array.Empty<string>(),
                deck_b = Array.Empty<string>()
            },
            confidence_notes = Array.Empty<string>()
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    internal static DeckComparisonResponse ParseComparisonResponse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Paste the deck_comparison JSON returned from ChatGPT into Step 3.");
        }

        var json = JsonTextFormatterService.ExtractJsonPayload(input);
        using var document = JsonDocument.Parse(json);

        JsonElement payload = document.RootElement;
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("deck_comparison", out var comparisonElement))
        {
            payload = comparisonElement;
        }

        var result = JsonSerializer.Deserialize<DeckComparisonResponse>(payload.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result is null)
        {
            throw new InvalidOperationException("The submitted ChatGPT response did not contain a valid deck_comparison payload.");
        }

        return result;
    }

    private static string BuildTimingSummary(IReadOnlyList<(string Label, long Ms, string? Detail)> timings, long totalMs)
    {
        var builder = new StringBuilder();
        foreach (var timing in timings)
        {
            builder.Append("- ");
            builder.Append(timing.Label);
            builder.Append(": ");
            builder.Append(timing.Ms);
            builder.Append(" ms");
            if (!string.IsNullOrWhiteSpace(timing.Detail))
            {
                builder.Append(" (");
                builder.Append(timing.Detail);
                builder.Append(')');
            }

            builder.AppendLine();
        }

        builder.Append("Total: ");
        builder.Append(totalMs);
        builder.Append(" ms");
        return builder.ToString();
    }

    private static readonly HashSet<string> SectionKeywordsThatAreNotCommanders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mainboard",
        "Sideboard",
        "Maybeboard",
        "Deck",
        "Commander",
        "Companion"
    };

    private static void ValidateSameCommander(string? deckACommander, string? deckBCommander)
    {
        ValidateCommanderIsRealCardName(deckACommander, "Deck A");
        ValidateCommanderIsRealCardName(deckBCommander, "Deck B");
        if (!string.Equals(deckACommander!.Trim(), deckBCommander!.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Both decks must share the same commander to be compared. Deck A: \"{deckACommander.Trim()}\". Deck B: \"{deckBCommander.Trim()}\". To compare different commanders, use the Deck Analysis tool on each deck individually.");
        }
    }

    private static void ValidateCommanderIsRealCardName(string? commander, string deckLabel)
    {
        if (string.IsNullOrWhiteSpace(commander))
        {
            throw new InvalidOperationException($"{deckLabel}'s commander could not be identified. Use a deck format that marks the commander (Archidekt/Moxfield URL with the commander assigned, or a decklist with a `Commander` section header above the commander card).");
        }
        if (SectionKeywordsThatAreNotCommanders.Contains(commander.Trim()))
        {
            throw new InvalidOperationException($"{deckLabel}'s commander parsed as the section keyword \"{commander.Trim()}\" — this means the deck list was pasted without a `Commander` section header. Re-paste the deck with a `Commander` line above the commander card, or use an Archidekt/Moxfield URL where the commander is explicitly assigned.");
        }
    }

    private static string ResolveDeckName(string requestedName, string commanderName, string fallback)
        => string.IsNullOrWhiteSpace(requestedName)
            ? FallbackText(commanderName, fallback)
            : requestedName.Trim();

    // Phase 15-02: promoted from private static to internal static for use by promoted helpers AppendPromptDeckSection / AppendComparisonPromptDeckXml.
    internal static string FallbackText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string BuildDecklistText(IReadOnlyList<DeckEntry> entries, IReadOnlyList<DeckEntry> optionalEntries, IReadOnlyDictionary<string, string>? oracleNameMap = null)
    {
        var builder = new StringBuilder();
        var commanderLines = entries
            .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => FormatDecklistLine(entry, oracleNameMap))
            .ToList();

        if (commanderLines.Count > 0)
        {
            builder.AppendLine("Commander");
            foreach (var line in commanderLines)
            {
                builder.AppendLine(line);
            }

            builder.AppendLine();
        }

        builder.AppendLine("Mainboard");
        foreach (var line in entries
                     .Where(entry => !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                     .Select(entry => FormatDecklistLine(entry, oracleNameMap)))
        {
            builder.AppendLine(line);
        }

        if (optionalEntries.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Possible Includes");
            foreach (var line in optionalEntries
                         .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                         .Select(entry => FormatDecklistLine(entry, oracleNameMap)))
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatDecklistLine(DeckEntry entry, IReadOnlyDictionary<string, string>? oracleNameMap)
    {
        if (oracleNameMap is not null && oracleNameMap.TryGetValue(entry.Name, out var resolvedName)
            && !string.Equals(resolvedName, entry.Name, StringComparison.OrdinalIgnoreCase))
        {
            return $"{entry.Quantity} {resolvedName} [printed as: {entry.Name}]";
        }

        return $"{entry.Quantity} {entry.Name}";
    }

    private static string NormalizeOracleText(ScryfallCard card)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(card.OracleText))
        {
            parts.Add(CollapseWhitespace(card.OracleText));
        }

        foreach (var face in card.CardFaces ?? Array.Empty<ScryfallCardFace>())
        {
            if (!string.IsNullOrWhiteSpace(face.OracleText))
            {
                parts.Add(CollapseWhitespace(face.OracleText));
            }
        }

        return string.Join(" ", parts);
    }

    private static string CollapseWhitespace(string value)
        => string.Join(" ", value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static int EstimateManaValue(string? manaCost)
    {
        if (string.IsNullOrWhiteSpace(manaCost))
        {
            return 0;
        }

        var total = 0;
        var tokenBuilder = new StringBuilder();
        var insideToken = false;

        foreach (var character in manaCost)
        {
            if (character == '{')
            {
                insideToken = true;
                tokenBuilder.Clear();
                continue;
            }

            if (character == '}')
            {
                if (insideToken)
                {
                    total += ParseManaToken(tokenBuilder.ToString());
                }

                insideToken = false;
                continue;
            }

            if (insideToken)
            {
                tokenBuilder.Append(character);
            }
        }

        return total;
    }

    private static int ParseManaToken(string token)
    {
        if (int.TryParse(token, out var numeric))
        {
            return numeric;
        }

        if (token.Contains('/', StringComparison.Ordinal))
        {
            return 1;
        }

        return token.Equals("X", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private static bool IsRampCard(string typeLine, string oracleText)
        => typeLine.Contains("Land", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("add one mana", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("add two mana", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("search your library for a basic land", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("search your library for up to", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("land", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("Treasure token", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("create a Treasure", StringComparison.OrdinalIgnoreCase);

    private static bool IsDrawCard(string oracleText)
        => oracleText.Contains("draw a card", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("draw two cards", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("draw X cards", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("investigate", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("connive", StringComparison.OrdinalIgnoreCase);

    private static bool IsInteractionCard(string typeLine, string oracleText)
        => typeLine.Contains("Instant", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("destroy target", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("exile target", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("counter target", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("return target spell", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("fight target", StringComparison.OrdinalIgnoreCase);

    private static bool IsBoardWipeCard(string oracleText)
        => oracleText.Contains("destroy all creatures", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("destroy all artifacts", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("destroy all enchantments", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("each creature", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("gets -", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("exile all", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecursionCard(string oracleText)
        => oracleText.Contains("return target card from your graveyard", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("return all land cards from your graveyard", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("return target permanent card from your graveyard", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("reanimate", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("from your graveyard to your hand", StringComparison.OrdinalIgnoreCase);

    private static bool IsClosingPowerCard(string typeLine, string oracleText)
        => oracleText.Contains("each opponent loses", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("you win the game", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("extra turn", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("double strike", StringComparison.OrdinalIgnoreCase)
            || typeLine.Contains("Craterhoof", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("combat damage to a player", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("draw", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("whenever this creature attacks", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("+X/+X", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> values, int size)
    {
        for (var index = 0; index < values.Count; index += size)
        {
            var count = Math.Min(size, values.Count - index);
            var chunk = new List<T>(count);
            for (var itemIndex = 0; itemIndex < count; itemIndex++)
            {
                chunk.Add(values[index + itemIndex]);
            }

            yield return chunk;
        }
    }

    // Phase 15-02: promoted from private static to internal static for use by Comparison and FollowUp variant classes.
    internal static string IndentJson(string json, int indentSize)
    {
        var indent = new string(' ', indentSize);
        var lines = json.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line => indent + line));
    }

    private sealed record LoadedDeck(
        IReadOnlyList<DeckEntry> AllEntries,
        IReadOnlyList<DeckEntry> PlayableEntries,
        IReadOnlyList<DeckEntry> OptionalEntries,
        string CommanderName);

    // Internal for test construction — exercised by the AI result contract tests.
    internal sealed record DeckComparisonDeckSummary(
        string Name,
        string CommanderName,
        CommanderBracketOption Bracket,
        int MainboardCount,
        int Lands,
        int Creatures,
        decimal AverageManaValue,
        IReadOnlyDictionary<string, int> ManaCurve,
        IReadOnlyList<string> ColorIdentity,
        IReadOnlyList<string> CategorySummaries,
        int Ramp,
        int Draw,
        int Interaction,
        int Wipes,
        int Recursion,
        int ClosingPower,
        IReadOnlyList<string> SharedThemes,
        IReadOnlyList<string> ComboSummaries,
        IReadOnlyList<string> AlmostComboSummaries,
        int IncludedComboCount,
        int AlmostIncludedComboCount);

    private static string BuildComboArtifactText(DeckComparisonDeckSummary deck)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{deck.Name} combos");
        builder.AppendLine($"Commander bracket: {deck.Bracket.Label}");
        builder.AppendLine($"Complete combos: {deck.IncludedComboCount}");
        builder.AppendLine($"Near-combos: {deck.AlmostIncludedComboCount}");
        builder.AppendLine();
        builder.AppendLine("Key combos:");
        if (deck.ComboSummaries.Count == 0)
        {
            builder.AppendLine("(none found)");
        }
        else
        {
            foreach (var combo in deck.ComboSummaries)
            {
                builder.AppendLine($"- {combo}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Near-combos:");
        if (deck.AlmostComboSummaries.Count == 0)
        {
            builder.AppendLine("(none found)");
        }
        else
        {
            foreach (var combo in deck.AlmostComboSummaries)
            {
                builder.AppendLine($"- {combo}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
