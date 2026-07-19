namespace DeckFlow.CLI;

/// <summary>
/// Resolves the shared Content KB database and artifact paths used by CLI runners.
/// </summary>
internal static class ContentKbCliPaths
{
    /// <summary>
    /// Resolves the Content KB database path from the optional CLI argument.
    /// </summary>
    /// <param name="db">Optional explicit database file path.</param>
    /// <returns>The full path to the Content KB database.</returns>
    public static string ResolveDatabasePath(FileInfo? db)
        => db?.FullName ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "content-kb.db");

    /// <summary>
    /// Resolves the creator deck-cache database path.
    /// </summary>
    /// <returns>The full path to the creator deck-cache database.</returns>
    public static string ResolveCreatorDeckCacheDatabasePath()
        => Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "creator-deck-cache.db");

    /// <summary>
    /// Resolves the Content KB artifact root from the current environment.
    /// </summary>
    /// <param name="db">Unused optional database file path kept for call-site compatibility.</param>
    /// <returns>The full path to the Content KB artifact root.</returns>
    public static string ResolveArtifactRoot(FileInfo? db)
    {
        var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDir))
        {
            return Path.GetFullPath(Path.Combine(dataDir, "content-kb"));
        }

        // Why: D-11 / HSK-04 collapses the dual artifact tree to the repo-root content-kb so drift is impossible.
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "content-kb"));
    }
}
