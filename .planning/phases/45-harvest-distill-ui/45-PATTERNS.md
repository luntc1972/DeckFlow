# Phase 45: Harvest + Distill UI - Pattern Map

**Mapped:** 2026-06-15
**Files analyzed:** 7 new/modified files
**Analogs found:** 6 / 7 (1 has no repo analog — Blazor background-task page; use RESEARCH.md canonical pattern)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Studio/Pages/Harvest.razor` | Blazor page | event-driven + streaming progress | `DeckFlow.Studio/Pages/Home.razor` | partial (same host; no background-task page exists in repo) |
| `DeckFlow.Studio/Shared/NavMenu.razor` | layout/nav | n/a | `DeckFlow.Studio/Shared/NavMenu.razor` (self) | modify-in-place |
| `DeckFlow.Studio/Program.cs` | DI composition | n/a | `DeckFlow.Studio/Program.cs` (self) | modify-in-place |
| `DeckFlow.Studio/StudioDistillConfig.cs` | config record | n/a | `DeckFlow.Studio/StudioConfig.cs` | exact |
| `DeckFlow.Studio/SessionCapOverride.cs` | singleton state | n/a | `DeckFlow.Studio/StudioConfig.cs` | role-match |
| `DeckFlow.Core/Content/ILlmSpendLedger.cs` | Core interface | n/a | `DeckFlow.Core/Content/ILlmSpendLedger.cs` (self) | modify-in-place |
| `DeckFlow.Core/Content/SpendLedgerBase.cs` | Core abstract class | n/a | `DeckFlow.Core/Content/SpendLedgerBase.cs` (self) | modify-in-place |
| `DeckFlow.Core.Tests/LlmSpendLedgerTests.cs` | xUnit test | n/a | `DeckFlow.Core.Tests/LlmSpendLedgerTests.cs` (self) | modify-in-place |

---

## Pattern Assignments

### `DeckFlow.Studio/Pages/Harvest.razor` (Blazor page, event-driven + streaming progress)

**Analog:** `DeckFlow.Studio/Pages/Home.razor` for page skeleton; no repo analog for background-task + live progress pattern.

**No background-task page exists in the Studio repo.** The closest Studio page (`Home.razor`) has no async button handlers or progress sinks. Use the RESEARCH.md canonical pattern (Pattern 1) as the primary template.

**Page skeleton pattern** (`DeckFlow.Studio/Pages/Home.razor`, lines 1-19):
```razor
@page "/"

<PageTitle>DeckFlow Studio</PageTitle>

<h1>DeckFlow Studio</h1>

@code {
    [Inject]
    private StudioConfig Config { get; set; } = default!;

    private bool _isProdConfigured;

    protected override void OnInitialized()
    {
        _isProdConfigured = Config.IsProdConfigured;
    }
}
```
Replicate: `@page "/harvest"`, `<PageTitle>Harvest + Distill</PageTitle>`, `[Inject]` for all DI dependencies.

**Inject pattern — multiple services injected via `[Inject]` property:**
```razor
@code {
    [Inject] private IHarvestOrchestrator HarvestOrchestrator { get; set; } = default!;
    [Inject] private IDistillOrchestrator DistillOrchestrator { get; set; } = default!;
    [Inject] private IYouTubeChannelVideoLister Lister { get; set; } = default!;
    [Inject] private IContentVideoStore VideoStore { get; set; } = default!;
    [Inject] private IContentSiteIndexStore IndexStore { get; set; } = default!;
    [Inject] private IBlockedVideoStore BlockedStore { get; set; } = default!;
    [Inject] private ILlmSpendLedger SpendLedger { get; set; } = default!;
    [Inject] private StudioDistillConfig DistillConfig { get; set; } = default!;
    [Inject] private SessionCapOverride CapOverride { get; set; } = default!;
}
```

**Background-task + live progress pattern** (from RESEARCH.md Pattern 1 — no repo analog):
```csharp
// In Harvest.razor @code section:
// Implements IDisposable — add @implements IDisposable at top of file.

private CancellationTokenSource? _cts;
private bool _operationInFlight;
private List<string> _logLines = new();

private async Task HarvestSelectedAsync()
{
    if (_operationInFlight) return;
    _operationInFlight = true;
    _cts = new CancellationTokenSource();
    _logLines.Clear();

    var progress = new ActionOrchestratorProgress(async msg =>
    {
        _logLines.Add(msg);
        await InvokeAsync(StateHasChanged);
    });

    _harvestResult = null;
    try
    {
        // Why: Task.Run moves the orchestrator off the Blazor sync context.
        // Without this, long-running IO blocks the SignalR circuit (Pitfall 7).
        _harvestResult = await Task.Run(
            () => HarvestOrchestrator.HarvestAsync(
                limit: _selectedVideoIds.Count,
                videoIds: _selectedVideoIds,
                progress: progress,
                cancellationToken: _cts.Token),
            _cts.Token);
    }
    catch (OperationCanceledException)
    {
        _logLines.Add("Harvest cancelled.");
    }
    finally
    {
        _operationInFlight = false;
        await InvokeAsync(StateHasChanged);
    }
}

public void Dispose()
{
    // Why: CTS disposed on component disposal so a circuit drop cancels in-flight ops (D-06).
    _cts?.Cancel();
    _cts?.Dispose();
}
```

**Progress bridge class** (Studio-local, can be a sealed class at bottom of @code block or a separate file):
```csharp
internal sealed class ActionOrchestratorProgress : IOrchestratorProgress
{
    private readonly Func<string, Task> _sink;

    internal ActionOrchestratorProgress(Func<string, Task> sink) => _sink = sink;

    public void Report(string message)
    {
        // Why: Report is void by design; fire-and-forget the async StateHasChanged bridge.
        _ = _sink(message);
    }
}
```

**Progress log markup** (from UI-SPEC Section 3 — no markup analog in repo):
```html
<pre class="bg-light border rounded p-2"
     style="height:200px; overflow-y:auto; font-size:0.8125rem; font-family:monospace"
     role="log"
     aria-live="polite">@string.Join("\n", _logLines)</pre>
```

**Spinner markup pattern** (from UI-SPEC):
```html
<div class="spinner-border spinner-border-sm text-primary"
     role="status"
     aria-label="Operation in progress">
    <span class="visually-hidden">Loading...</span>
</div>
```

**NavMenu icon pattern** (from `NavMenu.razor` line 13-16 — replicate for "Harvest"):
```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="" Match="NavLinkMatch.All">
        <span class="oi oi-home" aria-hidden="true"></span> Home
    </NavLink>
</div>
```
For Harvest: `href="harvest"`, icon `oi oi-cloud-download`, label `Harvest`.

---

### `DeckFlow.Studio/Shared/NavMenu.razor` (layout/nav, modify-in-place)

**Analog:** `DeckFlow.Studio/Shared/NavMenu.razor` (self, lines 13-16)

**Existing nav entry to replicate** (lines 12-16):
```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="" Match="NavLinkMatch.All">
        <span class="oi oi-home" aria-hidden="true"></span> Home
    </NavLink>
</div>
```

**Add directly below** (no `Match="NavLinkMatch.All"` on non-root pages):
```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="harvest">
        <span class="oi oi-cloud-download" aria-hidden="true"></span> Harvest
    </NavLink>
</div>
```

---

### `DeckFlow.Studio/Program.cs` (DI composition, modify-in-place)

**Analog:** `DeckFlow.Studio/Program.cs` (self)

**Existing singleton registration pattern** (lines 46-56 — replicate style for new registrations):
```csharp
builder.Services.AddSingleton(new StudioConfig(isProdConfigured));
builder.Services.AddSingleton<IContentSourceStore>(_ => new ContentSourceStore(contentKbDatabasePath));
builder.Services.AddSingleton<ILlmSpendLedger>(_ => new LlmSpendLedger(contentKbDatabasePath));
```

**Replace the `ILlmSpendLedger` line** (currently line 52) with the session-override wiring:
```csharp
// Why: SessionCapOverride registered first so the resolver closure can capture the reference.
// The same singleton ledger instance is injected into both the Harvest page and the orchestrator,
// so the override is seen by WouldExceedCapAsync inside DistillOrchestrator (D-03 / Pitfall 6).
var capOverride = new SessionCapOverride();
builder.Services.AddSingleton(capOverride);
builder.Services.AddSingleton<ILlmSpendLedger>(_ => new LlmSpendLedger(contentKbDatabasePath,
    key =>
    {
        if (key == "DECKFLOW_LLM_MONTHLY_CAP_USD" && capOverride.OverrideUsd.HasValue)
            return capOverride.OverrideUsd.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        return null;
    }));
```

**isSubscriptionProvider singleton** (add after the above, before `AddContentKbOrchestrator()`):
```csharp
// Why: Derive isSubscriptionProvider host-side at startup so the Harvest page reads a resolved
// bool and cannot misconfigure it per D-01. Replicates CLI derivation from
// DeckFlow.CLI/ContentKbCommandRunners.cs lines 95-97.
var providerEnv = builder.Configuration["DECKFLOW_LLM_PROVIDER"]
    ?? Environment.GetEnvironmentVariable(LlmDistillationProviderFactory.EnvironmentVariableName);
var isSubscriptionProvider = !string.IsNullOrWhiteSpace(providerEnv)
    && !string.Equals(providerEnv.Trim(), "openai", StringComparison.OrdinalIgnoreCase);
builder.Services.AddSingleton(new StudioDistillConfig(isSubscriptionProvider));
```

**Using directives to add** (follow existing file-scoped namespace order):
```csharp
using System.Globalization;
using DeckFlow.Core.Integration; // for LlmDistillationProviderFactory
```

---

### `DeckFlow.Studio/StudioDistillConfig.cs` (config record, new file)

**Analog:** `DeckFlow.Studio/StudioConfig.cs` (lines 1-6) — exact same sealed record pattern:
```csharp
namespace DeckFlow.Studio;

/// <summary>
/// Indicates whether the production Studio connection has been configured.
/// </summary>
public sealed record StudioConfig(bool IsProdConfigured);
```

**Copy pattern exactly:**
```csharp
namespace DeckFlow.Studio;

/// <summary>
/// Resolved at startup; indicates whether the wired LLM distillation backend is a
/// subscription provider (claude-CLI, $0 marginal cost) or a metered provider (OpenAI).
/// </summary>
public sealed record StudioDistillConfig(bool IsSubscriptionProvider);
```

---

### `DeckFlow.Studio/SessionCapOverride.cs` (singleton state, new file)

**Analog:** `DeckFlow.Studio/StudioConfig.cs` for file structure; class (not record) because it has mutable state.

**Pattern:**
```csharp
namespace DeckFlow.Studio;

/// <summary>
/// In-memory, session-scoped monthly cap override for the LLM spend ledger.
/// The operator can raise the cap from the Harvest page for the current Studio session only.
/// Resets to the environment/default ($15.00) on Studio restart (D-03).
/// </summary>
public sealed class SessionCapOverride
{
    /// <summary>
    /// Operator-raised monthly cap in USD. When <see langword="null"/>, the ledger uses
    /// the <c>DECKFLOW_LLM_MONTHLY_CAP_USD</c> environment variable or the $15.00 default.
    /// </summary>
    public decimal? OverrideUsd { get; set; }
}
```

---

### `DeckFlow.Core/Content/ILlmSpendLedger.cs` (Core interface, modify-in-place)

**Analog:** `DeckFlow.Core/Content/ILlmSpendLedger.cs` (self)

**Existing interface members** (lines 17-44 — note style: XML doc on every member, same param/returns pattern):
```csharp
Task RecordCallAsync(long videoId, int inputTokens, int outputTokens,
    decimal costUsd, string monthKey, CancellationToken cancellationToken = default);

Task<decimal> GetMonthlyTotalAsync(string yearMonth,
    CancellationToken cancellationToken = default);

Task<bool> WouldExceedCapAsync(decimal projectedCallCostUsd, string monthKey,
    CancellationToken cancellationToken = default);
```

**Add one new member** (append after `WouldExceedCapAsync`, before the closing `}`):
```csharp
/// <summary>
/// Returns the configured monthly USD cap for LLM spend, accounting for any
/// session-level override registered in the host composition root.
/// </summary>
/// <returns>Monthly cap in USD (defaults to $15.00 when not configured).</returns>
decimal GetMonthlyCapUsd();
```

---

### `DeckFlow.Core/Content/SpendLedgerBase.cs` (Core abstract class, modify-in-place)

**Analog:** `DeckFlow.Core/Content/SpendLedgerBase.cs` (self)

**Target: `ReadMonthlyCapUsd()`** (lines 135-151) — currently `private`:
```csharp
private decimal ReadMonthlyCapUsd()
{
    var configured = _configurationValueResolver?.Invoke(MonthlyCapConfigurationKey);
    if (string.IsNullOrWhiteSpace(configured))
    {
        configured = Environment.GetEnvironmentVariable(MonthlyCapConfigurationKey);
    }

    if (!string.IsNullOrWhiteSpace(configured)
        && decimal.TryParse(configured, NumberStyles.Number, CultureInfo.InvariantCulture, out var cap)
        && cap >= 0m)
    {
        return cap;
    }

    return DefaultMonthlyCapUsd;
}
```

**Change:** Promote to `protected` (visibility only — no logic change):
```csharp
protected decimal ReadMonthlyCapUsd()  // was: private
```

**Add a public implementation method** on `SpendLedgerBase` (not the interface — derived classes inherit it):
```csharp
/// <inheritdoc />
public decimal GetMonthlyCapUsd() => ReadMonthlyCapUsd();
```

This satisfies the new `ILlmSpendLedger.GetMonthlyCapUsd()` contract for all concrete ledger classes (`LlmSpendLedger`, `WhisperSpendLedger`) without touching their files.

---

### `DeckFlow.Core.Tests/LlmSpendLedgerTests.cs` (xUnit test, modify-in-place)

**Analog:** `DeckFlow.Core.Tests/LlmSpendLedgerTests.cs` (self)

**Existing test structure to replicate** (lines 1-122):
- `public sealed class LlmSpendLedgerTests : IDisposable` — sealed, implements `IDisposable`
- Constructor: `_dbPath = Path.Combine(Path.GetTempPath(), $"llm-spend-test-{Guid.NewGuid():N}.db")` — temp file isolation
- Dispose: `SqliteConnection.ClearAllPools(); GC.Collect(); GC.WaitForPendingFinalizers(); File.Delete(_dbPath)` — SQLite pool flush before delete
- `[Fact]` per test — no `[Theory]`
- Helper `BuildConfiguration(string capUsd)` (line 120-121): `=> key => key == "DECKFLOW_LLM_MONTHLY_CAP_USD" ? capUsd : null` — reuse for new tests

**Naming convention** (lines 38-100): `Method_Scenario_ExpectedResult`:
- `EnsureSchemaAsync_IsIdempotent`
- `GetMonthlyTotalAsync_SumsRecordedLlmCostsWithExactDecimal`
- `WouldExceedCapAsync_ReturnsFalseWhenProjectedCostIsUnderCap`

**Two new tests to add** (append after existing `[Fact]` methods, before `InsertVideoAsync`):

```csharp
[Fact]
public void GetMonthlyCapUsd_ReturnsDefaultWhenNoConfigurationSet()
{
    // _ledger was constructed with no configurationValueResolver and no env var set
    var cap = _ledger.GetMonthlyCapUsd();

    Assert.Equal(15.00m, cap);
}

[Fact]
public void GetMonthlyCapUsd_ReturnsOverrideValueWhenConfigurationResolverProvided()
{
    var ledger = new LlmSpendLedger(_dbPath, BuildConfiguration("25.00"));

    var cap = ledger.GetMonthlyCapUsd();

    Assert.Equal(25.00m, cap);
}
```

---

## Shared Patterns

### Blazor `[Inject]` Property Pattern
**Source:** `DeckFlow.Studio/Pages/Home.razor` lines 9-12
**Apply to:** `Harvest.razor`
```razor
[Inject]
private StudioConfig Config { get; set; } = default!;
```
Use `[Inject]` attribute (not `@inject` directive) for all injected services in the @code block. Use `default!` to satisfy nullable analysis.

### Sealed Record for Config/Payload Types
**Source:** `DeckFlow.Studio/StudioConfig.cs` lines 1-6
**Apply to:** `StudioDistillConfig.cs`
```csharp
public sealed record StudioConfig(bool IsProdConfigured);
```
One-line primary constructor record. XML doc on the type. File-scoped namespace. No `using` directives needed for primitive-only records.

### Singleton DI Factory Lambda
**Source:** `DeckFlow.Studio/Program.cs` lines 47-56
**Apply to:** New `ILlmSpendLedger` registration with `SessionCapOverride` closure
```csharp
builder.Services.AddSingleton<IContentSourceStore>(_ => new ContentSourceStore(contentKbDatabasePath));
builder.Services.AddSingleton<ILlmSpendLedger>(_ => new LlmSpendLedger(contentKbDatabasePath));
```
Lambda form `_ => new Concrete(...)` is the established pattern for singletons that need constructor args unavailable in DI. The `_` parameter is the `IServiceProvider` (unused when args are captured from outer scope).

### xUnit Temp-File Isolation
**Source:** `DeckFlow.Core.Tests/LlmSpendLedgerTests.cs` lines 20-24 and 29-36
**Apply to:** New `GetMonthlyCapUsd` tests (reuse the existing `_ledger` fixture — no new temp file needed)
```csharp
_dbPath = Path.Combine(Path.GetTempPath(), $"llm-spend-test-{Guid.NewGuid():N}.db");
_ledger = new LlmSpendLedger(_dbPath);

// Dispose:
SqliteConnection.ClearAllPools();
GC.Collect();
GC.WaitForPendingFinalizers();
File.Delete(_dbPath);
```

### ConfigurationValueResolver Helper
**Source:** `DeckFlow.Core.Tests/LlmSpendLedgerTests.cs` lines 120-121
**Apply to:** New `GetMonthlyCapUsd` tests
```csharp
private static Func<string, string?> BuildConfiguration(string capUsd)
    => key => key == "DECKFLOW_LLM_MONTHLY_CAP_USD" ? capUsd : null;
```
Reuse this existing helper — do not duplicate.

---

## No Analog Found

| File / Capability | Role | Data Flow | Reason |
|-------------------|------|-----------|--------|
| Background-task + live progress in a Blazor page | Blazor page | event-driven + streaming | No Studio page has a long-running button handler with `Task.Run` + `IOrchestratorProgress` + `InvokeAsync(StateHasChanged)` + `IDisposable` CTS disposal. Use RESEARCH.md Pattern 1 as the canonical reference. |
| `ActionOrchestratorProgress` local adapter | utility class | n/a | No `IOrchestratorProgress` consumer exists outside of Core and the CLI. The Studio needs a fire-and-forget bridge. Define inline in `Harvest.razor` @code block or as a separate file in `DeckFlow.Studio/`. |

---

## Metadata

**Analog search scope:** `DeckFlow.Studio/`, `DeckFlow.Core/Content/`, `DeckFlow.Core.Tests/`
**Files scanned:** 10 source files read directly
**Pattern extraction date:** 2026-06-15
