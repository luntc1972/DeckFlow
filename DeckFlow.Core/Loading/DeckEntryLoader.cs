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
    /// Ensures a Commander deck contains the expected playable card count.
    /// </summary>
    /// <param name="systemName">Display name used in validation messages.</param>
    /// <param name="entries">Deck entries to validate.</param>
    /// <param name="requiredDeckSize">Expected Commander deck size.</param>
    void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100);
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
