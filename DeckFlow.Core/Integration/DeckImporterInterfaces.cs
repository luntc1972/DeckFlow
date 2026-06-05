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
    string? FallbackNotice = null);

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
}
