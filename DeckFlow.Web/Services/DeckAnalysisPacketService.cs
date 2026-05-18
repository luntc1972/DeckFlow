using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using Microsoft.Extensions.Logging.Abstractions;
using DeckFlow.Web.Services.Http;
using Polly;
using Polly.Registry;
using RestSharp;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;

namespace DeckFlow.Web.Services;

/// <summary>
/// Builds analysis and set-upgrade prompt packets for the deck-analysis page.
/// </summary>
public interface IDeckAnalysisPacketService
{
    /// <summary>
    /// Builds the next packet outputs for the supplied workflow state.
    /// </summary>
    /// <param name="request">Current workflow request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DeckAnalysisPacketResult> BuildAsync(DeckAnalysisRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Returns the results of a deck-analysis packet build.
/// </summary>
public sealed record DeckAnalysisPacketResult(
    string InputSummary,
    string SuggestedChatTitle,
    string DeckProfileSchemaJson,
    string? ReferenceText,
    string? AnalysisPromptText,
    string? SetUpgradePromptText,
    string? RequestContextText,
    string? TimingSummary,
    DeckAnalysisResponse? AnalysisResponse = null,
    SetUpgradeResponse? SetUpgradeResponse = null,
    string? ImportWarning = null,
    string? ResolvedCommanderName = null,
    string? DecklistText = null);

/// <summary>
/// Builds analysis and set-upgrade prompt packets by hydrating decks via Scryfall, banlist, and Commander Spellbook lookups, then composing the JSON-bound prompt artifacts saved to the session zip.
/// </summary>
public sealed partial class DeckAnalysisPacketService : IDeckAnalysisPacketService
{
    private const int ScryfallBatchSize = 75;
    private static readonly Regex AbilityWordRegex = AbilityWordPattern();
    private static readonly JsonSerializerOptions IndentedJsonSerializerOptions = new()
    {
        WriteIndented = true
    };
    private readonly IMoxfieldDeckImporter _moxfieldDeckImporter;
    private readonly IArchidektDeckImporter _archidektDeckImporter;
    private readonly MoxfieldParser _moxfieldParser;
    private readonly ArchidektParser _archidektParser;
    private readonly IMechanicLookupService _mechanicLookupService;
    private readonly ICommanderBanListService _commanderBanListService;
    private readonly IScryfallSetService _scryfallSetService;
    private readonly ICommanderSpellbookService _commanderSpellbookService;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> _executeCollectionAsync;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>> _executeSearchAsync;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>> _executeNamedAsync;
    private readonly ILogger<DeckAnalysisPacketService> _logger;
    private readonly AnalysisPromptVariantRegistry _analysisPromptRegistry;
    private readonly SetUpgradePromptVariantRegistry _setUpgradePromptRegistry;

    internal DeckAnalysisPacketService(
        IScryfallRestClientFactory scryfallRestClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser,
        IMechanicLookupService mechanicLookupService,
        ICommanderBanListService commanderBanListService,
        IScryfallSetService scryfallSetService,
        ICommanderSpellbookService commanderSpellbookService,
        AnalysisPromptVariantRegistry analysisPromptRegistry,
        SetUpgradePromptVariantRegistry setUpgradePromptRegistry,
        ILogger<DeckAnalysisPacketService>? logger = null,
        RestClient? restClientOverride = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsyncOverride = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsyncOverride = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsyncOverride = null)
    {
        ArgumentNullException.ThrowIfNull(scryfallRestClientFactory);
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(moxfieldDeckImporter);
        ArgumentNullException.ThrowIfNull(archidektDeckImporter);
        ArgumentNullException.ThrowIfNull(moxfieldParser);
        ArgumentNullException.ThrowIfNull(archidektParser);
        ArgumentNullException.ThrowIfNull(mechanicLookupService);
        ArgumentNullException.ThrowIfNull(commanderBanListService);
        ArgumentNullException.ThrowIfNull(scryfallSetService);
        ArgumentNullException.ThrowIfNull(commanderSpellbookService);
        ArgumentNullException.ThrowIfNull(analysisPromptRegistry);
        ArgumentNullException.ThrowIfNull(setUpgradePromptRegistry);
        var pipeline = pipelineProvider.GetPipeline<RestResponse>("scryfall") ?? ResiliencePipeline<RestResponse>.Empty;
        _moxfieldDeckImporter = moxfieldDeckImporter;
        _archidektDeckImporter = archidektDeckImporter;
        _moxfieldParser = moxfieldParser;
        _archidektParser = archidektParser;
        _mechanicLookupService = mechanicLookupService;
        _commanderBanListService = commanderBanListService;
        _scryfallSetService = scryfallSetService;
        _commanderSpellbookService = commanderSpellbookService;
        _analysisPromptRegistry = analysisPromptRegistry;
        _setUpgradePromptRegistry = setUpgradePromptRegistry;
        _logger = logger ?? NullLogger<DeckAnalysisPacketService>.Instance;
        var client = restClientOverride ?? scryfallRestClientFactory.Create();
        _executeCollectionAsync = executeCollectionAsyncOverride
            ?? ((request, cancellationToken) => ScryfallThrottle.ExecuteAsync(token => pipeline.ExecuteAsync(
                async pollyCt => await client.ExecuteAsync<ScryfallCollectionResponse>(request, pollyCt).ConfigureAwait(false),
                token).AsTask(), cancellationToken));
        _executeSearchAsync = executeSearchAsyncOverride
            ?? ((request, cancellationToken) => ScryfallThrottle.ExecuteAsync(token => pipeline.ExecuteAsync(
                async pollyCt => await client.ExecuteAsync<ScryfallSearchResponse>(request, pollyCt).ConfigureAwait(false),
                token).AsTask(), cancellationToken));
        _executeNamedAsync = executeNamedAsyncOverride
            ?? ((request, cancellationToken) => ScryfallThrottle.ExecuteAsync(token => pipeline.ExecuteAsync(
                async pollyCt => await client.ExecuteAsync<ScryfallCard>(request, pollyCt).ConfigureAwait(false),
                token).AsTask(), cancellationToken));
    }

    /// <summary>
    /// Builds the requested prompt outputs for the current workflow state.
    /// </summary>
    /// <param name="request">Current workflow request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DeckAnalysisPacketResult> BuildAsync(DeckAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var overallStopwatch = Stopwatch.StartNew();
        var timings = new List<(string Label, long Ms, string? Detail)>();

        if (request.WorkflowStep == 3
            && string.IsNullOrWhiteSpace(request.DeckSource)
            && !string.IsNullOrWhiteSpace(request.DeckProfileJson))
        {
            var savedAnalysisResponse = ResponseParsers.ParseAnalysisResponse(request.DeckProfileJson);
            var savedDeckProfileSchemaJson = BuildDeckProfileSchemaJson(
                string.IsNullOrWhiteSpace(savedAnalysisResponse.Commander) ? null : savedAnalysisResponse.Commander,
                string.IsNullOrWhiteSpace(savedAnalysisResponse.Format) ? request.Format : savedAnalysisResponse.Format,
                savedAnalysisResponse.DeckVersions.Count > 0);
            var savedTimingSummary = BuildTimingSummary(timings, overallStopwatch.ElapsedMilliseconds);
            return new DeckAnalysisPacketResult(
                InputSummary: BuildAnalysisSummaryFromSavedJson(savedAnalysisResponse),
                SuggestedChatTitle: BuildSuggestedChatTitle(request, savedAnalysisResponse.Commander),
                DeckProfileSchemaJson: savedDeckProfileSchemaJson,
                ReferenceText: null,
                AnalysisPromptText: null,
                SetUpgradePromptText: null,
                RequestContextText: null,
                TimingSummary: savedTimingSummary,
                AnalysisResponse: savedAnalysisResponse,
                ResolvedCommanderName: savedAnalysisResponse.Commander);
        }

        if (request.WorkflowStep == 5
            && string.IsNullOrWhiteSpace(request.DeckSource)
            && !string.IsNullOrWhiteSpace(request.SetUpgradeResponseJson))
        {
            var savedSetUpgradeResponse = ResponseParsers.ParseSetUpgradeResponse(request.SetUpgradeResponseJson);
            var savedAnalysisResponse = string.IsNullOrWhiteSpace(request.DeckProfileJson)
                ? null
                : ResponseParsers.ParseAnalysisResponse(request.DeckProfileJson);
            var step5Commander = savedAnalysisResponse is null || string.IsNullOrWhiteSpace(savedAnalysisResponse.Commander)
                ? null
                : savedAnalysisResponse.Commander;
            var step5DeckProfileSchemaJson = BuildDeckProfileSchemaJson(
                step5Commander,
                savedAnalysisResponse is null || string.IsNullOrWhiteSpace(savedAnalysisResponse.Format) ? request.Format : savedAnalysisResponse.Format,
                (savedAnalysisResponse?.DeckVersions.Count ?? 0) > 0);
            var step5InputSummary = savedAnalysisResponse is null
                ? string.Empty
                : BuildAnalysisSummaryFromSavedJson(savedAnalysisResponse);
            var savedTimingSummary = BuildTimingSummary(timings, overallStopwatch.ElapsedMilliseconds);
            return new DeckAnalysisPacketResult(
                InputSummary: step5InputSummary,
                SuggestedChatTitle: BuildSuggestedChatTitle(request, savedAnalysisResponse?.Commander),
                DeckProfileSchemaJson: step5DeckProfileSchemaJson,
                ReferenceText: null,
                AnalysisPromptText: null,
                SetUpgradePromptText: null,
                RequestContextText: null,
                TimingSummary: savedTimingSummary,
                AnalysisResponse: savedAnalysisResponse,
                SetUpgradeResponse: savedSetUpgradeResponse,
                ResolvedCommanderName: savedAnalysisResponse?.Commander);
        }

        if (string.IsNullOrWhiteSpace(request.DeckSource))
        {
            throw new InvalidOperationException("A deck URL or pasted deck export is required.");
        }

        var loadDeckStopwatch = Stopwatch.StartNew();
        var entries = await LoadDeckEntriesAsync(request.DeckSource, cancellationToken).ConfigureAwait(false);
        timings.Add(("Deck load", loadDeckStopwatch.ElapsedMilliseconds, null));
        _logger.LogInformation("Deck Analysis packet deck load completed in {ElapsedMs}ms.", loadDeckStopwatch.ElapsedMilliseconds);
        var deckEntries = entries
            .Where(entry =>
                !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var possibleIncludeEntries = entries
            .Where(entry =>
                string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (deckEntries.Count == 0)
        {
            throw new InvalidOperationException("The submitted deck did not contain any commander or mainboard cards.");
        }

        var commanderName = deckEntries
            .FirstOrDefault(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            ?.Name;
        var inferredCommanderFromMoxfieldOrdering = false;

        // Fallback for Moxfield exports without a Commander section header.
        // By convention the commander (or partner pair) appears first in the list.
        if (commanderName is null && entries.Count > 0)
        {
            var leadingOneOfs = entries
                .TakeWhile(entry => entry.Quantity == 1
                    && !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();

            // If two candidates were found, confirm the second is a partner commander and not the
            // first card of an A-Z-sorted mainboard. When the second entry sorts alphabetically
            // before the third entry it fits naturally in a sorted mainboard sequence; in that case
            // only the first entry is the commander.
            if (leadingOneOfs.Count == 2 && entries.Count > 2)
            {
                var thirdEntry = entries[2];
                if (string.Compare(leadingOneOfs[1].Name, thirdEntry.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    leadingOneOfs = leadingOneOfs.Take(1).ToList();
                }
            }

            if (leadingOneOfs.Count > 0)
            {
                var commanderNames = leadingOneOfs.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                entries = entries
                    .Select(entry => commanderNames.Contains(entry.Name)
                        ? entry with { Board = "commander" }
                        : entry)
                    .ToList();
                deckEntries = deckEntries
                    .Select(entry => commanderNames.Contains(entry.Name)
                        ? entry with { Board = "commander" }
                        : entry)
                    .ToList();
                commanderName = leadingOneOfs[0].Name;
                inferredCommanderFromMoxfieldOrdering = true;
            }
        }

        if (string.Equals(request.Format, "Commander", StringComparison.OrdinalIgnoreCase) && inferredCommanderFromMoxfieldOrdering)
        {
            var inferredCommanderNames = entries
                .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (inferredCommanderNames.Count <= 1)
            {
                var validatedCommanderName = await ValidateCommanderAsync(entries, commanderName, cancellationToken).ConfigureAwait(false);
                commanderName = validatedCommanderName;
                entries = entries
                    .Select(entry => string.Equals(entry.Name, validatedCommanderName, StringComparison.OrdinalIgnoreCase)
                        ? entry with { Board = "commander" }
                        : string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)
                            ? entry with { Board = "main" }
                            : entry)
                    .ToList();
            }
            else
            {
                foreach (var inferredCommander in inferredCommanderNames)
                {
                    await ValidateCommanderAsync(entries, inferredCommander, cancellationToken).ConfigureAwait(false);
                }

                commanderName = inferredCommanderNames[0];
            }

            deckEntries = entries
                .Where(entry =>
                    !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
                .ToList();
            possibleIncludeEntries = entries
                .Where(entry =>
                    string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var inputSummary = BuildInputSummary(request, deckEntries, possibleIncludeEntries, commanderName);
        var decklistText = BuildDecklistText(deckEntries, possibleIncludeEntries);
        var requiresFullDecklists = AnalysisQuestionCatalog.RequiresFullDecklistOutput(request.SelectedAnalysisQuestions);
        var deckProfileSchemaJson = BuildDeckProfileSchemaJson(commanderName, request.Format, requiresFullDecklists);
        var requestContextText = BuildRequestContextText(request, commanderName);

        string? referenceText = null;
        string? analysisPromptText = null;
        string? setUpgradePromptText = null;
        DeckAnalysisResponse? analysisResponse = null;
        SetUpgradeResponse? setUpgradeResponse = null;

        if (request.WorkflowStep >= 3 && !string.IsNullOrWhiteSpace(request.DeckProfileJson))
        {
            analysisResponse = ResponseParsers.ParseAnalysisResponse(request.DeckProfileJson);
        }

        if (request.WorkflowStep >= 5 && !string.IsNullOrWhiteSpace(request.SetUpgradeResponseJson))
        {
            setUpgradeResponse = ResponseParsers.ParseSetUpgradeResponse(request.SetUpgradeResponseJson);
        }

        var deckProfileText = string.IsNullOrWhiteSpace(request.DeckProfileJson)
            ? deckProfileSchemaJson
            : ExtractJsonObject(request.DeckProfileJson);
        var selectedQuestions = AnalysisQuestionCatalog.NormalizeSelections(request.SelectedAnalysisQuestions);
        var wantsAnalysisPacket = request.WorkflowStep == 2;
        var wantsSetUpgradeOnly = request.WorkflowStep < 2
            && (!string.IsNullOrWhiteSpace(request.DeckProfileJson) || !string.IsNullOrWhiteSpace(request.SetPacketText));
        var wantsSetUpgradePacket = request.WorkflowStep == 4 || wantsSetUpgradeOnly;

        if (wantsAnalysisPacket && CommanderBracketCatalog.Find(request.TargetCommanderBracket) is null)
        {
            throw new InvalidOperationException("Choose a target Commander bracket before generating the analysis packet.");
        }

        if (wantsAnalysisPacket && selectedQuestions.Count == 0 && string.IsNullOrWhiteSpace(request.FreeformQuestion))
        {
            throw new InvalidOperationException("Select at least one analysis question before generating the analysis packet.");
        }

        if (wantsAnalysisPacket
            && selectedQuestions.Any(questionId => questionId is "card-worth-it" or "better-alternatives")
            && request.CardSpecificQuestionCardNames.Count == 0)
        {
            throw new InvalidOperationException("Enter at least one card name for the selected card-specific analysis questions.");
        }

        if (wantsAnalysisPacket
            && AnalysisQuestionCatalog.RequiresCategoryOutput(selectedQuestions)
            && string.IsNullOrWhiteSpace(request.DecklistExportFormat))
        {
            throw new InvalidOperationException("Choose Moxfield or Archidekt as the export format when assigning or updating categories — plain text does not support inline category formatting.");
        }

        if (wantsAnalysisPacket
            && selectedQuestions.Contains("budget-upgrades", StringComparer.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.BudgetUpgradeAmount))
        {
            throw new InvalidOperationException("Enter a budget amount for the selected budget upgrade question.");
        }

        // Only fetch banned list and set packet when the analysis or set-upgrade step actually needs them.
        if (wantsAnalysisPacket || wantsSetUpgradePacket)
        {
            // Fire banned-list and set-packet fetches in parallel — neither depends on the other.
            var parallelStopwatch = Stopwatch.StartNew();
            var bannedCardsTask = _commanderBanListService.GetBannedCardsAsync(cancellationToken);
            var setPacketTask = BuildGeneratedSetPacketAsync(request, cancellationToken);
            await Task.WhenAll(bannedCardsTask, setPacketTask).ConfigureAwait(false);
            timings.Add(("Ban list + set packet", parallelStopwatch.ElapsedMilliseconds, null));
            _logger.LogInformation("Deck Analysis packet banned-list + set-packet fetch completed in {ElapsedMs}ms.", parallelStopwatch.ElapsedMilliseconds);
            var bannedCards = bannedCardsTask.Result;
            var generatedSetPacket = setPacketTask.Result;

            if (wantsAnalysisPacket)
            {
                var analysisPossibleIncludeEntries = possibleIncludeEntries
                    .Where(entry =>
                        (request.IncludeSideboardInAnalysis && string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
                        || (request.IncludeMaybeboardInAnalysis && string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                var cardReferenceRequests = BuildAnalysisCardReferenceRequests(deckEntries, analysisPossibleIncludeEntries);

                // Start combo lookup immediately — only needs deckEntries, independent of Scryfall lookups.
                var comboStopwatch = Stopwatch.StartNew();
                var comboTask = AnalysisQuestionCatalog.RequiresComboLookup(selectedQuestions)
                    ? _commanderSpellbookService.FindCombosAsync(deckEntries, cancellationToken)
                    : Task.FromResult<CommanderSpellbookResult?>(null);

                var cardReferenceStopwatch = Stopwatch.StartNew();
                var cardReferenceBundle = await LookupCardReferencesAsync(cardReferenceRequests, cancellationToken).ConfigureAwait(false);
                timings.Add(("Scryfall card lookup", cardReferenceStopwatch.ElapsedMilliseconds, $"{cardReferenceBundle.CardReferences.Count} cards, {cardReferenceBundle.MechanicNames.Count} mechanics found"));
                _logger.LogInformation(
                    "Deck Analysis packet card reference lookup completed in {ElapsedMs}ms for {CardCount} cards and {MechanicCount} mechanics.",
                    cardReferenceStopwatch.ElapsedMilliseconds,
                    cardReferenceBundle.CardReferences.Count,
                    cardReferenceBundle.MechanicNames.Count);
                var mechanicReferenceStopwatch = Stopwatch.StartNew();
                var mechanicReferences = await LookupMechanicReferencesAsync(cardReferenceBundle.MechanicNames, cancellationToken).ConfigureAwait(false);
                timings.Add(("Mechanic rules lookup", mechanicReferenceStopwatch.ElapsedMilliseconds, $"{mechanicReferences.Count} mechanics resolved"));
                _logger.LogInformation(
                    "Deck Analysis packet mechanic lookup completed in {ElapsedMs}ms for {MechanicCount} mechanics.",
                    mechanicReferenceStopwatch.ElapsedMilliseconds,
                    mechanicReferences.Count);

                referenceText = BuildReferenceText(request, mechanicReferences, cardReferenceBundle.CardReferences, bannedCards);

                var comboResult = await comboTask.ConfigureAwait(false);
                if (AnalysisQuestionCatalog.RequiresComboLookup(selectedQuestions))
                {
                    timings.Add(("Commander Spellbook", comboStopwatch.ElapsedMilliseconds, $"{comboResult?.IncludedCombos.Count ?? 0} combos, {comboResult?.AlmostIncludedCombos.Count ?? 0} near-combos"));
                }
                _logger.LogInformation(
                    "Commander Spellbook lookup completed in {ElapsedMs}ms. Included={Included} AlmostIncluded={AlmostIncluded}.",
                    comboStopwatch.ElapsedMilliseconds,
                    comboResult?.IncludedCombos.Count ?? 0,
                    comboResult?.AlmostIncludedCombos.Count ?? 0);

                // Resolve commander name to oracle name if the deck used a renamed printing.
                if (commanderName is not null && cardReferenceBundle.OracleNameMap.TryGetValue(commanderName, out var oracleCommanderName))
                {
                    commanderName = oracleCommanderName;
                }

                var includeCardVersions = AnalysisQuestionCatalog.RequiresFullDecklistOutput(selectedQuestions) && request.IncludeCardVersions;
                var analysisDecklistText = includeCardVersions
                    ? BuildDecklistText(deckEntries, analysisPossibleIncludeEntries, includeVersions: true, oracleNameMap: cardReferenceBundle.OracleNameMap)
                    : BuildDecklistText(deckEntries, analysisPossibleIncludeEntries, oracleNameMap: cardReferenceBundle.OracleNameMap);
                analysisPromptText = BuildAnalysisPrompt(request, analysisDecklistText, referenceText, deckProfileSchemaJson, commanderName, selectedQuestions, bannedCards, comboResult, includeCardVersions);
                if (wantsSetUpgradePacket)
                {
                    var oracleResolvedDecklistText = BuildDecklistText(deckEntries, possibleIncludeEntries, oracleNameMap: cardReferenceBundle.OracleNameMap);
                    setUpgradePromptText = BuildSetUpgradePrompt(request, oracleResolvedDecklistText, deckProfileText, commanderName, generatedSetPacket, bannedCards);
                }
            }
            else if (wantsSetUpgradePacket)
            {
                setUpgradePromptText = BuildSetUpgradePrompt(request, decklistText, deckProfileText, commanderName, generatedSetPacket, bannedCards);
            }
        }

        _logger.LogInformation(
            "Deck Analysis packet build completed in {ElapsedMs}ms. AnalysisGenerated={AnalysisGenerated} SetPacketGenerated={SetPacketGenerated}.",
            overallStopwatch.ElapsedMilliseconds,
            !string.IsNullOrWhiteSpace(analysisPromptText),
            !string.IsNullOrWhiteSpace(setUpgradePromptText));

        var timingSummary = BuildTimingSummary(timings, overallStopwatch.ElapsedMilliseconds);

        var suggestedChatTitle = BuildSuggestedChatTitle(request, commanderName);

        return new DeckAnalysisPacketResult(
            inputSummary,
            suggestedChatTitle,
            deckProfileSchemaJson,
            referenceText,
            analysisPromptText,
            setUpgradePromptText,
            requestContextText,
            timingSummary,
            analysisResponse,
            setUpgradeResponse,
            ImportWarning: _lastImportNotice,
            ResolvedCommanderName: commanderName,
            DecklistText: decklistText);
    }


    /// <summary>
    /// Warning surfaced to the UI when the Moxfield fallback (Commander Spellbook) was used.
    /// Set during LoadDeckEntriesAsync, read during BuildAsync, cleared per call.
    /// </summary>
    private string? _lastImportNotice;

    /// <summary>
    /// Loads deck entries from a public URL or pasted export text.
    /// </summary>
    /// <param name="deckSource">Deck URL or pasted export text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed deck entries.</returns>
    private async Task<List<DeckEntry>> LoadDeckEntriesAsync(string deckSource, CancellationToken cancellationToken)
    {
        _lastImportNotice = null;
        if (Uri.TryCreate(deckSource.Trim(), UriKind.Absolute, out var uri))
        {
            if (uri.Host.Contains("moxfield.com", StringComparison.OrdinalIgnoreCase))
            {
                var result = await _moxfieldDeckImporter.ImportWithSourceAsync(deckSource, cancellationToken).ConfigureAwait(false);
                _lastImportNotice = result.FallbackNotice;
                return result.Entries;
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

    /// <summary>
    /// Builds the short deck summary shown above the generated ChatGPT packets.
    /// </summary>
    private static string BuildInputSummary(DeckAnalysisRequest request, IReadOnlyList<DeckEntry> entries, IReadOnlyList<DeckEntry> possibleIncludeEntries, string? commanderName)
    {
        var mainDeckCards = entries
            .Where(entry => string.Equals(entry.Board, "mainboard", StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Quantity);
        var commanderCards = entries
            .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Quantity);
        var sideboardCards = possibleIncludeEntries
            .Where(entry => string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Quantity);
        var maybeboardCards = possibleIncludeEntries
            .Where(entry => string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Quantity);
        var builder = new StringBuilder();
        builder.AppendLine($"Format: {NormalizeSingleLine(request.Format, "Commander")}");
        if (!string.IsNullOrWhiteSpace(request.DeckName))
        {
            builder.AppendLine($"Deck name: {request.DeckName.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(commanderName))
        {
            builder.AppendLine($"Commander: {commanderName}");
        }

        builder.AppendLine($"Main deck cards: {mainDeckCards}");
        if (!string.IsNullOrWhiteSpace(commanderName) || commanderCards > 0)
        {
            builder.AppendLine($"Commander cards: {commanderCards}");
        }

        if (possibleIncludeEntries.Count > 0)
        {
            builder.AppendLine($"Possible includes: {possibleIncludeEntries.Sum(entry => entry.Quantity)}");
            if (sideboardCards > 0)
            {
                builder.AppendLine($"Sideboard cards: {sideboardCards}");
            }

            if (maybeboardCards > 0)
            {
                builder.AppendLine($"Maybeboard cards: {maybeboardCards}");
            }
        }

        var bracket = CommanderBracketCatalog.Find(request.TargetCommanderBracket);
        if (bracket is not null)
        {
            builder.AppendLine($"Target commander bracket: {bracket.Label}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatDecklistLine(DeckEntry entry, bool includeVersions, IReadOnlyDictionary<string, string>? oracleNameMap = null)
    {
        var name = entry.Name;
        string? printedAs = null;
        if (oracleNameMap is not null && oracleNameMap.TryGetValue(entry.Name, out var oracleName)
            && !string.Equals(oracleName, entry.Name, StringComparison.OrdinalIgnoreCase))
        {
            printedAs = entry.Name;
            name = oracleName;
        }
        if (includeVersions)
        {
            var slash = name.IndexOf(" // ", StringComparison.Ordinal);
            if (slash >= 0) name = name[..slash].TrimEnd();
        }
        var line = $"{entry.Quantity} {name}";
        if (includeVersions && !string.IsNullOrWhiteSpace(entry.SetCode))
        {
            line += $" ({entry.SetCode.ToUpperInvariant()})";
            if (!string.IsNullOrWhiteSpace(entry.CollectorNumber))
                line += $" {entry.CollectorNumber}";
        }
        if (printedAs is not null)
        {
            line += $" [printed as: {printedAs}]";
        }
        return line;
    }

    /// <summary>
    /// Builds the analysis deck text, keeping possible includes separate from the playable list.
    /// When <paramref name="includeVersions"/> is <see langword="true"/>, each commander and mainboard
    /// line includes the set code and collector number when available.
    /// </summary>
    private static string BuildDecklistText(IReadOnlyList<DeckEntry> entries, IReadOnlyList<DeckEntry> possibleIncludeEntries, bool includeVersions = false, IReadOnlyDictionary<string, string>? oracleNameMap = null)
    {
        var commanderLines = entries
            .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => FormatDecklistLine(entry, includeVersions, oracleNameMap))
            .ToList();
        var mainboardLines = entries
            .Where(entry => !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => FormatDecklistLine(entry, includeVersions, oracleNameMap))
            .ToList();

        var builder = new StringBuilder();
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
        foreach (var line in mainboardLines)
        {
            builder.AppendLine(line);
        }

        if (possibleIncludeEntries.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Possible Includes");
            foreach (var line in possibleIncludeEntries
                         .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                         .Select(entry =>
                         {
                             if (oracleNameMap is not null && oracleNameMap.TryGetValue(entry.Name, out var oracleName)
                                 && !string.Equals(oracleName, entry.Name, StringComparison.OrdinalIgnoreCase))
                             {
                                 return $"{entry.Quantity} {oracleName} [printed as: {entry.Name}]";
                             }
                             return $"{entry.Quantity} {entry.Name}";
                         }))
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats the Commander Spellbook combo lookup result as a reference block for injection into the analysis prompt.
    /// Returns an empty string when no combo data is available.
    /// </summary>
    internal static string BuildComboReferenceText(CommanderSpellbookResult? result)
    {
        if (result is null
            || (result.IncludedCombos.Count == 0 && result.AlmostIncludedCombos.Count == 0))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Commander Spellbook combo reference (verified data — use this when answering combo questions):");

        if (result.IncludedCombos.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"COMPLETE COMBOS IN THIS DECK ({result.IncludedCombos.Count}):");
            for (var i = 0; i < result.IncludedCombos.Count; i++)
            {
                var combo = result.IncludedCombos[i];
                builder.AppendLine($"{i + 1}. Cards: {string.Join(" + ", combo.CardNames)}");
                builder.AppendLine($"   Result: {string.Join(", ", combo.Results)}");
                if (!string.IsNullOrWhiteSpace(combo.Instructions))
                {
                    builder.AppendLine($"   How: {combo.Instructions}");
                }
            }
        }

        if (result.AlmostIncludedCombos.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"COMBOS ONE CARD AWAY (within color identity) ({result.AlmostIncludedCombos.Count}):");
            for (var i = 0; i < result.AlmostIncludedCombos.Count; i++)
            {
                var combo = result.AlmostIncludedCombos[i];
                builder.AppendLine($"{i + 1}. Missing: {combo.MissingCard} | Have: {string.Join(" + ", combo.CardsInDeck)}");
                builder.AppendLine($"   Result: {string.Join(", ", combo.Results)}");
                if (!string.IsNullOrWhiteSpace(combo.Instructions))
                {
                    builder.AppendLine($"   How: {combo.Instructions}");
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Suggests a conversation title derived from the commander or deck name.
    /// </summary>
    private static string BuildSuggestedChatTitle(DeckAnalysisRequest request, string? commanderName)
    {
        var primaryName = !string.IsNullOrWhiteSpace(commanderName)
            ? commanderName.Trim()
            : !string.IsNullOrWhiteSpace(request.DeckName)
                ? request.DeckName.Trim()
                : "Commander Deck";

        return $"{primaryName} | AI Deck Analysis";
    }

    private static string BuildAnalysisSummaryFromSavedJson(DeckAnalysisResponse analysisResponse)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Format: {NormalizeSingleLine(analysisResponse.Format, "Commander")}");

        if (!string.IsNullOrWhiteSpace(analysisResponse.Commander))
        {
            builder.AppendLine($"Commander: {analysisResponse.Commander.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(analysisResponse.GamePlan))
        {
            builder.AppendLine($"Game plan: {analysisResponse.GamePlan.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(analysisResponse.Speed))
        {
            builder.AppendLine($"Speed: {analysisResponse.Speed.Trim()}");
        }

        if (analysisResponse.PrimaryAxes.Count > 0)
        {
            builder.AppendLine($"Primary axes: {string.Join(", ", analysisResponse.PrimaryAxes)}");
        }

        if (analysisResponse.SynergyTags.Count > 0)
        {
            builder.AppendLine($"Synergy tags: {string.Join(", ", analysisResponse.SynergyTags)}");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds the authoritative card, mechanic, and banned-list reference bundle used during analysis.
    /// </summary>
    private static string BuildReferenceText(
        DeckAnalysisRequest request,
        IReadOnlyList<MechanicReference> mechanicReferences,
        IReadOnlyList<CardReference> cardReferences,
        IReadOnlyList<string> bannedCards)
    {
        var builder = new StringBuilder();
        builder.AppendLine("reference_context:");
        builder.AppendLine("source: Scryfall Oracle and official Wizards Comprehensive Rules");
        builder.AppendLine($"generated_at_utc: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        builder.AppendLine($"format: {NormalizeSingleLine(request.Format, "Commander")}");
        builder.AppendLine();
        builder.AppendLine($"official_commander_banned_cards: {FormatBannedCardsLine(bannedCards)}");
        builder.AppendLine();
        builder.AppendLine("mechanics:");
        if (mechanicReferences.Count == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var mechanicReference in mechanicReferences)
            {
                builder.AppendLine($"{mechanicReference.Name}: {mechanicReference.Description}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("cards:");
        builder.AppendLine("[current_deck] = active deck. [candidate_include:sideboard] and [candidate_include:maybeboard] = optional candidates only.");
        if (cardReferences.Count == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var cardReference in cardReferences)
            {
                builder.AppendLine($"[{cardReference.Scope}] {cardReference.Name} | {cardReference.ManaCost} | {cardReference.TypeLine} | {cardReference.OracleText}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<CardReferenceRequest> BuildAnalysisCardReferenceRequests(
        IReadOnlyList<DeckEntry> deckEntries,
        IReadOnlyList<DeckEntry> analysisPossibleIncludeEntries)
    {
        var requests = new List<CardReferenceRequest>();

        requests.AddRange(deckEntries
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new CardReferenceRequest(entry.Name, "current_deck")));

        requests.AddRange(analysisPossibleIncludeEntries
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new CardReferenceRequest(
                entry.Name,
                string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase)
                    ? "candidate_include:sideboard"
                    : "candidate_include:maybeboard")));

        return requests
            .GroupBy(request => request.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    /// <summary>
    /// Builds the main analysis prompt from the deck text, references, bracket guidance, and selected questions.
    /// Internal for test access — per-AI dispatcher exercised by the AI result contract tests.
    /// </summary>
    // Phase 15-02: converted from internal static to instance method; dispatches via injected AnalysisPromptVariantRegistry.
    internal string BuildAnalysisPrompt(DeckAnalysisRequest request, string decklistText, string referenceText, string deckProfileSchemaJson, string? commanderName, IReadOnlyList<string> selectedQuestionIds, IReadOnlyList<string> bannedCards, CommanderSpellbookResult? comboResult = null, bool includeCardVersions = false)
    {
        return _analysisPromptRegistry.Build(
            AiPlatform.Normalize(request.TargetAiPlatform),
            request, decklistText, referenceText, deckProfileSchemaJson,
            commanderName, selectedQuestionIds, bannedCards,
            comboResult, includeCardVersions);
    }


    /// <summary>
    /// Builds the optional set-upgrade prompt used after the deck profile has been generated.
    /// Internal for test access — per-AI dispatcher exercised by the AI result contract tests.
    /// </summary>
    // Phase 15-02: converted from internal static to instance method; dispatches via injected SetUpgradePromptVariantRegistry.
    internal string BuildSetUpgradePrompt(DeckAnalysisRequest request, string decklistText, string deckProfileJson, string? commanderName, string? generatedSetPacket, IReadOnlyList<string> bannedCards)
    {
        return _setUpgradePromptRegistry.Build(
            AiPlatform.Normalize(request.TargetAiPlatform),
            request, decklistText, deckProfileJson, commanderName,
            generatedSetPacket, bannedCards);
    }


    /// <summary>
    /// Builds a condensed set packet from Scryfall for the selected set codes.
    /// </summary>
    private async Task<string?> BuildGeneratedSetPacketAsync(DeckAnalysisRequest request, CancellationToken cancellationToken)
    {
        if (request.SelectedSetCodes.Count == 0)
        {
            return string.IsNullOrWhiteSpace(request.SetPacketText) ? null : request.SetPacketText.Trim();
        }

        if (request.SelectedSetCodes.Count > 1 && string.IsNullOrWhiteSpace(request.SetPacketText))
        {
            throw new InvalidOperationException("Choose only one set or paste a condensed set packet override before generating the set-upgrade packet.");
        }

        var commanderColorIdentity = await LookupCommanderColorIdentityAsync(request.DeckSource, cancellationToken).ConfigureAwait(false);
        var generatedPacket = await _scryfallSetService
            .BuildSetPacketAsync([request.SelectedSetCodes[0]], commanderColorIdentity, cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(generatedPacket) ? null : generatedPacket;
    }

    /// <summary>
    /// Looks up the commander's color identity so generated set packets can filter to legal cards.
    /// </summary>
    private async Task<IReadOnlyList<string>> LookupCommanderColorIdentityAsync(string deckSource, CancellationToken cancellationToken)
    {
        var entries = await LoadDeckEntriesAsync(deckSource, cancellationToken).ConfigureAwait(false);
        var commanderName = entries
            .FirstOrDefault(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            ?.Name;
        if (string.IsNullOrWhiteSpace(commanderName))
        {
            return Array.Empty<string>();
        }

        var request = new RestRequest("cards/collection", Method.Post)
            .AddJsonBody(new
            {
                identifiers = new[]
                {
                    new { name = commanderName.Trim() }
                }
            });
        var response = await _executeCollectionAsync(request, cancellationToken).ConfigureAwait(false);
        var card = response.Data?.Data?.FirstOrDefault();
        if (card?.ColorIdentity is null)
        {
            return Array.Empty<string>();
        }

        return card.ColorIdentity
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Select(color => color.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns the deck-profile schema that ChatGPT should follow during analysis.
    /// </summary>
    private static string BuildDeckProfileSchemaJson(string? commanderName, string format, bool includeFullDecklists = false)
    {
        var payload = new Dictionary<string, object>
        {
            ["format"] = NormalizeSingleLine(format, "Commander"),
            ["commander"] = commanderName ?? string.Empty,
            ["game_plan"] = string.Empty,
            ["primary_axes"] = Array.Empty<string>(),
            ["speed"] = string.Empty,
            ["strengths"] = Array.Empty<string>(),
            ["weaknesses"] = Array.Empty<string>(),
            ["deck_needs"] = Array.Empty<string>(),
            ["weak_slots"] = new[]
            {
                new
                {
                    card = string.Empty,
                    reason = string.Empty
                }
            },
            ["synergy_tags"] = Array.Empty<string>(),
            ["question_answers"] = new[]
            {
                new
                {
                    question_number = 1,
                    question = string.Empty,
                    answer = string.Empty,
                    basis = "authoritative|inference|mixed"
                }
            }
        };

        if (includeFullDecklists)
        {
            payload["deck_versions"] = new[]
            {
                new
                {
                    version_name = string.Empty,
                    decklist = "complete 100-card decklist, one card per line, same format as the text code blocks",
                    cards_added = Array.Empty<string>(),
                    cards_cut = Array.Empty<string>()
                }
            };
        }

        return JsonSerializer.Serialize(payload, IndentedJsonSerializerOptions);
    }

    private async Task<CardReferenceBundle> LookupCardReferencesAsync(IReadOnlyList<CardReferenceRequest> cardRequests, CancellationToken cancellationToken)
    {
        if (cardRequests.Count == 0)
        {
            return new CardReferenceBundle(Array.Empty<CardReference>(), Array.Empty<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var resolvedCards = new Dictionary<string, CardReference>(StringComparer.OrdinalIgnoreCase);
        var oracleNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mechanicNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in Chunk(cardRequests, ScryfallBatchSize))
        {
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new
            {
                identifiers = chunk.Select(card => new { name = NormalizeForScryfall(card.Name) }).ToArray()
            });

            var response = await _executeCollectionAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall card reference lookup (cards/collection) returned HTTP {(int)response.StatusCode} while building the analysis packet.",
                    null,
                    response.StatusCode);
            }

            foreach (var card in response.Data.Data)
            {
                var matchingRequest = chunk.FirstOrDefault(entry => string.Equals(entry.Name, card.Name, StringComparison.OrdinalIgnoreCase));
                if (matchingRequest is null)
                {
                    continue;
                }

                oracleNameMap[matchingRequest.Name] = card.Name;
                resolvedCards[matchingRequest.Name] = new CardReference(
                    matchingRequest.Scope,
                    card.Name,
                    card.ManaCost ?? string.Empty,
                    card.TypeLine,
                    NormalizeOracleText(card));

                foreach (var mechanicName in ExtractMechanicNames(card))
                {
                    mechanicNames.Add(mechanicName);
                }
            }

            foreach (var unresolvedRequest in chunk.Where(card => !resolvedCards.ContainsKey(card.Name)))
            {
                var fallbackCard = await SearchFallbackCardAsync(unresolvedRequest.Name, cancellationToken).ConfigureAwait(false);
                if (fallbackCard is null)
                {
                    continue;
                }

                oracleNameMap[unresolvedRequest.Name] = fallbackCard.Name;
                var displayName = NormalizeLookupName(unresolvedRequest.Name) == NormalizeLookupName(fallbackCard.Name)
                    ? fallbackCard.Name
                    : $"submitted_name: {unresolvedRequest.Name} | resolved_card: {fallbackCard.Name}";

                resolvedCards[unresolvedRequest.Name] = new CardReference(
                    unresolvedRequest.Scope,
                    displayName,
                    fallbackCard.ManaCost ?? string.Empty,
                    fallbackCard.TypeLine,
                    NormalizeOracleText(fallbackCard));

                foreach (var mechanicName in ExtractMechanicNames(fallbackCard))
                {
                    mechanicNames.Add(mechanicName);
                }
            }
        }

        var cardReferences = cardRequests
            .Where(card => resolvedCards.ContainsKey(card.Name))
            .Select(card => resolvedCards[card.Name])
            .ToList();

        return new CardReferenceBundle(
            cardReferences,
            mechanicNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList(),
            oracleNameMap);
    }

    private async Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            return null;
        }

        var normalizedCardName = NormalizeLookupName(cardName);
        foreach (var query in new[]
        {
            $"(printed:\"{NormalizeForScryfall(cardName)}\" OR name:\"{NormalizeForScryfall(cardName)}\")",
            NormalizeForScryfall(cardName)
        })
        {
            var request = new RestRequest("cards/search", Method.Get);
            request.AddQueryParameter("q", query);
            request.AddQueryParameter("unique", "prints");
            request.AddQueryParameter("include_multilingual", "true");

            var response = await _executeSearchAsync(request, cancellationToken).ConfigureAwait(false);
            ScryfallThrottle.ThrowIfUpstreamUnavailable(response.StatusCode);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                continue;
            }

            var match = response.Data.Data
                .FirstOrDefault(card => NormalizeLookupName(card.Name) == normalizedCardName)
                ?? response.Data.Data.FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        var namedRequest = new RestRequest("cards/named", Method.Get);
        namedRequest.AddQueryParameter("fuzzy", NormalizeForScryfall(cardName));
        var namedResponse = await _executeNamedAsync(namedRequest, cancellationToken).ConfigureAwait(false);
        ScryfallThrottle.ThrowIfUpstreamUnavailable(namedResponse.StatusCode);
        if ((int)namedResponse.StatusCode >= 200 && (int)namedResponse.StatusCode < 300 && namedResponse.Data is not null)
        {
            return namedResponse.Data;
        }

        return null;
    }

    private static string NormalizeLookupName(string cardName)
        => cardName
            .Trim()
            .Replace('\u2019', '\'')
            .Replace('\u2018', '\'')
            .Replace('\u02BC', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .ToLowerInvariant();

    /// <summary>
    /// Normalizes a card name for use in Scryfall API payloads.
    /// Converts the single-slash DFC separator used by Archidekt exports (" / ")
    /// to the double-slash form Scryfall expects (" // ") so DFC cards resolve on
    /// the first /cards/collection attempt instead of cascading into per-card fallbacks.
    /// DeckEntry.Name is NOT modified \u2014 normalization happens only at the call site.
    /// </summary>
    private static string NormalizeForScryfall(string cardName)
        => cardName.Replace(" / ", " // ");

    private async Task<string> ValidateCommanderAsync(IReadOnlyList<DeckEntry> entries, string? commanderName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(commanderName))
        {
            throw new InvalidOperationException("The commander isn't in the deck text. Add a legal commander line before generating the analysis packet.");
        }

        var commanderEntry = entries.FirstOrDefault(entry => string.Equals(entry.Name, commanderName, StringComparison.OrdinalIgnoreCase));
        if (commanderEntry is null)
        {
            throw new InvalidOperationException("The commander isn't in the deck text. Add a legal commander line before generating the analysis packet.");
        }

        var commanderCard = await SearchFallbackCardAsync(commanderName, cancellationToken).ConfigureAwait(false);
        if (commanderCard is null || !IsCommanderEligible(commanderCard))
        {
            throw new InvalidOperationException($"The commander isn't in the deck text. \"{commanderName}\" is not a legal commander by this workflow's rules.");
        }

        return commanderEntry.Name;
    }

    private static bool IsCommanderEligible(ScryfallCard card)
    {
        var typeLine = card.TypeLine ?? string.Empty;
        var oracleText = NormalizeOracleText(card);
        if (IsLegendaryType(typeLine, "Creature"))
        {
            return true;
        }

        if (IsLegendaryType(typeLine, "Vehicle"))
        {
            return true;
        }

        return typeLine.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase)
            && oracleText.Contains("can be your commander", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegendaryType(string typeLine, string requiredType)
        => typeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase)
            && typeLine.Contains(requiredType, StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<MechanicReference>> LookupMechanicReferencesAsync(IReadOnlyList<string> mechanicNames, CancellationToken cancellationToken)
    {
        var tasks = mechanicNames
            .Select(async mechanicName =>
            {
                var result = await _mechanicLookupService.LookupAsync(mechanicName, cancellationToken).ConfigureAwait(false);
                var description = result.SummaryText ?? result.RulesText ?? "No official rules text found.";
                return new MechanicReference(
                    mechanicName,
                    CollapseWhitespace(description),
                    result.RuleReference);
            })
            .ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
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
            var faceParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(face.Name))
            {
                faceParts.Add(face.Name.Trim());
            }

            if (!string.IsNullOrWhiteSpace(face.ManaCost))
            {
                faceParts.Add(face.ManaCost.Trim());
            }

            if (!string.IsNullOrWhiteSpace(face.TypeLine))
            {
                faceParts.Add(CollapseWhitespace(face.TypeLine));
            }

            if (!string.IsNullOrWhiteSpace(face.OracleText))
            {
                faceParts.Add(CollapseWhitespace(face.OracleText));
            }

            if (!string.IsNullOrWhiteSpace(face.Power) && !string.IsNullOrWhiteSpace(face.Toughness))
            {
                faceParts.Add($"{face.Power}/{face.Toughness}");
            }

            if (faceParts.Count > 0)
            {
                parts.Add(string.Join(" | ", faceParts));
            }
        }

        if (!string.IsNullOrWhiteSpace(card.Power) && !string.IsNullOrWhiteSpace(card.Toughness))
        {
            parts.Add($"{card.Power}/{card.Toughness}");
        }

        return string.Join(" ", parts);
    }

    private static string CollapseWhitespace(string value)
    {
        return string.Join(" ", (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

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

    internal static string NormalizeSingleLine(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : CollapseWhitespace(value);

    private static string ExtractJsonObject(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                trimmed = trimmed[..closingFence];
            }
        }

        return trimmed.Trim();
    }

    internal static string BuildRequestContextText(DeckAnalysisRequest request, string? commanderName)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"workflow_step: {request.WorkflowStep}");
        builder.AppendLine($"format: {NormalizeSingleLine(request.Format, "Commander")}");
        builder.AppendLine($"deck_name: {NormalizeSingleLine(request.DeckName, string.Empty)}");
        builder.AppendLine($"commander: {NormalizeSingleLine(commanderName, string.Empty)}");
        builder.AppendLine($"target_commander_bracket: {NormalizeSingleLine(request.TargetCommanderBracket, string.Empty)}");
        builder.AppendLine($"target_ai_platform: {NormalizeSingleLine(request.TargetAiPlatform, "ChatGPT")}");
        builder.AppendLine($"include_sideboard_in_analysis: {request.IncludeSideboardInAnalysis}");
        builder.AppendLine($"include_maybeboard_in_analysis: {request.IncludeMaybeboardInAnalysis}");
        builder.AppendLine("card_specific_question_card_names:");
        foreach (var cardName in request.CardSpecificQuestionCardNames)
        {
            builder.AppendLine($"- {NormalizeSingleLine(cardName, string.Empty)}");
        }
        builder.AppendLine($"budget_upgrade_amount: {NormalizeSingleLine(request.BudgetUpgradeAmount, string.Empty)}");
        builder.AppendLine("selected_analysis_questions:");
        foreach (var questionId in AnalysisQuestionCatalog.NormalizeSelections(request.SelectedAnalysisQuestions))
        {
            builder.AppendLine($"- {questionId}");
        }

        builder.AppendLine("selected_set_codes:");
        foreach (var setCode in request.SelectedSetCodes.Where(setCode => !string.IsNullOrWhiteSpace(setCode)))
        {
            builder.AppendLine($"- {setCode.Trim()}");
        }

        AppendOptionalContextBlock(builder, "strategy_notes", request.StrategyNotes);
        AppendOptionalContextBlock(builder, "meta_notes", request.MetaNotes);
        AppendOptionalContextBlock(builder, "deck_source", request.DeckSource);
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendOptionalContextBlock(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"{label}:");
        builder.AppendLine(value.Trim());
    }

    internal static string FormatBannedCardsLine(IReadOnlyList<string> bannedCards)
        => bannedCards.Count == 0 ? "(unavailable)" : string.Join(", ", bannedCards);

    /// <summary>
    /// Parses a newline- or comma-separated list of card names into a deduplicated, trimmed list.
    /// </summary>
    internal static IReadOnlyList<string> ParseCardNameList(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        return input
            .Split(['\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> ExtractMechanicNames(ScryfallCard card)
    {
        foreach (var keyword in card.Keywords ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                yield return keyword.Trim();
            }
        }

        foreach (var oracleText in EnumerateOracleText(card))
        {
            foreach (var line in oracleText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                var abilityWordMatch = AbilityWordRegex.Match(trimmedLine);
                if (abilityWordMatch.Success)
                {
                    yield return abilityWordMatch.Groups["term"].Value.Trim();
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateOracleText(ScryfallCard card)
    {
        if (!string.IsNullOrWhiteSpace(card.OracleText))
        {
            yield return card.OracleText;
        }

        foreach (var face in card.CardFaces ?? Array.Empty<ScryfallCardFace>())
        {
            if (!string.IsNullOrWhiteSpace(face.OracleText))
            {
                yield return face.OracleText;
            }
        }
    }

    private static string BuildTimingSummary(List<(string Label, long Ms, string? Detail)> timings, long totalMs)
    {
        var sb = new StringBuilder();
        foreach (var (label, ms, detail) in timings)
        {
            sb.Append($"{label}: {ms:N0}ms");
            if (!string.IsNullOrWhiteSpace(detail))
            {
                sb.Append($" ({detail})");
            }

            sb.AppendLine();
        }

        sb.Append($"Total: {totalMs:N0}ms");
        return sb.ToString();
    }

    [GeneratedRegex(@"^(?<term>[A-Za-z][A-Za-z' -]{1,40})\s+—\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex AbilityWordPattern();


    private sealed record CardReferenceRequest(string Name, string Scope);
    private sealed record CardReference(string Scope, string Name, string ManaCost, string TypeLine, string OracleText);

    private sealed record CardReferenceBundle(IReadOnlyList<CardReference> CardReferences, IReadOnlyList<string> MechanicNames, IReadOnlyDictionary<string, string> OracleNameMap);

    private sealed record MechanicReference(string Name, string Description, string? RuleReference);
}
