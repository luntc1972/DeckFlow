---
phase: 47-direct-prod-db-scp-publish-path
plan: 03
subsystem: DeckFlow.Studio
tags: [prod-publish, scp, ssh-net, bunit, blazor, safe-upsert, secret-redaction]

# Dependency graph
requires:
  - phase: 47-01
    provides: ISshArtifactUploader/IProdStoreFactory contracts, SSH.NET 2025.0.0, DirectPushPageTests stub, FakeContentSiteIndexStore.UpsertMethodCalls seam
  - phase: 47-02
    provides: SftpArtifactUploader transport + Program.cs SCP detection + DI registrations
provides:
  - DirectPush.razor 3-stage gated PROD-write page (@page /direct-push) — built in Task 1 (commit 6026419)
  - Direct Push nav entry below Publish
  - 11 named DirectPush bUnit tests implemented (13 discrete cases — not-configured runs as a 3-case [Theory])
  - FakeContentSiteIndexStore fault-injection (KeysToFailOnUpsert / UpsertFailureMessage / ReadFailureMessage) + MEDIUM-4 full-row-upsert guard
  - DirectPush.InvokeWriteRowsForTest internal seam for the MEDIUM-1 hard-guard test
affects: [phase-48-ui-audit, prod-publish-operations]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - bUnit one-render-per-context (multi-variant gate tests must use [Theory], not multiple Render calls in one context)
    - sanitized-literal-only catch copy proven by sentinel-bearing fault injection (HIGH-2)
    - internal handler test-seam via InternalsVisibleTo for guard paths unreachable through a disabled button (MEDIUM-1)

key-files:
  created:
    - .planning/phases/47-direct-prod-db-scp-publish-path/47-03-SUMMARY.md
  modified:
    - DeckFlow.Studio.Tests/DirectPushPageTests.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs
    - DeckFlow.Studio/DeckFlow.Studio.csproj
    - DeckFlow.Studio/Pages/DirectPush.razor

key-decisions:
  - "DirectPush_NotConfigured_ButtonsDisabled converted from one [Fact] that rendered 3 components in a single BunitContext (illegal — services cannot be registered after first resolve) to a [Theory] with 3 InlineData variants; each variant renders in its own context. All 3 named variants (both-missing / prod-only / scp-only) still covered."
  - "InternalsVisibleTo(DeckFlow.Studio.Tests) + DirectPush.InvokeWriteRowsForTest are the test seam for the MEDIUM-1 hard-guard: bUnit will not dispatch a click to a disabled button, so the guard (return early when !_scpSuccess) is only reachable by invoking the production handler directly. The seam calls the exact production handler — no behavior duplicated."
  - "FakeContentSiteIndexStore full-row upserts (UpsertRowAsync / UpsertRowPreservingVisibilityAsync) throw InvalidOperationException on the prod fake (MEDIUM-4) so any accidental is_visible/is_evergreen clobber fails loudly, not just via an absent assertion."

patterns-established:
  - "Pattern: multi-config gate tests use [Theory]/[InlineData] so each render gets a fresh BunitContext"
  - "Pattern: HIGH-2 secret redaction is proven by driving a sentinel-bearing exception message ('Host=...;Password=hunter2') through every catch and asserting the markup contains none of the sentinel substrings"

requirements-completed: [PUB-04, PUB-05]

# Metrics
duration: ~18min (resume of interrupted execution)
completed: 2026-06-16
---

# Phase 47 Plan 03: DirectPush Page + 11 bUnit Tests Summary

**Completed the Direct Prod-DB + SCP publish surface: Task 1 page+nav (pre-committed at 6026419) plus the full 11-test bUnit suite proving artifact-first gating, safe-upsert-only prod writes, per-item reconcile, and sentinel-proven secret redaction across the happy, diff-read-failure, and DB-write-failure paths. Task 3 (SSH.NET supply-chain + PROD-write UI verify) is a blocking-human checkpoint owned by the orchestrator and was NOT executed.**

## Resume Context

This plan was resumed after a prior executor was interrupted by an API rate limit mid-Task-2.
Task 1 (`feat(47-03): add DirectPush 3-stage gated prod-publish page + nav entry`, commit
`6026419`) was already committed; the DirectPush.razor page and NavMenu entry existed and were
NOT recreated. Task 2 had uncommitted working-tree changes (11 written tests, extended fake,
csproj InternalsVisibleTo, razor test seam) which this resume verified, fixed, and committed.

## Performance

- **Duration:** ~18 min (resume only)
- **Completed:** 2026-06-16
- **Tasks:** Task 2 finished + committed; Task 1 pre-existing; Task 3 left for orchestrator
- **Files modified:** 4 (DirectPushPageTests.cs, FakeContentSiteIndexStore.cs, csproj, DirectPush.razor)

## Accomplishments

- Verified the 11 pre-written DirectPush tests against the actual DirectPush.razor markup —
  all asserted copy strings, the 2-`btn-danger` Stage-2/Stage-3 layout, the `prodReviewed`
  checkbox id, the `New: @_newCount`/`Updated: @_updatedCount` badges, and the sanitized catch
  literals all line up.
- **Fixed a real test bug (Rule 1):** `DirectPush_NotConfigured_ButtonsDisabled` rendered three
  components inside one `BunitContext`, which throws `InvalidOperationException` ("New
  services/implementations cannot be registered ... after the first service has been
  retrieved"). Converted to a `[Theory]` with 3 `InlineData` variants so each renders in its own
  context. Build + run confirm the fix.
- Full DirectPush filter: **13 passed, 0 failed** (10 facts + the 3 theory cases). Full Studio
  suite: **34 passed, 0 failed, 0 skipped**.
- All 11 named behaviors are covered, including the 3 Codex additions: HIGH-2 diff-read secret
  leak, HIGH-2 DB-write secret leak, and MEDIUM-1 Stage-3 hard-guard.

## Task Commits

1. **Task 1: Build DirectPush.razor + NavMenu entry** — `6026419` (feat) — pre-committed by the
   interrupted executor; verified present, not recreated.
2. **Task 2: 11 bUnit tests + fault-injection seams** — `ca9d824` (test) — un-stubbed
   RenderDirectPush, implemented all 11 facts, added fault-injection hooks + MEDIUM-4 full-row
   guard, `[Theory]` fix for the not-configured test; includes the InternalsVisibleTo +
   `InvokeWriteRowsForTest` seam (they exist solely to enable the tests).
3. **Task 3: SSH.NET supply-chain + PROD-write UI human-verify** — **PENDING — orchestrator-owned
   blocking-human checkpoint; NOT auto-approvable** (see below).

## Files Created/Modified

- `DeckFlow.Studio.Tests/DirectPushPageTests.cs` — 11 named DirectPush facts implemented (13
  discrete cases); `[Theory]` not-configured variant; sentinel-secret assertions across 3 paths.
- `DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs` — `KeysToFailOnUpsert`,
  `UpsertFailureMessage`, `ReadFailureMessage` fault-injection; full-row upserts throw (MEDIUM-4).
- `DeckFlow.Studio/DeckFlow.Studio.csproj` — `<InternalsVisibleTo Include="DeckFlow.Studio.Tests" />`
  (test seam only; no internal surface leaks to consumers).
- `DeckFlow.Studio/Pages/DirectPush.razor` — `internal Task InvokeWriteRowsForTest() =>
  WriteRowsAsync();` seam for the MEDIUM-1 hard-guard test (8 lines; page logic from Task 1 in
  6026419 unchanged).

## Task 3 — PENDING (blocking-human checkpoint, orchestrator-owned)

Task 3 is `type="checkpoint:human-verify" gate="blocking-human"` and is **NOT auto-approvable**
(`workflow.auto_advance` is explicitly ignored per T-47-SC). This resume executor did **not**
execute, simulate, or approve it. It requires a human operator to:

- **A. Supply-chain gate (T-47-SC):** confirm `DeckFlow.Studio.csproj` pins exactly `SSH.NET`
  `2025.0.0` (author Renci, github.com/sshnet/SSH.NET, MIT), verify the package + transitives
  LIVE on nuget.org at execution time, and confirm SSH.NET appears ONLY in DeckFlow.Studio.
- **B. PROD-write UI danger surface:** run Studio locally (`DECKFLOW_DISABLE_AUTO_BROWSER=true`),
  navigate to `/direct-push`, confirm the not-configured warning + disabled buttons, the
  TARGET:PRODUCTION banner, the checkbox→SCP gate, the artifact-first DB lock, and presence-only
  startup logs (no secret values).
- **C. (Optional) live SCP + prod-Postgres smoke** with real Render SSH + prod secrets.

The orchestrator owns presenting this checkpoint and collecting the operator's "approved".

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `DirectPush_NotConfigured_ButtonsDisabled` rendered 3 components in one BunitContext**
- **Found during:** Task 2 verification (test run reported 10/11 pass, this one failing).
- **Issue:** A single `[Fact]` called `RenderDirectPush` three times. `BunitContext` shares one
  `Services` collection per test instance; once the first `Render<DirectPush>()` resolves
  services, registering new singletons throws `InvalidOperationException`. The 2nd
  `RenderDirectPush` call failed.
- **Fix:** Converted the fact to a `[Theory]` with 3 `InlineData` rows (both-missing /
  prod-only / scp-only), each rendering once in its own context. xUnit gives each theory case a
  fresh test instance → fresh context.
- **Files modified:** `DeckFlow.Studio.Tests/DirectPushPageTests.cs`
- **Commit:** `ca9d824`

No other deviations. Task 1 was pre-committed exactly as planned; the fault-injection fake and
the razor/csproj test seams matched the plan's Task 2 action.

## Authentication Gates

None encountered during Task 2 — all tests use fakes; no live SSH or Postgres connection is
made. (Task 3's optional live smoke would involve real SSH/Postgres but is the operator's,
not this executor's, action.)

## Verification

- `DeckFlow.Studio.Tests` builds clean: **Build succeeded, 0 Warning(s)** (transitively builds
  DeckFlow.Studio).
- `dotnet test --filter "DirectPush"`: **13 passed, 0 failed** (the 11 named facts; the
  not-configured fact expands to 3 theory cases).
- Full Studio suite: **34 passed, 0 failed, 0 skipped**.
- All changed files are LF-only (0 CRLF lines); the `scripts/format-check-changed.sh staged`
  changed-lines gate returns exit 0 on the staged Task 2 files.
- Source assertions confirmed against DirectPush.razor: `UpsertContentColumnsOnlyAsync` present,
  `UpsertRowAsync`/`UpsertRowPreservingVisibilityAsync` absent from the write path; exactly 2
  `btn-danger` buttons (Stage 2 then Stage 3); the WriteRowsAsync hard-guard
  `if (!_scpSuccess || _operationInFlight || !_diffReady) return;` present; diff-read and
  DB-write catches use sanitized literals, never `ex.Message`.

### Pre-existing warning (out of scope)
`DeckFlow.Core/Orchestration/IContentIndexExporter.cs(40,20): CS1574` (unresolvable
`StageAndCommitAsync` cref) is pre-existing in DeckFlow.Core, unrelated to this plan, and already
logged in 47-01/47-02 summaries. Not fixed (scope boundary).

## Known Stubs

None. All 8 original stub facts were filled and the `Render<DirectPush>()` call un-stubbed; the 3
Codex-added tests are implemented. `grep "stub — implemented in 47-03"` returns nothing.

## Self-Check: PASSED

- FOUND: DeckFlow.Studio.Tests/DirectPushPageTests.cs
- FOUND: DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs
- FOUND: DeckFlow.Studio/Pages/DirectPush.razor
- FOUND: DeckFlow.Studio/DeckFlow.Studio.csproj
- FOUND commit: 6026419 (Task 1 — pre-existing)
- FOUND commit: ca9d824 (Task 2)
