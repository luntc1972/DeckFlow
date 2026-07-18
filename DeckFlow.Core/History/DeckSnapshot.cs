using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckFlow.Core.History;

/// <summary>One dated, complete snapshot of the deck plus the user's note for the change.</summary>
public sealed record DeckSnapshot
{
    /// <summary>Monotonically increasing version id assigned by DeckFlow (repaired on upload if hand-edited).</summary>
    public int Id { get; init; }

    /// <summary>UTC timestamp assigned when the snapshot was appended.</summary>
    public DateTimeOffset Date { get; init; }

    /// <summary>Optional short user label, e.g. "post-ban".</summary>
    public string? Label { get; init; }

    /// <summary>Free-text note explaining why this version changed. May be hand-edited later.</summary>
    public string? Notes { get; init; }

    /// <summary>Commander card name(s).</summary>
    public IReadOnlyList<string> Commander { get; init; } = [];

    /// <summary>The authoritative full mainboard snapshot.</summary>
    public IReadOnlyList<SnapshotCard> Cards { get; init; } = [];

    /// <summary>Derived changes vs the previous version. Recomputed on every upload; never trusted from the file.</summary>
    public SnapshotDelta? Delta { get; init; }

    /// <summary>Round-trips fields written by newer DeckFlow versions.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
