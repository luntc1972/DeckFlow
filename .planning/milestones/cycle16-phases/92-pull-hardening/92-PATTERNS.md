# Phase 92: Pull Hardening - Pattern Map

**Mapped:** 2026-07-10
**Files analyzed:** 8 (2 new capabilities on existing files; 0 brand-new files)
**Analogs found:** 8 / 8 (all in-repo; every analog is a real, already-shipped file in this same codebase)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `DeckFlow.Core/Integration/IGitRepository.cs` (add `FetchAsync`, `GetBehindCountAsync`) | interface / utility | request-response (process shell-out) | `GetSubjectsAheadOfRemoteAsync` member on the same interface (lines 136-160) + `IProdContentReader.cs:44-64` throwing-DIM idiom | exact |
| `DeckFlow.Core/Integration/GitRepository.cs` (implement the two new members) | service (process adapter) | request-response (shell-out + parse) | `GetSubjectsAheadOfRemoteAsync` impl (`GitRepository.cs:260-285`) + `PushAsync` (`GitRepository.cs:207-225`) | exact |
| `DeckFlow.Core/Content/SyncDiffEntry.cs` (add `BodyDivergenceStatus` enum + `BodyDivergence` property) | model | transform (pure data stamp) | `ArtifactDownloaded` property + its doc comment (`SyncDiffEntry.cs:68-72`) | exact |
| `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` (`PullAndClassifyAsync` staleness pre-check + divergence stamping; `ApplyAdoptionsAsync` defense-in-depth exclusion) | service / coordinator (orchestration) | request-response, CRUD (read prod → classify → write local) | itself, prior shape (`PullFromProdCoordinator.cs:71-127` classify loop, `137-217` apply loop) + `ContentKbReconcileClassifier.cs:101-109` body-hash pattern (with D-01a's opposite null handling) + `DirectPushCoordinator.cs:429-452` no-fetch/fail-closed precedent | exact (same file, extend existing shape) |
| `DeckFlow.Studio/Pages/PullFromProd.razor` + `.razor.cs` (freshness banner, divergence badge, per-entry opt-in, adopt pre-filter change) | component (Blazor Server page) | request-response (render + click-driven mutation) | itself, prior shape (`PullFromProd.razor:107-208` diff table + Kind badge switch; `PullFromProd.razor.cs:186-192` adopt-set filter) + `Reconcile.razor:100-124` seed-unavailable-notice card pattern (persistent banner precedent) | exact |
| `DeckFlow.Studio.Tests/TestDoubles/FakeGitRepository.cs` (add `Fetch`/`GetBehindCount` canned + fault fields) | test double | request-response (canned/fault injection) | its own `GetSubjectsAheadOfRemoteAsync` fake (`FakeGitRepository.cs:34-38, 106-114`) — identical canned+throw shape | exact |
| `DeckFlow.Studio.Tests/ViewModels/PullFromProdCoordinatorTests.cs` (new staleness + divergence test cases) | test | CRUD / unit | its own existing tests (`PullFromProdCoordinatorTests.cs:1-120`, `AdoptEntry` helper) | exact (same file) |
| `DeckFlow.Studio.Tests/PullFromProdPageTests.cs` (new banner + opt-in gating tests) | test (bUnit) | request-response / unit | its own existing `RenderPull` harness (`PullFromProdPageTests.cs:19-90`) | exact (same file) |

## Pattern Assignments

### `DeckFlow.Core/Integration/IGitRepository.cs` (interface, request-response)

**Analog:** `GetSubjectsAheadOfRemoteAsync` (same file, lines 136-160) for the git-seam member shape; `DeckFlow.Studio/Services/IProdContentReader.cs:44-64` for the throwing-default-interface-method idiom used three times already this cycle (P89 `SetBodySha256IfNullAsync`, P90 `TryReadFlagAsync`/`ReadFlagAsync`).

**Throwing-DIM pattern** (`IProdContentReader.cs:44-64`, exact text seen in prior research and re-verifiable at that path):
```csharp
Task<bool?> TryReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
    => throw new NotSupportedException("This prod content reader does not support flag reads.");
```

**Existing member shape to mirror** (`IGitRepository.cs:136-160`):
```csharp
/// <summary>
/// Returns the subject lines of the commits on <c>HEAD</c> that are NOT yet on
/// <c>{remote}/{branch}</c>, newest first, via <c>git log --format=%s {remote}/{branch}..HEAD</c>.
/// </summary>
/// <exception cref="GitCommandException">
/// Thrown when the remote-tracking ref <c>{remote}/{branch}</c> is unknown (never fetched) or the
/// command otherwise fails; the caller treats this as "cannot determine" and proceeds best-effort.
/// </exception>
Task<IReadOnlyList<string>> GetSubjectsAheadOfRemoteAsync(
    string repoRoot, string remote, string branch, CancellationToken ct = default);
```

**Recommended new members** (throwing DIMs so `FakeGitRepository` isn't forced to implement — though this phase's plan should still implement both explicitly in the fake per the "Fetch/GetBehindCount canned returns" file entry above, matching house style for testability):
```csharp
Task FetchAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
    => throw new NotSupportedException("This git repository does not support fetch.");

Task<int> GetBehindCountAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
    => throw new NotSupportedException("This git repository does not support behind-count.");
```
Doc-comment style: match the `<remarks>` "Why:" pattern already used on `PushAsync` (`IGitRepository.cs:85-113`) and `CountWorkingChangesAsync` (`IGitRepository.cs:115-134`) — explain the scoped (not `--all`) fetch and the `HEAD..{remote}/{branch}` operand-reversal relative to `GetSubjectsAheadOfRemoteAsync`.

---

### `DeckFlow.Core/Integration/GitRepository.cs` (service, request-response shell-out)

**Analog:** `GetSubjectsAheadOfRemoteAsync` impl (`GitRepository.cs:259-285`) for the git-log/rev-list output-parsing shape; `PushAsync` (`GitRepository.cs:206-225`) for the network-operation-that-can-fail shape; `BuildStartInfo`/`RunAndCaptureAsync`/`RunRawAsync` (`GitRepository.cs:289-340`) are the mandatory shared plumbing — every new member MUST route through `BuildStartInfo(repoRoot)` (sets `GIT_TERMINAL_PROMPT=0`, `UseShellExecute=false`, `ArgumentList`-only) rather than a hand-rolled `ProcessStartInfo` (Pitfall 6 in RESEARCH.md).

**Imports** (file header, `GitRepository.cs:1-3`):
```csharp
using System.Diagnostics;

namespace DeckFlow.Core.Integration;
```

**Core pattern — scoped fetch, mirroring `PushAsync`'s per-branch precision** (`GitRepository.cs:206-225`):
```csharp
/// <inheritdoc />
public async Task PushAsync(
    string repoRoot, string remote, string branch, CancellationToken ct = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
    ArgumentException.ThrowIfNullOrWhiteSpace(remote);
    ArgumentException.ThrowIfNullOrWhiteSpace(branch);

    var startInfo = BuildStartInfo(repoRoot);
    startInfo.ArgumentList.Add("push");
    startInfo.ArgumentList.Add(remote);
    startInfo.ArgumentList.Add($"HEAD:refs/heads/{branch}");

    await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
}
```

**Core pattern — output-count parsing, mirroring `GetSubjectsAheadOfRemoteAsync`** (`GitRepository.cs:259-285`):
```csharp
/// <inheritdoc />
public async Task<IReadOnlyList<string>> GetSubjectsAheadOfRemoteAsync(
    string repoRoot, string remote, string branch, CancellationToken ct = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
    ArgumentException.ThrowIfNullOrWhiteSpace(remote);
    ArgumentException.ThrowIfNullOrWhiteSpace(branch);

    var startInfo = BuildStartInfo(repoRoot);
    startInfo.ArgumentList.Add("log");
    startInfo.ArgumentList.Add("--format=%s");
    startInfo.ArgumentList.Add($"{remote}/{branch}..HEAD");

    var stdout = await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);

    return stdout
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim())
        .Where(s => !string.IsNullOrEmpty(s))
        .ToList();
}
```

**Recommended new implementations** (RESEARCH.md Pattern 2, verified against the file's own conventions):
```csharp
public async Task FetchAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
    ArgumentException.ThrowIfNullOrWhiteSpace(remote);
    ArgumentException.ThrowIfNullOrWhiteSpace(branch);

    var startInfo = BuildStartInfo(repoRoot);
    startInfo.ArgumentList.Add("fetch");
    startInfo.ArgumentList.Add(remote);
    startInfo.ArgumentList.Add(branch);

    await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
}

public async Task<int> GetBehindCountAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
    ArgumentException.ThrowIfNullOrWhiteSpace(remote);
    ArgumentException.ThrowIfNullOrWhiteSpace(branch);

    var startInfo = BuildStartInfo(repoRoot);
    startInfo.ArgumentList.Add("rev-list");
    startInfo.ArgumentList.Add("--count");
    startInfo.ArgumentList.Add($"HEAD..{remote}/{branch}");

    var stdout = await RunAndCaptureAsync(startInfo, ct).ConfigureAwait(false);
    return int.Parse(stdout.Trim(), CultureInfo.InvariantCulture);
}
```
Note: `GetBehindCountAsync` needs `using System.Globalization;` added to the file's using block if not already present — verify before adding (grep the file for `CultureInfo` first; none of the other members currently parse an int, so this may be a new using).

**Error handling pattern** (`GitRepository.cs:309-321`, `RunAndCaptureAsync`):
```csharp
private static async Task<string> RunAndCaptureAsync(ProcessStartInfo startInfo, CancellationToken ct)
{
    var (stdout, stderr, exitCode) = await RunRawAsync(startInfo, ct).ConfigureAwait(false);
    if (exitCode != 0)
    {
        throw new GitCommandException(
            $"git {string.Join(" ", startInfo.ArgumentList)} exited {exitCode}: {ProcessOutput.Tail(stderr)}");
    }

    return stdout;
}
```
This is reused as-is — `FetchAsync`/`GetBehindCountAsync` throw `GitCommandException` automatically on any non-zero exit (offline, auth failure, unknown remote branch); no new exception type needed. The coordinator catches `GitCommandException` specifically (not bare `Exception`) to distinguish "could not verify freshness" from unrelated coordinator failures.

---

### `DeckFlow.Core/Content/SyncDiffEntry.cs` (model, transform)

**Analog:** the existing `ArtifactDownloaded` property + its doc comment (lines 68-72) — same file, same record, same "classifier leaves default, coordinator stamps post-classify" convention. Also mirrors `SyncDiffKind`'s doc-comment style (lines 6-28) for the new `BodyDivergenceStatus` enum's XML docs.

**Existing property to mirror:**
```csharp
/// <summary>
/// Whether the production artifact for this entry was successfully downloaded into staging.
/// The classifier leaves this <c>false</c>; the page sets it after the SCP step (Plan 03).
/// </summary>
public bool ArtifactDownloaded { get; init; }
```

**New enum + property** (per RESEARCH.md Pattern 3 — this is an explicit design ratification point, A2 in RESEARCH.md's Assumptions Log: orthogonal flag, NOT a fifth `SyncDiffKind` value):
```csharp
public enum BodyDivergenceStatus
{
    NotApplicable,
    Clean,
    Confirmed,
    Indeterminate
}

// on SyncDiffEntry:
public BodyDivergenceStatus BodyDivergence { get; init; }
```
`ContentSyncDiffClassifier.BuildEntry` (`ContentSyncDiffClassifier.cs:124-135`) currently sets `ArtifactDownloaded = false` explicitly in the object initializer — add `BodyDivergence = BodyDivergenceStatus.NotApplicable` alongside it for the same "classifier leaves the default, coordinator stamps it" reason (classifier stays pure/I-O-free per its own class doc comment, lines 6-12).

---

### `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` (coordinator, request-response + CRUD)

**Analog (staleness):** `DirectPushCoordinator.cs:429-452` — the ONLY existing "ahead of remote" / fetch-adjacent precedent in the codebase, and critically the no-fetch, fail-closed-on-unknown-ref pattern this phase must deliberately NOT copy verbatim (Pull is warn-then-proceed, not fail-closed, per D-guidance).

**Analog (divergence):** `ContentKbReconcileClassifier.cs:101-109` — same `ComputeBodySha256`-against-git-body-text shape, but this phase's null-handling is the OPPOSITE (Pitfall 1: P91 silently skips null `body_sha256`; D-01a requires null → `Indeterminate` → surfaced, not skipped).

**Imports** (already in the file, `PullFromProdCoordinator.cs:1-7`):
```csharp
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
```
No new imports needed — `ContentSiteIndexContentSignature` lives in `DeckFlow.Core.Content`, already imported.

**Existing classify + stamp pattern to extend** (`PullFromProdCoordinator.cs:112-124`):
```csharp
onStage("classify");
log.Report("Classifying diff against local store...");

var localRows = await _indexStore.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);

var entries = ContentSyncDiffClassifier.Classify(prodRows, localRows, _logger)
    .Select(e => e with { ArtifactDownloaded = availableSet.Contains(e.ArtifactPath) })
    .ToList();

log.Report($"Done — {entries.Count} differing entry/entries found. "
    + $"{availableSet.Count}/{prodRows.Count} body/bodies resolved from the local repo.");

return entries;
```

**Recommended extension (divergence stamp, RESEARCH.md Pattern 3, adapted to the file's actual `e with {...}` style):**
```csharp
var entries = ContentSyncDiffClassifier.Classify(prodRows, localRows, _logger)
    .Select(e =>
    {
        var downloaded = availableSet.Contains(e.ArtifactPath);
        var divergence = BodyDivergenceStatus.NotApplicable;

        if (downloaded && e.ProdRow is not null
            && ArtifactPathSafety.TryBuildContainedPath(repoRoot, e.ArtifactPath, out var repoBody))
        {
            var bodyText = File.ReadAllText(repoBody);
            var computedHash = ContentSiteIndexContentSignature.ComputeBodySha256(bodyText);

            divergence = e.ProdRow.BodySha256 is null
                ? BodyDivergenceStatus.Indeterminate                              // D-01a
                : string.Equals(e.ProdRow.BodySha256, computedHash, StringComparison.Ordinal)
                    ? BodyDivergenceStatus.Clean
                    : BodyDivergenceStatus.Confirmed;                             // D-01
        }

        return e with { ArtifactDownloaded = downloaded, BodyDivergence = divergence };
    })
    .ToList();
```
Note: `repoRoot` is already resolved earlier in `PullAndClassifyAsync` at line 92 (`var repoRoot = await _git.ResolveRepoRootAsync(...)`) — reuse it, do not re-resolve. Per Pitfall 5 in RESEARCH.md, keep this as a SEPARATE loop pass from the existing `availableSet` exists-check loop (`PullFromProdCoordinator.cs:93-110`) rather than merging them, unless the plan explicitly scopes and tests a merge.

**Existing path-safety call to mirror exactly** (`PullFromProdCoordinator.cs:97, 171-172`):
```csharp
if (!ArtifactPathSafety.TryBuildContainedPath(repoRoot, r.ArtifactPath, out var repoBody))
```

**Staleness pre-check — new first stage** (RESEARCH.md Pattern 2, adapted to this file's `onStage`/`log.Report` conventions used throughout, e.g. lines 79-90):
```csharp
onStage("check local checkout freshness");
try
{
    var branch = await _git.GetCurrentBranchAsync(repoRoot, cancellationToken).ConfigureAwait(false);
    await _git.FetchAsync(repoRoot, "origin", branch, cancellationToken).ConfigureAwait(false);
    var behindCount = await _git.GetBehindCountAsync(repoRoot, "origin", branch, cancellationToken).ConfigureAwait(false);
    if (behindCount > 0)
    {
        log.Report($"WARNING: local checkout is {behindCount} commit(s) behind origin/{branch} — "
            + "consider running 'git pull' before adopting. Proceeding with the current git tree.");
    }
}
catch (OperationCanceledException)
{
    throw;
}
catch (GitCommandException)
{
    log.Report("Could not verify checkout freshness (fetch failed — offline, VPN, or auth). "
        + "Proceeding with the local git tree as-is.");
}
```
Where exactly this runs relative to `repoRoot`'s existing resolution (currently at line 92, inside the "resolve local repo bodies" stage) is a planner call — RESEARCH.md's Open Question 2 flags whether to widen the return type into a wrapper record (e.g. `PullClassifyResult(Entries, FreshnessStatus)`) vs. progress-log-only. Follow the `OperationCanceledException` rethrow-first convention already used identically in `ApplyAdoptionsAsync`'s catch block (`PullFromProdCoordinator.cs:199-202`).

**Defense-in-depth exclusion in `ApplyAdoptionsAsync`** — the existing guard to extend (`PullFromProdCoordinator.cs:150-153`):
```csharp
// Defensive: the page pre-filters these, but never adopt a local-only / prod-less row.
if (entry.Kind == SyncDiffKind.LocalOnly || entry.ProdRow is null)
{
    continue;
}
```
Extend with a divergence check (belt-and-suspenders — the page is the primary gate per D-01, this is defense-in-depth): a divergent/indeterminate entry reaching `ApplyAdoptionsAsync` without having been in the page's explicit-opt-in set should also be skippable here if the coordinator is given that information; the exact parameter shape (e.g. an `IReadOnlySet<string> divergenceOverrideKeys` parameter, or trusting the page's pre-filtered `adoptEntries` list entirely as today) is left to the plan — RESEARCH.md's own Pattern 3 code example keeps the override set page-side only, matching the existing "coordinator trusts the page's pre-filtered adopt set" division of labor already in place for `Kind`/`ProdRow` filtering.

---

### `DeckFlow.Studio/Pages/PullFromProd.razor` + `.razor.cs` (Blazor Server page)

**Analog (badge/table rendering):** the page's own existing `Kind` badge switch (`PullFromProd.razor:139-157`) and `ArtifactDownloaded` badge (`PullFromProd.razor:160-172`) — same table, same row, add a fourth `<td>` or fold into the existing "Artifact" column.

**Analog (persistent banner over progress-log-only):** `Reconcile.razor:113-124`'s seed-unavailable notice card — the closest precedent for "a run-level condition gets its own persistent card, not just a buried log line":
```razor
else
{
    <div class="card border-warning mt-2" data-testid="seed-unavailable-banner">
        <div class="card-body py-2">
            <h3 class="h6 fw-semibold text-warning mb-1">Seed Drift — SEED UNAVAILABLE</h3>
            <p class="mb-0 small">
                <code>index-seed.json</code> could not be read or parsed for this
                run, so NO seed-managed row was evaluated for drift. This is...
            </p>
        </div>
    </div>
}
```

**Existing Kind badge switch to extend for the divergence badge** (`PullFromProd.razor:138-158`):
```razor
<td>
    @switch (entry.Kind)
    {
        case SyncDiffKind.ProdNewer:
            <span class="badge bg-primary">Prod newer</span>
            break;
        case SyncDiffKind.MissingLocally:
            <span class="badge bg-success">Missing locally</span>
            break;
        case SyncDiffKind.LocalOnly:
            <span class="badge bg-secondary">Local only</span>
            break;
        case SyncDiffKind.Diverged:
            <span class="badge bg-warning text-dark">Diverged</span>
            @if (entry.LocalIsNewer)
            {
                <span class="text-muted small ms-1">local is newer</span>
            }
            break;
    }
</td>
```
Add a sibling `<th>Body</th>` / `<td>` column with a `@switch (entry.BodyDivergence)` following the identical badge-per-case shape (`bg-danger` for `Confirmed`, `bg-warning text-dark` for `Indeterminate`, no badge or `bg-success`-subtle for `Clean`/`NotApplicable`).

**Existing per-entry resolution radios to extend with the divergence opt-in checkbox** (`PullFromProd.razor:173-198`):
```razor
<div class="form-check form-check-inline">
    <input class="form-check-input" type="radio"
           name="res-@key" id="adopt-@key"
           checked="@(GetResolution(entry) == Resolution.AdoptProd)"
           @onchange="() => SetResolution(entry, Resolution.AdoptProd)" />
    <label class="form-check-label small" for="adopt-@key">adopt prod</label>
</div>
...
@if (!entry.ArtifactDownloaded)
{
    <div class="text-warning small">body missing in local repo — adopting updates the row only</div>
}
```
Add a conditional checkbox (only rendered when `entry.BodyDivergence is Confirmed or Indeterminate`) requiring separate explicit acknowledgement, matching this file's `_resolutions` dictionary pattern but as a NEW page-local `HashSet<string> _divergenceOverrides` keyed by the same `EntryKey(entry)` helper (`PullFromProd.razor.cs:237`).

**Existing adopt-set filter to extend** (`PullFromProd.razor.cs:186-192`):
```csharp
var adoptEntries = _diffEntries
    .Where(e => GetResolution(e) == Resolution.AdoptProd
        && e.Kind != SyncDiffKind.LocalOnly
        && e.ProdRow is not null)
    .ToList();
```
Recommended extension (RESEARCH.md Pattern 3, page-side gate — the primary D-01 enforcement point):
```csharp
var adoptEntries = _diffEntries
    .Where(e => GetResolution(e) == Resolution.AdoptProd
        && e.Kind != SyncDiffKind.LocalOnly
        && e.ProdRow is not null
        && (e.BodyDivergence is BodyDivergenceStatus.NotApplicable or BodyDivergenceStatus.Clean
            || _divergenceOverrides.Contains(EntryKey(e))))
    .ToList();
```

**Existing `EntryKey` helper to reuse (not duplicate)** (`PullFromProd.razor.cs:237`):
```csharp
private static string EntryKey(SyncDiffEntry entry) => $"{entry.NaturalKeyType}:{entry.NaturalKeyValue}";
```

**Error/sanitization pattern already established in the page, must extend identically to the new freshness-check failure path** (`PullFromProd.razor.cs:153-167`):
```csharp
catch (Exception ex)
{
    // Why: an Npgsql or git exception can carry host/db/user/path — NEVER surface ex.Message
    // in the UI (D-07). But DO log the full exception server-side (Serilog file sink) with the
    // failing stage so a failed pull is diagnosable; the operator reads the log, not the page.
    Logger.LogError(ex, "Pull from prod failed during stage: {PullStage}.", _pullStage);
    _pullError = $"Could not pull from production while trying to {_pullStage} — check the prod connection and local git repo, then try again. Nothing was written. (See the Studio log for details.)";
    ...
}
```

---

### `DeckFlow.Studio.Tests/TestDoubles/FakeGitRepository.cs` (test double)

**Analog:** its own `CannedSubjectsAhead`/`ThrowOnSubjectsAhead`/`GetSubjectsAheadOfRemoteAsync` triplet (`FakeGitRepository.cs:34-38, 106-114`) — the exact canned-return + fault-injection shape to replicate for the two new members.

**Pattern to copy verbatim, adapted:**
```csharp
/// <summary>Subjects returned by GetSubjectsAheadOfRemoteAsync — default empty (branch in sync).</summary>
public List<string> CannedSubjectsAhead { get; set; } = new();

/// <summary>When set, GetSubjectsAheadOfRemoteAsync throws it (simulates a missing remote-tracking ref).</summary>
public Exception? ThrowOnSubjectsAhead { get; set; }

...

public Task<IReadOnlyList<string>> GetSubjectsAheadOfRemoteAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
{
    if (ThrowOnSubjectsAhead is not null)
    {
        throw ThrowOnSubjectsAhead;
    }

    return Task.FromResult<IReadOnlyList<string>>(CannedSubjectsAhead);
}
```
Recommended new fields/methods (RESEARCH.md's own naming, `CannedBehindCount`/`ThrowOnFetch`):
```csharp
public int CannedBehindCount { get; set; }
public Exception? ThrowOnFetch { get; set; }
public List<(string RepoRoot, string Remote, string Branch)> FetchCalls { get; } = new();

public Task FetchAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
{
    FetchCalls.Add((repoRoot, remote, branch));
    if (ThrowOnFetch is not null)
    {
        throw ThrowOnFetch;
    }
    return Task.CompletedTask;
}

public Task<int> GetBehindCountAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
    => Task.FromResult(CannedBehindCount);
```
Note: implement both explicitly on the fake (do NOT rely on the interface's throwing-DIM default here) — RESEARCH.md explicitly calls this out: relying on the throwing default in the fake would make the "behind" and "fetch failed" coordinator branches untestable.

---

### `DeckFlow.Studio.Tests/ViewModels/PullFromProdCoordinatorTests.cs` (unit tests)

**Analog:** the file's own `Build(...)` helper (lines 71-90), `Youtube(...)` row builder (lines 55-69), and `AdoptEntry(...)` entry builder (lines 92-103) — reuse all three verbatim; add new test cases, do not duplicate the fixtures.

**Existing `Build` helper to reuse for FakeGitRepository injection:**
```csharp
private PullFromProdCoordinator Build(
    FakeContentSiteIndexStore localStore,
    FakeProdContentReader prodReader,
    FakeGitRepository? git = null)
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Studio:ProdConnectionString"] = "Host=fake;Database=fake",
        })
        .Build();

    return new PullFromProdCoordinator(
        localStore,
        git ?? new FakeGitRepository { CannedRepoRoot = _repoRoot },
        prodReader,
        configuration,
        new ContentKbOrchestratorOptions { ArtifactRoot = _artifactRoot },
        NullLogger<PullFromProdCoordinator>.Instance);
}
```
New test cases pass a `FakeGitRepository` configured with `CannedBehindCount`/`ThrowOnFetch` via the existing `git` optional parameter — no new test-construction pattern needed.

**Existing `AdoptEntry` helper to extend/mirror for divergence-stamping tests:**
```csharp
private static SyncDiffEntry AdoptEntry(ContentSiteIndexRow prodRow, bool artifactDownloaded)
    => new()
    {
        NaturalKeyType = ContentSourceType.Youtube,
        NaturalKeyValue = prodRow.YoutubeVideoId!,
        Kind = SyncDiffKind.ProdNewer,
        Title = prodRow.Title,
        ProdRow = prodRow,
        LocalRow = null,
        ArtifactPath = prodRow.ArtifactPath,
        ArtifactDownloaded = artifactDownloaded,
    };
```
New tests write a repo body file under `_repoRoot` (using `Directory.CreateDirectory`/`File.WriteAllText`, matching `PullFromProdPageTests.cs:78-80`'s convention) with content whose `ComputeBodySha256` either matches or mismatches `prodRow.BodySha256`, then assert `PullAndClassifyAsync`'s returned entries carry the expected `BodyDivergence` value.

---

### `DeckFlow.Studio.Tests/PullFromProdPageTests.cs` (bUnit page tests)

**Analog:** the file's own `RenderPull(...)` harness (lines 50-90+) — reuse verbatim; it already writes repo body files to a temp `repoRoot` and wires `FakeGitRepository`, `FakeContentSiteIndexStore`, `FakeProdContentReader` via DI (`Services.AddSingleton<...>`). New tests supply a `prodRows` row with a `BodySha256` that mismatches the written repo body text, then assert the divergence badge/opt-in checkbox renders and that `ApplyResolutionsAsync`'s adopt set excludes it until the opt-in is checked — using `InvokePullApplyForTest()` (`PullFromProd.razor.cs:265`) as the existing test seam for the apply-side assertion, matching the file's existing pattern for exercising the hard-guard.

**Existing harness signature to extend, if a `FakeGitRepository` fetch/behind-count configuration parameter is needed:**
```csharp
private (IRenderedComponent<PullFromProd> Cut,
         FakeContentSiteIndexStore LocalStore,
         FakeProdContentReader ProdReader,
         FakeGitRepository Git)
    RenderPull(
        IEnumerable<ContentSiteIndexRow>? localRows = null,
        IEnumerable<ContentSiteIndexRow>? prodRows = null,
        FakeProdContentReader? prodReaderOverride = null,
        IEnumerable<string>? missingRepoBodies = null,
        bool isProdConfigured = true,
        bool isScpConfigured = true)
```
Add an optional `FakeGitRepository? gitOverride = null` parameter (or set `CannedBehindCount`/`ThrowOnFetch` on the `Git` returned from the existing harness before triggering the pull) — follow whichever shape keeps the existing call sites compiling unchanged (all current callers use named/default args).

## Shared Patterns

### Throwing default-interface-method for new `IGitRepository` members
**Source:** `DeckFlow.Studio/Services/IProdContentReader.cs:44-64` (the idiom); `DeckFlow.Core/Integration/IGitRepository.cs` (the interface being extended)
**Apply to:** `IGitRepository.FetchAsync`, `IGitRepository.GetBehindCountAsync`
**Why:** avoids `CS0535` on any hand-written double that doesn't implement the new members; confirmed precedent exists 3x already this cycle (P89 `SetBodySha256IfNullAsync`, P90 `TryReadFlagAsync`/`ReadFlagAsync`).
```csharp
Task FetchAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
    => throw new NotSupportedException("This git repository does not support fetch.");
```

### `ArgumentList`-only shell-out, never string-concatenated `Arguments`
**Source:** `DeckFlow.Core/Integration/GitRepository.cs` class remarks (lines 40-49) and every existing member's `startInfo.ArgumentList.Add(...)` calls
**Apply to:** `GitRepository.FetchAsync`, `GitRepository.GetBehindCountAsync`
**Why:** shell-injection safety (already the established, audited convention — T-46-02-01) — never build a command string.

### `ArtifactPathSafety.TryBuildContainedPath` for any body-file read
**Source:** `DeckFlow.Studio/Services/ArtifactPathSafety.cs:22-42`; already called at `PullFromProdCoordinator.cs:97` and `171-172`
**Apply to:** the new divergence-check `File.ReadAllText(repoBody)` call in `PullFromProdCoordinator.PullAndClassifyAsync`
**Why:** the ONE Studio path-safety implementation (90-CONTEXT D-11); the divergence read must reuse the same guard the exists-check already applies, not a second unguarded `File.ReadAllText`.
```csharp
public static bool TryBuildContainedPath(string root, string artifactPath, out string resolvedPath)
```

### `ContentSiteIndexContentSignature.ComputeBodySha256` — the ONE hash surface
**Source:** `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs:122-145`
**Apply to:** the divergence stamp in `PullFromProdCoordinator.PullAndClassifyAsync`
**Why:** its own doc comment: "This is the ONE hash surface — both the publish-time compute and the render-time guard must call this method." Duplicating it risks reintroducing the CP437-mojibake/EOL-mismatch bug class it exists to prevent.
```csharp
public static string ComputeBodySha256(string rawArtifactText)
```

### D-07 sanitized-UI / full-server-log error convention
**Source:** `PullFromProd.razor.cs:153-167` (existing `catch (Exception ex)` block) and `PullFromProdCoordinator.cs:203-211` (`ApplyAdoptionsAsync`'s catch block)
**Apply to:** the new `FetchAsync`/`GetBehindCountAsync` failure paths — log full `GitCommandException` detail via `_logger`, surface only sanitized copy in the progress log/banner. Never render `ex.Message` (can carry connection-string/path detail).

### Composite natural-key `EntryKey` for any new per-entry page state
**Source:** `PullFromProd.razor.cs:237` — `$"{entry.NaturalKeyType}:{entry.NaturalKeyValue}"`, already used for `_resolutions`
**Apply to:** a new `_divergenceOverrides` per-entry opt-in set — reuse `EntryKey(entry)`, do not invent a second key scheme.

### `IReadOnlyList<T>` / `sealed record` return shapes
**Source:** house-wide convention (`./CLAUDE.md` Function Design), already followed by `SyncDiffEntry`, `PullApplyRowResult`, `ReconcileApplyResult`
**Apply to:** any new coordinator result type (e.g. a freshness-status wrapper, if the plan chooses the wrapper-record option from RESEARCH.md's Open Question 2).

## No Analog Found

None — every file this phase touches or extends already exists with a directly-applicable in-repo analog (either itself, in its prior shape, or a sibling P89/P90/P91 file solving the identical shape of problem). This phase creates zero brand-new files.

## Metadata

**Analog search scope:** `DeckFlow.Core/Integration/`, `DeckFlow.Core/Content/`, `DeckFlow.Studio/ViewModels/`, `DeckFlow.Studio/Pages/`, `DeckFlow.Studio/Services/`, `DeckFlow.Studio.Tests/` (TestDoubles, ViewModels, page tests)
**Files scanned (full read this session):** `IGitRepository.cs`, `GitRepository.cs`, `PullFromProdCoordinator.cs`, `ContentSyncDiffClassifier.cs`, `SyncDiffEntry.cs`, `ContentSiteIndexContentSignature.cs`, `FakeGitRepository.cs`, `PullFromProd.razor.cs`, `PullFromProd.razor`, `ContentKbReconcileClassifier.cs`, `ReconcileCoordinator.cs`, `Reconcile.razor` (partial), `PullFromProdCoordinatorTests.cs` (partial), `PullFromProdPageTests.cs` (partial), `ArtifactPathSafety.cs`, `DirectPushCoordinator.cs` (lines 380-460)
**Pattern extraction date:** 2026-07-10

---

## PATTERN MAPPING COMPLETE

**Phase:** 92 - Pull Hardening
**Files classified:** 8
**Analogs found:** 8 / 8

### Coverage
- Files with exact analog: 8
- Files with role-match analog: 0
- Files with no analog: 0

### Key Patterns Identified
- New `IGitRepository` members (`FetchAsync`, `GetBehindCountAsync`) must be throwing default-interface-methods (idiom confirmed 3x this cycle at `IProdContentReader.cs:44-64`, P89/P90), implemented for real only in `GitRepository.cs` (mirroring `GetSubjectsAheadOfRemoteAsync`/`PushAsync` shell-out shape) and explicitly (not via the throwing default) in `FakeGitRepository.cs` so both success and fault paths are testable.
- Body-divergence is an orthogonal stamp (`BodyDivergenceStatus` on `SyncDiffEntry`, alongside the existing `ArtifactDownloaded`), NOT a fifth `SyncDiffKind` value — stamped by the coordinator post-classify, same shape as the existing `ArtifactDownloaded` stamp; classifier itself stays pure/I-O-free.
- D-01a's null-`body_sha256` → `Indeterminate` handling is the deliberate INVERSE of the nearest analog (`ContentKbReconcileClassifier.cs:101`, which silently skips null hashes) — do not copy that guard verbatim.
- Every new file read/path-join reuses `ArtifactPathSafety.TryBuildContainedPath` and every new hash reuses `ContentSiteIndexContentSignature.ComputeBodySha256` — both are the codebase's single canonical implementation per prior-phase ratification (90-CONTEXT D-11, P89 hash-surface doc comment).
- All new user-facing error copy follows the established D-07 sanitized-UI/full-server-log split already implemented in `PullFromProd.razor.cs`'s and `PullFromProdCoordinator.cs`'s existing catch blocks.

### File Created
`/mnt/c/users/chrislunt/source/personal/deckflow-cycle16/.planning/phases/92-pull-hardening/92-PATTERNS.md`

### Ready for Planning
Pattern mapping complete. Planner can now reference analog patterns in PLAN.md files.
