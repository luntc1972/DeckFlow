using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Detailed measured-style build result carrying the persisted profile together with the
/// derivation inputs an admin surface can report on.
/// </summary>
public sealed record MeasuredStyleBuildResult
{
    /// <summary>The persisted measured creator style profile.</summary>
    public required CreatorStyleProfile Profile { get; init; }

    /// <summary>
    /// Creator deck samples after oversized-deck filtering, before near-precon flagging or staple
    /// stripping — the decks as crawled, suitable for a report describing what was actually pulled.
    /// </summary>
    public required IReadOnlyList<CreatorDeckSample> Samples { get; init; }

    /// <summary>Resolved multi-bucket category map keyed by card name.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> CardCategories { get; init; }

    /// <summary>Global category baseline used to derive this build's lift metrics.</summary>
    public required GlobalCategoryBaseline Baseline { get; init; }
}
