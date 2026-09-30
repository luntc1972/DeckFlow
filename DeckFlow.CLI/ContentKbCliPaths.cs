using DeckFlow.Core.Content;

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
    /// Resolves the creator deck-cache database next to the Content KB database.
    /// </summary>
    /// <param name="db">Optional explicit Content KB database file path.</param>
    /// <returns>The full path to the creator deck-cache database.</returns>
    public static string ResolveCreatorDeckCacheDatabasePath(FileInfo? db)
    {
        var directory = Path.GetDirectoryName(ResolveDatabasePath(db))
            ?? Directory.GetCurrentDirectory();
        return Path.Combine(directory, "creator-deck-cache.db");
    }

    /// <summary>
    /// Resolves the Content KB artifact root from the current environment.
    /// </summary>
    /// <param name="db">Unused optional database file path kept for call-site compatibility.</param>
    /// <param name="environmentVariableGetter">Optional environment-variable lookup override; tests inject one, production reads the process environment.</param>
    /// <returns>The full path to the Content KB artifact root.</returns>
    public static string ResolveArtifactRoot(FileInfo? db, Func<string, string?>? environmentVariableGetter = null)
    {
        var dataDir = environmentVariableGetter is null
            ? Environment.GetEnvironmentVariable("MTG_DATA_DIR")
            : environmentVariableGetter("MTG_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDir))
        {
            return Path.GetFullPath(Path.Combine(dataDir, "content-kb"));
        }

        return environmentVariableGetter is null
            ? PrivateKbRoot.FromEnvironment().ContentKbDir
            : PrivateKbRoot.FromEnvironment(environmentVariableGetter).ContentKbDir;
    }
}
