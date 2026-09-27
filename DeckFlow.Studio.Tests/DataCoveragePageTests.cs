using Bunit;
using DeckFlow.Studio.Pages;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

/// <summary>bUnit tests for the production <c>/data</c> coverage page.</summary>
public sealed class DataCoveragePageTests : BunitContext
{
    [Fact]
    public void RunAudit_AuditThrows_ShowsErrorWithoutReport()
    {
        var audit = new FakeDataCoverageAudit { ExceptionToThrow = new InvalidOperationException("SSH unavailable") };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Studio:ProdConnectionString"] = "Host=example" })
            .Build();

        Services.AddLogging();
        Services.AddSingleton<IDataCoverageAudit>(audit);
        Services.AddSingleton<IStudioProdConnectionSource>(new StudioProdConnectionSource(configuration));
        var cut = Render<DataCoverage>();

        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("The audit could not be completed.", cut.Markup);
            Assert.DoesNotContain("data-coverage-results", cut.Markup);
        });
    }

    [Fact]
    public void RunAudit_RendersCountsAndFailingRows()
    {
        var audit = new FakeDataCoverageAudit
        {
            CannedReport = new DataCoverageReport(
                3,
                1,
                1,
                0,
                1,
                new[]
                {
                    new DataCoverageFailureRow("content-kb/example/mismatch.md", DataCoverageFailureBucket.Mismatch),
                    new DataCoverageFailureRow("content-kb/example/no-hash.md", DataCoverageFailureBucket.NoStoredHash),
                }),
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Studio:ProdConnectionString"] = "Host=example",
            })
            .Build();

        Services.AddLogging();
        Services.AddSingleton<IDataCoverageAudit>(audit);
        Services.AddSingleton<IStudioProdConnectionSource>(new StudioProdConnectionSource(configuration));
        var cut = Render<DataCoverage>();

        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Present + match: 1", cut.Markup);
            Assert.Contains("Present + mismatch: 1", cut.Markup);
            Assert.Contains("Missing: 0", cut.Markup);
            Assert.Contains("No stored hash: 1", cut.Markup);
            Assert.Contains("content-kb/example/mismatch.md", cut.Markup);
            Assert.Contains("no stored hash", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }
}
