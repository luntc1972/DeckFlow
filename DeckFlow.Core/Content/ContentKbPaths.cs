namespace DeckFlow.Core.Content;

/// <summary>
/// Shared repo-relative paths for Content KB tracked seed artifacts.
/// </summary>
public static class ContentKbPaths
{
    private const string SeedDirectory = "content-kb/seed/";

    /// <summary>
    /// Repo-relative forward-slash path to the tracked seed index file; the single source of truth shared by Web, Studio, and CLI.
    /// </summary>
    public const string SeedRelativePath = SeedDirectory + "index-seed.json";

    /// <summary>
    /// Repo-relative forward-slash path to the tracked creator style-profile seed file.
    /// </summary>
    public const string CreatorStyleProfileSeedRelativePath = SeedDirectory + "creator-style-profiles.json";

    /// <summary>
    /// Repo-relative forward-slash path to the tracked creator deck-cache seed file.
    /// </summary>
    public const string CreatorDeckCacheSeedRelativePath = SeedDirectory + "creator-deck-cache.json";

    /// <summary>
    /// Repo-relative forward-slash path to the tracked creator stated-rules seed file.
    /// </summary>
    public const string CreatorStatedRulesSeedRelativePath = SeedDirectory + "creator-stated-rules.json";
}
