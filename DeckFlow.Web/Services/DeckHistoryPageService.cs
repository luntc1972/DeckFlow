using DeckFlow.Core.History;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.Packets;
using DeckFlow.Web.Services.PromptBuilders.Evolution;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services;

/// <summary>
/// Loads an optional current deck, parses an optional history file, appends or inspects history,
/// computes a comparison pair, and renders the evolution prompt for the requested AI platform.
/// </summary>
public interface IDeckHistoryPageService
{
    /// <summary>
    /// Processes the request and optional uploaded history file content into a page result.
    /// </summary>
    /// <param name="request">Current deck-history request.</param>
    /// <param name="uploadedHistoryJson">Uploaded history JSON; when non-null it wins over the hidden field.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DeckHistoryProcessResult> ProcessAsync(
        DeckHistoryRequest request,
        string? uploadedHistoryJson,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Returns the result of deck-history page processing.
/// </summary>
public sealed record DeckHistoryProcessResult
{
    /// <summary>The resulting parsed or appended history file, when available.</summary>
    public DeckHistoryFile? File { get; init; }

    /// <summary>The serialized round-trip history JSON, when <see cref="File"/> is non-null.</summary>
    public string? SerializedJson { get; init; }

    /// <summary>True when the current request appended a new snapshot.</summary>
    public bool Appended { get; init; }

    /// <summary>The selected older version id for pairwise diff display, when available.</summary>
    public int? PairOlderId { get; init; }

    /// <summary>The selected newer version id for pairwise diff display, when available.</summary>
    public int? PairNewerId { get; init; }

    /// <summary>The selected pairwise diff, when at least two versions are available.</summary>
    public VersionDiff? PairDiff { get; init; }

    /// <summary>The rendered evolution prompt text for the target AI platform.</summary>
    public string PromptText { get; init; } = string.Empty;

    /// <summary>Non-blocking warnings collected while parsing, loading, or appending.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>User-facing error message on hard failure; null on success.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Default implementation of <see cref="IDeckHistoryPageService"/>.
/// </summary>
internal sealed class DeckHistoryPageService : IDeckHistoryPageService
{
    private readonly IDeckEntryLoader _deckEntryLoader;
    private readonly EvolutionPromptVariantRegistry _evolutionPromptVariantRegistry;
    private readonly IScryfallCardResolver _scryfallCardResolver;
    private readonly ScryfallReferenceResolver _scryfallReferenceResolver;
    private readonly ILogger<DeckHistoryPageService> _logger;
    private readonly Func<DateTimeOffset> _nowUtc;

    /// <summary>
    /// Initializes the production deck-history page service.
    /// </summary>
    /// <param name="deckEntryLoader">Shared deck loader used for public deck URLs and pasted exports.</param>
    /// <param name="evolutionPromptVariantRegistry">Evolution prompt variant registry.</param>
    /// <param name="scryfallCardResolver">Shared Scryfall resolver used for card-reference enrichment.</param>
    /// <param name="logger">Logger for non-blocking card-reference failures.</param>
    public DeckHistoryPageService(
        IDeckEntryLoader deckEntryLoader,
        EvolutionPromptVariantRegistry evolutionPromptVariantRegistry,
        IScryfallCardResolver scryfallCardResolver,
        ILogger<DeckHistoryPageService> logger)
        : this(
            deckEntryLoader,
            evolutionPromptVariantRegistry,
            scryfallCardResolver,
            logger,
            () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    /// Initializes the test seam with a deterministic clock.
    /// </summary>
    /// <param name="deckEntryLoader">Shared deck loader used for public deck URLs and pasted exports.</param>
    /// <param name="evolutionPromptVariantRegistry">Evolution prompt variant registry.</param>
    /// <param name="scryfallCardResolver">Shared Scryfall resolver used for card-reference enrichment.</param>
    /// <param name="logger">Logger for non-blocking card-reference failures.</param>
    /// <param name="nowUtc">Clock used for new snapshots.</param>
    internal DeckHistoryPageService(
        IDeckEntryLoader deckEntryLoader,
        EvolutionPromptVariantRegistry evolutionPromptVariantRegistry,
        IScryfallCardResolver scryfallCardResolver,
        ILogger<DeckHistoryPageService>? logger,
        Func<DateTimeOffset> nowUtc)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        ArgumentNullException.ThrowIfNull(evolutionPromptVariantRegistry);
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);
        ArgumentNullException.ThrowIfNull(nowUtc);

        _deckEntryLoader = deckEntryLoader;
        _evolutionPromptVariantRegistry = evolutionPromptVariantRegistry;
        _scryfallCardResolver = scryfallCardResolver;
        _scryfallReferenceResolver = new ScryfallReferenceResolver(scryfallCardResolver);
        _logger = logger ?? NullLogger<DeckHistoryPageService>.Instance;
        _nowUtc = nowUtc;
    }

    /// <inheritdoc />
    public async Task<DeckHistoryProcessResult> ProcessAsync(
        DeckHistoryRequest request,
        string? uploadedHistoryJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<string>();
        var historyJson = ResolveHistoryJson(request, uploadedHistoryJson);
        DeckHistoryFile? file = null;
        string? countWarning = null;

        if (historyJson is not null)
        {
            var parse = DeckHistorySerializer.Parse(historyJson);
            if (!string.IsNullOrWhiteSpace(parse.Error))
            {
                return Error(parse.Error, parse.Warnings);
            }

            file = parse.File;
            warnings.AddRange(parse.Warnings);
        }

        var (_, url, _, deckSource) = DeckInputReconciler.Reconcile(
            request.DeckInputSource,
            request.DeckUrl,
            request.DeckText,
            request.DeckSource);

        DeckSourceLoadResult? load = null;
        if (!string.IsNullOrWhiteSpace(deckSource))
        {
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

            var count = load.Entries
                .Where(entry => !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase))
                .Sum(entry => entry.Quantity);
            if (count != 100)
            {
                countWarning = $"Deck has {count} cards — Commander decks run 100.";
            }
        }

        if (load is null && file is null)
        {
            return Error("Upload a history file, import a deck, or both.", warnings);
        }

        var appended = false;
        if (load is not null)
        {
            file ??= DeckHistoryAppender.CreateNew(
                string.IsNullOrWhiteSpace(request.DeckName) ? "Commander Deck" : request.DeckName.Trim(),
                BuildSource(request.DeckInputSource, url));
            var append = DeckHistoryAppender.Append(file, BuildSnapshot(load.Entries, request));
            file = append.File;
            appended = append.Appended;
            if (countWarning is not null)
            {
                warnings.Add(appended ? $"{countWarning} Snapshot saved anyway." : countWarning);
            }

            if (!string.IsNullOrWhiteSpace(append.Warning))
            {
                warnings.Add(append.Warning);
            }
        }
        else
        {
            file = DeckHistoryAppender.RecomputeDeltas(file!);
        }

        var (pairOlderId, pairNewerId, pairDiff) = SelectPair(file, request);
        string promptText;
        if (file is not null && file.Versions.Count >= 2)
        {
            IReadOnlyList<EvolutionCardReference>? cardReferences = null;
            try
            {
                cardReferences = await ResolveCardReferencesAsync(file, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                var warning = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception);
                _logger.LogWarning(exception, "Scryfall card reference lookup failed while building the deck history evolution prompt.");
                warnings.Add(warning);
            }

            promptText = _evolutionPromptVariantRegistry.Build(
                AiPlatform.Normalize(request.TargetAiPlatform),
                file,
                cardReferences,
                cancellationToken);
        }
        else
        {
            promptText = string.Empty;
        }

        return new DeckHistoryProcessResult
        {
            File = file,
            SerializedJson = file is null ? null : DeckHistorySerializer.Serialize(file),
            Appended = appended,
            PairOlderId = pairOlderId,
            PairNewerId = pairNewerId,
            PairDiff = pairDiff,
            PromptText = promptText,
            Warnings = warnings,
        };
    }

    private async Task<IReadOnlyList<EvolutionCardReference>> ResolveCardReferencesAsync(
        DeckHistoryFile file,
        CancellationToken cancellationToken)
    {
        var requestNames = BuildCardReferenceRequestNames(file);
        if (requestNames.Count == 0)
        {
            return Array.Empty<EvolutionCardReference>();
        }

        var batchResolution = await _scryfallReferenceResolver.ResolveBatchAsync(
            requestNames,
            SearchPrintingFallbackCardAsync,
            normalizeForScryfall: true,
            cancellationToken).ConfigureAwait(false);

        return batchResolution.Resolutions
            .Select(resolution => new EvolutionCardReference(
                resolution.Card.Name,
                resolution.Card.ManaCost ?? string.Empty,
                resolution.Card.TypeLine,
                resolution.Card.OracleText ?? string.Empty))
            .ToList();
    }

    private static IReadOnlyList<string> BuildCardReferenceRequestNames(DeckHistoryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var orderedNames = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var latest = file.Versions[^1];

        foreach (var commander in latest.Commander)
        {
            AddCardReferenceName(orderedNames, seen, commander);
        }

        foreach (var card in latest.Cards)
        {
            AddCardReferenceName(orderedNames, seen, card.Name);
        }

        foreach (var version in file.Versions)
        {
            if (version.Delta is null)
            {
                continue;
            }

            foreach (var add in version.Delta.Adds)
            {
                AddCardReferenceName(orderedNames, seen, add.Name);
            }

            foreach (var cut in version.Delta.Cuts)
            {
                AddCardReferenceName(orderedNames, seen, cut.Name);
            }
        }

        return orderedNames;
    }

    private static void AddCardReferenceName(List<string> orderedNames, HashSet<string> seen, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var normalized = CardNormalizer.Normalize(name);
        if (!seen.Add(normalized))
        {
            return;
        }

        orderedNames.Add(name.Trim());
    }

    private Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
        => _scryfallCardResolver.SearchPrintingFallbackCardAsync(cardName, cancellationToken);

    private static DeckHistoryProcessResult Error(string message, IReadOnlyList<string>? warnings = null) =>
        new()
        {
            ErrorMessage = message,
            Warnings = warnings ?? [],
        };

    private DeckSnapshot BuildSnapshot(IReadOnlyList<DeckEntry> entries, DeckHistoryRequest request) =>
        DeckHistoryAppender.BuildSnapshot(entries, request.Notes, request.Label, _nowUtc());

    private static string? ResolveHistoryJson(DeckHistoryRequest request, string? uploadedHistoryJson)
    {
        if (uploadedHistoryJson is not null)
        {
            return uploadedHistoryJson;
        }

        return string.IsNullOrWhiteSpace(request.HistoryJson) ? null : request.HistoryJson;
    }

    private static DeckHistorySource? BuildSource(DeckInputSource inputSource, string deckUrl)
    {
        if (inputSource != DeckInputSource.PublicUrl
            || !Uri.TryCreate(deckUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new DeckHistorySource
        {
            Site = DeckSourceHost.IsMoxfield(uri)
                ? "moxfield"
                : DeckSourceHost.IsArchidekt(uri)
                    ? "archidekt"
                    : null,
            Url = deckUrl,
        };
    }

    private static (int? OlderId, int? NewerId, VersionDiff? Diff) SelectPair(
        DeckHistoryFile file,
        DeckHistoryRequest request)
    {
        if (file.Versions.Count < 2)
        {
            return (null, null, null);
        }

        var explicitOlder = request.OlderVersionId is int olderRequested
            ? file.Versions.FirstOrDefault(version => version.Id == olderRequested)
            : null;
        var explicitNewer = request.NewerVersionId is int newerRequested
            ? file.Versions.FirstOrDefault(version => version.Id == newerRequested)
            : null;

        DeckSnapshot older;
        DeckSnapshot newer;
        if (explicitOlder is not null && explicitNewer is not null && explicitOlder.Id < explicitNewer.Id)
        {
            older = explicitOlder;
            newer = explicitNewer;
            return (older.Id, newer.Id, VersionDiffProjector.Project(older, newer));
        }

        older = file.Versions[^2];
        newer = file.Versions[^1];
        var delta = newer.Delta ?? new SnapshotDelta();
        return (older.Id, newer.Id, new VersionDiff(delta.Adds, delta.Cuts, delta.QtyChanges));
    }
}
