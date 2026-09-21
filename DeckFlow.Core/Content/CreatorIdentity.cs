namespace DeckFlow.Core.Content;

#pragma warning disable CS1591

/// <summary>Canonical set of representations for one creator.</summary>
public sealed record CreatorIdentity(
    string CanonicalSlug,
    IReadOnlyList<long> SourceIds,
    IReadOnlyList<string> DisplayNames,
    IReadOnlyList<string> FolderSlugs);
#pragma warning restore CS1591
