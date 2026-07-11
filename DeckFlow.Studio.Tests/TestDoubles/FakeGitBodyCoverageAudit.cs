using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

internal sealed class FakeGitBodyCoverageAudit : IGitBodyCoverageAudit
{
    public GitBodyCoverageReport? CannedReport { get; set; }

    public Exception? ThrowOnRun { get; set; }

    public Task<GitBodyCoverageReport> RunAsync(
        string prodConnectionString,
        string repoRoot,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnRun is not null)
        {
            throw ThrowOnRun;
        }

        return Task.FromResult(CannedReport ?? new GitBodyCoverageReport(Array.Empty<GitBodyCoverageMissingRow>()));
    }
}
