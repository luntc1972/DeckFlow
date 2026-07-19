using DeckFlow.Web.Services.CreatorStyle;

namespace DeckFlow.Web.Models;

/// <summary>
/// View model for the creator-style critique page.
/// </summary>
public sealed class CreatorStyleViewModel
{
    /// <summary>
    /// Server-populated creator picker option.
    /// </summary>
    public sealed record CreatorPickerOption
    {
        /// <summary>
        /// Gets the creator slug posted back to the server.
        /// </summary>
        public required string Slug { get; init; }

        /// <summary>
        /// Gets the human-readable creator label with evidence depth.
        /// </summary>
        public required string DisplayLabel { get; init; }
    }

    /// <summary>
    /// Gets the active deck-tool tab.
    /// </summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.CreatorStyle;

    /// <summary>
    /// Gets the posted request so the form re-renders with the prior values.
    /// </summary>
    public CreatorStyleRequest Request { get; init; } = new();

    /// <summary>
    /// Gets the user-facing error message when the request fails.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the computed creator-style packet result.
    /// </summary>
    public CreatorStylePacketResult? Result { get; init; }

    /// <summary>
    /// Gets the available creators for the native picker.
    /// </summary>
    public IReadOnlyList<CreatorPickerOption> AvailableCreators { get; init; } = Array.Empty<CreatorPickerOption>();

    /// <summary>
    /// Gets a value indicating whether the creator-profile store is empty.
    /// </summary>
    public bool NoProfilesLoaded { get; init; }

    /// <summary>
    /// Gets a value indicating whether the result packet block should render.
    /// </summary>
    public bool HasResult => Result is not null && !Result.ProfileUnavailable;
}
