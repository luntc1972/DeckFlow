---
phase: 27-deck-cache-content-hash-dedup-5-day-refresh
verified: 2026-05-26T17:10:00Z
status: passed
score: 6/6 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: none
  previous_score: n/a
---

# Phase 27: Deck-Cache Content-Hash Dedup + 5-Day Refresh — Verification Report

**Phase Goal:** The harvest skips rewriting a deck's cached rows when its cards/categories are unchanged (content hash per deck source), and re-checks a deck only after 5 days — cutting write amplification on the category cache while keeping data fresh.
**Verified:** 2026-05-26T17:10:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Success Criterion | Verdict | Evidence |
|---|-------------------|---------|----------|
| 1 | Re-harvesting an UNCHANGED deck performs NO delete/insert on fact tables (only `last_checked_utc` updates) — proven by a write-counting test | ✓ PASS | `PersistDeckAsync` (ArchidektDeckCacheSession.cs:184-187) returns `Unchanged` and skips `ReplaceDeckEntriesAsync` on hash match. Proven by `RunAsync_UnchangedDeck_SkipsFactTableWrites` (ContentHashDedupTests.cs:186-217): ages `last_checked_utc` to -6d, re-queues via `AddDeckIdsAsync`, captures full before/after fact-row snapshots of BOTH tables incl. `last_seen_utc` (lines 371-390), asserts `before == after` (line 210), `DecksUnchanged==1`, `DecksProcessed==0`, and `last_checked_utc` advanced (line 216). Connection-safe snapshot equality per plan — no TEMP triggers/data_version. GREEN. |
| 2 | Re-harvesting a CHANGED deck rewrites rows (replace semantics) AND updates stored hash; totals-only changes (uncategorized card / board move) force rewrite | ✓ PASS | Changed path (session.cs:189-192): clear hash → `ReplaceDeckEntriesAsync` (DELETE+INSERT) → set new hash. `RunAsync_ChangedDeck_RewritesAndUpdatesHash` (tests:220-247) asserts old card gone, new card present, hash changed. Totals-only coverage: `ComputeHash_UncategorizedCardChangesHash` (tests:89-102) + `ComputeHash_BoardMoveChangesHash` (tests:104-113) — hash covers BOTH observations and totals (DeckCategoryCacheWriter.cs:96-112). GREEN. |
| 3 | Content hash is stable, order-independent, injection-safe over the full written shape | ✓ PASS | `ComputeCanonicalHash` (writer.cs:91-118): builds from shared `BuildCanonicalBatch`, length-prefixed `EncodeRecord` (writer.cs:120-131), Ordinal sort, SHA-256, lowercase hex. Tests: `ComputeHash_OrderIndependent`, `ComputeHash_Deterministic` (64-char lowercase hex), `ComputeHash_DelimiterInjectionSafe` ("A\|B"/"c" vs "A"/"b\|c"), `ComputeHash_AggregatesDuplicates`, `ComputeHash_SplitsMultiCategory`. GREEN. |
| 4 | Processed deck not re-fetched until 5 days after last check (`last_checked_utc`-based) | ✓ PASS | `DeckRefreshCooldown = TimeSpan.FromDays(5)` (CategoryKnowledgeRepository.cs:19); requeue predicate at lines 738/743 gates on `last_checked_utc <= @requeueBeforeUtc` where `requeueBeforeUtc = UtcNow - DeckRefreshCooldown`. `FiveDayCooldown_RequeueRespectsLastChecked` (tests:300-318): within-cooldown stays `processed=1`, aged -6d flips `processed=0`. GREEN. |
| 5 | Hash stored idempotently (additive schema); NULL-hash rows recompute once; partial failure self-heals to NULL | ✓ PASS | Idempotent ADD COLUMN guard via `GetTableColumnsAsync` (repo.cs:78-84) + `content_hash TEXT NULL` already in CREATE; `EnsureSchema_IsIdempotentForContentHash` (tests:173-183). Clear-before/set-after ordering (session.cs:189-191) makes a mid-replace failure leave NULL — `ChangedPath_PartialFailureLeavesNullHash` (tests:249-268) injects a real SQLite ABORT trigger on observation insert and asserts hash is NULL after. `NullHash_RecomputesOnce` (tests:270-298) proves recompute-once then stabilize. GREEN. |
| 6 | Build clean; Core + Web tests pass except known AdminCssPhase1Tests CSS debt | ✓ PASS | Orchestrator confirmed Release build 0 warn/0 err; Core 98/98 Failed:0 (incl. 17 ContentHashDedup, re-confirmed in-process by verifier: Failed:0 Passed:17); Web 463 pass / 13 fail — all 13 are pre-existing AdminCssPhase1Tests CSS debt, zero new failures. Telemetry log added (CategoryKnowledgeStore.cs:221-226) with `DecksProcessed` return contract preserved (line 227); operator note added (AdminHarvest/Index.cshtml:137); no harvest_runs schema column added (deferred per plan). |

**Score:** 6/6 success criteria verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckCategoryCacheWriter.cs` | `BuildCanonicalBatch` shared by writer+hash; dual-kind injection-safe `ComputeCanonicalHash` | ✓ VERIFIED | `BuildCanonicalBatch` defined once (line 50), consumed by `PersistDeckEntriesAsync` (45) and `ComputeCanonicalHash` (93) — grep count 3. Hash covers obs + totals; length-prefixed encoding. |
| `CategoryKnowledgeRepository.cs` | Get/SetContentHashAsync(nullable), 5-day cooldown, idempotent ADD COLUMN guard | ✓ VERIFIED | Methods at 916/937 (parameterized, DBNull clear); `DeckRefreshCooldown=FromDays(5)` (19); guard at 78-84. |
| `ArchidektDeckCacheSession.cs` | Write gate clear-before/set-after; `Unchanged` enum + `DecksUnchanged` | ✓ VERIFIED | Gate at 169-193; enum has Added/Updated/Unchanged (196-201); `DecksUnchanged` positional param (206); `DecksProcessed => DecksAdded + DecksUpdated` unchanged (208). |
| `CategoryKnowledgeStore.cs` | Telemetry log; return contract unchanged | ✓ VERIFIED | Structured log 221-226; `return result.DecksProcessed` (227). |
| `AdminHarvest/Index.cshtml` | Operator note | ✓ VERIFIED | Note at line 137. |
| `ContentHashDedupTests.cs` | 17 tests incl. SC1 snapshot proof | ✓ VERIFIED | 17 [Fact] tests, all GREEN; real CategoryKnowledgeRepository + temp SQLite, no mocks. |

### Key Link Verification

| From | To | Via | Status |
|------|----|----|--------|
| `PersistDeckEntriesAsync` | `BuildCanonicalBatch` | writer consumes same batch builder | ✓ WIRED (writer.cs:45) |
| `ComputeCanonicalHash` | `BuildCanonicalBatch` | hash serializes both obs+totals from shared batch | ✓ WIRED (writer.cs:93) |
| `PersistDeckAsync` | `Get/SetContentHashAsync` | read+compare; clear NULL before replace, set after success | ✓ WIRED (session.cs:183,189,191) — two SetContentHashAsync on changed path |

### Behavioral / Probe Execution

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| ContentHashDedup suite | `dotnet test --filter FullyQualifiedName~ContentHashDedup` | Failed:0 Passed:17 Total:17 | ✓ PASS (re-run by verifier in-process) |

### Requirements Coverage

| Requirement | Source Plan | Status | Evidence |
|-------------|-------------|--------|----------|
| CAT-02 | 27-01-PLAN | ✓ SATISFIED | Content-hash dedup write gate + 5-day refresh + telemetry — all 6 SCs verified. |

### Anti-Patterns Found

None blocking. No `TODO`/`FIXME`/`XXX`/`HACK`/`PLACEHOLDER` markers in the changed files. No new packages. SHA-256 is BCL. `ICategoryKnowledgeStore` and `RunCacheSweepAsync` contracts preserved.

### Human Verification Required

None. All success criteria are programmatically verified via the executed test suite and code inspection. This is an internal write-path optimization with no visual/UX surface; the one UI change (an operator note in the admin view) is a static text addition confirmed by grep. Live prod write-churn reduction is an operational outcome that will surface naturally post-deploy but is not a gating success criterion for this phase.

### Gaps Summary

No gaps. All 6 ROADMAP success criteria PASS. The SC1 zero-fact-DML proof — the highest-risk criterion — is implemented exactly as the Codex-approved plan prescribed: a cooldown-aged, re-queued deck driven through `RunAsync`, with connection-safe full before/after fact-row snapshot equality (including `last_seen_utc`) rather than connection-scoped TEMP triggers. The partial-failure self-heal (SC5) is proven with a real injected SQLite failure, not just call-order assertion. The `DecksProcessed` semantics and `ICategoryKnowledgeStore` interface are preserved per the phase constraints. Deferred item (persisting `DecksUnchanged` in `harvest_runs`) is explicitly out of scope per plan and surfaced via structured log only.

---

_Verified: 2026-05-26T17:10:00Z_
_Verifier: Claude (gsd-verifier)_
