---
phase: 260627-qyc
plan: "01"
subsystem: DeckFlow.Studio / DeckFlow.Core
tags: [prod-write-integrity, transactional-batch, content-diff, security, studio]
dependency-graph:
  requires: []
  provides: [UpsertContentColumnsOnlyBatchAsync, ContentSiteIndexContentSignature, H4-transactional-batch, M2-content-aware-diff]
  affects: [DeckFlow.Core.Content, DeckFlow.Studio.Pages.DirectPush, DeckFlow.Studio.Tests]
tech-stack:
  added: []
  patterns: [one-connection-one-transaction, default-interface-implementation, tdd-red-green]
key-files:
  created:
    - DeckFlow.Core/Content/ContentSiteIndexBatchUpsertException.cs
    - DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs
    - DeckFlow.Core.Tests/Content/ContentSiteIndexStoreBatchUpsertTests.cs
  modified:
    - DeckFlow.Core/Content/IContentSiteIndexStore.cs
    - DeckFlow.Core/Content/ContentSiteIndexStore.cs
    - DeckFlow.Studio/Pages/DirectPush.razor
    - DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs
    - DeckFlow.Studio.Tests/DirectPushPageTests.cs
decisions:
  - "H4: batch upsert uses one DbConnection + one DbTransaction wrapping the existing UpsertContentColumnsOnlySql const verbatim — no SQL duplication, dual-dialect parity maintained"
  - "M2: content signature truncates dates to whole seconds to prevent cross-dialect false positives (SQLite=1s precision, Postgres=microsecond precision)"
  - "ContentSiteIndexBatchUpsertException carries only non-secret row identity (Title + key); DB exception stays in InnerException for sink only (D-07 / SC5)"
  - "Validation runs inside the transaction loop (not all up-front) to prove real rollback semantics rather than skip-bad-row behavior"
  - "IContentSiteIndexStore.UpsertContentColumnsOnlyBatchAsync uses default-throw implementation following the DeleteAllRowsAsync precedent — three non-DirectPush implementers compile unchanged"
metrics:
  duration: "~45 minutes"
  completed: "2026-06-27"
  tasks_completed: 3
  files_changed: 8
---

# Phase 260627-qyc Plan 01: DirectPush Prod-Write Integrity (H4 + M2) Summary

Transactional all-or-nothing batch prod upsert (H4) + content-aware New/Updated/Unchanged diff (M2) for DirectPush, replacing a partial-write-prone per-row loop and a presence-only key diff.

## What Was Built

### H4 — Transactional Batch Prod Upsert

`ContentSiteIndexStore.UpsertContentColumnsOnlyBatchAsync` opens ONE `DbConnection`, begins ONE `DbTransaction`, runs the existing `UpsertContentColumnsOnlySql` for each row inside the transaction, and commits at the end. Any row failure triggers `RollbackAsync` and re-throws as `ContentSiteIndexBatchUpsertException` carrying the failing row's title and natural key (non-secret). The DB exception stays in `InnerException` for the log sink.

`DirectPush.WriteRowsAsync` now calls this single batch method instead of looping `UpsertContentColumnsOnlyAsync`. A `ContentSiteIndexBatchUpsertException` catch block before the generic catch logs the inner exception, surfaces "NOTHING was written to production" + the failing row title in the UI, marks all `_publishRows` as "Rolled back — not written", and skips stamp/visibility (PUB-01).

### M2 — Content-Aware Diff

`ContentSiteIndexContentSignature.BuildSignature` hashes the exact `UpsertContentColumnsOnlySql` column set: source, title, video_url, artifact_path, published_utc, indexed_utc, archetype_tags, bracket_tags, card_category_tags. Dates are truncated to whole UTC seconds (cross-dialect precision parity). Tags serialized via `ContentArtifactSpec.SerializeTags`.

`DirectPush.ComputeDiffAsync` replaces the `HashSet<string>` presence check with a `Dictionary<string, ContentSiteIndexRow>` keyed by natural key value. Each local row is classified:
- Key absent from prod → **New** (added to `_publishRows`)
- Key present, content differs → **Updated** (added to `_publishRows`)
- Key present, content identical → **Unchanged** (excluded from `_publishRows`, SCP, and DB write)

The Diff Preview now shows `Unchanged: N` badge. Stage 2 and 3 only operate on `_publishRows`.

### Success Message (plan-checker M2 note)

Stage-3 success message updated from "approved row(s)" to "new/updated row(s) written... unchanged rows were already up to date and were skipped." — accurately reflects that only the publish set was written.

## Tasks Completed

| # | Name | Commit | Key Output |
|---|------|--------|-----------|
| 1 (RED) | Core test stubs | d10dd5be | Exception, Signature, Interface method, 13 failing tests |
| 1 (GREEN) | Core implementation | 15e9efe6 | ContentSiteIndexStore override, 13 tests passing |
| 2 | Studio wiring | 9a8546b1 | DirectPush M2+H4, Unchanged badge, success copy |
| 3 | Studio.Tests | b9c26300 | FakeContentSiteIndexStore batch, 8 new bUnit tests, 4 updated |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `isNew` local variable unused + DiffRow positional record named-arg mismatch**
- **Found during:** Task 2 build
- **Issue:** Used `isNew: true` as named argument but `DiffRow` positional record parameter is `IsNew` (PascalCase); also had unused `bool isNew` variable
- **Fix:** Removed the local variable; used positional (unnamed) arguments for `DiffRow` constructor calls
- **Files modified:** `DeckFlow.Studio/Pages/DirectPush.razor`
- **Commit:** 9a8546b1

**2. [Rule 1 - Tests] Four existing DirectPush tests needed updates for H4/M2 behavior change**
- **Found during:** Task 3 test run (4 failures in 23 tests)
- **Issues:**
  - `DirectPush_DiffPreview_ShowsNewUpdatedCounts`: prod row was content-identical to local (Unchanged, not Updated) — test predated content-aware diff
  - `DirectPush_UsesContentColumnsOnlyUpsert`: asserted per-row `UpsertContentColumnsOnlyAsync` calls; now a single batch call
  - `DirectPush_DbPartialFailure_PerRowListShown`: tested "partial failure" model that no longer exists in H4 (all-or-nothing replaces it)
  - `DirectPush_DbWriteFailure_SecretsNeverSurface`: expected "Prod upsert failed for this row" (per-row copy); now "NOTHING was written to production" (batch rollback copy)
- **Fix:** Updated all four tests to reflect the new H4/M2 semantics; `DirectPush_DbPartialFailure_PerRowListShown` renamed to `DirectPush_DbBatchFailure_AllOrNothingRollback_MessageShown`
- **Files modified:** `DeckFlow.Studio.Tests/DirectPushPageTests.cs`
- **Commit:** b9c26300

## Build and Test Status

| Project | Build | Tests |
|---------|-------|-------|
| DeckFlow.Core | PASS (0 warnings) | 13/13 batch+signature tests pass |
| DeckFlow.Core.Tests | PASS (0 warnings) | — |
| DeckFlow.Studio | PASS (0 warnings) | — |
| DeckFlow.Studio.Tests | PASS (0 warnings) | 23/23 DirectPush tests pass; 175/175 full suite (one pre-existing bUnit flake in BlockedPageTests noted — passes in isolation and on second run, unrelated to this plan) |

**Postgres note:** Transaction/rollback behavior verified on SQLite (Postgres cannot run in WSL). The `DbConnection.BeginTransactionAsync` wrapper is dialect-agnostic and `UpsertContentColumnsOnlySql` is shared verbatim. Postgres execution is NOT claimed.

## Known Stubs

None — all new functionality is fully wired.

## Threat Surface Scan

No new network endpoints, auth paths, or schema changes introduced. `UpsertContentColumnsOnlyBatchAsync` writes to the existing `content_site_index` table via the same SQL as the existing per-row method. The threat model items (T-qyc-01 through T-qyc-04) are all addressed:

| Threat ID | Status |
|-----------|--------|
| T-qyc-01 | MITIGATED — SQLite rollback test proves all-or-nothing |
| T-qyc-02 | MITIGATED — SentinelSecret bUnit assertion guards DB exception from markup |
| T-qyc-03 | MITIGATED — Core test asserts is_visible/is_evergreen preserved after batch |
| T-qyc-04 | MITIGATED — stamp/visibility skipped on ContentSiteIndexBatchUpsertException |

## Self-Check: PASSED

All 8 required files exist on disk. All 4 task commits verified in git log. Core batch tests: 13 passed. Studio DirectPush tests: 23 passed. All 4 projects build with `-warnaserror`, 0 warnings.

## Plan-check / Codex review / Verification (post-execution)

- **Plan-checker:** VERIFICATION PASSED (0 blockers, 2 minor warnings — success-copy wording folded into Task 2; a doc-precision nit).
- **Codex cross-AI review** (gpt-5.4, medium) of the diff: CHANGES-REQUESTED → resolved. 1 MED, all other dimensions CLEAN (atomicity, D-08, signature columns, D-07 UI secrecy, blast radius, test intent).
  - **MED (data loss) — FIXED `6836fc95`.** M2 diff keyed on the bare value (youtube id OR rss guid); a prod podcast row + local youtube row sharing a value could collide → matching signature → misclassified Unchanged → silently skipped from publish. Now keyed on the full `(type, value)` composite. +1 regression test (`M2_ComputeDiff_DifferentKeyTypeSameValue_NotMisclassifiedUnchanged`).
- **Verifier:** PASSED 8/8 must-haves against the codebase. One INFO (non-blocking) — cancellation-path rollback used the already-cancelled token; **fixed `2ce8e47c`** to roll back with `CancellationToken.None`.

Final verification: full solution build clean (0/0); Core batch 13/13; DirectPush bUnit 24/24.
