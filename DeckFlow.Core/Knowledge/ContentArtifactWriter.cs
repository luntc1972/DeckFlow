using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Renders and writes Content KB artifact files using the locked <see cref="ContentArtifactSpec"/> layout.
/// </summary>
public static class ContentArtifactWriter
{
    /// <summary>
    /// Computes the relative artifact path stored in the site index.
    /// </summary>
    /// <param name="sourceSlug">Source slug path segment.</param>
    /// <param name="videoId">Video or episode identifier path segment.</param>
    /// <returns>A relative <c>content-kb/{sourceSlug}/{videoId}.md</c> path using forward slashes.</returns>
    public static string ComputeRelativeArtifactPath(string sourceSlug, string videoId)
    {
        var (safeSourceSlug, fileName) = BuildArtifactSegments(sourceSlug, videoId);
        return $"content-kb/{safeSourceSlug}/{fileName}";
    }

    /// <summary>
    /// Computes the relative sibling prompt path stored alongside the notes artifact.
    /// </summary>
    /// <param name="sourceSlug">Source slug path segment.</param>
    /// <param name="videoId">Video or episode identifier path segment.</param>
    /// <returns>A relative <c>content-kb/{sourceSlug}/{videoId}.prompt.md</c> path using forward slashes.</returns>
    public static string ComputeRelativePromptPath(string sourceSlug, string videoId)
    {
        var (safeSourceSlug, fileName) = BuildPromptSegments(sourceSlug, videoId);
        return $"content-kb/{safeSourceSlug}/{fileName}";
    }

    /// <summary>
    /// Renders artifact text from metadata, summary text, and in-memory clip results.
    /// </summary>
    /// <param name="metadata">Artifact front matter metadata.</param>
    /// <param name="summary">Standalone summary text.</param>
    /// <param name="clips">In-memory clip excerpts with nullable timestamps.</param>
    /// <returns>Markdown artifact text matching the locked Content KB layout.</returns>
    public static string ToText(
        ContentArtifactMetadata metadata,
        string summary,
        IReadOnlyList<(int? TimestampSeconds, string Excerpt)> clips)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(metadata.ArchetypeTags);
        ArgumentNullException.ThrowIfNull(metadata.BracketTags);
        ArgumentNullException.ThrowIfNull(metadata.CardCategoryTags);

        var videoId = GetArtifactVideoId(metadata);
        var builder = new StringBuilder();

        builder.AppendLine("---");
        builder.Append("source: ").AppendLine(Quote(metadata.Source));
        builder.Append("title: ").AppendLine(Quote(metadata.Title));
        builder.Append("url: ").AppendLine(Quote(metadata.Url));
        builder.Append("video_id: ").AppendLine(Quote(videoId));
        builder.AppendLine("tags:");
        builder.Append("  archetype: ").AppendLine(ContentArtifactSpec.SerializeTags(metadata.ArchetypeTags));
        builder.Append("  bracket: ").AppendLine(ContentArtifactSpec.SerializeTags(metadata.BracketTags));
        builder.Append("  card_category: ").AppendLine(ContentArtifactSpec.SerializeTags(metadata.CardCategoryTags));
        builder.Append("generated_utc: ").AppendLine(Quote(FormatGeneratedUtc(metadata.GeneratedUtc)));
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine(summary);
        builder.AppendLine();
        builder.AppendLine("## Key Clips");
        builder.AppendLine();

        foreach (var clip in clips)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(clip.Excerpt);
            if (clip.TimestampSeconds is { } timestampSeconds)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(timestampSeconds);
                builder.Append("- **[")
                    .Append(FormatClipTimestamp(timestampSeconds))
                    .Append("]** ")
                    .AppendLine(clip.Excerpt);
            }
            else
            {
                builder.Append("- ").AppendLine(clip.Excerpt);
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Tags");
        builder.AppendLine();
        builder.Append("**Archetypes/Strategy:** ").AppendLine(JoinTags(metadata.ArchetypeTags));
        builder.Append("**Format/Bracket:** ").AppendLine(JoinTags(metadata.BracketTags));
        builder.Append("**Card Categories:** ").AppendLine(JoinTags(metadata.CardCategoryTags));

        return builder.ToString();
    }

    /// <summary>
    /// Writes rendered artifact text beneath an artifact root.
    /// </summary>
    /// <param name="artifactRoot">Absolute or relative root directory for Content KB artifacts.</param>
    /// <param name="sourceSlug">Source slug path segment.</param>
    /// <param name="videoId">Video or episode identifier path segment.</param>
    /// <param name="text">Rendered artifact text.</param>
    /// <returns>The absolute path written.</returns>
    public static string WriteFile(string artifactRoot, string sourceSlug, string videoId, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var (safeSourceSlug, fileName) = BuildArtifactSegments(sourceSlug, videoId);
        var outputPath = Path.GetFullPath(Path.Combine(artifactRoot, safeSourceSlug, fileName));
        var parent = Path.GetDirectoryName(outputPath)
            ?? throw new ArgumentException("Artifact output path must have a parent directory.", nameof(artifactRoot));

        Directory.CreateDirectory(parent);
        File.WriteAllText(outputPath, text);
        return outputPath;
    }

    /// <summary>
    /// Writes the baked, paste-ready prompt to the sibling <c>{videoId}.prompt.md</c> file beneath
    /// an artifact root, next to the notes artifact.
    /// </summary>
    /// <param name="artifactRoot">Absolute or relative root directory for Content KB artifacts.</param>
    /// <param name="sourceSlug">Source slug path segment.</param>
    /// <param name="videoId">Video or episode identifier path segment.</param>
    /// <param name="promptText">The rendered, paste-ready prompt text.</param>
    /// <returns>The absolute path written.</returns>
    public static string WritePromptFile(string artifactRoot, string sourceSlug, string videoId, string promptText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptText);

        var (safeSourceSlug, fileName) = BuildPromptSegments(sourceSlug, videoId);
        var outputPath = Path.GetFullPath(Path.Combine(artifactRoot, safeSourceSlug, fileName));
        var parent = Path.GetDirectoryName(outputPath)
            ?? throw new ArgumentException("Prompt output path must have a parent directory.", nameof(artifactRoot));

        Directory.CreateDirectory(parent);
        File.WriteAllText(outputPath, promptText);
        return outputPath;
    }

    // Why: WriteFile and ComputeRelativeArtifactPath must agree on the on-disk layout or the
    // site index points at files that were never written — derive the segments in one place.
    private static (string SourceSlug, string FileName) BuildArtifactSegments(string sourceSlug, string videoId)
        => (SanitizePathSegment(sourceSlug, nameof(sourceSlug)),
            SanitizePathSegment(videoId, nameof(videoId)) + ".md");

    // Why: WritePromptFile and ComputeRelativePromptPath must agree on the sibling layout, and the
    // sibling shares the notes' sanitized segments so it lands next to it — derive in one place.
    private static (string SourceSlug, string FileName) BuildPromptSegments(string sourceSlug, string videoId)
        => (SanitizePathSegment(sourceSlug, nameof(sourceSlug)),
            SanitizePathSegment(videoId, nameof(videoId)) + ".prompt.md");

    private static string GetArtifactVideoId(ContentArtifactMetadata metadata)
    {
        var hasYoutubeVideoId = !string.IsNullOrWhiteSpace(metadata.YoutubeVideoId);
        var hasRssGuid = !string.IsNullOrWhiteSpace(metadata.RssGuid);
        if (hasYoutubeVideoId == hasRssGuid)
        {
            throw new ArgumentException(
                "Exactly one of YoutubeVideoId or RssGuid must be supplied for a content artifact.",
                nameof(metadata));
        }

        return hasYoutubeVideoId ? metadata.YoutubeVideoId! : metadata.RssGuid!;
    }

    private static string FormatClipTimestamp(int timestampSeconds)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{timestampSeconds / 60:00}:{timestampSeconds % 60:00}");

    private static string FormatGeneratedUtc(DateTimeOffset generatedUtc)
        => generatedUtc
            .ToUniversalTime()
            .UtcDateTime
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string JoinTags(IReadOnlyList<string> tags)
        => string.Join(", ", tags);

    private static string Quote(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return JsonSerializer.Serialize(value);
    }

    private static string SanitizePathSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        var trimmed = value.Trim();
        if (Path.IsPathRooted(trimmed)
            || IsWindowsRootedPath(trimmed)
            || trimmed.Contains("..", StringComparison.Ordinal)
            || trimmed.Contains('/', StringComparison.Ordinal)
            || trimmed.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("Artifact path segments must be relative file-name segments.", parameterName);
        }

        var builder = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            builder.Append(IsAllowedPathSegmentCharacter(character) ? character : '-');
        }

        var sanitized = builder.ToString().Trim('-', '.');
        if (string.IsNullOrWhiteSpace(sanitized)
            || string.Equals(sanitized, ".", StringComparison.Ordinal)
            || string.Equals(sanitized, "..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Artifact path segments must contain at least one safe file-name character.", parameterName);
        }

        return sanitized;
    }

    private static bool IsAllowedPathSegmentCharacter(char character)
        => char.IsLetterOrDigit(character)
            || character == '-'
            || character == '_'
            || character == '.';

    private static bool IsWindowsRootedPath(string artifactPath)
        => artifactPath.Length >= 3
            && char.IsLetter(artifactPath[0])
            && artifactPath[1] == ':'
            && (artifactPath[2] == '\\' || artifactPath[2] == '/');
}
