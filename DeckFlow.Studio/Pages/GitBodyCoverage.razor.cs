using DeckFlow.Core.Integration;
using DeckFlow.Studio.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Pages;

/// <summary>
/// Code-behind for the Git Body Coverage page: a read-only SYNC-07 pre-flip audit that reads
/// approved+visible production rows and checks whether each body's expected <c>.md</c> exists in
/// the local git tree that becomes <c>/app</c> after deploy.
/// </summary>
public partial class GitBodyCoverage
{
    [Inject]
    private IGitBodyCoverageAudit Audit { get; set; } = default!;

    [Inject]
    private IGitRepository Git { get; set; } = default!;

    [Inject]
    private IStudioProdConnectionSource ProdConnection { get; set; } = default!;

    // Why: the UI error stays sanitized (D-07), but the full exception still needs to be logged
    // server-side so a failed audit is diagnosable.
    [Inject]
    private ILogger<GitBodyCoverage> Logger { get; set; } = default!;

    private bool _isRunning;
    private string _runError = string.Empty;
    private GitBodyCoverageReport? _report;

    private async Task RunAuditAsync()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _runError = string.Empty;
        _report = null;
        SafeStateHasChanged();

        try
        {
            var repoRoot = await Git.ResolveRepoRootAsync(StudioRepoLocator.ResolveStartDirectory(), Cts.Token);
            var connectionString = ProdConnection.ConnectionString;
            var report = await Audit.RunAsync(connectionString, repoRoot, Cts.Token);

            _report = report;
        }
        catch (OperationCanceledException)
        {
            _runError = "The audit was cancelled.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Git body coverage audit failed.");
            _runError = "The audit could not be completed.";
        }
        finally
        {
            _isRunning = false;
            await SafeStateHasChangedAsync();
        }
    }

    private static string FormatNaturalKey(GitBodyCoverageMissingRow row)
        => string.IsNullOrEmpty(row.NaturalKeyType) && string.IsNullOrEmpty(row.NaturalKeyValue)
            ? "\u2014"
            : $"{row.NaturalKeyType}:{row.NaturalKeyValue}";
}
