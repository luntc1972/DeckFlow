using System.Net;
using System.Text.RegularExpressions;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Services.Scryfall;
using RestSharp;
using CoreScryfallCollectionIdentifier = DeckFlow.Core.Normalization.ScryfallCollectionIdentifier;

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
/// <item>After the raw match-back above, any request names it left unmatched get ONE additional,
/// punctuation-tolerant, slash-preserving pass over the SAME already-received response (zero extra
/// Scryfall calls), matching only keys that are unambiguous on both sides. This pass is additive:
/// it never overrides a raw-pass result and never suppresses the fallback for a name it does not
/// confidently match. See <c>docs/decisions/0004-scryfall-batch-match-key-asymmetry.md</c>.</item>
/// </list>
/// This collaborator ends at "resolved references (original name + card + fallback flag) + oracle
/// name map" -- mechanic-name extraction, <c>CardReference</c> construction, the Analysis
/// <c>displayName</c> logic, and stat-input mapping stay in each consuming service.
/// </remarks>
internal sealed partial class ScryfallReferenceResolver
{
    private const int ScryfallBatchSize = 75;

    private readonly IScryfallCardResolver _scryfallCardResolver;

    public ScryfallReferenceResolver(IScryfallCardResolver scryfallCardResolver)
    {
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);
        _scryfallCardResolver = scryfallCardResolver;
    }

    /// <summary>
    /// Resolves a batch of card names: chunks into batches of <c>75</c>, submits
    /// <c>cards/collection</c> with single-face identifiers from
    /// <see cref="CoreScryfallCollectionIdentifier.ToFaceIdentifier(string)"/>, validates 2xx + non-null
    /// <c>Data</c>, matches returned cards back to the ORIGINAL request name, and dispatches
    /// <paramref name="fallbackStrategy"/> for each still-unresolved original name.
    /// </summary>
    /// <param name="requestNames">Original request names, already de-duplicated/ordered by the caller.</param>
    /// <param name="fallbackStrategy">Per-caller miss-handling strategy (e.g. printed-name-fallback vs exact-name-fallback).</param>
    /// <param name="normalizeForScryfall">Retained for caller compatibility; collection identifiers always use single-face names. Never affects the match key.</param>
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

        foreach (var chunk in Chunk(requestNames, ScryfallBatchSize))
        {
            string[] chunkIdentifiers = chunk
                .Select(CoreScryfallCollectionIdentifier.ToFaceIdentifier)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var request = new RestRequest("cards/collection", Method.Post);
            // Why: Scryfall cards/collection name identifiers match a single face name; combined A // B returns not_found.
            request.AddJsonBody(new
            {
                identifiers = chunkIdentifiers
                    .Select(name => new { name })
                    .ToArray()
            });

            var response = await _scryfallCardResolver.ExecuteCollectionAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                throw new ScryfallReferenceCollectionException(
                    $"Scryfall card reference lookup (cards/collection) returned HTTP {(int)response.StatusCode}.",
                    response.StatusCode);
            }

            // Why: one pass records the exact matches and collects the leftovers the tolerant pass
            // below needs, instead of scanning the response twice. TryGetValue recovers the caller's
            // original spelling, which is the only reason the exact match needs a lookup at all.
            var chunkNames = new HashSet<string>(chunk, StringComparer.OrdinalIgnoreCase);
            var unclaimedCards = new List<ScryfallCard>();
            foreach (var card in response.Data.Data)
            {
                if (!chunkNames.TryGetValue(card.Name, out var matchingName))
                {
                    unclaimedCards.Add(card);
                    continue;
                }

                oracleNameMap[matchingName] = card.Name;
                resolved[matchingName] = new ScryfallReferenceResolution(matchingName, card, FromFallback: false);
            }

            // Second pass (ADR 0004): punctuation-tolerant, slash-preserving match over the SAME
            // response, for names the raw pass above left unmatched. Zero additional Scryfall calls.
            // Why both sides are scoped to THIS chunk: filtering cards against the run-wide
            // oracleNameMap would hide a card from a later chunk merely because an earlier chunk had
            // already claimed that name, costing a needless fallback search for the drifted spelling.
            var unmatchedNames = chunk.Where(name => !resolved.ContainsKey(name)).ToList();
            if (unmatchedNames.Count > 0)
            {
                // Why: a key may match ONLY when it is unambiguous on BOTH sides. If two distinct
                // names collapse to one key, neither is matched rather than one matched arbitrarily.
                var unambiguousCardByKey = unclaimedCards
                    .GroupBy(card => BatchMatchKey(card.Name))
                    .Where(group => group.Count() == 1)
                    .ToDictionary(group => group.Key, group => group.First());

                foreach (var nameGroup in unmatchedNames.GroupBy(BatchMatchKey))
                {
                    if (nameGroup.Count() != 1
                        || !unambiguousCardByKey.TryGetValue(nameGroup.Key, out var card))
                    {
                        continue;
                    }

                    var requestName = nameGroup.First();
                    oracleNameMap[requestName] = card.Name;
                    resolved[requestName] = new ScryfallReferenceResolution(requestName, card, FromFallback: false);
                }
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

    /// <summary>
    /// Match key for the second pass (ADR 0004 --
    /// <c>docs/decisions/0004-scryfall-batch-match-key-asymmetry.md</c>): trims, lowercases,
    /// DELETES (never space-replaces) punctuation so <c>"Smuggler's Copter"</c> keys identically to
    /// <c>"Smugglers Copter"</c> -- space-replacing (as <c>CardNormalizer</c> does) would leave
    /// <c>"smuggler s copter"</c> vs <c>"smugglers copter"</c>, still distinct. <c>/</c> is
    /// deliberately PRESERVED (not deleted, not collapsed with other punctuation) so a single-slash
    /// DFC name (<c>"a / b"</c>) and its double-slash card (<c>"a // b"</c>) remain distinct keys and
    /// the DFC fallback path is unchanged. <c>CardNormalizer.Normalize</c> is deliberately NOT reused
    /// here: it truncates at the first DFC separator, so both slash forms would collapse to the same
    /// key and the H2 lock test would break.
    /// </summary>
    private static string BatchMatchKey(string name)
    {
        var key = name.Trim().ToLowerInvariant();
        key = BatchMatchKeyPunctuationRegex().Replace(key, string.Empty);
        key = BatchMatchKeyMultiSpaceRegex().Replace(key, " ").Trim();
        return key;
    }

    [GeneratedRegex(@"[^\p{L}\p{N}\s/]", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex BatchMatchKeyPunctuationRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex BatchMatchKeyMultiSpaceRegex();
}
