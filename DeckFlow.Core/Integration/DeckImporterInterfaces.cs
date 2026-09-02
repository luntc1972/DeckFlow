using DeckFlow.Core.Models;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Indicates whether Moxfield entries were fetched directly or via the Commander Spellbook fallback proxy.
/// </summary>
public enum MoxfieldImportSource
{
    /// <summary>Entries came directly from Moxfield.</summary>
    Direct,
    /// <summary>Entries came from the Commander Spellbook fallback proxy.</summary>
    CommanderSpellbookFallback
}

/// <summary>
/// Wraps the imported deck entries with metadata about the import source and any user-visible notice.
/// </summary>
public sealed record MoxfieldImportResult(
    List<DeckEntry> Entries,
    MoxfieldImportSource Source,
    string? FallbackNotice = null,
    string? DetectedCompanionName = null);

/// <summary>
/// Imports a Moxfield deck by URL or deck identifier.
/// </summary>
public interface IMoxfieldDeckImporter
{
    /// <summary>
    /// Imports a Moxfield deck by URL or deck identifier.
    /// </summary>
    /// <param name="urlOrDeckId">Moxfield deck URL or deck identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported deck entries.</returns>
    Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Like ImportAsync but also reports whether the entries came from the direct Moxfield API or
    /// the Commander Spellbook fallback proxy (used when Moxfield's Cloudflare edge blocks the
    /// request — typical when running on cloud-hosted IPs). Callers that surface user warnings
    /// should use this overload. Default implementation wraps ImportAsync and reports Direct.
    /// </summary>
    async Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
    {
        var entries = await ImportAsync(urlOrDeckId, cancellationToken).ConfigureAwait(false);
        return new MoxfieldImportResult(entries, MoxfieldImportSource.Direct);
    }
}

/// <summary>
/// Curated top-level Archidekt deck metadata captured from the same payload request that
/// already returns cards[]. Malformed or missing source fields become null; CapturedUtc is
/// always set when a recognizable Archidekt deck payload was parsed.
/// </summary>
public sealed record ArchidektDeckMetadata(
    int? EdhBracket,
    int? DeckFormat,
    bool? Theorycrafted,
    DateTimeOffset? CreatedUtc,
    DateTimeOffset? UpdatedUtc,
    DateTimeOffset CapturedUtc);

/// <summary>
/// Wraps the imported deck entries with optional deck-level Archidekt metadata. Metadata is
/// null when the payload was not recognizable as an Archidekt deck payload — that is how
/// "nothing was captured" is distinguished from "captured, but all curated fields absent".
/// </summary>
public sealed record ArchidektDeckImportResult(List<DeckEntry> Entries, ArchidektDeckMetadata? Metadata);

/// <summary>
/// Imports an Archidekt deck by URL or deck identifier.
/// </summary>
public interface IArchidektDeckImporter
{
    /// <summary>
    /// Imports an Archidekt deck by URL or deck identifier.
    /// </summary>
    /// <param name="urlOrDeckId">Archidekt deck URL or deck identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported deck entries.</returns>
    Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Like ImportAsync but also captures curated deck-level metadata parsed from the same
    /// payload request (no extra HTTP request). Default implementation is intentionally
    /// unsupported — real support is provided only by ArchidektApiDeckImporter, so that
    /// no implementer can fabricate a CapturedUtc for a payload it never actually parsed.
    /// </summary>
    Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{GetType().Name} does not support {nameof(ImportWithMetadataAsync)}.");
}
