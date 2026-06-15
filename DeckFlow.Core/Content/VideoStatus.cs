namespace DeckFlow.Core.Content;

/// <summary>
/// Badge state for a YouTube video as shown in the Harvest page status column.
/// Members map one-to-one to the UI-SPEC badge vocabulary (45-UI-SPEC.md lines 110-126).
/// </summary>
public enum VideoStatus
{
    /// <summary>The video has not been harvested into any enabled source.</summary>
    NotHarvested,

    /// <summary>The video has been harvested and exists in at least one enabled source, but has not been distilled.</summary>
    Harvested,

    /// <summary>The video has been distilled; a content_site_index row exists for it. Implies harvested.</summary>
    Distilled,

    /// <summary>The video is blocked and will be skipped on future harvest runs.</summary>
    Blocked,

    /// <summary>The video is a duplicate of an already-harvested or already-distilled entry (UI-layer signal).</summary>
    Duplicate,
}
