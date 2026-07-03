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
    /// Why: the git-only <c>Publish</c> page never pushes (D-01) — the operator runs <c>git push</c>
    /// after reviewing the commit. The Direct Push page instead calls <see cref="PushAsync"/> so one
    /// operator action publishes bodies to git AND production; see that method's remarks.
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

    /// <summary>
    /// Pushes the current <c>HEAD</c> to <paramref name="branch"/> on <paramref name="remote"/>
    /// via <c>git push {remote} HEAD:refs/heads/{branch}</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why: the Direct Push page needs one operator action to make content live in production AND
    /// durable in git. This runs as the operator (their local machine, their configured git
    /// credentials); it is not an automated CI push. Terminal credential prompts are disabled
    /// (<c>GIT_TERMINAL_PROMPT=0</c>) so a missing credential fails fast instead of hanging the UI.
    /// </para>
    /// <para>
    /// Why: <c>HEAD:refs/heads/{branch}</c> pushes whatever the operator currently has checked out
    /// to the named branch — the caller passes the current branch so a push never targets a branch
    /// the operator is not on.
    /// </para>
    /// </remarks>
    /// <param name="repoRoot">Absolute path to the git working tree root.</param>
    /// <param name="remote">The remote name (e.g. <c>origin</c>).</param>
    /// <param name="branch">The destination branch name (e.g. <c>main</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="GitCommandException">
    /// Thrown when the push exits with a non-zero code (auth failure, non-fast-forward, no network).
    /// </exception>
    Task PushAsync(
        string repoRoot,
        string remote,
        string branch,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the NUMBER of <paramref name="paths"/> that have an uncommitted change (modified,
    /// staged, or untracked) relative to the working tree, via <c>git status --porcelain -- {paths}</c>
    /// (one output line per changed path).
    /// </summary>
    /// <remarks>
    /// Why: Direct Push copies the pushed bodies into the tree and must (1) distinguish "there is
    /// something to commit" from "the bodies are byte-identical to what is already committed" — the
    /// latter is a legitimate no-op — and (2) report how many bodies the commit ACTUALLY contains.
    /// An <c>Updated</c> row whose DB columns changed but whose body file did not is copied but not
    /// committed, so the copied count overstates the commit; this count reflects only changed files.
    /// </remarks>
    /// <param name="repoRoot">Absolute path to the git working tree root.</param>
    /// <param name="paths">Repo-relative paths to inspect.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The count of the supplied paths that differ from the working tree/index (0 = none).</returns>
    Task<int> CountWorkingChangesAsync(
        string repoRoot,
        IReadOnlyList<string> paths,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the subject lines of the commits on <c>HEAD</c> that are NOT yet on
    /// <c>{remote}/{branch}</c>, newest first, via <c>git log --format=%s {remote}/{branch}..HEAD</c>.
    /// </summary>
    /// <remarks>
    /// Why: pushing a branch ref publishes <c>HEAD</c> AND every ancestor not already on the remote,
    /// not just the last commit. Before Direct Push pushes, the caller inspects these subjects to
    /// distinguish its own durability commits (safe to publish) from foreign unpushed commits (which
    /// must not be published without review) — and to detect a truly in-sync branch so a pointless
    /// push is skipped.
    /// </remarks>
    /// <param name="repoRoot">Absolute path to the git working tree root.</param>
    /// <param name="remote">The remote name (e.g. <c>origin</c>).</param>
    /// <param name="branch">The branch name (e.g. <c>main</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The unpushed commit subjects (empty when the branch is in sync).</returns>
    /// <exception cref="GitCommandException">
    /// Thrown when the remote-tracking ref <c>{remote}/{branch}</c> is unknown (never fetched) or the
    /// command otherwise fails; the caller treats this as "cannot determine" and proceeds best-effort.
    /// </exception>
    Task<IReadOnlyList<string>> GetSubjectsAheadOfRemoteAsync(
        string repoRoot,
        string remote,
        string branch,
        CancellationToken ct = default);
}
