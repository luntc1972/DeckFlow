using Bunit;
using DeckFlow.Core.Integration;
using DeckFlow.Studio.Pages;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit behavioral tests for GitBodyCoverage.razor, covering the SYNC-07 pre-flip audit's
/// success, missing-row reporting, and D-07 safe-error behavior.
/// </summary>
public sealed class GitBodyCoveragePageTests : BunitContext
{
    private const string SentinelSecret = "Host=prod-db.example.com;Username=admin;Password=hunter2";

    private static readonly string[] SentinelSubstrings =
    {
        "Host=", "Password", "hunter2", "prod-db.example.com",
    };

    private (IRenderedComponent<GitBodyCoverage> Cut, FakeGitBodyCoverageAudit Audit) RenderPage(
        FakeGitBodyCoverageAudit? auditOverride = null,
        FakeGitRepository? gitOverride = null)
    {
        var audit = auditOverride ?? new FakeGitBodyCoverageAudit();
        var git = gitOverride ?? new FakeGitRepository
        {
            CannedRepoRoot = "/repo/root",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Studio:ProdConnectionString"] = SentinelSecret,
            })
            .Build();

        Services.AddLogging();
        Services.AddSingleton<IGitBodyCoverageAudit>(audit);
        Services.AddSingleton<IGitRepository>(git);
        Services.AddSingleton<IStudioProdConnectionSource>(new StudioProdConnectionSource(configuration));

        var cut = Render<GitBodyCoverage>();
        return (cut, audit);
    }

    [Fact]
    public void RunAudit_WhenNoRowsMissing_RendersSuccessAlert()
    {
        var audit = new FakeGitBodyCoverageAudit
        {
            CannedReport = new GitBodyCoverageReport(Array.Empty<GitBodyCoverageMissingRow>()),
        };

        var (cut, _) = RenderPage(audit);

        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("All approved+visible production bodies are present in the git tree.", cut.Markup);
            Assert.Contains("SYNC-07 precondition is satisfied", cut.Markup);
        });
    }

    [Fact]
    public void RunAudit_WhenRowsAreMissing_RendersDangerAlertAndTable()
    {
        var audit = new FakeGitBodyCoverageAudit
        {
            CannedReport = new GitBodyCoverageReport(
                new[]
                {
                    new GitBodyCoverageMissingRow(
                        "youtube_channel",
                        "abc123",
                        "Missing Title",
                        "content-kb/test-channel/abc123.md"),
                }),
        };

        var (cut, _) = RenderPage(audit);

        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForAssertion(() =>
        {
            // The danger sentence wraps across a source line in the .razor, so assert only the
            // contiguous leading run (count + phrase up to the line break) to stay whitespace-robust.
            Assert.Contains("1 approved+visible row(s) have no body in the git", cut.Markup);
            Assert.Contains("Missing Title", cut.Markup);
            Assert.Contains("content-kb/test-channel/abc123.md", cut.Markup);
            Assert.Contains("youtube_channel:abc123", cut.Markup);
        });
    }

    [Fact]
    public void RunAudit_WhenAuditThrows_RendersSafeErrorWithoutSecretLeak()
    {
        var audit = new FakeGitBodyCoverageAudit
        {
            ThrowOnRun = new InvalidOperationException($"bad connection: {SentinelSecret}"),
        };

        var (cut, _) = RenderPage(audit);

        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForAssertion(() => Assert.Contains("The audit could not be completed.", cut.Markup));

        foreach (var substring in SentinelSubstrings)
        {
            Assert.DoesNotContain(substring, cut.Markup, StringComparison.Ordinal);
        }
    }
}
