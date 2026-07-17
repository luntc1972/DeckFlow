using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckFlow.Core.History;

/// <summary>
/// Root of a user-owned deck version-history file (format "deckflow-history").
/// Snapshot-per-version: every entry in <see cref="Versions"/> carries the complete decklist.
/// </summary>
public sealed record DeckHistoryFile
{
    /// <summary>Format marker; must equal <see cref="DeckHistorySerializer.FormatMarker"/>.</summary>
    public string Format { get; init; } = DeckHistorySerializer.FormatMarker;

    /// <summary>Schema version as "major.minor". Minor bumps are additive-only.</summary>
    public string FormatVersion { get; init; } = DeckHistorySerializer.CurrentFormatVersion;

    /// <summary>Display name of the deck this history tracks.</summary>
    public string DeckName { get; init; } = string.Empty;

    /// <summary>Optional origin of the deck (site + URL).</summary>
    public DeckHistorySource? Source { get; init; }

    /// <summary>Append-ordered snapshots, oldest first.</summary>
    public IReadOnlyList<DeckSnapshot> Versions { get; init; } = [];

    /// <summary>Round-trips fields written by newer DeckFlow versions so re-saving never drops them.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
