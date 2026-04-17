using DeckFlow.Core.Diffing;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

/// <summary>
/// Defines the deck synchronization service used by the web UI.
/// </summary>
public interface IDeckSyncService
{
    /// <summary>
    /// Compares two decks according to the provided request.
    /// </summary>
    /// <param name="request">Deck diff request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DeckSyncResult> CompareDecksAsync(DeckDiffRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Carries the loaded deck entries alongside the generated diff.
/// </summary>
public sealed record DeckSyncResult(DeckDiff Diff, LoadedDecks LoadedDecks);

/// <summary>
/// Loads deck inputs from either site, validates Commander deck size, and produces compare results.
/// </summary>
public sealed class DeckSyncService : IDeckSyncService
{
    private readonly IDeckEntryLoader _deckEntryLoader;

    /// <summary>
    /// Creates a new instance that relies on the shared deck loader.
    /// </summary>
    /// <param name="deckEntryLoader">Shared loader used to parse and validate decks.</param>
    public DeckSyncService(IDeckEntryLoader deckEntryLoader)
    {
        _deckEntryLoader = deckEntryLoader;
    }

    /// <summary>
    /// Compares the two decks based on the supplied request.
    /// </summary>
    /// <param name="request">Deck diff request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DeckSyncResult> CompareDecksAsync(DeckDiffRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loadedDecks = new LoadedDecks(
            await LoadLeftEntriesAsync(request, cancellationToken).ConfigureAwait(false),
            await LoadRightEntriesAsync(request, cancellationToken).ConfigureAwait(false));

        _deckEntryLoader.ValidateCommanderDeckSize(DeckSyncSupport.GetLeftPanelSystem(request.Direction), loadedDecks.MoxfieldEntries);
        _deckEntryLoader.ValidateCommanderDeckSize(DeckSyncSupport.GetRightPanelSystem(request.Direction), loadedDecks.ArchidektEntries);

        var diff = new DiffEngine(request.Mode).Compare(
            DeckSyncSupport.GetSourceEntries(request.Direction, loadedDecks),
            DeckSyncSupport.GetTargetEntries(request.Direction, loadedDecks));

        return new DeckSyncResult(diff, loadedDecks);
    }

    /// <summary>
    /// Loads Moxfield entries by parsing text or calling the API.
    /// </summary>
    /// <param name="request">Request containing inputs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private Task<List<DeckEntry>> LoadLeftEntriesAsync(DeckDiffRequest request, CancellationToken cancellationToken)
    {
        var systemName = DeckSyncSupport.GetLeftPanelSystem(request.Direction);
        return _deckEntryLoader.LoadAsync(
            new DeckLoadRequest(
                GetPlatform(systemName),
                request.MoxfieldInputSource == DeckInputSource.PublicUrl ? DeckInputKind.PublicUrl : DeckInputKind.PastedText,
                request.MoxfieldInputSource == DeckInputSource.PublicUrl ? request.MoxfieldUrl ?? string.Empty : request.MoxfieldText ?? string.Empty,
                ExcludeMaybeboard: string.Equals(systemName, "Moxfield", StringComparison.OrdinalIgnoreCase)),
            cancellationToken);
    }

    /// <summary>
    /// Loads Archidekt entries either via API or text parsing.
    /// </summary>
    /// <param name="request">Request containing inputs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private Task<List<DeckEntry>> LoadRightEntriesAsync(DeckDiffRequest request, CancellationToken cancellationToken)
    {
        var systemName = DeckSyncSupport.GetRightPanelSystem(request.Direction);
        return _deckEntryLoader.LoadAsync(
            new DeckLoadRequest(
                GetPlatform(systemName),
                request.ArchidektInputSource == DeckInputSource.PublicUrl ? DeckInputKind.PublicUrl : DeckInputKind.PastedText,
                request.ArchidektInputSource == DeckInputSource.PublicUrl ? request.ArchidektUrl ?? string.Empty : request.ArchidektText ?? string.Empty,
                ExcludeMaybeboard: string.Equals(systemName, "Moxfield", StringComparison.OrdinalIgnoreCase)),
            cancellationToken);
    }

    /// <summary>
    /// Maps a display system name to the shared deck platform enum.
    /// </summary>
    /// <param name="systemName">Display name from the compare workflow.</param>
    /// <returns>The corresponding deck platform.</returns>
    private static DeckPlatform GetPlatform(string systemName)
    {
        return string.Equals(systemName, "Archidekt", StringComparison.OrdinalIgnoreCase)
            ? DeckPlatform.Archidekt
            : DeckPlatform.Moxfield;
    }
}
