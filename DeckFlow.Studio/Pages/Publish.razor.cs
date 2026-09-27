using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.AspNetCore.Components;

namespace DeckFlow.Studio.Pages;

/// <summary>Exports approved content to the configured private KB root.</summary>
public partial class Publish
{
    [Inject]
    private PublishCoordinator Coordinator { get; set; } = default!;

    private bool _inFlight;
    private bool _success;
    private string _error = string.Empty;

    private async Task ExportToPrivateRootAsync()
    {
        if (_inFlight)
        {
            return;
        }

        _inFlight = true;
        _success = false;
        _error = string.Empty;
        try
        {
            var init = await Coordinator.LoadInitDataAsync(Cts.Token);
            var progress = new ActionOrchestratorProgress(_ => Task.CompletedTask);
            var result = await Coordinator.ExportToPrivateRootAsync(init.DataRoot, progress, Cts.Token);
            if (result.Status == PublishExportStatus.Success)
            {
                _success = true;
            }
            else
            {
                _error = result.Status == PublishExportStatus.SeedExportFailed
                    ? result.SeedExportMessage
                    : "Could not copy approved content bodies to the private KB root.";
            }
        }
        catch (OperationCanceledException)
        {
            _error = "Export cancelled.";
        }
        catch (Exception exception)
        {
            _error = exception.Message;
        }
        finally
        {
            _inFlight = false;
        }
    }
}
