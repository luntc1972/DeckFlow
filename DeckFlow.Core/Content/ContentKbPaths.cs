namespace DeckFlow.Core.Content;

/// <summary>
/// Shared relative paths for private Content KB seed artifacts.
/// </summary>
public static class ContentKbPaths
{
    private const string SeedDirectory = "content-kb/seed/";

    /// <summary>
    /// Forward-slash path to the private seed index file; the single source of truth shared by Web, Studio, and CLI.
    /// </summary>
    public const string SeedRelativePath = SeedDirectory + "index-seed.json";

    /// <summary>
    /// Forward-slash path to the private creator style-profile seed file.
    /// </summary>
    public const string CreatorStyleProfileSeedRelativePath = SeedDirectory + "creator-style-profiles.json";

    /// <summary>
    /// Forward-slash path to the private creator deck-cache seed file.
    /// </summary>
    public const string CreatorDeckCacheSeedRelativePath = SeedDirectory + "creator-deck-cache.json";

    /// <summary>
    /// Forward-slash path to the private creator stated-rules seed file.
    /// </summary>
    public const string CreatorStatedRulesSeedRelativePath = SeedDirectory + "creator-stated-rules.json";
}
