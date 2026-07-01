using DeckFlow.Core.Content;
using DeckFlow.Studio.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Pages;

/// <summary>
/// Code-behind for the Pull-from-Production page. The read-only prod pull (prod read, local
/// git-tree body resolution, local classify) and the local-only adopt apply live in
/// <see cref="PullFromProdCoordinator"/> (H1 split); this page keeps the progress log, resolution
/// map, busy guards, sanitized top-level error copy, cancellation, and re-render marshalling. The
/// page never writes to production. Behavior is identical to the prior inline implementation.
/// </summary>
public partial class PullFromProd
{
    // ── Injected services ───────────────────────────────────────────────────
    // Why: all prod read / git body resolution / store I/O is delegated to the coordinator so this page is thin UI
    // glue and the pull/apply sequences are unit-testable without bUnit (H1).
    [Inject]
    private PullFromProdCoordinator Coordinator { get; set; } = default!;

    // Config stays injected here: the markup gates the Pull button on IsProdConfigured.
    [Inject]
    private StudioConfig Config { get; set; } = default!;

    // Why: the UI message is sanitized (D-07) so the operator never sees secrets, but the full
    // exception MUST be logged server-side or a failed pull is undiagnosable. Logged to the Serilog
    // file sink only — never rendered.
    [Inject]
    private ILogger<PullFromProd> Logger { get; set; } = default!;

    // ── Init state ──────────────────────────────────────────────────────────
    private bool _initInFlight = true;
    private string? _initError;

    // dataRoot = parent of ArtifactRoot = {studioDataDirectory}; ArtifactPath already carries
    // content-kb/, so the data root is the correct base for both staging and the live tree.
    private string _dataRoot = string.Empty;
    private string _stagingRoot = string.Empty;

    // ── Shared in-flight guard ──────────────────────────────────────────────
    private bool _operationInFlight;

    // ── Stage 1 — pull & classify ───────────────────────────────────────────
    private bool _pullInFlight;
    private string _pullError = string.Empty;
    private string _pullStage = string.Empty;
    private bool _diffReady;
    private List<SyncDiffEntry> _diffEntries = new();
    private readonly Dictionary<string, Resolution> _resolutions = new(StringComparer.Ordinal);

    // ── Progress log (live console view, SUI-03) ────────────────────────────
    // Why: bounded to last ProgressLogMaxLines so an enormous pull (many artifacts) never grows
    // unbounded — DoS mitigation (T-62-06). Shown while a pull runs and after.
    private const int ProgressLogMaxLines = 500;
    private List<string> _progressLog = new();

    // ── Stage 2 — resolve ───────────────────────────────────────────────────
    private bool _applyInFlight;
    private bool _applySuccess;
    private string _applyError = string.Empty;
    private List<PullApplyRowResult> _rowResults = new();

    private enum Resolution
    {
        None,
        AdoptProd,
        KeepLocal,
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var paths = await Task.Run(() => Coordinator.ResolvePaths(), Cts.Token);
            _dataRoot = paths.DataRoot;
            _stagingRoot = paths.StagingRoot;
        }
        catch (OperationCanceledException)
        {
            // Component disposed mid-load — swallow.
        }
        catch (Exception ex)
        {
            // Why: never include any secret value in the init error (D-07); log full detail server-side.
            Logger.LogError(ex, "Pull-from-prod page initialization failed.");
            _initError = "Initialization failed — check Studio configuration and retry.";
        }
        finally
        {
            _initInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Stage 1: Pull & Classify ────────────────────────────────────────────
    private async Task PullAndClassifyAsync()
    {
        if (_operationInFlight || !Config.IsProdConfigured)
        {
            return;
        }

        _operationInFlight = true;
        _pullInFlight = true;
        _pullError = string.Empty;
        _diffReady = false;
        _diffEntries = new();
        _progressLog = new();
        _resolutions.Clear();
        _applySuccess = false;
        _applyError = string.Empty;
        _rowResults = new();

        // Why: Progress<T> created here captures the Blazor sync context, so its log callbacks marshal
        // back automatically — no manual InvokeAsync needed for each line (Codex MEDIUM).
        var log = new Progress<string>(line =>
        {
            AppendProgressLine(line);
            SafeStateHasChanged();
        });
        // Why: stage is a SYNCHRONOUS callback (not Progress<T>) so a fault in the background task
        // reads the exact stage in flight — Progress<T> posts asynchronously and could leave
        // _pullStage one stage stale in the failure copy (Codex MED). It is a plain string field set
        // on the background thread and read only in the catch, matching the original inline behavior.
        Action<string> onStage = stage => _pullStage = stage;

        try
        {
            var entries = await Task.Run(
                () => Coordinator.PullAndClassifyAsync(_stagingRoot, log, onStage, Cts.Token),
                Cts.Token);

            _diffEntries = entries.ToList();
            _diffReady = true;
            _pullInFlight = false;
            _operationInFlight = false;
            await SafeStateHasChangedAsync();
        }
        catch (OperationCanceledException)
        {
            _pullError = "Pull was cancelled.";
            _pullInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(() =>
            {
                AppendProgressLine("Pull cancelled.");
                SafeStateHasChanged();
            });
        }
        catch (Exception ex)
        {
            // Why: an Npgsql or git exception can carry host/db/user/path — NEVER surface ex.Message
            // in the UI (D-07). But DO log the full exception server-side (Serilog file sink) with the
            // failing stage so a failed pull is diagnosable; the operator reads the log, not the page.
            Logger.LogError(ex, "Pull from prod failed during stage: {PullStage}.", _pullStage);
            _pullError = $"Could not pull from production while trying to {_pullStage} — check the prod connection and local git repo, then try again. Nothing was written. (See the Studio log for details.)";
            _pullInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(() =>
            {
                AppendProgressLine($"Pull failed during: {_pullStage} — see the Studio log for details.");
                SafeStateHasChanged();
            });
        }
    }

    // ── Stage 2: Apply Resolutions (LOCAL store only) ───────────────────────
    private async Task ApplyResolutionsAsync()
    {
        // Why: hard-guard before any write — a stale render, test invocation, or future refactor
        // must never reach the apply before a classify produced entries (mirror DirectPush).
        if (!_diffReady || _operationInFlight)
        {
            return;
        }

        _operationInFlight = true;
        _applyInFlight = true;
        _applyError = string.Empty;
        _applySuccess = false;
        _rowResults = new();

        // Adopt set = entries the operator chose to adopt that carry a prod row (LocalOnly is
        // display-only — never adoptable). The coordinator applies each to the LOCAL store only.
        var adoptEntries = _diffEntries
            .Where(e => GetResolution(e) == Resolution.AdoptProd
                && e.Kind != SyncDiffKind.LocalOnly
                && e.ProdRow is not null)
            .ToList();

        // Progress<T> captures the sync context: each per-entry snapshot re-renders incrementally.
        var progress = new Progress<IReadOnlyList<PullApplyRowResult>>(snapshot =>
        {
            _rowResults = snapshot.ToList();
            SafeStateHasChanged();
        });

        try
        {
            var results = await Task.Run(
                () => Coordinator.ApplyAdoptionsAsync(adoptEntries, _stagingRoot, _dataRoot, progress, Cts.Token),
                Cts.Token);

            _rowResults = results.ToList();
            _applySuccess = results.All(r => r.Success);
            if (!_applySuccess)
            {
                _applyError = "Some resolutions failed — see the per-entry list below. Production was not modified.";
            }

            _applyInFlight = false;
            _operationInFlight = false;
            await SafeStateHasChangedAsync();
        }
        catch (OperationCanceledException)
        {
            _applyError = "Apply was cancelled.";
            _applyInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            // Why: never surface ex.Message (D-07); log the full detail server-side for diagnosis.
            Logger.LogError(ex, "Applying pull-from-prod resolutions failed.");
            _applyError = "Applying resolutions failed — check Studio configuration and try again. Production was not modified. (See the Studio log for details.)";
            _applyInFlight = false;
            _operationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Resolution helpers ──────────────────────────────────────────────────
    private static string EntryKey(SyncDiffEntry entry) => $"{entry.NaturalKeyType}:{entry.NaturalKeyValue}";

    private Resolution GetResolution(SyncDiffEntry entry) =>
        _resolutions.TryGetValue(EntryKey(entry), out var r) ? r : Resolution.None;

    private void SetResolution(SyncDiffEntry entry, Resolution resolution) =>
        _resolutions[EntryKey(entry)] = resolution;

    private bool AnyResolutionChosen() => _resolutions.Values.Any(r => r != Resolution.None);

    // ── Progress log helpers (SUI-03 / T-62-04) ────────────────────────────
    /// <summary>
    /// Appends a line to the progress log on the Blazor sync context. Caps the list to the last
    /// <see cref="ProgressLogMaxLines"/> lines (DoS guard T-62-06).
    /// </summary>
    private void AppendProgressLine(string line)
    {
        _progressLog.Add(line);
        if (_progressLog.Count > ProgressLogMaxLines)
        {
            _progressLog.RemoveAt(0);
        }
    }

    // ── Test seam ───────────────────────────────────────────────────────────
    // Why: exercises the ApplyResolutionsAsync hard-guard directly. bUnit will not dispatch a click
    // to a disabled button, so the guard (which protects against a stale render / future refactor
    // reaching the local apply before classify) is unreachable through the UI in a test.
    internal Task InvokePullApplyForTest() => ApplyResolutionsAsync();
}
