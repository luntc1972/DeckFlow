using DeckFlow.Core.Content;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Pages;

/// <summary>
/// Code-behind for the Reconcile page (91-07): a "Run dry-run" action calling
/// <see cref="ReconcileCoordinator.RunDryRunAsync"/> and a results panel grouping discrepancies by
/// class with counts. Renders a "seed unavailable" notice in place of the seed-drift group when
/// <see cref="ReconcileDryRunResult.SeedAvailable"/> is <see langword="false"/> (T-91-28) so an
/// unreadable seed reads as a warning, never phantom drift or a false "no drift". Read-only /
/// detection-only this plan — no Apply action lives here (91-08).
/// </summary>
public partial class Reconcile
{
    // Why: all I/O is delegated to the coordinator so this page is thin UI glue (H1 convention).
    [Inject]
    private ReconcileCoordinator Coordinator { get; set; } = default!;

    // The markup gates the Run button on IsProdConfigured — the orchestrator reads prod exactly
    // once per dry-run.
    [Inject]
    private StudioConfig Config { get; set; } = default!;

    // Why: the UI message stays sanitized (no connection string / path / raw exception), but the
    // full exception MUST be logged server-side or a failed dry-run is undiagnosable.
    [Inject]
    private ILogger<Reconcile> Logger { get; set; } = default!;

    private bool _runInFlight;
    private string _runError = string.Empty;
    private ReconcileDryRunResult? _result;
    private DateTimeOffset? _lastRunUtc;

    private async Task RunDryRunAsync()
    {
        if (_runInFlight || !Config.IsProdConfigured)
        {
            return;
        }

        _runInFlight = true;
        _runError = string.Empty;
        SafeStateHasChanged();

        try
        {
            var result = await Task.Run(
                () => Coordinator.RunDryRunAsync(cancellationToken: Cts.Token),
                Cts.Token);

            _result = result;
            _lastRunUtc = DateTimeOffset.UtcNow;
            _runInFlight = false;
            await SafeStateHasChangedAsync();
        }
        catch (OperationCanceledException)
        {
            _runError = "Dry-run was cancelled.";
            _runInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            // Why: never surface ex.Message (D-07 precedent); log the full detail server-side.
            Logger.LogError(ex, "Reconcile dry-run failed.");
            _runError = "Could not run the reconcile dry-run — check the prod connection and local "
                + "git repo, then try again. Nothing was written. (See the Studio log for details.)";
            _runInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static IReadOnlyList<ContentKbReconcileDiscrepancy> Items(ReconcileDryRunResult result, ContentKbReconcileKind kind)
        => result.Discrepancies.Where(d => d.Kind == kind).ToList();

    // Why: a manual RenderTreeBuilder fragment (rather than a foreach in markup) keeps the four
    // near-identical group cards in Reconcile.razor free of duplicated list/empty-state markup.
    private static RenderFragment RenderDiscrepancyList(IReadOnlyList<ContentKbReconcileDiscrepancy> items) => builder =>
    {
        if (items.Count == 0)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "text-muted small");
            builder.AddContent(2, "none");
            builder.CloseElement();
            return;
        }

        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "class", "small mb-0");
        // Why (ASP0006): reuse the SAME literal sequence numbers on every loop iteration — this is
        // the correct manual-RenderTreeBuilder loop pattern (paired with SetKey for stable identity),
        // not an ever-incrementing counter, which is what the analyzer actually flags.
        foreach (var item in items)
        {
            var label = item.Kind == ContentKbReconcileKind.FileOrphan
                ? item.ArtifactPath
                : $"{item.Title} [{item.NaturalKeyType}:{item.NaturalKeyValue}] ({item.ArtifactPath})";
            builder.OpenElement(2, "li");
            builder.SetKey(item.Id);
            builder.AddContent(3, label);
            builder.CloseElement();
        }

        builder.CloseElement();
    };

    // ── Test seam ───────────────────────────────────────────────────────────
    // Why: exercises RunDryRunAsync's hard-guard directly. bUnit will not dispatch a click to a
    // disabled button, so the config-gate guard is unreachable through the UI in a test.
    internal Task InvokeRunDryRunForTest() => RunDryRunAsync();
}
