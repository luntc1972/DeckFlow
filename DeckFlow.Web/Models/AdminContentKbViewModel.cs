using DeckFlow.Core.Content;

namespace DeckFlow.Web.Models;

/// <summary>
/// View model for the /Admin/ContentKb curation page (Phase 22, KB-09). Carries the index
/// status panel, the per-source bulk groups, and the per-entry curation rows over ALL index
/// rows (published, unpublished, and hidden).
/// </summary>
public sealed class AdminContentKbViewModel
{
    /// <summary>Index status summary (counts, generation timestamp, flag state).</summary>
    public required KbIndexStatus Status { get; init; }

    /// <summary>Per-source groups for the bulk publish/hide controls.</summary>
    public IReadOnlyList<KbSourceGroup> Sources { get; init; } = Array.Empty<KbSourceGroup>();

    /// <summary>All index entries (published + hidden) for the per-entry grid.</summary>
    public IReadOnlyList<KbEntryRow> Entries { get; init; } = Array.Empty<KbEntryRow>();

    /// <summary>Normalized entry visibility filter applied to the entries table.</summary>
    public string VisibilityFilter { get; init; } = "all";

    /// <summary>Success banner text from TempData after a mutating action, or <see langword="null"/>.</summary>
    public string? SuccessBanner { get; init; }
}

/// <summary>
/// Index status summary shown in the admin status panel. <see cref="IndexGeneratedUtc"/> is the
/// index-GENERATION time (max indexed_utc across rows), honestly labeled "Index generated" — NOT
/// a seed-reload time (D-22D).
/// </summary>
public sealed record KbIndexStatus
{
    /// <summary>Total index rows (published + unpublished + hidden).</summary>
    public required int TotalCount { get; init; }

    /// <summary>Count of rows currently published (is_visible = true).</summary>
    public required int PublishedCount { get; init; }

    /// <summary>Count of rows currently unpublished (is_visible = false AND is_hidden = false).</summary>
    public required int UnpublishedCount { get; init; }

    /// <summary>Count of rows currently hidden (is_hidden = true).</summary>
    public required int HiddenCount { get; init; }

    /// <summary>Count of distinct sources represented in the index.</summary>
    public required int SourceCount { get; init; }

    /// <summary>
    /// Max indexed_utc across all rows — the index-generation time (D-22D honest label),
    /// or <see langword="null"/> when there are no rows.
    /// </summary>
    public DateTimeOffset? IndexGeneratedUtc { get; init; }

    /// <summary>Current state of the tool.knowledge-base.enabled feature flag.</summary>
    public required bool FlagEnabled { get; init; }
}

/// <summary>A source group for the per-source bulk publish/hide controls.</summary>
/// <param name="Source">Source display name / slug used as the bulk key.</param>
/// <param name="EntryCount">Number of index entries belonging to this source.</param>
public sealed record KbSourceGroup(string Source, int EntryCount);

/// <summary>A single index entry row in the per-entry curation grid.</summary>
public sealed record KbEntryRow
{
    /// <summary>Surrogate row id (the SetVisibility key).</summary>
    public required long Id { get; init; }

    /// <summary>Entry title.</summary>
    public required string Title { get; init; }

    /// <summary>Source display name.</summary>
    public required string Source { get; init; }

    /// <summary>Archetype + bracket tag chips for display.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>Whether this entry is currently published to the public surface.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Whether this entry is currently hidden from normal curation views.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Whether this entry is currently marked as evergreen.</summary>
    public bool IsEvergreen { get; init; }

    /// <summary>UTC instant the operator last pushed this entry to production, or null if never pushed.</summary>
    public DateTimeOffset? PushedToProdUtc { get; init; }

    /// <summary>UTC timestamp when the local index row was generated (distill time).</summary>
    public required DateTimeOffset IndexedUtc { get; init; }

    /// <summary>Derived publish state, computed at mapping time from PushedToProdUtc, IsVisible, and IndexedUtc.</summary>
    public PublishState PublishState { get; init; } = PublishState.NeverPublished;
}
