using DeckFlow.Core.Models;

namespace DeckFlow.Web.Models;

/// <summary>Request model bound from the deck sync comparison form.</summary>
public sealed class DeckDiffRequest
{
    /// <summary>Direction that determines which deck is the source and target.</summary>
    public SyncDirection Direction { get; set; } = SyncDirection.MoxfieldToArchidekt;

    /// <summary>How the Moxfield deck input was supplied.</summary>
    public DeckInputSource MoxfieldInputSource { get; set; } = DeckInputSource.PasteText;

    /// <summary>Moxfield deck URL submitted by the user.</summary>
    public string MoxfieldUrl { get; set; } = string.Empty;

    /// <summary>Raw Moxfield-format deck text submitted by the user.</summary>
    public string MoxfieldText { get; set; } = string.Empty;

    /// <summary>How the Archidekt deck input was supplied.</summary>
    public DeckInputSource ArchidektInputSource { get; set; } = DeckInputSource.PasteText;

    /// <summary>Archidekt deck URL submitted by the user.</summary>
    public string ArchidektUrl { get; set; } = string.Empty;

    /// <summary>Raw Archidekt-format deck text submitted by the user.</summary>
    public string ArchidektText { get; set; } = string.Empty;

    /// <summary>Matching mode used when comparing cards across the two decks.</summary>
    public MatchMode Mode { get; set; } = MatchMode.Loose;

    /// <summary>
    /// Controls how category/tag data is used when producing exports.
    /// </summary>
    public CategorySyncMode CategorySyncMode { get; set; } = CategorySyncMode.TargetCategories;

    /// <summary>User-selected printing conflict resolutions keyed by card identity.</summary>
    public Dictionary<string, PrintingChoice> Resolutions { get; set; } = new(StringComparer.Ordinal);
}
