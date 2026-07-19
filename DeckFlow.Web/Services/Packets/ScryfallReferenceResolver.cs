using System.Net;
using DeckFlow.Web.Services.Scryfall;
using RestSharp;

namespace DeckFlow.Web.Services.Packets;

/// <summary>
/// Raised when the shared <c>cards/collection</c> batch call itself returns a non-success status or a
/// null payload. This is intentionally a DISTINCT type from a plain <see cref="HttpRequestException"/>
/// thrown by a caller's per-name fallback delegate: consuming services re-label ONLY the
/// collection-call failure with their own deck/tool-specific message, and let a fallback-search
/// failure propagate with its ORIGINAL message so it keeps routing through
/// <c>UpstreamErrorMessageBuilder</c> exactly as it did pre-Phase-83 (WR-01). Because it derives from
/// <see cref="HttpRequestException"/>, existing broad <c>catch (HttpRequestException)</c> handlers
/// (e.g. the controllers) still catch it unchanged.
/// </summary>
internal sealed class ScryfallReferenceCollectionException : HttpRequestException
{
    /// <summary>Creates the exception, preserving the upstream status code.</summary>
    public ScryfallReferenceCollectionException(string message, HttpStatusCode? statusCode)
        : base(message, inner: null, statusCode)
    {
    }
}

/// <summary>
/// One resolved Scryfall reference: the caller's ORIGINAL request name (never the normalized
/// submission, never the returned card's own name), the resolved card, and whether the resolution
/// came from the per-caller fallback strategy rather than a direct collection hit.
/// </summary>
internal sealed record ScryfallReferenceResolution(string RequestName, ScryfallCard Card, bool FromFallback);

/// <summary>
/// Result of a batch resolution: each resolved reference in original request order, plus an oracle
/// name map keyed by the original request name -> the returned card's own <c>Name</c>.
/// </summary>
internal sealed record ScryfallBatchResolution(
    IReadOnlyList<ScryfallReferenceResolution> Resolutions,
    IReadOnlyDictionary<string, string> OracleNameMap);

/// <summary>
/// Shared Scryfall reference-RESOLUTION collaborator (Cluster A / PKTSVC-02 from Phase 83
/// research). Wraps the already-registered <see cref="IScryfallCardResolver"/> with the
/// batch-chunk(75) -&gt; cards/collection -&gt; validate -&gt; match-back -&gt; per-miss-fallback loop
/// mechanically shared by <c>DeckAnalysisPacketService.LookupCardReferencesAsync</c>,
/// <c>DeckComparisonService.LookupCardDetailsAsync</c>, and
/// <c>MetaGapService.ResolveOracleNameMapAsync</c>. It does NOT construct an <c>HttpClient</c> or a
/// Polly pipeline directly -- every upstream call is routed through the injected
/// <see cref="IScryfallCardResolver"/> (which already owns RestSharp/Polly/<c>ScryfallThrottle</c>).
/// </summary>
/// <remarks>
/// LOAD-BEARING behaviors preserved from the three current implementations (do not "fix"):
/// <list type="number">
/// <item>Results are keyed by the caller's ORIGINAL request name, not the normalized submission and
/// not the returned card's <c>Name</c>.</item>
/// <item>Collection hits are matched back by comparing the ORIGINAL request name to the RETURNED
/// card's <c>Name</c> (Ordinal-IgnoreCase). When <c>normalizeForScryfall</c>-equivalent
/// normalization is ON, it affects ONLY the submitted identifier -- never the match key -- so a
/// single-slash Archidekt name (<c>"A / B"</c>) that normalizes to <c>"A // B"</c> on submission and
/// gets returned as <c>"A // B"</c> will NOT match its original <c>"A / B"</c> request and falls
/// through to the fallback strategy, exactly as today.</item>
/// <item>The fallback strategy is a required delegate parameter -- Analysis's
/// <c>SearchPrintingFallbackCardAsync</c> and Comparison/MetaGap's <c>SearchFallbackCardAsync</c>
/// are intentionally different and neither is hardcoded here.</item>
/// <item>The non-2xx / null-<c>Data</c> collection response throws the same
/// <see cref="HttpRequestException"/> shape (upstream status preserved) as today -- relaxed for
/// no caller.</item>
/// </list>
/// This collaborator ends at "resolved references (original name + card + fallback flag) + oracle
/// name map" -- mechanic-name extraction, <c>CardReference</c> construction, the Analysis
/// <c>displayName</c> logic, and stat-input mapping stay in each consuming service.
/// </remarks>
internal sealed class ScryfallReferenceResolver
{
    private readonly IScryfallCardResolver _scryfallCardResolver;

    public ScryfallReferenceResolver(IScryfallCardResolver scryfallCardResolver)
    {
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);
        _scryfallCardResolver = scryfallCardResolver;
    }

    /// <summary>
    /// Resolves a batch of card names: chunks into batches of <c>75</c>, submits
    /// <c>cards/collection</c> (identifiers = <see cref="ScryfallCardResolver.NormalizeForScryfall"/>
    /// of each name when <paramref name="normalizeForScryfall"/> is <see langword="true"/>, else the
    /// raw name), validates 2xx + non-null <c>Data</c>, matches returned cards back to the ORIGINAL
    /// request name, and dispatches <paramref name="fallbackStrategy"/> for each still-unresolved
    /// original name.
    /// </summary>
    /// <param name="requestNames">Original request names, already de-duplicated/ordered by the caller.</param>
    /// <param name="fallbackStrategy">Per-caller miss-handling strategy (e.g. printed-name-fallback vs exact-name-fallback).</param>
    /// <param name="normalizeForScryfall">When <see langword="true"/>, submits <see cref="ScryfallCardResolver.NormalizeForScryfall"/>(name) instead of the raw name. Never affects the match key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task<ScryfallBatchResolution> ResolveBatchAsync(
        IReadOnlyList<string> requestNames,
        Func<string, CancellationToken, Task<ScryfallCard?>> fallbackStrategy,
        bool normalizeForScryfall = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestNames);
        ArgumentNullException.ThrowIfNull(fallbackStrategy);

        if (requestNames.Count == 0)
        {
            return new ScryfallBatchResolution(
                Array.Empty<ScryfallReferenceResolution>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var resolved = new Dictionary<string, ScryfallReferenceResolution>(StringComparer.OrdinalIgnoreCase);
        var oracleNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in Chunk(requestNames, ScryfallLimits.CollectionBatchSize))
        {
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new
            {
                identifiers = chunk
                    .Select(name => new { name = normalizeForScryfall ? ScryfallCardResolver.NormalizeForScryfall(name) : name })
                    .ToArray()
            });

            var response = await _scryfallCardResolver.ExecuteCollectionAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                throw new ScryfallReferenceCollectionException(
                    $"Scryfall card reference lookup (cards/collection) returned HTTP {(int)response.StatusCode}.",
                    response.StatusCode);
            }

            foreach (var card in response.Data.Data)
            {
                var matchingName = chunk.FirstOrDefault(name => string.Equals(name, card.Name, StringComparison.OrdinalIgnoreCase));
                if (matchingName is null)
                {
                    continue;
                }

                oracleNameMap[matchingName] = card.Name;
                resolved[matchingName] = new ScryfallReferenceResolution(matchingName, card, FromFallback: false);
            }

            foreach (var unresolvedName in chunk.Where(name => !resolved.ContainsKey(name)))
            {
                var fallbackCard = await fallbackStrategy(unresolvedName, cancellationToken).ConfigureAwait(false);
                if (fallbackCard is null)
                {
                    continue;
                }

                oracleNameMap[unresolvedName] = fallbackCard.Name;
                resolved[unresolvedName] = new ScryfallReferenceResolution(unresolvedName, fallbackCard, FromFallback: true);
            }
        }

        var orderedResolutions = requestNames
            .Where(resolved.ContainsKey)
            .Select(name => resolved[name])
            .ToList();

        return new ScryfallBatchResolution(orderedResolutions, oracleNameMap);
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
}
