using System.Diagnostics;
using System.Globalization;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Thrown when a git sub-command exits with a non-zero exit code.
/// </summary>
public class GitCommandException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new <see cref="GitCommandException"/> with <paramref name="message"/>.
    /// </summary>
    /// <param name="message">User-facing error description (from captured stderr).</param>
    public GitCommandException(string message)
        : base(message) { }
}

/// <summary>
/// Thrown when <see cref="GitRepository.StageAndCommitAsync"/> detects pre-staged paths
/// outside the allowed publish set, preventing unrelated changes from entering the commit.
/// </summary>
public sealed class GitForeignStagedChangesException : GitCommandException
{
    /// <summary>
    /// Initializes a new <see cref="GitForeignStagedChangesException"/> listing the offending paths.
    /// </summary>
    /// <param name="offendingPaths">Staged paths that are not in the allowed set.</param>
    public GitForeignStagedChangesException(IReadOnlyList<string> offendingPaths)
        : base(
            $"Cannot commit — unrelated changes are already staged: {string.Join(", ", offendingPaths)}. " +
            "Unstage them and retry.")
    {
        OffendingPaths = offendingPaths;
    }

    /// <summary>Gets the staged paths that triggered the guard.</summary>
    public IReadOnlyList<string> OffendingPaths { get; }
}

/// <summary>
/// Shells out to the system <c>git</c> CLI to read repository metadata and perform a
/// pathspec-scoped, foreign-staged-path-guarded stage-and-commit.
/// </summary>
/// <remarks>
/// Uses <see cref="ProcessStartInfo.ArgumentList"/> (never string-concatenated
/// <see cref="ProcessStartInfo.Arguments"/>) to avoid shell-quoting vulnerabilities.
/// <c>UseShellExecute = false</c> + <c>CreateNoWindow = true</c> ensure no shell
/// metacharacter expansion (T-46-02-01).
/// </remarks>
public sealed class GitRepository : IGitRepository
{
    private const string GitExecutable = "git";

    /// <inheritdoc />
    public async Task<string> GetCurrentBranchAsync(string repoRoot, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("--abbrev-ref");
        startInfo.ArgumentList.Add("HEAD");

        var stdout = await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
        return stdout.Trim();
    }

    /// <inheritdoc />
    public async Task<string> ResolveRepoRootAsync(string startDir, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDir);

        // Why: -C {startDir} runs git as if called from that directory so callers
        // that only know a subdirectory get the canonical repo root.
        var startInfo = BuildStartInfo(workingDirectory: startDir);
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(startDir);
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("--show-toplevel");

        var stdout = await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
        return stdout.Trim();
    }

    /// <inheritdoc />
    public async Task<string> DiffAsync(
        string repoRoot,
        IReadOnlyList<string> paths,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentNullException.ThrowIfNull(paths);

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("diff");
        startInfo.ArgumentList.Add("--");
        foreach (var path in paths)
        {
            startInfo.ArgumentList.Add(path);
        }

        return await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> CatHeadSeedAsync(
        string repoRoot,
        string seedRelativePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedRelativePath);

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("show");
        startInfo.ArgumentList.Add($"HEAD:{seedRelativePath}");

        // Why: git show exits non-zero when the path does not exist at HEAD (first publish).
        // Treat that as "no committed seed yet" rather than an error.
        var (stdout, _, exitCode) = await RunRawAsync(startInfo, ct).ConfigureAwait(false);
        return exitCode == 0 ? stdout : string.Empty;
    }

    /// <inheritdoc />
    public async Task<string> StageAndCommitAsync(
        string repoRoot,
        IReadOnlyList<string> paths,
        string message,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (paths.Count == 0)
        {
            throw new ArgumentException("At least one path must be supplied for a scoped commit.", nameof(paths));
        }

        // Build the normalized allowed-path set (forward-slash, no leading slash).
        var allowedSet = new HashSet<string>(
            paths.Select(NormalizeGitPath),
            StringComparer.OrdinalIgnoreCase);

        // ── STEP 1: FOREIGN-STAGED GUARD ──────────────────────────────────────────
        // Why: pathspec-scoped commit + foreign-staged guard so a pre-staged unrelated
        // hunk is never swept into the publish commit (D-01).
        var alreadyStaged = await GetCachedPathListAsync(repoRoot, ct).ConfigureAwait(false);

        var offenders = alreadyStaged
            .Where(p => !allowedSet.Contains(NormalizeGitPath(p)))
            .ToList();

        if (offenders.Count > 0)
        {
            throw new GitForeignStagedChangesException(offenders);
        }

        // ── STEP 2: git add -- {paths} ────────────────────────────────────────────
        // Why: never git add -A or git add "." — scope staging to exactly the listed paths.
        var addInfo = BuildStartInfo(repoRoot);
        addInfo.ArgumentList.Add("add");
        addInfo.ArgumentList.Add("--");
        foreach (var path in paths)
        {
            addInfo.ArgumentList.Add(path);
        }

        await RunAndCaptureAsync(addInfo, ct).ConfigureAwait(false);

        // ── STEP 3: PATHSPEC-SCOPED COMMIT ────────────────────────────────────────
        // Why: pathspec-scoped commit so even if something else ended up staged between
        // the guard and here, the commit is limited to the allowed pathspecs (D-01).
        var commitInfo = BuildStartInfo(repoRoot);
        commitInfo.ArgumentList.Add("commit");
        commitInfo.ArgumentList.Add("-m");
        commitInfo.ArgumentList.Add(message);
        commitInfo.ArgumentList.Add("--");
        foreach (var path in paths)
        {
            commitInfo.ArgumentList.Add(path);
        }

        await RunAndCaptureAsync(commitInfo, ct).ConfigureAwait(false);

        // ── STEP 4: return short SHA ──────────────────────────────────────────────
        return await GetShortHeadShaAsync(repoRoot, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> CommitEmptyAsync(
        string repoRoot,
        string message,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var alreadyStaged = await GetCachedPathListAsync(repoRoot, ct).ConfigureAwait(false);
        if (alreadyStaged.Count > 0)
        {
            throw new GitForeignStagedChangesException(alreadyStaged);
        }

        var commitInfo = BuildStartInfo(repoRoot);
        commitInfo.ArgumentList.Add("commit");
        commitInfo.ArgumentList.Add("--allow-empty");
        commitInfo.ArgumentList.Add("-m");
        commitInfo.ArgumentList.Add(message);

        await RunAndCaptureAsync(commitInfo, ct).ConfigureAwait(false);

        if (!await IsHeadCommitEmptyAsync(repoRoot, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "Expected git commit --allow-empty to create an empty commit, but HEAD changed files.");
        }

        return await GetShortHeadShaAsync(repoRoot, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PushAsync(
        string repoRoot,
        string remote,
        string branch,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("push");
        startInfo.ArgumentList.Add(remote);
        // Why: explicit HEAD:refs/heads/{branch} so the push targets the named branch regardless of
        // upstream tracking config, and never a branch the operator is not currently on.
        startInfo.ArgumentList.Add($"HEAD:refs/heads/{branch}");

        await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task FetchAsync(
        string repoRoot,
        string remote,
        string branch,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("fetch");
        startInfo.ArgumentList.Add(remote);
        // Why: explicit refspec forces refs/remotes/{remote}/{branch} to advance so the behind-count
        // reads a fresh tracking ref; a bare branch fetch can leave it stale and falsely report behind=0.
        startInfo.ArgumentList.Add($"+refs/heads/{branch}:refs/remotes/{remote}/{branch}");

        await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountWorkingChangesAsync(
        string repoRoot,
        IReadOnlyList<string> paths,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentNullException.ThrowIfNull(paths);

        if (paths.Count == 0)
        {
            return 0;
        }

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("status");
        startInfo.ArgumentList.Add("--porcelain");
        startInfo.ArgumentList.Add("--");
        foreach (var path in paths)
        {
            startInfo.ArgumentList.Add(path);
        }

        var stdout = await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);

        // Why: porcelain emits exactly one line per changed/untracked path; the line count is the
        // number of the scoped paths that differ from HEAD (modified, staged, or untracked-new).
        return stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => !string.IsNullOrWhiteSpace(line));
    }

    /// <inheritdoc />
    public async Task<int> GetBehindCountAsync(
        string repoRoot,
        string remote,
        string branch,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("rev-list");
        startInfo.ArgumentList.Add("--count");
        startInfo.ArgumentList.Add($"HEAD..{remote}/{branch}");

        var stdout = await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
        return int.Parse(stdout.Trim(), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetSubjectsAheadOfRemoteAsync(
        string repoRoot,
        string remote,
        string branch,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("log");
        startInfo.ArgumentList.Add("--format=%s");
        // Why: {remote}/{branch}..HEAD = commits reachable from HEAD but not from the remote-tracking
        // ref. RunAndCaptureAsync throws GitCommandException when that ref does not exist (never
        // fetched) — the caller treats that as "cannot determine" and proceeds best-effort.
        startInfo.ArgumentList.Add($"{remote}/{branch}..HEAD");

        var stdout = await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);

        return stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<string> GetHeadSubjectAsync(string repoRoot, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("log");
        startInfo.ArgumentList.Add("-1");
        startInfo.ArgumentList.Add("--format=%s");
        startInfo.ArgumentList.Add("HEAD");

        var stdout = await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
        return stdout.Trim();
    }

    /// <inheritdoc />
    public async Task<bool> IsHeadCommitEmptyAsync(string repoRoot, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var startInfo = BuildStartInfo(repoRoot);
        startInfo.ArgumentList.Add("diff-tree");
        startInfo.ArgumentList.Add("--no-commit-id");
        startInfo.ArgumentList.Add("--name-only");
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add("HEAD");

        var stdout = await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(stdout);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<string>> GetCachedPathListAsync(
        string repoRoot,
        CancellationToken ct)
    {
        var diffCachedInfo = BuildStartInfo(repoRoot);
        diffCachedInfo.ArgumentList.Add("diff");
        diffCachedInfo.ArgumentList.Add("--cached");
        diffCachedInfo.ArgumentList.Add("--name-only");

        var (cachedOut, _, _) = await RunRawAsync(diffCachedInfo, ct).ConfigureAwait(false);
        return cachedOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
    }

    private static async Task<string> GetShortHeadShaAsync(
        string repoRoot,
        CancellationToken ct)
    {
        var revParseInfo = BuildStartInfo(repoRoot);
        revParseInfo.ArgumentList.Add("rev-parse");
        revParseInfo.ArgumentList.Add("--short");
        revParseInfo.ArgumentList.Add("HEAD");

        var sha = await RunAndCaptureAsync(revParseInfo, ct).ConfigureAwait(false);
        return sha.Trim();
    }

    private static ProcessStartInfo BuildStartInfo(string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(GitExecutable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Why: never let git block the process on an interactive credential/passphrase prompt —
        // a missing credential must fail fast with a non-zero exit (surfaced as GitCommandException)
        // rather than hang the Studio UI waiting on stdin that will never come. Harmless for the
        // read-only sub-commands; essential for PushAsync.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        return startInfo;
    }

    private static async Task<string> RunAndCaptureAsync(
        ProcessStartInfo startInfo,
        CancellationToken ct)
    {
        var (stdout, stderr, exitCode) = await RunRawAsync(startInfo, ct).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new GitCommandException(
                $"git {string.Join(" ", startInfo.ArgumentList)} exited {exitCode}: {ProcessOutput.Tail(stderr)}");
        }

        return stdout;
    }

    private static async Task<(string Stdout, string Stderr, int ExitCode)> RunRawAsync(
        ProcessStartInfo startInfo,
        CancellationToken ct)
    {
        using var process = Process.Start(startInfo)
            ?? throw new GitCommandException(
                $"Failed to start git process for: {string.Join(" ", startInfo.ArgumentList)}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return (stdout, stderr, process.ExitCode);
    }

    /// <summary>
    /// Normalizes a path to the repo-relative, forward-slash form that <c>git diff --cached
    /// --name-only</c> emits so the foreign-staged guard comparison is consistent.
    /// </summary>
    private static string NormalizeGitPath(string path)
        => path.Replace('\\', '/').TrimStart('/');
}
