namespace DeckFlow.Core.Integration;

/// <summary>
/// Shells out to the <c>git</c> CLI to read repository metadata and perform a scoped
/// stage-and-commit. Studio uses this to display branch info, diff previews, and to
/// commit the approved seed + artifacts in one atomic operation.
/// </summary>
public interface IGitRepository
{
    /// <summary>
    /// Returns the current branch name (e.g. <c>v1.7</c>) via
    /// <c>git rev-parse --abbrev-ref HEAD</c>.
    /// </summary>
    /// <param name="repoRoot">Absolute path to the git working tree root.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current branch name, trimmed.</returns>
    Task<string> GetCurrentBranchAsync(string repoRoot, CancellationToken ct = default);

    /// <summary>
    /// Resolves the repository root starting from <paramref name="startDir"/> via
    /// <c>git -C {startDir} rev-parse --show-toplevel</c>. Useful when Studio only
    /// knows a subdirectory and needs the canonical tree root.
    /// </summary>
    /// <param name="startDir">Any directory inside the git working tree.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The absolute path to the repository root.</returns>
    Task<string> ResolveRepoRootAsync(string startDir, CancellationToken ct = default);

    /// <summary>
    /// Returns the raw textual <c>git diff</c> output for the given working-tree paths
    /// (unstaged changes relative to the index).
    /// </summary>
    /// <param name="repoRoot">Absolute path to the git working tree root.</param>
    /// <param name="paths">Repo-relative paths to include in the diff.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw diff text (may be empty if no changes).</returns>
    Task<string> DiffAsync(string repoRoot, IReadOnlyList<string> paths, CancellationToken ct = default);

    /// <summary>
    /// Returns the content of <paramref name="seedRelativePath"/> at <c>HEAD</c> via
    /// <c>git show HEAD:{seedRelativePath}</c>. Returns an empty string when the path
    /// does not exist at <c>HEAD</c> (first publish, not an error).
    /// </summary>
    /// <param name="repoRoot">Absolute path to the git working tree root.</param>
    /// <param name="seedRelativePath">Repo-relative path to the seed file (e.g. <c>content-kb/seed/index-seed.json</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The file contents at HEAD, or an empty string when absent.</returns>
    Task<string> CatHeadSeedAsync(string repoRoot, string seedRelativePath, CancellationToken ct = default);

    /// <summary>
    /// Stages exactly <paramref name="paths"/> and commits them with <paramref name="message"/>,
    /// then returns the short SHA of the new commit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why: Studio never pushes (D-01) — only the operator runs <c>git push</c> after reviewing
    /// the commit. This method intentionally has no push counterpart.
    /// </para>
    /// <para>
    /// Why: pathspec-scoped commit + foreign-staged guard so a pre-staged unrelated hunk is
    /// never swept into the publish commit (D-01). The guard checks
    /// <c>git diff --cached --name-only</c> before staging and refuses to proceed if any
    /// already-staged path is outside the supplied allowed set. The commit itself is also
    /// pathspec-scoped (<c>git commit -m {msg} -- {paths}</c>) as defense-in-depth.
    /// </para>
    /// </remarks>
    /// <param name="repoRoot">Absolute path to the git working tree root.</param>
    /// <param name="paths">Repo-relative paths to stage and commit (seed + artifacts).</param>
    /// <param name="message">Commit message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The short SHA of the resulting commit.</returns>
    /// <exception cref="GitForeignStagedChangesException">
    /// Thrown when pre-staged paths outside <paramref name="paths"/> are detected.
    /// </exception>
    /// <exception cref="GitCommandException">
    /// Thrown when any git sub-command exits with a non-zero code.
    /// </exception>
    Task<string> StageAndCommitAsync(
        string repoRoot,
        IReadOnlyList<string> paths,
        string message,
        CancellationToken ct = default);
}
