# Phase 92: Pull Hardening - Research

**Researched:** 2026-07-10
**Domain:** Blazor Server Studio ops tool — git CLI shell-out (staleness detection), content-hash divergence detection, local-only-write orchestration hardening
**Confidence:** HIGH (this phase extends four already-shipped, already-read codebase surfaces; almost nothing here is external/library research)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Body-vs-index divergence (SYNC-15)**
- **D-01:** When a git body's computed hash != prod's `body_sha256` for an entry being adopted, that entry is **blocked from silent adoption**. It is classified as a **distinct divergence class** in the Pull diff and **excluded from the default adopt set**; the operator must **explicitly opt-in per entry** to adopt a divergent row. This honors SYNC-15 ("surfaced to the operator instead of silently adopted"). Reuse of the P91 reconcile discrepancy vocabulary was considered and rejected — keep the operator flow on the single Pull page rather than splitting it across the reconcile page.
- **D-01a:** Divergence detection uses the shared `ContentSiteIndexContentSignature.ComputeBodySha256` helper (the same UTF-8 + LF-normalized, body-only hash shipped in P89) against the resolved git-tree body, compared to the prod row's `body_sha256`. When prod's `body_sha256` is null/absent (legacy unbackfilled row), treat as **indeterminate → surface, do not auto-adopt** (fail-safe, mirrors P90/P91 fail-safe posture).

**Field authority (SYNC-13)**
- **D-02:** **Ratify the current adopt field split.** Body FILE ← git tree (copied into the live tree, local only). Content-index columns (title/tags/artifact path/`body_sha256`) ← the prod row via `UpsertContentColumnsOnlyAsync`. `approval_status` ← prod-mirror via `SetApprovalStatusAsync` (Pull is the *adopt-prod* direction, so taking prod's operator decision is correct — this does **not** conflict with P90 D-03's "approval is LOCAL-authoritative + mirror," which governs the **push** direction). `is_visible` / `is_hidden` are **ALWAYS preserved-local** — the content-only upsert never touches them, so adopting never auto-publishes or auto-hides.
- **D-02a:** The D-01 divergence guard is what keeps D-02 coherent: prod's `body_sha256` is only adopted into the local index when it **matches** the git body being copied, so the local index row and local body file never fall out of sync as a result of Pull. "content ← git tree" (SYNC-13) resolves to: the **body file** is the git tree's; the index columns that *describe* it are prod's, and are only adopted when the two agree.

**Flag gating**
- **D-03:** **Always-on, no `sync.*` flag.** Pull writes LOCAL-only and never has a destructive-prod blast radius (unlike P90 DirectPush and P91 reconcile Apply, which are flag-gated because they mutate prod). The staleness guard and divergence surfacing are strictly *protective*, so shipping them always-on is strictly safer than gating them. No `FeatureFlagCatalog` entry, no Studio flag read for this phase.

### Claude's Discretion
- **SYNC-14 staleness guard — mechanism and warn-vs-refuse left to the planner/research.** The gray area was intentionally not deep-dived. Guidance for the planner: `IGitRepository` today has `GetCurrentBranchAsync`, `CountWorkingChangesAsync` (dirty-tree), and `GetSubjectsAheadOfRemoteAsync` (local-ahead) but **no fetch and no behind-detection** — a new capability is needed to know the checkout is behind its remote. Preferred direction (not locked): add a behind-detection git seam (e.g., `git fetch` + behind-count, a network op) and **WARN + let the operator proceed** rather than hard-refuse, consistent with this phase's "surface to the operator" theme; a hard-refuse option is acceptable if research shows the fetch is reliable and cheap in the Studio host. Keep the guard's git work behind the existing `IGitRepository` abstraction (testable seam) and honor `ArtifactPathSafety` for any path use. Must never SFTP or touch prod.
- Exact UI treatment of the divergence class (badge, section, per-entry opt-in control) and progress-log copy — follow the existing `PullFromProd` page conventions and the P91 reconcile page's class-grouping precedent.
- Test seams and doubles follow the established `Fake*`/`IProdContentReader`/`FakeGitRepository` patterns from P90/P91.

### Deferred Ideas (OUT OF SCOPE)
- Auto-`git pull` / auto-remediation of a stale checkout — SYNC-14 lands as a *guard* (warn/refuse), not an auto-fixer; automatic pulling is out of scope for this phase.
- Any prod-side write or SFTP body fetch — permanently rejected for Pull by the cycle's git-SoT stance.
- Merging the Pull divergence view into the P91 reconcile page — considered and rejected (D-01); could be revisited if a unified "sync health" operator surface is ever scoped.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SYNC-13 | Pull-from-Prod per-field master — body+content ← git tree; DB-only operator fields (`is_visible`/`is_hidden`/`approval_status`) ← prod, preserved not clobbered (M7) | D-02/D-02a already ratify the field split; §Architecture Patterns confirms `ApplyAdoptionsAsync` (PullFromProdCoordinator.cs:137-217) already implements it correctly — this requirement is a **ratification + regression-test-lock**, not new code, except where D-01's divergence guard changes *when* `body_sha256`/body are adopted together (§Code Examples) |
| SYNC-14 | Pull warns/refuses when the local checkout is behind (`git pull` first staleness guard); never SFTP-downloads prod bodies | §Architecture Patterns "SYNC-14 staleness guard" is the deep-dive: recommends two new `IGitRepository` members (`FetchAsync` + `GetBehindCountAsync`), warn-then-proceed posture, throwing-DIM idiom, and the exact shell-out shape to add to `GitRepository.cs` |
| SYNC-15 | Body-vs-index divergence surfaced to the operator instead of silently adopted | §Architecture Patterns "Divergence stamping" + §Code Examples give the concrete `SyncDiffEntry` extension, coordinator stamping loop, and page adopt-filter/opt-in changes; §Common Pitfalls flags the P91 `ContentKbReconcileClassifier` null-hash-skip behavior this phase must NOT copy (D-01a diverges from it) |

</phase_requirements>

## Summary

Phase 92 hardens an already-extracted, already-correct-by-design Pull-from-Prod flow (`PullFromProdCoordinator` + `PullFromProd.razor(.cs)`) with two protective, LOCAL-only-write guards. Nearly all of the "research" here is close reading of four existing shipped surfaces (`IGitRepository`/`GitRepository`, `ContentSiteIndexContentSignature`, `ContentSyncDiffClassifier`/`SyncDiffEntry`, and the P91 reconcile classifier/coordinator as the analog) plus one genuinely novel design decision: how to detect that a local git checkout is behind its remote from a .NET host that shells out to `git`.

The single highest-signal finding: **the codebase's one existing "ahead of remote" check (`GetSubjectsAheadOfRemoteAsync`, used by DirectPush) deliberately never calls `git fetch`** — it reads whatever the local remote-tracking ref (`origin/{branch}`) already knows, and DirectPush fails closed with an operator-facing message ("run `git fetch`") when that ref is unknown. This is a critical precedent: **no code path in Studio has ever auto-fetched from a remote.** For SYNC-14 to detect genuine staleness (not just "staleness as of some undated prior fetch"), a fetch is unavoidable — the guard must add its own `FetchAsync` alongside a new `GetBehindCountAsync`, mirroring `GetSubjectsAheadOfRemoteAsync`'s `{remote}/{branch}..HEAD` shape but with the operands reversed (`HEAD..{remote}/{branch}`). `PushAsync`'s existing remarks establish that Studio runs on the operator's own machine with their own git credentials and network access — so a fetch is a reasonable, low-risk operation there, but it is still a network call that can fail (offline, VPN, transient auth) and must never hang or block the page (the codebase already disables `GIT_TERMINAL_PROMPT` for exactly this class of risk).

For SYNC-15, the closest analog is `ContentKbReconcileClassifier`'s `BodyHashMismatch` branch (P91) — same `ComputeBodySha256`-against-git-body-text pattern — but it makes a choice this phase must explicitly NOT copy: it skips (does not flag) rows with a null `body_sha256`. D-01a requires the opposite (null hash = indeterminate = surface, don't auto-adopt). Divergence is also an *orthogonal* axis to the existing `SyncDiffKind` (which encodes a *temporal* ProdNewer/MissingLocally/LocalOnly/Diverged classification) — a `ProdNewer` or `MissingLocally` entry can independently be body-divergent, so folding divergence into a fifth `SyncDiffKind` value would either lose information or require re-deriving the temporal kind from the divergence check. The stamping pattern the coordinator already uses for `ArtifactDownloaded` (a boolean stamped post-classify in the `.Select(...)` at `PullFromProdCoordinator.cs:119-121`) is the right shape to extend.

**Primary recommendation:** Add `FetchAsync`/`GetBehindCountAsync` to `IGitRepository` as throwing default-interface-methods (matching the `SetBodySha256IfNullAsync`/`TryReadFlagAsync` idiom from P89/P90); call them from `PullAndClassifyAsync` in a try/catch that WARNS (progress-log line + a structured field the page can render as a persistent banner) on both "behind" and "fetch failed" — never hard-refuses, since Pull is a protective, local-only-write flow and a network hiccup must not block it. Add a `BodyDivergenceStatus` enum (`NotApplicable` / `Clean` / `Confirmed` / `Indeterminate`) to `SyncDiffEntry`, stamped alongside `ArtifactDownloaded` using the same file already resolved for the exists-check, and require a SEPARATE explicit per-entry acknowledgement (not just the existing "adopt prod" radio) before a divergent/indeterminate row enters `ApplyAdoptionsAsync`'s adopt set.

## Architectural Responsibility Map

DeckFlow.Studio is a Blazor **Server** admin tool, not a browser/SSR/API-split web app — the standard web-tier table does not map cleanly onto it. The project's own layering (Core domain / Studio Services / Studio ViewModels / Studio Pages, per `./CLAUDE.md` §Layers) is used as the tier axis instead, to avoid misassigning capabilities the way a literal Browser/API/CDN table would.

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Git behind-detection (fetch + rev-list count) | `DeckFlow.Core` (`IGitRepository`/`GitRepository`) | `DeckFlow.Studio.ViewModels` (`PullFromProdCoordinator` calls it) | Every existing git shell-out lives in `DeckFlow.Core/Integration/GitRepository.cs`; this is a pure I/O adapter, not orchestration — matches `GetSubjectsAheadOfRemoteAsync`'s placement exactly |
| Body-hash divergence computation | `DeckFlow.Core` (`ContentSiteIndexContentSignature.ComputeBodySha256`, reused not re-hand-rolled) | `DeckFlow.Studio.ViewModels` (stamping loop) | The hash function is pure domain logic already in Core (P89); the *stamping* (reading the file, comparing, deciding Clean/Confirmed/Indeterminate) is orchestration that belongs with the rest of the classify pipeline in the coordinator, mirroring how `ArtifactDownloaded` is stamped today |
| Field-authority adopt split (SYNC-13) | `DeckFlow.Studio.ViewModels` (`PullFromProdCoordinator.ApplyAdoptionsAsync`) | `DeckFlow.Core` (`IContentSiteIndexStore.UpsertContentColumnsOnlyAsync`/`SetApprovalStatusAsync`) | Already correctly split today (D-02 ratifies, does not move it); the coordinator orchestrates, the store interface enforces the column-scoped SQL |
| Local store write (LOCAL SQLite/Postgres only) | Database/Storage (`IContentSiteIndexStore` impl) | — | Never prod — `IProdContentReader` is structurally read-only (no upsert/delete method exists on it at all) |
| Divergence/staleness UI surfacing | `DeckFlow.Studio.Pages` (`PullFromProd.razor`/`.razor.cs`) | — | Server-rendered Blazor component; no separate "browser tier" exists in this app — the page IS the render tier |
| Production content read (prod row source for classify + divergence compare) | External Integration (`IProdContentReader` → Postgres, read-only SELECT) | — | Structurally incapable of writing (D-03 precedent from P90/P91); this phase does not touch it |

## Standard Stack

**No new external dependencies are required or recommended for this phase.** Every capability needed (git shell-out, SHA-256 hashing, Postgres read) is already provided by packages already in the solution.

### Core
| Library | Version | Purpose | Why Standard (for this phase) |
|---------|---------|---------|--------------|
| `System.Diagnostics.Process` (BCL) | net10.0 | Shells out to the `git` CLI for the new `FetchAsync`/`GetBehindCountAsync` members | The ONLY git-access mechanism used anywhere in Studio (`GitRepository.cs`); adding a git library (e.g. LibGit2Sharp) would be a NEW PACKAGE requiring explicit user approval per `./CLAUDE.md` "Dependency additions" and would create a second, divergent git-access pathway (credential handling, `GIT_TERMINAL_PROMPT` suppression) to keep in sync |
| `System.Security.Cryptography.SHA256` (BCL) | net10.0 | Already the engine behind `ContentSiteIndexContentSignature.ComputeBodySha256` | Reused verbatim, not re-implemented — this phase calls the existing helper, it does not touch cryptography code |

### Supporting
None new.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Shell out to `git fetch`/`git rev-list --count` via `Process` | `LibGit2Sharp` (managed git library) | Would avoid a subprocess spawn, but is a brand-new NuGet dependency (blocked by CLAUDE.md's "no new packages without asking"), introduces a second credential-handling surface distinct from `GIT_TERMINAL_PROMPT=0`, and breaks the "one git access pattern" convention every other `IGitRepository` member follows. Not recommended. |

**Installation:** None — no new packages.

**Version verification:** N/A — no new package versions to verify. The `git` CLI itself is an environment dependency already required by every other Studio git operation (Publish, DirectPush, Reconcile); no new minimum-version requirement is introduced (`git fetch <remote> <refspec>` and `git rev-list --count` are long-stable, pre-2.0-era git CLI features per `git-fetch(1)`/`git-rev-list(1)`).

## Package Legitimacy Audit

**Not applicable — this phase installs no external packages.** No `slopcheck`/registry verification was run because there is nothing to verify; every new capability is built on BCL types and the existing `git` CLI shell-out convention.

## Architecture Patterns

### System Architecture Diagram

```
PullFromProd.razor.cs (Blazor Server page — render tier)
  │
  │ Stage 1: PullAndClassifyAsync()
  ▼
PullFromProdCoordinator.PullAndClassifyAsync   [DeckFlow.Studio.ViewModels]
  │
  ├─► (NEW, SYNC-14) resolve repoRoot + branch ──► IGitRepository.GetCurrentBranchAsync
  │        │
  │        ├─► IGitRepository.FetchAsync(repoRoot, "origin", branch)   ── network op, git CLI
  │        │        │
  │        │        ├─ success ──► IGitRepository.GetBehindCountAsync(repoRoot, "origin", branch)
  │        │        │                    │
  │        │        │                    └─ behindCount > 0 → WARN (progress log + freshness field)
  │        │        │                       behindCount == 0 → no warning
  │        │        │
  │        │        └─ GitCommandException (offline/auth/no-ref) ──► WARN "could not verify freshness"
  │        │                                                          (never hard-refuse; proceed)
  │
  ├─► IProdContentReader.ReadAllAsync(prodConnStr)      — plain SELECT, read-only, NO DDL
  │        (existing — unchanged)
  │
  ├─► for each prod row: ArtifactPathSafety.TryBuildContainedPath(repoRoot, row.ArtifactPath)
  │        │
  │        ├─ File.Exists → ArtifactDownloaded stamp (existing)
  │        │
  │        └─► (NEW, SYNC-15) File.ReadAllText(repoBody)
  │                 → ContentSiteIndexContentSignature.ComputeBodySha256(bodyText)
  │                 → compare vs prodRow.BodySha256
  │                      null            → BodyDivergenceStatus.Indeterminate
  │                      mismatch        → BodyDivergenceStatus.Confirmed
  │                      match           → BodyDivergenceStatus.Clean
  │
  ├─► ContentSyncDiffClassifier.Classify(prodRows, localRows)   — pure, I/O-free, UNCHANGED signature
  │        (temporal axis: ProdNewer / MissingLocally / LocalOnly / Diverged)
  │
  └─► .Select(e => e with { ArtifactDownloaded, BodyDivergence })   — orthogonal stamp, same pattern
           │
           ▼
      IReadOnlyList<SyncDiffEntry>  ──►  PullFromProd.razor renders:
                                            - freshness banner (if behind/indeterminate)
                                            - diff table: Kind badge (existing) + Divergence badge (NEW)
                                            - Resolution radios (existing: adopt/keep)
                                            - divergence opt-in checkbox (NEW, required for Confirmed/
                                              Indeterminate rows before they enter the adopt set)

  │ Stage 2: ApplyResolutionsAsync() — operator clicks "Apply Resolutions (local)"
  ▼
PullFromProdCoordinator.ApplyAdoptionsAsync   [unchanged shape, new pre-filter upstream in the page]
  │
  ├─► IContentSiteIndexStore.UpsertContentColumnsOnlyAsync(prodRow)   — LOCAL store, content columns only
  ├─► IContentSiteIndexStore.SetApprovalStatusAsync(...)              — LOCAL store, mirrors prod approval
  └─► File.Copy(repoBody → liveDest)                                  — LOCAL filesystem, best-effort

Production is READ-ONLY throughout this entire flow — IProdContentReader exposes no write method,
structurally, at the interface level.
```

### Recommended Project Structure

No new files/folders are needed — this phase extends four existing files and their test doubles:

```
DeckFlow.Core/
├── Integration/
│   ├── IGitRepository.cs          # ADD: FetchAsync, GetBehindCountAsync (throwing DIMs)
│   └── GitRepository.cs           # ADD: real shell-out implementations
├── Content/
│   ├── SyncDiffEntry.cs           # ADD: BodyDivergenceStatus enum + property on SyncDiffEntry
│   └── ContentSyncDiffClassifier.cs   # UNCHANGED — stays pure/I-O-free (divergence is stamped
│                                       # post-classify by the coordinator, same as ArtifactDownloaded)
DeckFlow.Studio/
├── ViewModels/
│   └── PullFromProdCoordinator.cs # EDIT: staleness pre-check + divergence stamping in
│                                   #       PullAndClassifyAsync; adopt-exclusion defense-in-depth
│                                   #       in ApplyAdoptionsAsync (belt-and-suspenders — the page
│                                   #       is the primary gate, per D-01 "operator must opt-in")
└── Pages/
    └── PullFromProd.razor(.cs)    # EDIT: freshness banner, divergence badge, opt-in control,
                                    #       adopt pre-filter change
DeckFlow.Studio.Tests/
├── TestDoubles/
│   └── FakeGitRepository.cs       # EDIT: implement Fetch/BehindCount canned returns + fault injection
├── ViewModels/
│   └── PullFromProdCoordinatorTests.cs   # EDIT: new tests for staleness + divergence stamping
└── PullFromProdPageTests.cs       # EDIT: new tests for banner + opt-in gating
```

### Pattern 1: Throwing default-interface-method for new interface members

**What:** New members added to a long-lived interface (`IGitRepository`, `IContentSiteIndexStore`, `IProdContentReader`) are declared with a default body that throws `NotSupportedException`, rather than as abstract members, so hand-written test doubles that don't implement them keep compiling (no `CS0535`).

**When to use:** Any time this phase adds a member to `IGitRepository`. Confirmed precedent exists for exactly this shape three times already this cycle (`SetBodySha256IfNullAsync` — 89-02, `TryReadFlagAsync`/`ReadFlagAsync` — 90-03/90-04). Even though `FakeGitRepository` is the ONLY hand-written double for `IGitRepository` today (verified — no `Mock<IGitRepository>`/`Substitute.For<IGitRepository>` usage exists in the solution), the plan should still follow this idiom for consistency with the rest of the interface and because `GitRepository` (the real implementation) will always override the default with a working shell-out — the throwing default only ever matters for doubles that choose not to implement it.

**Example (existing precedent, `IProdContentReader.cs:44-64`):**
```csharp
// Source: DeckFlow.Studio/Services/IProdContentReader.cs (already shipped, P90)
Task<bool?> TryReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
    => throw new NotSupportedException("This prod content reader does not support flag reads.");
```

**Recommended shape for the two new `IGitRepository` members:**
```csharp
// New — IGitRepository.cs
Task FetchAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
    => throw new NotSupportedException("This git repository does not support fetch.");

Task<int> GetBehindCountAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
    => throw new NotSupportedException("This git repository does not support behind-count.");
```
`GitRepository` (the sole production implementation) overrides both with real shell-outs; `FakeGitRepository` should be updated in-plan to implement both explicitly (canned return + fault-injection fields, matching its existing `CannedSubjectsAhead`/`ThrowOnSubjectsAhead` shape) so the coordinator's tests can exercise both the "behind" and "fetch failed" branches — relying on the throwing default in the fake would make those code paths untestable.

### Pattern 2: SYNC-14 staleness detection — the fetch-then-count shape, mirroring the existing ahead-of-remote check

**What:** `GetSubjectsAheadOfRemoteAsync` (already shipped, `GitRepository.cs:260-285`) computes **ahead** via `git log --format=%s {remote}/{branch}..HEAD` against whatever the local remote-tracking ref already knows — it explicitly does **not** fetch (its own XML doc says the exception is thrown "when the remote-tracking ref … is unknown (never fetched)", and `DirectPushCoordinator.cs:429-452` catches that and tells the OPERATOR to run `git fetch` manually). This is the strongest single piece of evidence in this research: **no code path in this codebase has ever auto-fetched.**

**Why SYNC-14 cannot reuse that no-fetch pattern:** SYNC-14's literal requirement is "warns/refuses when the local checkout is behind" — i.e., relative to the TRUE current state of the remote, not merely "behind whatever `origin/{branch}` last happened to record." Without a fresh fetch, a "behind count" computed from a stale remote-tracking ref could read 0 (falsely reassuring) even when origin has since advanced.

**When to use:** In `PullFromProdCoordinator.PullAndClassifyAsync`, as a NEW first stage (before "read production content_site_index"), because a stale checkout affects everything downstream (git-tree body resolution, divergence detection against that body).

**Recommended git commands (verified via local `git-fetch(1)`/`git-rev-list(1)` man pages, both long-stable, pre-2.0 features):**
```
git fetch {remote} {branch}              # scoped fetch — does NOT use --all; updates ONLY
                                          # refs/remotes/{remote}/{branch} (confirmed: "git fetch
                                          # can fetch from either a single named repository ...";
                                          # a scoped <repository> <refspec> arg pair updates the
                                          # corresponding remote-tracking branch, not every ref)
git rev-list --count HEAD..{remote}/{branch}   # count of commits reachable from the remote-tracking
                                                # ref but NOT from HEAD — i.e. how many commits the
                                                # local checkout is missing. This is the exact operand
                                                # reversal of GetSubjectsAheadOfRemoteAsync's existing
                                                # "{remote}/{branch}..HEAD" ahead-check.
```

**Example (new `GitRepository.cs` methods, following the file's existing `BuildStartInfo`/`RunAndCaptureAsync` convention):**
```csharp
// Source: pattern mined from GitRepository.cs's existing PushAsync/GetSubjectsAheadOfRemoteAsync
public async Task FetchAsync(string repoRoot, string remote, string branch, CancellationToken ct = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
    ArgumentException.ThrowIfNullOrWhiteSpace(remote);
    ArgumentException.ThrowIfNullOrWhiteSpace(branch);

    var startInfo = BuildStartInfo(repoRoot);   // already sets GIT_TERMINAL_PROMPT=0 — no hang risk
    startInfo.ArgumentList.Add("fetch");
    startInfo.ArgumentList.Add(remote);
    startInfo.ArgumentList.Add(branch);

    // Throws GitCommandException on non-zero exit (offline, auth failure, unknown branch, etc.) —
    // the caller (coordinator) treats this as indeterminate and WARNS, never hard-refuses (D-guidance).
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

**Coordinator usage (warn-then-proceed, never hard-refuse):**
```csharp
onStage("check local checkout freshness");
try
{
    var branch = await _git.GetCurrentBranchAsync(repoRoot, cancellationToken).ConfigureAwait(false);
    await _git.FetchAsync(repoRoot, "origin", branch, cancellationToken).ConfigureAwait(false);
    var behindCount = await _git.GetBehindCountAsync(repoRoot, "origin", branch, cancellationToken).ConfigureAwait(false);
    if (behindCount > 0)
    {
        log.Report($"WARNING: local checkout is {behindCount} commit(s) behind origin/{branch} — " +
            "consider running 'git pull' before adopting. Proceeding with the current git tree.");
        // stamp a freshness field the page renders as a persistent banner (not just log-buried)
    }
}
catch (OperationCanceledException) { throw; }  // cancellation must propagate, never be swallowed
catch (GitCommandException)
{
    log.Report("Could not verify checkout freshness (fetch failed — offline, VPN, or auth). " +
        "Proceeding with the local git tree as-is.");
}
```

**Warn-vs-refuse recommendation:** WARN-then-proceed, per the CONTEXT guidance and this phase's own "surface to the operator" theme (matching D-01's divergence posture). Hard-refuse is NOT recommended because: (1) the fetch is a real network operation whose failure modes (offline, VPN split-tunnel, transient credential prompt) are common enough on an operator's local machine that a hard block would make Pull unusable exactly when it might be most needed for triage; (2) unlike DirectPush (which refuses to push to preserve prod correctness — a destructive-direction concern) or Reconcile Apply (which refuses to hide prod rows), Pull writes LOCAL-only (D-03) — the actual harm of proceeding on a stale checkout is "the divergence/adopt decisions are made against a body that's one or more commits old," which the existing D-01 divergence guard and the operator's own judgment can catch, not a silent-corruption risk.

### Pattern 3: SYNC-15 divergence stamping — orthogonal to `SyncDiffKind`, not a fifth kind value

**What:** `SyncDiffKind` (`SyncDiffEntry.cs:11-28`) encodes a **temporal** classification (ProdNewer / MissingLocally / LocalOnly / Diverged, based on `IndexedUtc` comparison + content-signature equality). Body-hash divergence (SYNC-15) is an **independent axis** — a `ProdNewer` or `MissingLocally` entry can equally have a divergent body hash (e.g., prod's index row says "newer" but the specific body_sha256 disagrees with the git body about to be adopted). Folding it into a fifth `SyncDiffKind` value would force a choice between the two axes and lose information the operator needs (both "why prod/local differ" AND "is the body itself corrupted/mismatched").

**When to use:** Add a new field to `SyncDiffEntry`, stamped in the SAME `.Select(...)` the coordinator already uses to stamp `ArtifactDownloaded` (`PullFromProdCoordinator.cs:119-121`).

**Recommended shape (extends `SyncDiffEntry.cs`):**
```csharp
// New enum — SyncDiffEntry.cs
/// <summary>
/// SYNC-15: whether a git-tree body's computed hash agrees with prod's stored body_sha256 for an
/// entry. Orthogonal to SyncDiffKind (a temporal classification) — a ProdNewer or MissingLocally
/// entry can independently be divergent.
/// </summary>
public enum BodyDivergenceStatus
{
    /// <summary>No git body was resolved, or the entry has no ProdRow (LocalOnly) — divergence cannot be evaluated.</summary>
    NotApplicable,
    /// <summary>Git body hash matches prod's body_sha256 — safe to adopt (subject to D-02 field-authority rules).</summary>
    Clean,
    /// <summary>Git body hash DIFFERS from prod's body_sha256 (D-01) — excluded from the default adopt set.</summary>
    Confirmed,
    /// <summary>Prod's body_sha256 is null (legacy unbackfilled row, D-01a) — cannot confirm or deny; treated like Confirmed for adopt-exclusion.</summary>
    Indeterminate
}

// SyncDiffEntry — new property
public BodyDivergenceStatus BodyDivergence { get; init; }
```

**Coordinator stamping (extends the existing `.Select` at `PullFromProdCoordinator.cs:119-121`):**
```csharp
var entries = ContentSyncDiffClassifier.Classify(prodRows, localRows, _logger)
    .Select(e =>
    {
        var downloaded = availableSet.Contains(e.ArtifactPath);
        var divergence = BodyDivergenceStatus.NotApplicable;

        // Divergence only meaningful when there IS a resolved git body AND a prod row to compare
        // against — matches D-01's "for an entry being adopted" framing (LocalOnly has no ProdRow).
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

**Page adopt-filter + explicit opt-in (extends `PullFromProd.razor.cs`'s `ApplyResolutionsAsync`):**
```csharp
// D-01: divergent/indeterminate rows require a SEPARATE explicit acknowledgement beyond the
// normal "adopt prod" radio — a per-entry checkbox the operator must independently check.
var adoptEntries = _diffEntries
    .Where(e => GetResolution(e) == Resolution.AdoptProd
        && e.Kind != SyncDiffKind.LocalOnly
        && e.ProdRow is not null
        && (e.BodyDivergence is BodyDivergenceStatus.NotApplicable or BodyDivergenceStatus.Clean
            || _divergenceOverrides.Contains(EntryKey(e))))   // NEW: explicit per-entry opt-in set
    .ToList();
```

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Git behind/ahead/fetch operations | A `LibGit2Sharp`-based graph walker, or hand-parsed `git log`/`git status` scraping outside `IGitRepository` | Extend `IGitRepository`/`GitRepository` with `FetchAsync`/`GetBehindCountAsync` via the existing `Process`-based shell-out convention (`BuildStartInfo`/`RunAndCaptureAsync`) | Every git operation in Studio already goes through this ONE seam; a second git-access pathway would need its own credential/env handling (`GIT_TERMINAL_PROMPT=0` equivalent) and is a NEW PACKAGE requiring explicit user approval per `./CLAUDE.md` |
| Body-content hashing for divergence detection | A second/local SHA-256-over-body routine | `ContentSiteIndexContentSignature.ComputeBodySha256` | P89 established this as the ONE hash surface (its own doc comment: "This is the ONE hash surface — both the publish-time compute and the render-time guard must call this method"); duplicating it risks silently reintroducing the CP437-mojibake / EOL-mismatch bug class it exists to prevent |
| Divergence discrepancy persistence | A new persisted "divergence" table/store mirroring `ContentKbReconcileStore` | An in-memory `SyncDiffEntry` stamp, recomputed fresh on every Pull run | D-01 explicitly rejected merging this into the P91 reconcile page/store — Pull's divergence view is transient per-run UI state, not a persisted operator queue |
| Artifact path containment checks | A new/duplicate `..`-traversal or rooted-path guard for the divergence-check file read | `ArtifactPathSafety.TryBuildContainedPath` | Already the ONE Studio path-safety implementation (ratified 90-CONTEXT D-11 specifically to eliminate duplicate copies); the divergence-check read uses the exact same repoRoot + `entry.ArtifactPath` the exists-check already validates |
| Composite natural-key formatting for a new per-entry override set | A bespoke divergence-override key scheme | The page's existing `EntryKey(SyncDiffEntry)` helper (`$"{NaturalKeyType}:{NaturalKeyValue}"`, `PullFromProd.razor.cs:237`) | Already used for `_resolutions`; a second override dictionary keyed the same way keeps the page's existing per-entry state pattern consistent |

**Key insight:** This phase's entire surface area is a hardening pass on code that already exists and is already correct in its core design (D-02 *ratifies* rather than changes the field split). The risk is not "missing library" — it is duplicating logic (a second hash function, a second path guard, a second discrepancy store) that already has exactly one canonical home elsewhere in the cycle's own prior phases.

## Common Pitfalls

### Pitfall 1: Copying P91's null-hash-skip behavior into this phase's divergence check
**What goes wrong:** `ContentKbReconcileClassifier.Classify` (P91, `ContentKbReconcileClassifier.cs:101`) gates its `BodyHashMismatch` check on `!string.IsNullOrEmpty(row.BodySha256)` — it silently SKIPS (does not flag) rows with a null hash.
**Why it happens:** It's the nearest, most-recently-shipped analog in the codebase, and a planner/implementer skimming for "how did we do this last time" will find this exact pattern first.
**How to avoid:** D-01a for THIS phase requires the opposite: a null `body_sha256` is `Indeterminate`, which must be treated like `Confirmed` for adopt-exclusion purposes (surface, don't auto-adopt) — not silently passed through as "no issue." Any implementation that mirrors the P91 null-guard verbatim will silently auto-adopt legacy unbackfilled rows, which is exactly the SYNC-15 failure mode the requirement exists to prevent.
**Warning signs:** A test asserting "prod row with null `BodySha256` classifies as `Clean`/is adopted without operator opt-in" — this should fail review.

### Pitfall 2: Auto-fetching unscoped (`git fetch --all` or bare `git fetch`)
**What goes wrong:** An unscoped fetch pulls every branch and tag from the remote, which is slower, noisier, and can update remote-tracking refs for branches unrelated to the current checkout — inflating the network/latency cost of every single Pull run.
**Why it happens:** `git fetch` with no arguments is the most commonly reached-for form.
**How to avoid:** Scope to `git fetch {remote} {branch}` (exactly the current branch), matching `PushAsync`'s existing per-branch precision (`HEAD:refs/heads/{branch}`) rather than a repo-wide operation.
**Warning signs:** `FetchAsync`'s `ArgumentList` missing an explicit branch argument.

### Pitfall 3: Treating a `GetBehindCountAsync` failure the same as "behind"
**What goes wrong:** Collapsing "fetch/rev-list failed (indeterminate)" and "confirmed N commits behind" into the same warning copy loses the distinction between "we know you're stale" and "we couldn't check" — the operator needs different guidance for each (git-pull vs. check-network/credentials).
**Why it happens:** Both are "not confirmed clean," tempting a single boolean.
**How to avoid:** Keep them as distinct outcomes (mirrors the P90/P91 house-wide tri-state fail-safe pattern — e.g. `TryReadFlagAsync`'s `true`/`false`/`null` distinguishing "confirmed off" from "could not read") — surface separate copy for "N commits behind" vs. "could not verify freshness."
**Warning signs:** A single `bool _stale` field instead of a small status type carrying both the behind-count and whether the check itself succeeded.

### Pitfall 4: Restricting the divergence check to `SyncDiffKind.Diverged` entries only
**What goes wrong:** Body-hash divergence is orthogonal to the temporal `Kind` classification (see Pattern 3 above) — gating the divergence check on `Kind == Diverged` will miss divergent `ProdNewer` and `MissingLocally` entries entirely.
**Why it happens:** The word "diverged" appears in both concepts, inviting an incorrect mental shortcut that they're the same thing.
**How to avoid:** Compute divergence for every entry with a resolved git body AND a non-null `ProdRow`, regardless of `Kind` (excluding only `LocalOnly`, which has no `ProdRow`).
**Warning signs:** A test with a `ProdNewer`-kind entry whose body hash mismatches, expected to show no divergence badge.

### Pitfall 5: Double-reading the git body file (once for `File.Exists`, once for hashing) without noticing the duplication
**What goes wrong:** Not a correctness bug, but the existing exists-check loop (`PullFromProdCoordinator.cs:93-110`) only calls `File.Exists`; the new divergence stamp needs the actual text, so a second `File.ReadAllText` per entry is added in a separate pass. For small operator-triggered Pull runs (content-kb markdown bodies, dozens to low hundreds of entries) this is not a performance problem, but an implementer might be tempted to "optimize" by merging the two loops mid-plan, which changes the existing exists-check's control flow (currently also emits a per-row progress log line) and risks a regression in that logging.
**Why it happens:** Natural refactor impulse when two loops touch overlapping data.
**How to avoid:** Either (a) leave the two passes separate (simplest, matches existing code shape, safe), or (b) merge deliberately as an explicit plan task with its own test coverage for the merged logging behavior — not as an incidental drive-by change while implementing SYNC-15.
**Warning signs:** A diff that touches the exists-check loop's progress-log lines without a task in the plan calling that out.

### Pitfall 6: Blocking the Pull button/page on a hanging fetch
**What goes wrong:** If the new `FetchAsync` doesn't honor the same non-interactive/cancellable conventions as every other `IGitRepository` member, a credential prompt or slow network could hang the whole Pull operation indefinitely.
**Why it happens:** Easy to forget when adding a new member to `BuildStartInfo`-based code, but `BuildStartInfo` already sets `GIT_TERMINAL_PROMPT=0` for every command built through it (`GitRepository.cs:304`) — so this is actually a NON-issue as long as `FetchAsync` uses `BuildStartInfo` and NOT a hand-rolled `ProcessStartInfo`.
**How to avoid:** Always route through the existing `BuildStartInfo(repoRoot)` helper, and pass `cancellationToken` through to `RunAndCaptureAsync` exactly like every other member.
**Warning signs:** A `new ProcessStartInfo(...)` constructed directly inside `FetchAsync` instead of calling `BuildStartInfo`.

## Code Examples

See §Architecture Patterns above — Pattern 2 (staleness detection) and Pattern 3 (divergence stamping) contain the complete, verified-against-actual-source code shapes for both new capabilities, mined directly from the existing `GitRepository.cs`, `PullFromProdCoordinator.cs`, and `PullFromProd.razor.cs` conventions rather than external sources (this phase has no external-library surface).

## State of the Art

Not applicable in the usual "library version drift" sense — this phase's "state of the art" question is entirely intra-codebase: does the newest sync-hardening phase (91, reconcile) suggest a different approach than what's proposed here? Yes, on one specific point (the null-hash handling — see Pitfall 1), and the CONTEXT.md decisions (D-01, D-01a) already resolve that divergence deliberately. No external framework/library changed underneath this phase since P89-91 shipped (days apart, same milestone).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Recommending TWO new `IGitRepository` members (`FetchAsync` + `GetBehindCountAsync`) rather than one combined member is a design choice, not verified against any spec — it mirrors the existing `GetSubjectsAheadOfRemoteAsync` granularity (one op, one purpose) but is not the only valid decomposition. | Architecture Patterns, Pattern 2 | Low — if the planner prefers a single combined method, the git commands and warn-then-proceed logic are unaffected; only the interface shape changes. |
| A2 | Recommending an orthogonal `BodyDivergenceStatus` enum on `SyncDiffEntry` rather than folding divergence into `SyncDiffKind` is this research's own design recommendation — CONTEXT.md's own code_context section hedges with "likely a new SyncDiffKind value," leaving this genuinely open. | Architecture Patterns, Pattern 3 | Medium — if the planner instead adds a 5th `SyncDiffKind` value, the existing `ProdNewer`/`MissingLocally`/`Diverged` temporal semantics would need to be re-derived or discarded for divergent entries, which changes more of the existing badge-rendering switch statement in `PullFromProd.razor` than the orthogonal-flag approach does. Flagging this explicitly so the planner makes it a deliberate choice, not a default. |
| A3 | Warn-then-proceed (never hard-refuse) is this research's recommendation based on Pull's local-only-write risk profile — CONTEXT.md itself frames this as "not locked," acceptable either way if research shows fetch is reliable. This research found fetch to be *usually* reliable (operator's own machine, own credentials, existing `GIT_TERMINAL_PROMPT=0` precedent) but did not — and could not, without live network access from this session — measure actual fetch latency/failure rates on the operator's real machine. | Architecture Patterns, Pattern 2 | Low-Medium — a hard-refuse design is explicitly sanctioned as acceptable by CONTEXT.md; if the operator's environment turns out to have unreliable git remote access, warn-then-proceed is strictly safer (never blocks Pull) than a refuse design would be. |

## Open Questions

1. **Should the freshness check run every Pull, or be a separate on-demand action?**
   - What we know: CONTEXT.md's guidance frames it as part of the Pull flow ("Pull warns/refuses when the local checkout is behind"), and D-03 says the whole phase is always-on with no flag.
   - What's unclear: whether operators will find a fetch-on-every-Pull-click acceptable latency-wise, versus a separate "Check freshness" button that's optional.
   - Recommendation: Run it as the first stage of `PullAndClassifyAsync` (as designed above) since that's the literal reading of SYNC-14 and D-03's "always-on" framing; if latency becomes a real operator complaint post-ship, a follow-on could add a skip toggle — but that is out of THIS phase's scope per the Deferred Ideas list (no new gating).

2. **Exact return-type/wiring for surfacing the freshness result to the page (progress-log line only, vs. a dedicated field/banner).**
   - What we know: the existing `PullAndClassifyAsync` signature returns `Task<IReadOnlyList<SyncDiffEntry>>` and takes an `IProgress<string> log` + `Action<string> onStage`; a progress-log line alone satisfies "surfaced," but CONTEXT's emphasis on "surface to the operator" (matching the divergence UI treatment) suggests a persistent banner is more in keeping with house style (the page already has a persistent "READ-ONLY toward production" banner, not just a log line).
   - What's unclear: whether to widen `PullAndClassifyAsync`'s return type (e.g., a wrapper record `PullClassifyResult(Entries, FreshnessStatus)`) or add a separate coordinator method the page calls alongside it.
   - Recommendation: Left to the planner (CONTEXT explicitly defers "exact UI treatment"); a wrapper record is the lower-risk option since it keeps freshness and entries produced by the same atomic call, avoiding a second round-trip that could observe a different git state.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `git` CLI | Every `IGitRepository` operation, including the two new SYNC-14 members | ✓ | (verified present in this research session; `git-fetch(1)`/`git-rev-list(1)` man pages resolved locally) | None needed — already a hard requirement for Publish/DirectPush/Reconcile; this phase adds no NEW environment dependency |
| Network access to the git remote (`origin`) from the Studio host | `FetchAsync` (SYNC-14) | Not verifiable from this research session (depends on the operator's actual machine at Pull-run time) | — | Warn-then-proceed design (Pattern 2) — a fetch failure is caught and logged as "could not verify freshness," Pull proceeds with the existing (possibly stale) local git tree; never a hard block |
| Postgres access to prod (read-only) | `IProdContentReader.ReadAllAsync` | Already required by the existing, unmodified Pull flow | — | Already gated by `Config.IsProdConfigured` on the page — unaffected by this phase |

**Missing dependencies with no fallback:** None — `git` itself is a pre-existing hard requirement, unchanged by this phase.

**Missing dependencies with fallback:** Network access for `FetchAsync` — falls back to "proceed with a possibly-stale local tree" per the warn-then-proceed design, matching D-03's "protective, not blocking" framing.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Studio.Tests.csproj`), `bunit` 2.7.2 available but this phase's page tests follow the established non-bUnit pattern (direct code-behind invocation, e.g. `InvokePullApplyForTest()`) |
| Config file | none — standard `dotnet test` discovery |
| Quick run command | `dotnet test DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj --filter "FullyQualifiedName~PullFromProd"` |
| Full suite command | `dotnet test DeckFlow.sln` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SYNC-13 | Adopt still preserves `is_visible`/`is_hidden`, mirrors `approval_status`, copies body from git tree — regression-locks D-02's already-shipped behavior | unit | `dotnet test --filter "FullyQualifiedName~PullFromProdCoordinatorTests"` | ✅ (extend existing) |
| SYNC-14 | `PullAndClassifyAsync` warns (not refuses) when `GetBehindCountAsync` > 0; warns distinctly when `FetchAsync` throws `GitCommandException`; never blocks the pull in either case | unit | `dotnet test --filter "FullyQualifiedName~PullFromProdCoordinatorTests"` | ❌ Wave 0 — new test cases needed, extend `FakeGitRepository` first |
| SYNC-15 | A prod row with a body-hash mismatch is stamped `BodyDivergenceStatus.Confirmed` and excluded from the coordinator's adopt path unless explicitly overridden; a null prod `body_sha256` is stamped `Indeterminate` and excluded identically (D-01a) | unit | `dotnet test --filter "FullyQualifiedName~PullFromProdCoordinatorTests"` | ❌ Wave 0 — new test cases needed |
| SYNC-15 (UI) | Page renders a divergence badge and requires the separate opt-in checkbox before `ApplyResolutionsAsync` includes a divergent entry in `adoptEntries` | unit (code-behind, non-bUnit) | `dotnet test --filter "FullyQualifiedName~PullFromProdPageTests"` | ❌ Wave 0 — new test cases needed |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~PullFromProd"` (fast, scoped)
- **Per wave merge:** `dotnet test DeckFlow.sln` (full suite — this phase touches `DeckFlow.Core` interfaces consumed elsewhere, e.g. `PublishCoordinator`/`DirectPushCoordinator`/`ContentKbReconcileOrchestrator` all depend on `IGitRepository`, so a full-solution build+test is the correct blast-radius check, matching `./CLAUDE.md`'s "Verify builds test project" feedback item)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Studio.Tests/TestDoubles/FakeGitRepository.cs` — needs `Fetch`/`GetBehindCount` canned-return + fault-injection fields (`CannedBehindCount`, `ThrowOnFetch`) before any coordinator test can exercise the new staleness paths
- [ ] `DeckFlow.Studio.Tests/ViewModels/PullFromProdCoordinatorTests.cs` — new test cases for: behind-count > 0 (warn, proceed), fetch throws (warn, proceed), divergence Confirmed/Indeterminate/Clean stamping
- [ ] `DeckFlow.Studio.Tests/PullFromProdPageTests.cs` — new test cases for the divergence opt-in gating and the freshness banner render
- [ ] No new test project or framework needed — everything extends the existing `DeckFlow.Studio.Tests` project per `./CLAUDE.md`'s "no new test project without explicit ask" rule

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | This phase touches no auth surface (Studio has no login; Admin BasicAuth is a public-web-app concern, unaffected) |
| V3 Session Management | No | N/A |
| V4 Access Control | No | Pull is already gated on `Config.IsProdConfigured` (unchanged); no new access-control surface introduced |
| V5 Input Validation | Yes | `ArtifactPathSafety.TryBuildContainedPath` (reused, not reimplemented) for the new divergence-check file read; `git` CLI arguments passed via `ProcessStartInfo.ArgumentList` (never string-concatenated), matching the existing shell-injection-safe convention documented in `GitRepository.cs`'s own class remarks |
| V6 Cryptography | Yes | SHA-256 body hashing — reused verbatim via `ContentSiteIndexContentSignature.ComputeBodySha256`; this phase must NOT hand-roll a second hash routine (see Don't Hand-Roll) |
| V7 Error Handling / Logging | Yes | Exception detail (git stderr, connection strings) must never be surfaced to the UI, per the established D-07 convention already enforced throughout `PullFromProdCoordinator`/`PullFromProd.razor.cs` (`ex.Message` never rendered; full detail logged server-side only) — the new `FetchAsync`/`GetBehindCountAsync` failure paths must follow this exactly (log full `GitCommandException` detail via `_logger`, surface only sanitized copy in the progress log/banner) |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Shell/argument injection via git remote/branch names | Tampering | `ProcessStartInfo.ArgumentList` (not string-concatenated `Arguments`), `UseShellExecute=false` — already the established `GitRepository.cs` convention (class-level remarks explicitly call this out); the new `FetchAsync`/`GetBehindCountAsync` must follow it identically |
| Path traversal via a malicious/corrupted prod `artifact_path` reaching the new divergence-check file read | Tampering / Information Disclosure | `ArtifactPathSafety.TryBuildContainedPath` — already the gate on the identical read for the exists-check; the divergence read must go through the SAME call, not a second unguarded `File.ReadAllText` |
| Credential/connection-string leakage via an unhandled git or Postgres exception surfaced verbatim in the UI | Information Disclosure | Sanitized UI copy + full detail to server-side Serilog only (D-07) — already enforced pattern in this file, must be extended to the new failure paths, not bypassed |
| Silent data corruption served to end users (the original CP437-mojibake motivating incident) | Tampering | This is precisely what SYNC-15's divergence guard defends against — the whole phase IS the mitigation for this threat class; no additional control needed beyond correctly implementing D-01/D-01a |

## Sources

### Primary (HIGH confidence — direct codebase reads, this session)
- `.planning/phases/92-pull-hardening/92-CONTEXT.md` — locked decisions D-01/D-01a/D-02/D-02a/D-03, discretion guidance
- `.planning/REQUIREMENTS.md` §SYNC-13/14/15
- `.planning/STATE.md` — cycle status, prior-phase decision log
- `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` — full read, both methods
- `DeckFlow.Studio/Pages/PullFromProd.razor.cs` + `.razor` — full read
- `DeckFlow.Core/Integration/IGitRepository.cs` + `GitRepository.cs` — full read (interface + all 8 shipped members' implementation)
- `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` — full read, `ComputeBodySha256`
- `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` + `SyncDiffEntry.cs` — full read
- `DeckFlow.Studio/Services/ArtifactPathSafety.cs` — full read
- `DeckFlow.Studio/Services/IProdContentReader.cs` + `ProdContentReader.cs` — full read
- `DeckFlow.Core/Content/ContentKbReconcileClassifier.cs` + `ContentKbReconcileDiscrepancy.cs` — full read (P91 analog)
- `DeckFlow.Studio/Services/ContentKbReconcileOrchestrator.cs` — full read (git-tree body read pattern for hashing)
- `DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs` — full read (operator-gated action pattern, tri-state flag idiom)
- `DeckFlow.Studio/Pages/Reconcile.razor.cs` — grep for discrepancy-list render pattern
- `DeckFlow.Studio.Tests/TestDoubles/FakeGitRepository.cs` — full read (confirms only-one-double, existing canned/fault-injection shape)
- `DeckFlow.Studio.Tests/ViewModels/PullFromProdCoordinatorTests.cs` — partial read (constructor/test shape)
- `DeckFlow.Studio/Services/StudioRepoLocator.cs` — full read (`DECKFLOW_REPO_ROOT` env override)
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (lines 395-454) — full read of the existing ahead-of-remote / no-fetch precedent
- `./CLAUDE.md` (project root) — dependency-addition, testing, and commit conventions
- Local `git-fetch(1)` and `git-rev-list(1)` man pages (via `git fetch --help` / `git rev-list --help` in this session) — verified `git fetch {remote} {refspec}` updates the corresponding remote-tracking branch, and `git rev-list --count` semantics

### Secondary (MEDIUM confidence)
None — this phase required no external web research; every claim traces to a codebase read or a locally-resolved git man page.

### Tertiary (LOW confidence)
None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; every capability already exists in the solution
- Architecture: HIGH — directly read every file this phase touches or must extend; the one design choice (orthogonal divergence flag vs. new `SyncDiffKind`) is explicitly flagged as an assumption (A2) for the planner to ratify or override
- Pitfalls: HIGH — five of six pitfalls are drawn from direct contrast with already-shipped P91 code in this same codebase, not speculation

**Research date:** 2026-07-10
**Valid until:** 30 days (stable — this is an internal hardening phase on code that does not depend on any external library version; the only external-facing dependency, the `git` CLI's `fetch`/`rev-list --count` behavior, has been stable for over a decade)
