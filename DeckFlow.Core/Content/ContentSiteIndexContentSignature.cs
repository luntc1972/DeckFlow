using System.Globalization;
using System.Text;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Stable content signature over the exact column set written by
/// <c>UpsertContentColumnsOnlySql</c>: source, title, video_url, artifact_path,
/// published_utc, indexed_utc, archetype_tags, bracket_tags, card_category_tags.
/// Used by DirectPush to classify rows as New / Updated / Unchanged without re-writing
/// content that is already identical in production.
/// </summary>
/// <remarks>
/// Deliberately excludes is_visible, is_hidden, is_evergreen, approval_status, and
/// pushed_to_prod_utc — those are admin/publish columns not touched by the content upsert
/// (D-08 / T-qyc-03).
/// <para>
/// Date columns are normalized to UTC and truncated to whole seconds before hashing.
/// SQLite stores timestamps as ISO-8601 text with 1-second precision; Postgres stores them
/// as TIMESTAMPTZ with microsecond precision. Truncating to seconds prevents false-positive
/// "Updated" classifications that would occur if a Postgres sub-second value were compared
/// against a SQLite-deserialized whole-second value.
/// </para>
/// <para>
/// Tags are serialized via <see cref="ContentArtifactSpec.SerializeTags"/> — the same
/// serializer used by the upsert — ensuring list-reference equality is never required.
/// </para>
/// </remarks>
public static class ContentSiteIndexContentSignature
{
    /// <summary>
    /// Delimiter that cannot appear in any of the signed column values (null byte).
    /// Using a control character prevents any value from accidentally spanning field
    /// boundaries in the signature string.
    /// </summary>
    private const char FieldDelimiter = '\0';

    /// <summary>
    /// Sentinel token substituted for a null <see cref="ContentSiteIndexRow.PublishedUtc"/>.
    /// Must not be a valid ISO-8601 UTC timestamp.
    /// </summary>
    private const string NullDateSentinel = "(null)";

    /// <summary>
    /// Builds a stable string signature over the content columns for <paramref name="row"/>.
    /// Two rows with identical content columns produce equal signatures regardless of object
    /// identity, sub-second timestamp precision differences, or tag list reference equality.
    /// </summary>
    /// <param name="row">Row to sign.</param>
    /// <returns>Stable content signature string.</returns>
    public static string BuildSignature(ContentSiteIndexRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var sb = new StringBuilder();

        // source
        sb.Append(row.Source);
        sb.Append(FieldDelimiter);

        // title
        sb.Append(row.Title);
        sb.Append(FieldDelimiter);

        // video_url
        sb.Append(row.VideoUrl);
        sb.Append(FieldDelimiter);

        // artifact_path
        sb.Append(row.ArtifactPath);
        sb.Append(FieldDelimiter);

        // published_utc — null sentinel so null != any real date
        sb.Append(row.PublishedUtc.HasValue
            ? TruncateToSeconds(row.PublishedUtc.Value)
            : NullDateSentinel);
        sb.Append(FieldDelimiter);

        // Why: indexed_utc is included because it changes only when an artifact is
        // re-distilled, which IS a real content change worth pushing to prod.
        sb.Append(TruncateToSeconds(row.IndexedUtc));
        sb.Append(FieldDelimiter);

        // tags via the exact serializer the upsert uses — no list-reference equality required
        sb.Append(ContentArtifactSpec.SerializeTags(row.ArchetypeTags));
        sb.Append(FieldDelimiter);
        sb.Append(ContentArtifactSpec.SerializeTags(row.BracketTags));
        sb.Append(FieldDelimiter);
        sb.Append(ContentArtifactSpec.SerializeTags(row.CardCategoryTags));

        return sb.ToString();
    }

    /// <summary>
    /// Returns <see langword="true"/> when the content columns of <paramref name="a"/> and
    /// <paramref name="b"/> are equal by stable signature comparison.
    /// </summary>
    /// <param name="a">First row.</param>
    /// <param name="b">Second row.</param>
    /// <returns><see langword="true"/> if content columns match; otherwise <see langword="false"/>.</returns>
    public static bool AreContentEqual(ContentSiteIndexRow a, ContentSiteIndexRow b)
        => string.Equals(BuildSignature(a), BuildSignature(b), StringComparison.Ordinal);

    /// <summary>
    /// Normalizes a <see cref="DateTimeOffset"/> to UTC and truncates to whole seconds,
    /// formatted as a fixed-length ISO-8601 UTC string with InvariantCulture.
    /// </summary>
    private static string TruncateToSeconds(DateTimeOffset dt)
    {
        // Convert to UTC and strip sub-second ticks before formatting so SQLite (1-second
        // precision) and Postgres (microsecond precision) produce identical signature tokens.
        var utc = dt.UtcDateTime;
        var truncated = new DateTime(utc.Year, utc.Month, utc.Day,
            utc.Hour, utc.Minute, utc.Second,
            DateTimeKind.Utc);
        return truncated.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}
