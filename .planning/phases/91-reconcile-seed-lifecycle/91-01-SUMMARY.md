---
phase: 91-reconcile-seed-lifecycle
plan: 01
subsystem: database
tags: [content-kb, postgres, sqlite, dapper, seed-json, sync]

# Dependency graph
requires:
  - phase: 89-content-hash-foundation
    provides: body_sha256 dialect-guarded additive DDL pattern (SetBodySha256IfNullAsync template)
  - phase: 90-directpush-correctness-seed-sync
    provides: awaiting_confirm_utc dialect-guarded nullable-timestamp DDL pattern
provides:
  - "ContentSiteIndexRow.SeedManaged nullable bool property (NULL=unclassified, false=prod-owned, true=seed-owned)"
  - "seed_managed column on content_site_index, dialect-guarded additive DDL (SQLite INTEGER NULL / Postgres BOOLEAN NULL)"
  - "SetSeedManagedIfNullAsync null-only idempotent backfill write (throwing default interface method + real implementation)"
  - "SeedIndexFileReader.Read: shared 3-outcome index-seed.json natural-key reader (SeedIndexReadResult)"
affects: [91-02-write-path-stamping, 91-03-seed-managed-backfill, 91-04-reconcile-classifier, 91-06-reconcile-orchestrator, 91-08-apply-gated-removal]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dialect-guarded nullable-marker column threaded through DDL/create-table/5-SELECT/mapping/upsert exactly mirroring the body_sha256/awaiting_confirm_utc rollout"
    - "3-outcome result record (SeedAvailable + set) as the sole read API to prevent an unavailable data source collapsing into an empty-set false positive downstream"

key-files:
  created:
    - DeckFlow.Core/Content/SeedIndexFileReader.cs
    - DeckFlow.Core.Tests/Content/SeedManagedSchemaTests.cs
    - DeckFlow.Core.Tests/Content/SeedManagedWritePathTests.cs
    - DeckFlow.Core.Tests/Content/SeedIndexFileReaderTests.cs
  modified:
    - DeckFlow.Core/Knowledge/ContentArtifactSpec.cs
    - DeckFlow.Core/Content/ContentSiteIndexStore.cs
    - DeckFlow.Core/Content/IContentSiteIndexStore.cs

key-decisions:
  - "seed_managed is BOOLEAN NULL (Postgres) / INTEGER NULL (SQLite), never a non-nullable bool with a DEFAULT, so NULL (unclassified) stays distinct from false (classified prod-owned)"
  - "seed_managed added to the two seed-managed upsert variants (UpsertContentColumnsOnlySql, UpsertPreservingVisibilitySql) only, always OVERWRITTEN from EXCLUDED on UpsertPreservingVisibilitySql — every row passing the seed-load path is by definition (re)entering the seed-managed set"
  - "SeedIndexFileReader.Read is the ONLY public read API (no bare-set ReadNaturalKeys overload) so SeedAvailable can never be bypassed by a downstream consumer"

patterns-established:
  - "SetSeedManagedIfNullAsync mirrors SetBodySha256IfNullAsync's throwing-default-interface-method idiom so ~12 existing IContentSiteIndexStore test doubles compile unchanged"

requirements-completed: [SYNC-17]

# Metrics
duration: ~15min
completed: 2026-07-09
---

# Phase 91 Plan 01: Seed-Ownership Marker Foundation Summary

**Nullable `seed_managed` marker on `content_site_index` (dialect-guarded additive DDL, bound-parameter upserts, null-only backfill setter) plus a shared 3-outcome `SeedIndexFileReader` that treats an unavailable `index-seed.json` as "do nothing," never as an empty set.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-07-09T21:31:26Z
- **Completed:** 2026-07-09T21:44:00Z
- **Tasks:** 3
- **Files modified:** 7 (3 modified, 4 created)

## Accomplishments
- `ContentSiteIndexRow.SeedManaged` (`bool?`) threaded through the store's DDL, create-table SQL (both dialects), all 5 SELECTs, row mapping, and `BuildUpsertParameters` as a bound `@seedManaged` parameter — never a SQL literal.
- `SetSeedManagedIfNullAsync` — a null-only idempotent backfill write, proven never to overwrite a row already classified `true` or `false`.
- `SeedIndexFileReader.Read` — the sole public read API for `index-seed.json` membership, returning `SeedIndexReadResult(SeedAvailable, NaturalKeys)` so a missing/unreadable/malformed seed can never masquerade as "the seed is empty."

## Task Commits

Each task was committed atomically (Tasks 1 and 2 combined into one commit — tightly coupled: the backfill setter has no meaning without the column it writes; see Deviations):

1. **Tasks 1+2: seed_managed column threading + SetSeedManagedIfNullAsync backfill setter** - `6f596912` (feat)
2. **Task 3: SeedIndexFileReader shared 3-outcome seed-key reader** - `ecb3921b` (feat)

_Plan metadata commit and STATE/ROADMAP updates follow this SUMMARY._

## Files Created/Modified
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` - Added `ContentSiteIndexRow.SeedManaged` (`bool?`)
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` - `seed_managed` DDL/create-table/SELECTs/mapping/upsert threading + `SetSeedManagedIfNullAsync`
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` - `SetSeedManagedIfNullAsync` throwing default interface method
- `DeckFlow.Core/Content/SeedIndexFileReader.cs` - New shared `index-seed.json` reader (`SeedIndexReadResult`, `Read`)
- `DeckFlow.Core.Tests/Content/SeedManagedSchemaTests.cs` - Fresh-DB CREATE, idempotent ALTER, prod-mode no-ALTER
- `DeckFlow.Core.Tests/Content/SeedManagedWritePathTests.cs` - Bound-param invariant across upsert variants + backfill setter idempotency
- `DeckFlow.Core.Tests/Content/SeedIndexFileReaderTests.cs` - All 3 outcomes (entries / valid-empty / absent-unreadable-malformed) + separator + skip-malformed-entry

## Decisions Made
- `seed_managed` follows the `awaiting_confirm_utc` dialect-guarded-both-branches DDL shape (not the single-line `body_sha256` shape) because it must be `BOOLEAN NULL` / `INTEGER NULL` specifically — never a non-nullable-with-DEFAULT column, per D-01's explicit "NULL means unclassified, distinct from false" requirement.
- `UpsertPreservingVisibilitySql`'s `seed_managed = EXCLUDED.seed_managed` is documented as an intentional always-overwrite (unlike `is_visible`/`is_hidden`/`is_evergreen`, which are preserved) because every row reaching this path via the seed-load call site is definitionally seed-managed.
- `SeedIndexFileReader.Read` takes an optional `ILogger? logger = null` parameter (matching `ContentSyncDiffClassifier`'s optional-logger convention) rather than a second overload — this does not violate the "single Read API" contract since it is still the one method, still requires `SeedAvailable` to be observed by the caller.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed literal NUL bytes written into two source files during authoring**
- **Found during:** Task 3 (`SeedIndexFileReader.cs` and its test file)
- **Issue:** When authoring the U+0000 natural-key separator, the Write tool interpreted the intended `\u0000` C# escape-sequence *text* as an actual NUL byte and embedded it directly in the `.cs` source (both the XML doc comment and the runtime string-interpolation call site, plus 5 occurrences in the test file's string literals). Raw NUL bytes in source files break `grep`/diff tooling and are the known "subagent NUL byte" class of authoring bug documented in project memory.
- **Fix:** Located every literal `\x00` byte via a Python byte-scan and replaced it with the literal 6-character escape-sequence text `\u0000`, so the compiler (not the file bytes) produces the runtime NUL character. Verified zero remaining NUL bytes and pure-LF line endings in both files, then rebuilt `DeckFlow.Core` — the one XML-doc warning (CS1570, "invalid character 0x00") that had appeared cleared to 0 warnings.
- **Files modified:** `DeckFlow.Core/Content/SeedIndexFileReader.cs`, `DeckFlow.Core.Tests/Content/SeedIndexFileReaderTests.cs`
- **Verification:** `dotnet build DeckFlow.Core.csproj` and `DeckFlow.Core.Tests.csproj` both clean, 0 warnings; targeted test run 6/6 green.
- **Committed in:** `ecb3921b` (Task 3 commit — the bytes never reached a prior commit)

---

**Total deviations:** 1 auto-fixed (1 bug, authoring-tool artifact, caught before commit)
**Impact on plan:** No scope creep — pure correctness fix to the exact code the plan specified, caught by build/test verification before any commit.

### Task grouping note (not a deviation, a commit-shaping choice)
Tasks 1 and 2 were implemented together and committed as a single commit rather than two, because `SetSeedManagedIfNullAsync` (Task 2) has no independent meaning without the `seed_managed` column it writes (Task 1) — splitting the diff would have required hand-separating hunks inside the same store file with no coherent intermediate state. This matches the project's `config.json` `granularity: "coarse"` setting. Task 3 (`SeedIndexFileReader`, an independently meaningful unit with zero dependency on Tasks 1/2) was committed separately as planned.

## Issues Encountered
- `dotnet build DeckFlow.sln` (whole-solution) and `dotnet build DeckFlow.Web.csproj` both fail with `Access to the path 'TypeScript.Tasks.dll' is denied` — a pre-existing, unrelated file lock held by a currently-running local dev server (`dotnet.exe` listening on `127.0.0.1:5173`, confirmed via `netstat`), not caused by any change in this plan. This plan's `files_modified` scope is entirely within `DeckFlow.Core`/`DeckFlow.Core.Tests`, which build clean; `DeckFlow.Studio`, `DeckFlow.Studio.Tests`, and `DeckFlow.CLI` (all of which reference `DeckFlow.Core`) were also verified to build clean, confirming the interface addition doesn't break any downstream project. `DeckFlow.Web`/`DeckFlow.Web.Tests` were not build-verified directly due to the lock; code inspection of `DeckFlow.Web.Tests/TestDoubles/FakeContentSiteIndexStore.cs` confirms it is a plain `class : IContentSiteIndexStore` implementation that will compile unchanged against the new default-interface-method addition, exactly matching the proven `SetBodySha256IfNullAsync` precedent already live across ~12 such doubles.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `ContentSiteIndexRow.SeedManaged`, the store's `seed_managed` column threading, `SetSeedManagedIfNullAsync`, and `SeedIndexFileReader.Read` are all in place for 91-02 (write-path stamping: seed loader + DirectPush + `ProdContentReader` read extension) and 91-03 (host-agnostic `SeedManagedBackfill` D-02 backfill) to build directly on.
- No blockers. The one open item is confirming `DeckFlow.Web`/`DeckFlow.Web.Tests` build clean once the locked dev-server process releases `TypeScript.Tasks.dll` — low risk given the default-interface-method precedent, but worth a quick `dotnet build DeckFlow.sln` sanity pass before Phase 91 closes out.

---
*Phase: 91-reconcile-seed-lifecycle*
*Completed: 2026-07-09*

## Self-Check: PASSED

All 7 created/modified files verified present on disk; both task commit hashes (`6f596912`, `ecb3921b`) verified present in git log.
