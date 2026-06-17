using DeckFlow.Core.Integration;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// In-memory test fake for <see cref="IGitRepository"/>.
/// Records StageAndCommitAsync invocations; canned returns for branch/repoRoot/diff/headSeed.
/// Can be configured to throw <see cref="GitForeignStagedChangesException"/> on commit.
/// </summary>
internal sealed class FakeGitRepository : IGitRepository
{
    // ── Canned returns ──────────────────────────────────────────────────────
    public string CannedBranch { get; set; } = "v1.7";
    public string CannedRepoRoot { get; set; } = "/fake/repo";
    public string CannedDiff { get; set; } = "diff --git a/content-kb/seed/index-seed.json b/content-kb/seed/index-seed.json\nindex abc..def 100644\n--- a/content-kb/seed/index-seed.json\n+++ b/content-kb/seed/index-seed.json\n@@ -0,0 +1,5 @@\n+[]\n";
    public string CannedHeadSeed { get; set; } = string.Empty;
    public string CannedCommitSha { get; set; } = "abc1234";

    // ── Fault injection ─────────────────────────────────────────────────────
    /// <summary>When set, StageAndCommitAsync throws this exception instead of succeeding.</summary>
    public Exception? ThrowOnCommit { get; set; }

    // ── Call recording ──────────────────────────────────────────────────────
    public List<(string RepoRoot, IReadOnlyList<string> Paths, string Message)> CommitCalls { get; } = new();

    // ── IGitRepository ──────────────────────────────────────────────────────
    public Task<string> GetCurrentBranchAsync(string repoRoot, CancellationToken ct = default)
        => Task.FromResult(CannedBranch);

    public Task<string> ResolveRepoRootAsync(string startDir, CancellationToken ct = default)
        => Task.FromResult(CannedRepoRoot);

    public Task<string> DiffAsync(string repoRoot, IReadOnlyList<string> paths, CancellationToken ct = default)
        => Task.FromResult(CannedDiff);

    public Task<string> CatHeadSeedAsync(string repoRoot, string seedRelativePath, CancellationToken ct = default)
        => Task.FromResult(CannedHeadSeed);

    public Task<string> StageAndCommitAsync(
        string repoRoot,
        IReadOnlyList<string> paths,
        string message,
        CancellationToken ct = default)
    {
        CommitCalls.Add((repoRoot, paths, message));

        if (ThrowOnCommit is not null)
        {
            throw ThrowOnCommit;
        }

        return Task.FromResult(CannedCommitSha);
    }
}
