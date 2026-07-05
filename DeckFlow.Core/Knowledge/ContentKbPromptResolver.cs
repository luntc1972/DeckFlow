namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Resolves the paste-ready AI prompt for a Content KB entry. New distills bake the framed prompt
/// into a sibling <c>{id}.prompt.md</c> next to the notes <c>{id}.md</c>; older artifacts have no
/// sibling and their prompt is reconstructed on the fly from the notes body via
/// <see cref="ContentKbPromptWrapper"/>. Both the public copy button and the Studio review queue
/// call this so a baked and a reconstructed prompt are identical output for the same notes.
/// </summary>
public static class ContentKbPromptResolver
{
    /// <summary>The file-name suffix that distinguishes a baked prompt sibling from the notes file.</summary>
    public const string PromptSuffix = ".prompt.md";

    private const string NotesSuffix = ".md";

    /// <summary>
    /// Derives the sibling prompt path for a stored relative artifact path. A
    /// <c>content-kb/{slug}/{id}.md</c> notes path maps to <c>content-kb/{slug}/{id}.prompt.md</c>.
    /// </summary>
    /// <param name="relativeArtifactPath">The stored notes artifact path (e.g. <c>content-kb/{slug}/{id}.md</c>).</param>
    /// <returns>The sibling prompt path, or <see langword="null"/> when the input is not a <c>.md</c> path.</returns>
    public static string? PromptPathFor(string relativeArtifactPath)
    {
        if (string.IsNullOrWhiteSpace(relativeArtifactPath)
            || !relativeArtifactPath.EndsWith(NotesSuffix, StringComparison.OrdinalIgnoreCase)
            || relativeArtifactPath.EndsWith(PromptSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return string.Concat(relativeArtifactPath.AsSpan(0, relativeArtifactPath.Length - NotesSuffix.Length), PromptSuffix);
    }

    /// <summary>
    /// Returns the baked prompt when a non-empty sibling was read; otherwise reconstructs the prompt
    /// from the notes body. Returns <see langword="null"/> when the notes are unavailable, so callers
    /// treat the artifact as missing (nothing to copy or preview).
    /// </summary>
    /// <param name="siblingPromptText">The sibling <c>.prompt.md</c> content, or <see langword="null"/>/empty when absent.</param>
    /// <param name="notesRawText">The raw notes artifact text (with frontmatter), or <see langword="null"/> when unavailable.</param>
    /// <param name="title">Video title for grounding when reconstructing.</param>
    /// <param name="source">Creator/source name for grounding when reconstructing.</param>
    /// <param name="videoUrl">Source video URL for provenance when reconstructing.</param>
    /// <returns>The paste-ready prompt, or <see langword="null"/> when neither a sibling nor notes exist.</returns>
    public static string? BuildOrReconstruct(
        string? siblingPromptText,
        string? notesRawText,
        string title,
        string source,
        string videoUrl)
    {
        // A baked sibling is authoritative: it was framed at distill time from the same notes.
        if (!string.IsNullOrWhiteSpace(siblingPromptText))
        {
            return siblingPromptText;
        }

        // No sibling (pre-bake artifact): reconstruct from the notes so old and new entries match.
        if (string.IsNullOrWhiteSpace(notesRawText))
        {
            return null;
        }

        var (_, body) = ContentArtifactParser.SplitHeader(notesRawText);
        return ContentKbPromptWrapper.Wrap(title, source, videoUrl, body);
    }
}
