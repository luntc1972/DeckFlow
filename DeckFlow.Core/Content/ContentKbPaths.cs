namespace DeckFlow.Core.Content;

/// <summary>
/// Shared repo-relative paths for Content KB tracked seed artifacts.
/// </summary>
public static class ContentKbPaths
{
    /// <summary>
    /// Repo-relative forward-slash path to the tracked seed index file; the single source of truth shared by Web, Studio, and CLI.
    /// </summary>
    public const string SeedRelativePath = "content-kb/seed/index-seed.json";

    /// <summary>
    /// Repo-relative forward-slash path to the tracked creator style-profile seed file.
    /// </summary>
    public const string CreatorStyleProfileSeedRelativePath = "content-kb/seed/creator-style-profiles.json";

    /// <summary>
    /// Repo-relative forward-slash path to the tracked creator deck-cache seed file.
    /// </summary>
    public const string CreatorDeckCacheSeedRelativePath = "content-kb/seed/creator-deck-cache.json";
}
