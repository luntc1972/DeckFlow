using System.Diagnostics;
using System.Text;
using DeckFlow.Core.Diffing;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.Packets;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DeckFlow.Web.Services;

/// <summary>
/// Builds deck-primer prompt packets for all enabled AI platforms.
/// </summary>
public interface IDeckPrimerPacketService
{
    /// <summary>
    /// Builds the primer packet outputs for the supplied request.
    /// </summary>
    /// <param name="request">Current deck-primer request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DeckPrimerPacketResult> BuildAsync(DeckPrimerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to compute the packet-session cache key for the supplied request.
    /// </summary>
    /// <param name="request">Current deck-primer request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> TryComputeCacheKeyAsync(DeckPrimerRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Evaluates whether the current deck has drifted from the deck a primer was generated against.
    /// PURE and synchronous: performs no I/O and never fetches or rebuilds. The deck-only multiset hash
    /// is computed identically to the primer cache key's deck component so fresh/stale never disagrees
    /// with cache behavior (PRIMER-02). When the deck differs, the changed-card count is computed from
    /// the in-hand saved/current decks via a loose diff, excluding printing-only differences (PRIMER-04).
    /// </summary>
    /// <param name="generatedPrimerHash">Deck-only multiset hash captured when the primer was generated
    /// (browser hidden field or uploaded zip); null/empty resolves to fresh.</param>
    /// <param name="currentDeckEntries">The in-hand current deck entries; null/empty means no comparison is possible.</param>
    /// <param name="savedGenerationDeckEntries">The deck entries the primer was generated from, when
    /// available; null/empty suppresses the changed-card count.</param>
    /// <returns>Stale flag, optional changed-card count, and the current deck's multiset hash.</returns>
    PrimerStaleness EvaluateStaleness(
        string? generatedPrimerHash,
        IReadOnlyList<DeckEntry>? currentDeckEntries,
        IReadOnlyList<DeckEntry>? savedGenerationDeckEntries)
        => new(IsStale: false, ChangedCardCount: null, CurrentDeckHash: null);

    /// <summary>
    /// Parses pasted deck export text into entries using ONLY the local Moxfield/Archidekt text parsers.
    /// Network-free: an absolute Moxfield/Archidekt URL is rejected (returns null) rather than imported,
    /// so this primitive can never trigger an upstream call (PRIMER-03). Blank or unrecognized input
    /// returns null and never throws.
    /// </summary>
    /// <param name="deckExportText">Untrusted user paste text.</param>
    /// <returns>Parsed entries, or null when blank, a deck URL, or unrecognized.</returns>
    IReadOnlyList<DeckEntry>? TryParseDeckTextLocal(string? deckExportText) => null;
}

/// <summary>
/// Returns the results of a deck-primer packet build.
/// </summary>
/// <param name="InputSummary">Short summary of the loaded deck and selected primer target.</param>
/// <param name="SuggestedChatTitle">Suggested conversation title for the downstream AI chat.</param>
/// <param name="RequestContextText">Round-trip request context persisted into primer zip artifacts.</param>
/// <param name="PromptTextsByPlatform">Enabled-platform prompt texts keyed by <see cref="AiPlatform.Key"/>.</param>
/// <param name="TimingSummary">Human-readable timing summary for upstream work.</param>
/// <param name="ImportWarning">Optional warning surfaced when deck import succeeds with caveats.</param>
/// <param name="ResolvedCommanderName">Resolved commander name when known.</param>
/// <param name="DecklistText">Normalized decklist text block.</param>
/// <param name="DeckMultisetHash">Deck-only multiset hash of the generation deck, used by the controller
/// to re-arm the staleness hidden field and persist the hash into the download zip.</param>
// Why: this positional record must stay JSON-round-trippable; do not convert properties to get-only
// accessors because System.Text.Json drops get-only positional members in modern runtimes.
// PromptTextsByPlatform intentionally contains only the currently enabled AI platforms.
public sealed record DeckPrimerPacketResult(
    string InputSummary,
    string SuggestedChatTitle,
    string RequestContextText,
    IReadOnlyDictionary<string, string> PromptTextsByPlatform,
    string? TimingSummary,
    string? ImportWarning = null,
    string? ResolvedCommanderName = null,
    string? DecklistText = null,
    string? DeckMultisetHash = null);

/// <summary>
/// Result of a pure deck-staleness evaluation: whether the current deck drifted from the deck a
/// primer was generated against, the optional changed-card count (null when suppressed), and the
/// current deck's multiset hash (null when no current deck was available).
/// </summary>
/// <param name="IsStale">True when the current deck differs from the generated primer's deck.</param>
/// <param name="ChangedCardCount">Add + remove + quantity-change count (printing swaps excluded), or
/// null when there is no saved generation snapshot to diff against.</param>
/// <param name="CurrentDeckHash">Deck-only multiset hash of the current deck, or null when absent.</param>
public sealed record PrimerStaleness(bool IsStale, int? ChangedCardCount, string? CurrentDeckHash);

/// <summary>
/// Builds deck-primer packets by loading a deck, grounding combo/category/matchup context once,
/// then rendering one prompt per enabled AI platform.
/// </summary>
public sealed partial class DeckPrimerPacketService : IDeckPrimerPacketService
{
    /// <summary>
    /// Feature-flag key that, when enabled, surfaces a "deck changed since this primer was generated"
    /// stale banner on the Deck Primer page (shown only on resume-without-rebuild when the current deck
    /// differs from the generated primer's deck). Default-off (seeded FALSE); never auto-rebuilds or
    /// re-fetches, so output and zips are byte-identical when off. D-04 / RESEARCH §"Resolved Open
    /// Questions #6".
    /// </summary>
    public const string StaleFlag = "tool.primer.stale-flag";

    private const string ComboRankingVerdict = "sufficient"; // 31-SPIKE.md verdict: active branch = priority-rank.
    private const string CedhArchetypeVerdict = "meta-query-available"; // 31-SPIKE.md verdict: use GetTopArchetypesAsync.
    private const int CedhArchetypeCount = 8;
    private const int MaxNearCombos = 15;

    private readonly IDeckEntryLoader? _deckEntryLoader;
    private readonly ICommanderSpellbookService? _commanderSpellbookService;
    private readonly IEdhTop16Client? _edhTop16Client;
    private readonly ICategoryKnowledgeStore? _knowledgeStore;
    private readonly PrimerPromptVariantRegistry _primerPromptRegistry;
    private readonly PacketSessionCache _packetCache;
    private readonly ILogger<DeckPrimerPacketService> _logger;
    private readonly AiPlatformOptions _aiPlatformOptions;
    private readonly MoxfieldParser _moxfieldParser;
    private readonly ArchidektParser _archidektParser;
    private readonly Func<string, CancellationToken, Task<List<DeckEntry>>>? _loadDeckEntriesAsyncOverride;
    private readonly Func<string, IReadOnlyList<DeckEntry>?>? _parseDeckTextLocalOverride;
    private readonly Func<IReadOnlyList<DeckEntry>, CancellationToken, Task<CommanderSpellbookResult?>>? _findCombosAsyncOverride;
    private readonly Func<int, CancellationToken, Task<IReadOnlyList<EdhTop16Entry>>>? _getTopArchetypesAsyncOverride;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<CategoryKnowledgeRow>>>? _getCategoryRowsForCommanderAsyncOverride;

    /// <summary>
    /// Initializes the production deck-primer packet service.
    /// </summary>
    /// <param name="deckEntryLoader">Shared deck loader used for public deck URLs and pasted exports.</param>
    /// <param name="commanderSpellbookService">Commander Spellbook lookup service.</param>
    /// <param name="edhTop16Client">EDH Top 16 client.</param>
    /// <param name="knowledgeStore">Category knowledge store.</param>
    /// <param name="primerPromptRegistry">Primer prompt variant registry.</param>
    /// <param name="packetCache">Dedicated packet cache.</param>
    /// <param name="aiPlatformOptions">AI-platform options controlling Gemini enablement.</param>
    /// <param name="moxfieldParser">Local Moxfield text parser.</param>
    /// <param name="archidektParser">Local Archidekt text parser.</param>
    /// <param name="logger">Optional logger.</param>
    internal DeckPrimerPacketService(
        IDeckEntryLoader deckEntryLoader,
        ICommanderSpellbookService commanderSpellbookService,
        IEdhTop16Client edhTop16Client,
        ICategoryKnowledgeStore knowledgeStore,
        PrimerPromptVariantRegistry primerPromptRegistry,
        PacketSessionCache packetCache,
        IOptions<AiPlatformOptions> aiPlatformOptions,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser,
        ILogger<DeckPrimerPacketService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(commanderSpellbookService);
        ArgumentNullException.ThrowIfNull(edhTop16Client);
        ArgumentNullException.ThrowIfNull(knowledgeStore);
        ArgumentNullException.ThrowIfNull(primerPromptRegistry);
        ArgumentNullException.ThrowIfNull(packetCache);
        ArgumentNullException.ThrowIfNull(aiPlatformOptions);
        ArgumentNullException.ThrowIfNull(moxfieldParser);
        ArgumentNullException.ThrowIfNull(archidektParser);

        _deckEntryLoader = deckEntryLoader;
        _commanderSpellbookService = commanderSpellbookService;
        _edhTop16Client = edhTop16Client;
        _knowledgeStore = knowledgeStore;
        _primerPromptRegistry = primerPromptRegistry;
        _packetCache = packetCache;
        _aiPlatformOptions = aiPlatformOptions.Value;
        _moxfieldParser = moxfieldParser;
        _archidektParser = archidektParser;
        _logger = logger ?? NullLogger<DeckPrimerPacketService>.Instance;
    }

    internal DeckPrimerPacketService(
        PrimerPromptVariantRegistry primerPromptRegistry,
        PacketSessionCache packetCache,
        Func<string, CancellationToken, Task<List<DeckEntry>>>? loadDeckEntriesAsyncOverride = null,
        Func<IReadOnlyList<DeckEntry>, CancellationToken, Task<CommanderSpellbookResult?>>? findCombosAsyncOverride = null,
        Func<int, CancellationToken, Task<IReadOnlyList<EdhTop16Entry>>>? getTopArchetypesAsyncOverride = null,
        Func<string, CancellationToken, Task<IReadOnlyList<CategoryKnowledgeRow>>>? getCategoryRowsForCommanderAsyncOverride = null,
        Func<string, IReadOnlyList<DeckEntry>?>? parseDeckTextLocalOverride = null,
        bool geminiEnabled = false,
        ILogger<DeckPrimerPacketService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(primerPromptRegistry);
        ArgumentNullException.ThrowIfNull(packetCache);

        _primerPromptRegistry = primerPromptRegistry;
        _packetCache = packetCache;
        _aiPlatformOptions = new AiPlatformOptions { GeminiEnabled = geminiEnabled };
        _logger = logger ?? NullLogger<DeckPrimerPacketService>.Instance;
        _loadDeckEntriesAsyncOverride = loadDeckEntriesAsyncOverride;
        _findCombosAsyncOverride = findCombosAsyncOverride;
        _getTopArchetypesAsyncOverride = getTopArchetypesAsyncOverride;
        _getCategoryRowsForCommanderAsyncOverride = getCategoryRowsForCommanderAsyncOverride;
        _parseDeckTextLocalOverride = parseDeckTextLocalOverride;

        // Why: TryParseDeckTextLocal must exercise the real local parsers in unit tests that do not
        // supply parseDeckTextLocalOverride; the parsers are network-free so default-constructing them
        // here keeps the primitive's no-fetch contract while leaving the override as the bypass seam.
        _moxfieldParser = new MoxfieldParser();
        _archidektParser = new ArchidektParser();
    }

    /// <inheritdoc/>
    public async Task<string?> TryComputeCacheKeyAsync(DeckPrimerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DeckSource))
        {
            return null;
        }

        List<DeckEntry> entries;
        try
        {
            if (_loadDeckEntriesAsyncOverride is not null)
            {
                _lastImportNotice = null;
                entries = await _loadDeckEntriesAsyncOverride(request.DeckSource, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var loaded = await _deckEntryLoader!.LoadFromSourceAsync(request.DeckSource, cancellationToken: cancellationToken).ConfigureAwait(false);
                _lastImportNotice = loaded.FallbackNotice;
                entries = loaded.Entries;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or DeckParseException or HttpRequestException)
        {
            return null;
        }

        if (entries.Count == 0)
        {
            return null;
        }

        var playableEntries = entries
            .Where(entry =>
                !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (playableEntries.Count == 0)
        {
            return null;
        }

        var commanderName = ResolveCommanderName(playableEntries);
        if (string.IsNullOrWhiteSpace(commanderName))
        {
            return null;
        }

        var primerOptions = NormalizePrimerOptions(request);
        request.PrimerStyle = primerOptions.EffectiveStyle;

        var selectedSectionIds = primerOptions.SelectedSectionIds;

        var inputs = new PrimerCacheInputs(
            Commander: commanderName,
            NormalizedDeckSource: BuildCanonicalDeckSourceText(entries),
            TargetBracket: NormalizeSingleLine(request.TargetCommanderBracket, string.Empty),
            PrimerStyle: request.PrimerStyle,
            SelectedSectionIds: selectedSectionIds,
            GeminiEnabled: _aiPlatformOptions.GeminiEnabled);

        return PacketSessionCache.ComputeKey(inputs);
    }

    /// <inheritdoc/>
    public async Task<DeckPrimerPacketResult> BuildAsync(DeckPrimerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DeckSource))
        {
            throw new InvalidOperationException("A deck URL or pasted deck export is required.");
        }

        var overallStopwatch = Stopwatch.StartNew();
        var timings = new List<(string Label, long Ms, string? Detail)>();

        var cacheKey = await TryComputeCacheKeyAsync(request, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(cacheKey)
            && _packetCache.TryGet<DeckPrimerPacketResult>(cacheKey, out var cached)
            && cached is not null)
        {
            return cached;
        }

        var bracket = CommanderBracketCatalog.Find(request.TargetCommanderBracket)
            ?? throw new InvalidOperationException("Choose a target Commander bracket before generating the primer packet.");
        var bracketNumber = CommanderBracketCatalog.Options
            .Select((option, index) => new { Option = option, Number = index + 1 })
            .First(item => string.Equals(item.Option.Value, bracket.Value, StringComparison.OrdinalIgnoreCase))
            .Number;

        var primerOptions = NormalizePrimerOptions(request);
        request.PrimerStyle = primerOptions.EffectiveStyle;

        var selectedSectionIds = primerOptions.SelectedSectionIds;
        var selectedSections = PrimerSectionCatalog.AllSections
            .Where(section => selectedSectionIds.Contains(section.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var loadStopwatch = Stopwatch.StartNew();
        List<DeckEntry> entries;
        if (_loadDeckEntriesAsyncOverride is not null)
        {
            _lastImportNotice = null;
            entries = await _loadDeckEntriesAsyncOverride(request.DeckSource, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var loaded = await _deckEntryLoader!.LoadFromSourceAsync(request.DeckSource, cancellationToken: cancellationToken).ConfigureAwait(false);
            _lastImportNotice = loaded.FallbackNotice;
            entries = loaded.Entries;
        }
        timings.Add(("Deck load", loadStopwatch.ElapsedMilliseconds, null));

        // Deck-only multiset hash over ALL loaded entries (matching the cache key's deck component) so
        // the controller can re-arm the staleness hidden field and persist it into the download zip.
        var deckMultisetHash = PacketSessionCache.ComputeKey(BuildCanonicalDeckSourceText(entries));

        var playableEntries = entries
            .Where(entry =>
                !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (playableEntries.Count == 0)
        {
            throw new InvalidOperationException("The submitted deck did not contain any commander or mainboard cards.");
        }

        var commanderName = ResolveCommanderName(playableEntries)
            ?? throw new InvalidOperationException("The submitted deck did not contain a Commander entry.");
        var possibleIncludeEntries = entries
            .Where(entry =>
                string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var decklistText = PacketTextAssembler.BuildSectionedDecklistText(playableEntries, possibleIncludeEntries);

        var comboStopwatch = Stopwatch.StartNew();
        var comboResult = await FindCombosAsync(playableEntries, cancellationToken).ConfigureAwait(false);
        timings.Add(("Commander Spellbook", comboStopwatch.ElapsedMilliseconds, $"{comboResult?.IncludedCombos.Count ?? 0} combos, {comboResult?.AlmostIncludedCombos.Count ?? 0} near-combos"));

        var categoryStopwatch = Stopwatch.StartNew();
        CategoryDistributionSummary? categoryDistribution;
        try
        {
            var categoryRows = await GetCategoryRowsForCommanderAsync(commanderName, cancellationToken).ConfigureAwait(false);
            categoryDistribution = BuildCategoryDistribution(categoryRows);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Category knowledge lookup failed for commander {CommanderName}; omitting category distribution block.", commanderName);
            categoryDistribution = null;
        }

        timings.Add(("Category knowledge", categoryStopwatch.ElapsedMilliseconds, categoryDistribution is null ? "0 rows" : "grounded counts"));

        IReadOnlyList<EdhTop16Entry>? top16Entries = null;
        if (string.Equals(bracket.Value, "cEDH", StringComparison.OrdinalIgnoreCase)
            && string.Equals(CedhArchetypeVerdict, "meta-query-available", StringComparison.Ordinal))
        {
            var top16Stopwatch = Stopwatch.StartNew();
            try
            {
                top16Entries = await GetTopArchetypesAsync(CedhArchetypeCount, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EDH Top 16 archetype query failed for commander {CommanderName}; falling back to generic matchup buckets.", commanderName);
                top16Entries = null;
            }

            timings.Add(("EDH Top 16", top16Stopwatch.ElapsedMilliseconds, $"{top16Entries?.Count ?? 0} archetypes"));
        }

        var enabledPlatforms = GetEnabledPlatforms();
        var promptTextsByPlatform = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var platform in enabledPlatforms)
        {
            promptTextsByPlatform[platform.Key] = _primerPromptRegistry.Build(
                platform,
                request,
                decklistText,
                selectedSections,
                comboResult,
                top16Entries,
                categoryDistribution,
                bracketNumber,
                cancellationToken);
        }

        var result = new DeckPrimerPacketResult(
            InputSummary: BuildInputSummary(request, playableEntries, possibleIncludeEntries, commanderName, bracket, enabledPlatforms),
            SuggestedChatTitle: BuildSuggestedChatTitle(request, commanderName),
            RequestContextText: BuildRequestContextText(request, commanderName, selectedSectionIds),
            PromptTextsByPlatform: promptTextsByPlatform,
            TimingSummary: BuildTimingSummary(timings, overallStopwatch.ElapsedMilliseconds),
            ImportWarning: _lastImportNotice,
            ResolvedCommanderName: commanderName,
            DecklistText: decklistText,
            DeckMultisetHash: deckMultisetHash);

        if (!string.IsNullOrWhiteSpace(cacheKey))
        {
            _packetCache.Set(cacheKey, result, EstimateSizeBytes(result));
        }

        _logger.LogInformation(
            "Deck Primer packet build completed in {ElapsedMs}ms for commander {CommanderName} across {PlatformCount} platform(s).",
            overallStopwatch.ElapsedMilliseconds,
            commanderName,
            promptTextsByPlatform.Count);

        return result;
    }

    /// <summary>
    /// Warning surfaced to the UI when the Moxfield fallback was used.
    /// Set from the shared deck loader result, read during BuildAsync, cleared per call.
    /// </summary>
    private string? _lastImportNotice;

    private async Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
    {
        if (_findCombosAsyncOverride is not null)
        {
            return await _findCombosAsyncOverride(entries, cancellationToken).ConfigureAwait(false);
        }

        return await _commanderSpellbookService!
            .FindCombosAsync(entries, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<EdhTop16Entry>> GetTopArchetypesAsync(int count, CancellationToken cancellationToken)
    {
        if (_getTopArchetypesAsyncOverride is not null)
        {
            return await _getTopArchetypesAsyncOverride(count, cancellationToken).ConfigureAwait(false);
        }

        return await _edhTop16Client!
            .GetTopArchetypesAsync(count, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken)
    {
        if (_getCategoryRowsForCommanderAsyncOverride is not null)
        {
            return await _getCategoryRowsForCommanderAsyncOverride(commanderName, cancellationToken).ConfigureAwait(false);
        }

        return await _knowledgeStore!
            .GetCategoryRowsForCommanderAsync(commanderName, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static string BuildComboReferenceText(CommanderSpellbookResult? combos, string spikeVerdict)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Known Combos (ground truth — do not speculate)");

        if (combos is null)
        {
            builder.AppendLine("No verified combos available — treat all synergies as speculative.");
            builder.AppendLine("(Commander Spellbook API was unreachable at generation time.)");
        }
        else if (combos.IncludedCombos.Count == 0)
        {
            builder.AppendLine("No verified combos available — treat all synergies as speculative.");
        }
        else
        {
            IEnumerable<SpellbookCombo> orderedCombos = combos.IncludedCombos;
            if (string.Equals(spikeVerdict, "sufficient", StringComparison.Ordinal))
            {
                // Rank popularity DESC (most-played first), manaValueNeeded ASC tiebreak
                // (cheaper to assemble), then stable API order when both are equal/absent.
                orderedCombos = combos.IncludedCombos
                    .Select((combo, index) => new { Combo = combo, Index = index })
                    .OrderByDescending(item => item.Combo.Popularity ?? 0)
                    .ThenBy(item => item.Combo.ManaValueNeeded ?? int.MaxValue)
                    .ThenBy(item => item.Index)
                    .Select(item => item.Combo);
            }
            else
            {
                builder.AppendLine("Keep the API order above and rank the practical combo lines yourself.");
            }

            var comboIndex = 1;
            foreach (var combo in orderedCombos)
            {
                builder.AppendLine($"{comboIndex}. Cards: {string.Join(" + ", combo.CardNames)}");
                builder.AppendLine($"   Result: {string.Join(", ", combo.Results)}");
                if (!string.IsNullOrWhiteSpace(combo.Instructions))
                {
                    builder.AppendLine($"   How: {combo.Instructions}");
                }

                comboIndex++;
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Speculative Synergies (you propose)");
        builder.AppendLine("Suggest plausible interactions or lines that are not in the ground-truth block above.");
        builder.AppendLine("Label every speculative item as unverified and do not restate it as a known combo.");

        if (combos?.AlmostIncludedCombos.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Near-Combos (one card away)");
            foreach (var combo in combos.AlmostIncludedCombos.Take(MaxNearCombos))
            {
                builder.AppendLine($"- Missing: {combo.MissingCard} | Have: {string.Join(" + ", combo.CardsInDeck)}");
                builder.AppendLine($"  Result: {string.Join(", ", combo.Results)}");
                if (!string.IsNullOrWhiteSpace(combo.Instructions))
                {
                    builder.AppendLine($"  How: {combo.Instructions}");
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    internal static string NormalizeSingleLine(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : CollapseWhitespace(value);

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var sawWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                sawWhitespace = true;
                continue;
            }

            if (sawWhitespace && builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(character);
            sawWhitespace = false;
        }

        return builder.Length == 0 ? string.Empty : builder.ToString();
    }

    private static int EstimateSizeBytes(DeckPrimerPacketResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return (result.InputSummary?.Length ?? 0)
            + (result.SuggestedChatTitle?.Length ?? 0)
            + (result.RequestContextText?.Length ?? 0)
            + result.PromptTextsByPlatform.Sum(entry => (entry.Key?.Length ?? 0) + (entry.Value?.Length ?? 0))
            + (result.TimingSummary?.Length ?? 0)
            + (result.ImportWarning?.Length ?? 0)
            + (result.ResolvedCommanderName?.Length ?? 0)
            + (result.DecklistText?.Length ?? 0)
            + (result.DeckMultisetHash?.Length ?? 0);
    }

    private static IReadOnlyList<AiPlatform> GetEnabledPlatforms(bool geminiEnabled)
        => AiPlatform.All
            .Where(platform => geminiEnabled || !string.Equals(platform.Key, AiPlatform.Gemini.Key, StringComparison.Ordinal))
            .ToList();

    private IReadOnlyList<AiPlatform> GetEnabledPlatforms()
        => GetEnabledPlatforms(_aiPlatformOptions.GeminiEnabled);

    private static string? ResolveCommanderName(IReadOnlyList<DeckEntry> entries)
    {
        var commander = entries.FirstOrDefault(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase));
        if (commander is not null)
        {
            return commander.Name;
        }

        return entries.Count > 0 ? entries[0].Name : null;
    }

    private static CategoryDistributionSummary? BuildCategoryDistribution(IReadOnlyList<CategoryKnowledgeRow> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        var ramp = 0;
        var draw = 0;
        var tutor = 0;
        var interaction = 0;

        foreach (var row in rows)
        {
            var category = row.Category ?? string.Empty;
            if (category.Contains("ramp", StringComparison.OrdinalIgnoreCase))
            {
                ramp++;
            }

            if (category.Contains("draw", StringComparison.OrdinalIgnoreCase))
            {
                draw++;
            }

            if (category.Contains("tutor", StringComparison.OrdinalIgnoreCase))
            {
                tutor++;
            }

            if (category.Contains("interaction", StringComparison.OrdinalIgnoreCase)
                || category.Contains("removal", StringComparison.OrdinalIgnoreCase))
            {
                interaction++;
            }
        }

        return ramp == 0 && draw == 0 && tutor == 0 && interaction == 0
            ? null
            : new CategoryDistributionSummary(ramp, draw, tutor, interaction);
    }

    private static string BuildInputSummary(
        DeckPrimerRequest request,
        IReadOnlyList<DeckEntry> playableEntries,
        IReadOnlyList<DeckEntry> possibleIncludeEntries,
        string commanderName,
        CommanderBracketOption bracket,
        IReadOnlyList<AiPlatform> enabledPlatforms)
    {
        var mainDeckCards = playableEntries
            .Where(entry => !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Quantity);
        var commanderCards = playableEntries
            .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Quantity);
        var builder = new StringBuilder();
        builder.AppendLine($"Format: {NormalizeSingleLine(request.Format, "Commander")}");
        builder.AppendLine($"Commander: {commanderName}");
        builder.AppendLine($"Main deck cards: {mainDeckCards}");
        builder.AppendLine($"Commander cards: {commanderCards}");
        if (possibleIncludeEntries.Count > 0)
        {
            builder.AppendLine($"Possible includes: {possibleIncludeEntries.Sum(entry => entry.Quantity)}");
        }

        builder.AppendLine($"Target commander bracket: {bracket.Label}");
        builder.AppendLine($"Enabled AI variants: {string.Join(", ", enabledPlatforms.Select(platform => platform.Key))}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildSuggestedChatTitle(DeckPrimerRequest request, string commanderName)
    {
        var primaryName = !string.IsNullOrWhiteSpace(commanderName)
            ? commanderName.Trim()
            : !string.IsNullOrWhiteSpace(request.DeckName)
                ? request.DeckName.Trim()
                : "Commander Deck";

        return $"{primaryName} | Deck Primer";
    }

    private static string BuildRequestContextText(DeckPrimerRequest request, string commanderName, IReadOnlyList<string> selectedSectionIds)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"workflow_step: {request.WorkflowStep}");
        PacketTextAssembler.AppendKeyValueLine(builder, "format", request.Format, "Commander", NormalizeSingleLine);
        PacketTextAssembler.AppendKeyValueLine(builder, "deck_name", request.DeckName, string.Empty, NormalizeSingleLine);
        PacketTextAssembler.AppendKeyValueLine(builder, "commander", commanderName, string.Empty, NormalizeSingleLine);
        PacketTextAssembler.AppendKeyValueLine(builder, "target_commander_bracket", request.TargetCommanderBracket, string.Empty, NormalizeSingleLine);
        PacketTextAssembler.AppendKeyValueLine(builder, "target_ai_platform", request.TargetAiPlatform, AiPlatform.Default.Key, NormalizeSingleLine);
        builder.AppendLine($"primer_style: {request.PrimerStyle}");
        builder.AppendLine("selected_section_ids:");
        foreach (var sectionId in selectedSectionIds)
        {
            builder.AppendLine($"- {sectionId}");
        }

        builder.AppendLine("deck_source:");
        builder.AppendLine(request.DeckSource.Trim());
        return builder.ToString().TrimEnd();
    }

    private static PrimerPromptOptions NormalizePrimerOptions(DeckPrimerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bracket = CommanderBracketCatalog.Find(request.TargetCommanderBracket);
        var isCedh = CommanderBracketCatalog.IsCedh(request.TargetCommanderBracket);
        var effectiveStyle = request.PrimerStyle == PrimerOutputStyle.FullCedh && !isCedh
            ? PrimerOutputStyle.MoxfieldRich
            : request.PrimerStyle;
        var selectedSectionIds = effectiveStyle == PrimerOutputStyle.FullCedh && isCedh
            ? PrimerSectionCatalog.GetPresetForBracket(bracket!.Value)
            : PrimerSectionCatalog.NormalizeSelections(request.SelectedSectionIds, request.TargetCommanderBracket);

        return new PrimerPromptOptions(effectiveStyle, selectedSectionIds);
    }

    private static string BuildTimingSummary(IReadOnlyList<(string Label, long Ms, string? Detail)> timings, long totalMs)
    {
        if (timings.Count == 0)
        {
            return $"Total: {totalMs} ms";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Total: {totalMs} ms");
        foreach (var (label, ms, detail) in timings)
        {
            builder.Append("- ");
            builder.Append(label);
            builder.Append(": ");
            builder.Append(ms);
            builder.Append(" ms");
            if (!string.IsNullOrWhiteSpace(detail))
            {
                builder.Append(" (");
                builder.Append(detail);
                builder.Append(')');
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    // Why: internal (not private) so the staleness primitives and golden tests reuse the EXACT
    // canonicalization the primer cache key hashes — guarantees fresh/stale never diverges from cache
    // behavior (Pitfall 1: no second hash path). Order/format MUST NOT change.
    internal static string BuildCanonicalDeckSourceText(IReadOnlyList<DeckEntry> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries
                     .OrderBy(entry => entry.Board, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(entry => entry.Quantity))
        {
            builder.Append(entry.Board);
            builder.Append('|');
            builder.Append(entry.Quantity);
            builder.Append('|');
            builder.Append(entry.Name);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    /// <inheritdoc/>
    public PrimerStaleness EvaluateStaleness(
        string? generatedPrimerHash,
        IReadOnlyList<DeckEntry>? currentDeckEntries,
        IReadOnlyList<DeckEntry>? savedGenerationDeckEntries)
    {
        var currentDeckHash = currentDeckEntries is null || currentDeckEntries.Count == 0
            ? null
            : PacketSessionCache.ComputeKey(BuildCanonicalDeckSourceText(currentDeckEntries));

        // No generation/current hash to compare, or same multiset (incl. reorder / printing-only swap) → fresh.
        if (string.IsNullOrEmpty(generatedPrimerHash)
            || currentDeckHash is null
            || string.Equals(currentDeckHash, generatedPrimerHash, StringComparison.Ordinal))
        {
            return new PrimerStaleness(IsStale: false, ChangedCardCount: null, CurrentDeckHash: currentDeckHash);
        }

        // Deck changed but we have no generation snapshot to diff against → stale, count suppressed.
        if (savedGenerationDeckEntries is null || savedGenerationDeckEntries.Count == 0)
        {
            return new PrimerStaleness(IsStale: true, ChangedCardCount: null, CurrentDeckHash: currentDeckHash);
        }

        // Changed-card count = add + remove + quantity-change; printing-only swaps land in
        // PrintingConflicts and are intentionally EXCLUDED (locked decision #3). Clamped >= 0.
        var diff = new DiffEngine(MatchMode.Loose).Compare(
            savedGenerationDeckEntries.ToList(),
            currentDeckEntries!.ToList());
        var changedCardCount = Math.Max(0, diff.ToAdd.Count + diff.CountMismatch.Count + diff.OnlyInArchidekt.Count);

        return new PrimerStaleness(IsStale: true, ChangedCardCount: changedCardCount, CurrentDeckHash: currentDeckHash);
    }

    /// <inheritdoc/>
    public IReadOnlyList<DeckEntry>? TryParseDeckTextLocal(string? deckExportText)
    {
        if (_parseDeckTextLocalOverride is not null)
        {
            return _parseDeckTextLocalOverride(deckExportText ?? string.Empty);
        }

        if (string.IsNullOrWhiteSpace(deckExportText))
        {
            return null;
        }

        // Reject absolute Moxfield/Archidekt URLs: importing them would require a network call, which
        // this primitive must never do (PRIMER-03 — proves "no fetch on resume").
        if (Uri.TryCreate(deckExportText.Trim(), UriKind.Absolute, out var uri)
            && (DeckSourceHost.IsMoxfield(uri) || DeckSourceHost.IsArchidekt(uri)))
        {
            return null;
        }

        try
        {
            return _moxfieldParser.ParseText(deckExportText);
        }
        catch (DeckParseException)
        {
        }

        try
        {
            return _archidektParser.ParseText(deckExportText);
        }
        catch (DeckParseException)
        {
            return null;
        }
    }
}

internal sealed record PrimerCacheInputs(
    string Commander,
    string NormalizedDeckSource,
    string TargetBracket,
    PrimerOutputStyle PrimerStyle,
    IReadOnlyList<string> SelectedSectionIds,
    bool GeminiEnabled);

internal sealed record PrimerPromptOptions(
    PrimerOutputStyle EffectiveStyle,
    IReadOnlyList<string> SelectedSectionIds);
