using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// The four ways a single content entry can differ between production and the local store.
/// There is deliberately no fifth "LocalNewer" kind — a local-newer entry is classified as
/// <see cref="Diverged"/> with <see cref="SyncDiffEntry.LocalIsNewer"/> set, so the UI can show
/// which side leads without expanding the kind set.
/// </summary>
public enum SyncDiffKind
{
    /// <summary>The same key exists on both sides and production's index timestamp is newer.</summary>
    ProdNewer,

    /// <summary>The key exists in production but not in the local store.</summary>
    MissingLocally,

    /// <summary>The key exists in the local store but not in production.</summary>
    LocalOnly,

    /// <summary>
    /// The key exists on both sides but the rows differ in a way that is not a clean prod-newer:
    /// either local's timestamp is newer (<see cref="SyncDiffEntry.LocalIsNewer"/> is <c>true</c>)
    /// or the timestamps are equal but the content fingerprint differs.
    /// </summary>
    Diverged
}

/// <summary>
/// One classified difference between a production and a local <see cref="ContentSiteIndexRow"/>,
/// keyed by its natural key. Produced by <see cref="ContentSyncDiffClassifier"/>; consumed by the
/// Studio Pull-from-Prod page. In-sync (identical) pairs are never represented — only real diffs.
/// </summary>
public sealed record SyncDiffEntry
{
    /// <summary>
    /// The natural-key type in the stored vocabulary: <see cref="ContentSourceType.Youtube"/>
    /// (<c>"youtube_channel"</c>) when the row carries a YouTube id, else <see cref="ContentSourceType.Podcast"/>
    /// (<c>"podcast_rss"</c>).
    /// </summary>
    public required string NaturalKeyType { get; init; }

    /// <summary>The natural-key value (<c>YoutubeVideoId ?? RssGuid</c>).</summary>
    public required string NaturalKeyValue { get; init; }

    /// <summary>Which of the four <see cref="SyncDiffKind"/> categories this entry falls into.</summary>
    public required SyncDiffKind Kind { get; init; }

    /// <summary>Display title, taken from the production row when present, otherwise the local row.</summary>
    public required string Title { get; init; }

    /// <summary>The production row, or <c>null</c> when <see cref="Kind"/> is <see cref="SyncDiffKind.LocalOnly"/>.</summary>
    public required ContentSiteIndexRow? ProdRow { get; init; }

    /// <summary>The local row, or <c>null</c> when <see cref="Kind"/> is <see cref="SyncDiffKind.MissingLocally"/>.</summary>
    public required ContentSiteIndexRow? LocalRow { get; init; }

    /// <summary>The relative artifact path, taken from the production row when present, otherwise the local row.</summary>
    public required string ArtifactPath { get; init; }

    /// <summary>
    /// Only meaningful when <see cref="Kind"/> is <see cref="SyncDiffKind.Diverged"/>: <c>true</c> when the
    /// local row's index timestamp is strictly newer than production's. Defaults to <c>false</c>.
    /// </summary>
    public bool LocalIsNewer { get; init; }

    /// <summary>
    /// Whether the production artifact for this entry was successfully downloaded into staging.
    /// The classifier leaves this <c>false</c>; the page sets it after the SCP step (Plan 03).
    /// </summary>
    public bool ArtifactDownloaded { get; init; }
}
