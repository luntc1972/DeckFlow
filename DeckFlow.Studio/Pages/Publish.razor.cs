using DeckFlow.Core.Content;
using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Integration;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Pages;

/// <summary>Exports approved content to the configured private KB root.</summary>
public partial class Publish
{
    [Inject]
    private PublishCoordinator Coordinator { get; set; } = default!;

    [Inject]
    private ILogger<Publish> Logger { get; set; } = default!;

    private bool _inFlight;
    private bool _loading = true;
    private bool _success;
    private int _approvedCount;
    private IReadOnlyList<(PublishState State, int Count)> _publishStateSummary = Array.Empty<(PublishState State, int Count)>();
    private string _error = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var init = await Coordinator.LoadInitDataAsync(Cts.Token);
            _approvedCount = init.ApprovedCount;
            _publishStateSummary = init.StateSummary;
        }
        catch (OperationCanceledException)
        {
            _error = "Loading cancelled.";
        }
        catch (Exception exception)
        {
            SetUnexpectedError(exception);
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ExportToPrivateRootAsync()
    {
        if (_inFlight || _approvedCount == 0)
        {
            return;
        }

        _inFlight = true;
        _success = false;
        _error = string.Empty;
        try
        {
            var init = await Coordinator.LoadInitDataAsync(Cts.Token);
            if (init.ApprovedCount == 0)
            {
                _approvedCount = init.ApprovedCount;
                _publishStateSummary = init.StateSummary;
                _error = "Nothing is approved for export.";
                return;
            }

            var progress = new ActionOrchestratorProgress(_ => Task.CompletedTask);
            var result = await Coordinator.ExportToPrivateRootAsync(init.DataRoot, progress, Cts.Token);
            if (result.Status == PublishExportStatus.Success)
            {
                _success = true;
            }
            else
            {
                _error = result.Status == PublishExportStatus.SeedExportFailed
                    ? $"Seed export failed — {result.SeedExportMessage}"
                    : "Could not copy approved content bodies to the private KB root.";
            }
        }
        catch (OperationCanceledException)
        {
            _error = "Export cancelled.";
        }
        catch (Exception exception)
        {
            SetUnexpectedError(exception);
        }
        finally
        {
            _inFlight = false;
        }
    }

    private void SetUnexpectedError(Exception exception)
    {
        Logger.LogError(exception, "Private KB root export failed.");
        _error = exception is InvalidOperationException
            && exception.Message.Contains(PrivateKbRoot.EnvironmentVariableName, StringComparison.Ordinal)
            ? exception.Message
            : "Export failed — check the Studio logs and retry.";
    }
}
