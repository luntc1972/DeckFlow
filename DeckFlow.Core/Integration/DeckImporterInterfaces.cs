using DeckFlow.Core.Models;

namespace DeckFlow.Core.Integration;

public enum MoxfieldImportSource
{
    Direct,
    CommanderSpellbookFallback
}

public sealed record MoxfieldImportResult(
    List<DeckEntry> Entries,
    MoxfieldImportSource Source,
    string? FallbackNotice = null);

public interface IMoxfieldDeckImporter
{
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

public interface IArchidektDeckImporter
{
    Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default);
}
