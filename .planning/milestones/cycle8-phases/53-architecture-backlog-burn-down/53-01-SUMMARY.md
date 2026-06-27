---
phase: 53-architecture-backlog-burn-down
plan: 01
subsystem: database
tags: [dapper, sqlite, postgres, refactor, srp, knowledge-cache]

# Dependency graph
requires:
  - phase: 49-dapper-adoption
    provides: Dapper-based data access in CategoryKnowledgeRepository
  - phase: 38-controller-cli-srp-split
    provides: facade-then-extract split pattern
provides:
  - CategoryCacheSchema internal sealed class (DDL/migration/index ownership)
  - DeckQueueRepository internal sealed class (deck_queue + crawl_state)
  - CardCategoryRepository internal sealed class (card/category read/write)
  - Thin CategoryKnowledgeRepository facade delegating to all three
affects:
  - 53-02-PLAN, 53-03-PLAN, 53-04-PLAN (same cycle; arch backlog)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Facade-then-extract: preserve public surface, delegate to internal collaborators"
    - "One collaborator owns one reason-to-change (Schema / Queue / CardCategory)"
    - "Shared CategoryCacheSchema passed to collaborator ctors so EnsureSchemaAsync runs once per method call, not twice"

key-files:
  created:
    - DeckFlow.Core/Knowledge/CategoryCacheSchema.cs
    - DeckFlow.Core/Knowledge/DeckQueueRepository.cs
    - DeckFlow.Core/Knowledge/CardCategoryRepository.cs
  modified:
    - DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs

key-decisions:
  - "Facade-then-extract chosen over full interface extraction to preserve zero caller repoints"
  - "Three collaborators (Schema / Queue / CardCategory) map directly to the five reasons-to-change identified in the Phase 39 audit"
  - "CategoryCacheSchema is a shared instance passed by reference to DeckQueueRepository and CardCategoryRepository so EnsureSchemaAsync is idempotent across all entry methods"
  - "ArchidektLiveSourcePrefix constant moved to CardCategoryRepository where it is only used"
  - "All SQL moved verbatim — no query text changed, preserving F-51-PG-01 Postgres parity fix"

patterns-established:
  - "Internal sealed class collaborators: use internal, not public, to keep split invisible to callers outside the assembly"
  - "Ctor receives ConnectionInfo + shared Schema; no DI registration needed for collaborators"

requirements-completed: [ARCH-02]

# Metrics
duration: 45min
completed: 2026-06-17
---

# Phase 53 Plan 01: CategoryKnowledgeRepository Facade-Then-Extract Summary

**Split 1272-LOC god-file into CategoryCacheSchema + DeckQueueRepository + CardCategoryRepository collaborators via facade-then-extract, zero caller repoints, 50 safety-net tests green**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-06-17T18:40:00Z
- **Completed:** 2026-06-17T19:25:00Z
- **Tasks:** 3
- **Files modified:** 4 (1 modified + 3 created)

## Accomplishments

- Extracted all DDL / migration / index DDL into `CategoryCacheSchema` (226 LOC); `EnsureSchemaAsync` in facade is now a one-line delegation
- Extracted 14 deck_queue + crawl_state methods into `DeckQueueRepository` (445 LOC); F-51-PG-01 `::timestamptz` dialect-guard, DeckRefreshCooldown, D-17/B2 commander-capture comments all preserved verbatim
- Extracted 10 card/category read/write methods plus all private helpers into `CardCategoryRepository` (638 LOC); ArchidektLiveSourcePrefix, all ON CONFLICT upserts, NormalizeBoard, FilterGenericCategoryRowsWithFallback moved there
- `CategoryKnowledgeRepository` reduced to 274-LOC thin facade: two ctors building three collaborators, `DatabasePath` property, 25 one-line delegations — no inline Dapper queries remain
- Full solution builds clean (0 new warnings); 50 safety-net tests pass (17 round-trip facts, parity, dedup, cache-writer, cache-session)
- No caller modified (`CategoryKnowledgeStore.cs`, `ArchidektDeckCacheSession.cs`, `DeckCategoryCacheWriter.cs`, `DeckCommandRunners.cs`)

## Task Commits

1. **Task 1: Extract CategoryCacheSchema** - `61b1328` (refactor)
2. **Task 2: Extract DeckQueueRepository** - `6dec2fc` (refactor)
3. **Task 3: Extract CardCategoryRepository, thin facade, safety net** - `e45efa7` (refactor)

## Files Created/Modified

- `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs` - All CREATE TABLE, ALTER TABLE, CREATE INDEX DDL + migration + GetTableColumnsAsync; 226 LOC; internal sealed class
- `DeckFlow.Core/Knowledge/DeckQueueRepository.cs` - deck_queue and crawl_state operations (Add/Next/Mark/ContentHash/CrawlPage/commander queries); 445 LOC; internal sealed class
- `DeckFlow.Core/Knowledge/CardCategoryRepository.cs` - card_category_observations + card_deck_totals + sources + cards read/write/upsert, filtering, normalization; 638 LOC; internal sealed class
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` - Reduced from 1272 LOC to 274 LOC; now purely a thin facade with 25 one-line delegations

## Decisions Made

- **Facade-then-extract pattern**: preserved `CategoryKnowledgeRepository` public/internal surface exactly so Web, CLI, and hosted-job callers require no changes. Matches Phase 38 split pattern referenced in context.
- **Three collaborators**: Schema (DDL), Queue (harvest), CardCategory (facts) — one class per reason-to-change from the Phase 39 audit Finding B.
- **Shared CategoryCacheSchema instance**: passed by reference to both DeckQueueRepository and CardCategoryRepository ctors so schema-ensure calls are shared and idempotent.
- **All SQL verbatim**: no query text was modified; behavior-preservation is guaranteed by the safety net (50 tests, 0 failed).

## Deviations from Plan

**1. [Rule 1 - Bug] Added missing `DeckFlow.Core.Reporting` using directive in facade**
- **Found during:** Task 3 (compilation after removing old usings)
- **Issue:** Removed `DeckFlow.Core.Models` but `CategoryKnowledgeRow` and `CardDeckTotals` live in `DeckFlow.Core.Reporting` — build failed with CS0246
- **Fix:** Replaced `using DeckFlow.Core.Models;` with `using DeckFlow.Core.Reporting;` in facade
- **Files modified:** `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`
- **Verification:** Build succeeded immediately after fix
- **Committed in:** e45efa7 (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - wrong using directive)
**Impact on plan:** Trivial correctness fix; no scope change.

## Issues Encountered

None beyond the using-directive fix above.

## Known Stubs

None. This is a behavior-preserving refactor; all data flows are unchanged.

## Threat Flags

None. This plan introduces no new network endpoints, auth paths, file access patterns, or schema changes. All SQL moved verbatim with the safety net as regression guard (T-53-01 mitigated).

## Next Phase Readiness

- 53-01 complete; 53-02 (Program.cs DI extract + Services/ foldering) can proceed immediately
- `CategoryKnowledgeRepository` public surface is unchanged — no downstream updates needed
- Safety net suite (50 tests) is the regression guard for any future changes to the collaborators

## Self-Check

- [x] `CategoryCacheSchema.cs` exists: yes (226 LOC)
- [x] `DeckQueueRepository.cs` exists: yes (445 LOC)
- [x] `CardCategoryRepository.cs` exists: yes (638 LOC)
- [x] `CategoryKnowledgeRepository.cs` is thin facade: yes (274 LOC, 25 delegations)
- [x] `CREATE TABLE` count in facade = 0: verified
- [x] `ON CONFLICT` count in facade = 0: verified
- [x] Commits exist: 61b1328, 6dec2fc, e45efa7

---
*Phase: 53-architecture-backlog-burn-down*
*Completed: 2026-06-17*
