namespace DeckFlow.Studio.Services;

/// <summary>
/// Resolves the directory the Studio git commands (Publish / Direct Push / Pull from Prod) start
/// from. Honors the <see cref="RepoRootEnvironmentVariable" /> when set so a distributed Studio
/// executable — whose working directory is not the repo (e.g. an installed copy under a tools
/// folder) — can still locate the git working tree. Falls back to the process current directory,
/// preserving the run-from-repo behavior.
/// </summary>
public static class StudioRepoLocator
{
    /// <summary>
    /// Environment variable that, when set to a non-blank path, overrides the git start directory.
    /// </summary>
    public const string RepoRootEnvironmentVariable = "DECKFLOW_REPO_ROOT";

    /// <summary>
    /// Resolves the git start directory from the environment, falling back to the current directory.
    /// </summary>
    /// <returns>The configured repo root, or the process current directory when unset.</returns>
    public static string ResolveStartDirectory()
        => ResolveStartDirectory(Environment.GetEnvironmentVariable(RepoRootEnvironmentVariable));

    /// <summary>
    /// Resolves the git start directory from a provided override value (test seam — no environment
    /// read). A blank or whitespace value falls back to the process current directory.
    /// </summary>
    /// <param name="repoRootEnvValue">Raw override value (typically the env var contents).</param>
    /// <returns>The trimmed override when non-blank, otherwise the current directory.</returns>
    internal static string ResolveStartDirectory(string? repoRootEnvValue)
        => string.IsNullOrWhiteSpace(repoRootEnvValue)
            ? Directory.GetCurrentDirectory()
            : repoRootEnvValue.Trim();
}
