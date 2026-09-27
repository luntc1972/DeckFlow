using DeckFlow.Studio.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Pages;

/// <summary>Code-behind for the read-only production <c>/data</c> coverage audit.</summary>
public partial class DataCoverage
{
    [Inject]
    private IDataCoverageAudit Audit { get; set; } = default!;

    [Inject]
    private IStudioProdConnectionSource ProdConnection { get; set; } = default!;

    [Inject]
    private ILogger<DataCoverage> Logger { get; set; } = default!;

    private bool _isRunning;
    private string _runError = string.Empty;
    private DataCoverageReport? _report;

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
            _report = await Audit.RunAsync(ProdConnection.ConnectionString, Cts.Token);
        }
        catch (OperationCanceledException)
        {
            _runError = "The audit was cancelled.";
        }
        catch (DataCoverageTransportException ex)
        {
            Logger.LogError(ex, "Data coverage audit could not download an artifact over SSH.");
            _runError = DataCoverageTransportException.OperatorMessage;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Data coverage audit failed.");
            _runError = "The audit could not be completed.";
        }
        finally
        {
            _isRunning = false;
            await SafeStateHasChangedAsync();
        }
    }

    private static string FormatBucket(DataCoverageFailureBucket bucket)
        => bucket switch
        {
            DataCoverageFailureBucket.Mismatch => "present + mismatch",
            DataCoverageFailureBucket.Missing => "missing",
            DataCoverageFailureBucket.NoStoredHash => "no stored hash",
            _ => bucket.ToString(),
        };
}
