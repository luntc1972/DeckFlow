using DeckFlow.Core.Filtering;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;

namespace DeckFlow.Core.Loading;

/// <summary>
/// Loads deck entries from the supported platforms and validates Commander deck size.
/// </summary>
public interface IDeckEntryLoader
{
    /// <summary>
    /// Loads a deck using the supplied request.
    /// </summary>
    /// <param name="request">Deck loading request to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed or imported deck entries.</returns>
    Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a deck from a URL or pasted export text by auto-detecting the source platform.
    /// </summary>
    /// <param name="deckSource">Deck URL or pasted export text.</param>
    /// <param name="unrecognizedBehavior">How to surface an unrecognized pasted deck after both parsers are attempted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded deck entries and optional import notice.</returns>
    Task<DeckSourceLoadResult> LoadFromSourceAsync(
        string deckSource,
        UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a Commander deck contains the expected playable card count.
    /// </summary>
    /// <param name="systemName">Display name used in validation messages.</param>
    /// <param name="entries">Deck entries to validate.</param>
    /// <param name="requiredDeckSize">Expected Commander deck size.</param>
    void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100);
}

/// <summary>
/// Carries source-loaded deck entries alongside any optional import notice.
/// </summary>
public sealed record DeckSourceLoadResult(List<DeckEntry> Entries, string? FallbackNotice);

/// <summary>
/// Controls how the loader surfaces unrecognized pasted deck text after trying both parsers.
/// </summary>
public enum UnrecognizedPasteBehavior
{
    /// <summary>
    /// Swallow both parse failures and throw the generic not-recognized error.
    /// </summary>
    ThrowNotRecognized,

    /// <summary>
    /// Let the second parser's <see cref="DeckParseException"/> propagate.
    /// </summary>
    PropagateParseException
}

/// <summary>
/// Default implementation of <see cref="IDeckEntryLoader"/>.
/// </summary>
public sealed class DeckEntryLoader : IDeckEntryLoader
{
    private readonly IMoxfieldDeckImporter _moxfieldDeckImporter;
    private readonly IArchidektDeckImporter _archidektDeckImporter;
    private readonly MoxfieldParser _moxfieldParser;
    private readonly ArchidektParser _archidektParser;

    /// <summary>
    /// Creates a new loader with the importers and parsers needed for deck loading.
    /// </summary>
    /// <param name="moxfieldDeckImporter">Moxfield importer.</param>
    /// <param name="archidektDeckImporter">Archidekt importer.</param>
    /// <param name="moxfieldParser">Moxfield text parser.</param>
    /// <param name="archidektParser">Archidekt text parser.</param>
    public DeckEntryLoader(
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser)
    {
        _moxfieldDeckImporter = moxfieldDeckImporter;
        _archidektDeckImporter = archidektDeckImporter;
        _moxfieldParser = moxfieldParser;
        _archidektParser = archidektParser;
    }

    /// <inheritdoc />
    public async Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entries = request.Platform switch
        {
            DeckPlatform.Moxfield => await LoadMoxfieldAsync(request, cancellationToken).ConfigureAwait(false),
            DeckPlatform.Archidekt => await LoadArchidektAsync(request, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported deck platform: {request.Platform}."),
        };

        if (request.ExcludeMaybeboard && request.Platform == DeckPlatform.Moxfield)
        {
            return DeckEntryFilter.ExcludeMaybeboard(entries);
        }

        return entries;
    }

    /// <inheritdoc />
    public async Task<DeckSourceLoadResult> LoadFromSourceAsync(
        string deckSource,
        UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
        CancellationToken cancellationToken = default)
    {
        var normalizedDeckSource = deckSource.Trim();
        if (Uri.TryCreate(normalizedDeckSource, UriKind.Absolute, out var uri))
        {
            if (uri.Host.Contains("moxfield.com", StringComparison.OrdinalIgnoreCase))
            {
                var result = await _moxfieldDeckImporter.ImportWithSourceAsync(deckSource, cancellationToken).ConfigureAwait(false);
                return new DeckSourceLoadResult(result.Entries.ToList(), result.FallbackNotice);
            }

            if (uri.Host.Contains("archidekt.com", StringComparison.OrdinalIgnoreCase))
            {
                var entries = await _archidektDeckImporter.ImportAsync(deckSource, cancellationToken).ConfigureAwait(false);
                return new DeckSourceLoadResult(entries, null);
            }
        }

        try
        {
            return new DeckSourceLoadResult(_moxfieldParser.ParseText(deckSource), null);
        }
        catch (DeckParseException)
        {
        }

        if (unrecognizedBehavior == UnrecognizedPasteBehavior.PropagateParseException)
        {
            return new DeckSourceLoadResult(_archidektParser.ParseText(deckSource), null);
        }

        try
        {
            return new DeckSourceLoadResult(_archidektParser.ParseText(deckSource), null);
        }
        catch (DeckParseException)
        {
        }

        throw new InvalidOperationException("The submitted deck was not recognized as a Moxfield URL, Archidekt URL, Moxfield export, or Archidekt export.");
    }

    /// <inheritdoc />
    public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100)
    {
        var count = entries
            .Where(entry => !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Quantity);

        if (count != requiredDeckSize)
        {
            throw new InvalidOperationException($"{systemName} deck must contain exactly {requiredDeckSize} cards across commander and mainboard. Found {count}.");
        }
    }

    /// <summary>
    /// Loads a Moxfield deck using the request settings.
    /// </summary>
    /// <param name="request">Deck loading request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded deck entries.</returns>
    private async Task<List<DeckEntry>> LoadMoxfieldAsync(DeckLoadRequest request, CancellationToken cancellationToken)
    {
        return request.InputKind switch
        {
            DeckInputKind.PublicUrl => await _moxfieldDeckImporter.ImportAsync(request.InputValue, cancellationToken).ConfigureAwait(false),
            DeckInputKind.PastedText => _moxfieldParser.ParseText(request.InputValue),
            DeckInputKind.FilePath => _moxfieldParser.ParseFile(request.InputValue),
            _ => throw new InvalidOperationException($"Unsupported deck input kind: {request.InputKind}."),
        };
    }

    /// <summary>
    /// Loads an Archidekt deck using the request settings.
    /// </summary>
    /// <param name="request">Deck loading request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded deck entries.</returns>
    private async Task<List<DeckEntry>> LoadArchidektAsync(DeckLoadRequest request, CancellationToken cancellationToken)
    {
        return request.InputKind switch
        {
            DeckInputKind.PublicUrl => await _archidektDeckImporter.ImportAsync(request.InputValue, cancellationToken).ConfigureAwait(false),
            DeckInputKind.PastedText => _archidektParser.ParseText(request.InputValue),
            DeckInputKind.FilePath => _archidektParser.ParseFile(request.InputValue),
            _ => throw new InvalidOperationException($"Unsupported deck input kind: {request.InputKind}."),
        };
    }
}
