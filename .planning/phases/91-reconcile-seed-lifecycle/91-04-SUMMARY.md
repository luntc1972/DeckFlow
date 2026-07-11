---
phase: 91-reconcile-seed-lifecycle
plan: 04
subsystem: content-kb
tags: [content-kb, reconcile, classifier, seed-managed, pure-core]

# Dependency graph
requires:
  - phase: 91-reconcile-seed-lifecycle (91-01)
    provides: "seed_managed column + SeedIndexFileReader.Read (availability-aware SeedIndexReadResult)"
provides:
  - "ContentKbReconcileClassifier.Classify: pure, I/O-free 4-class discrepancy classifier (published-orphan, file-orphan, seed-drift, body-hash-mismatch)"
  - "ContentKbReconcileDiscrepancy + ContentKbReconcileKind + deterministic U+0000-keyed BuildId"
affects: [91-06-reconcile-orchestrator, 91-08-apply-gated-removal]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure static classifier (no DI, no I/O) taking already-loaded in-memory collections + an optional ILogger, mirroring ContentSyncDiffClassifier's shape exactly"
    - "Availability-gated destructive-adjacent classification: a boolean-with-meaning (SeedAvailable) gates an entire discrepancy class rather than treating an empty collection as equivalent to 'no data'"

key-files:
  created:
    - DeckFlow.Core/Content/ContentKbReconcileDiscrepancy.cs
    - DeckFlow.Core/Content/ContentKbReconcileClassifier.cs
    - DeckFlow.Core.Tests/Content/ContentKbReconcileClassifierTests.cs
  modified: []

key-decisions:
  - "Tasks 1 (discrepancy record + BuildId) and 2 (classifier) committed together as a single feat commit — the record has no independent meaning without the classifier that emits it, and both are exercised by one test file (mirrors the 91-01 Task 1+2 grouping precedent under config.json's coarse granularity)"
  - "File-orphan identity is ARTIFACT PATH ONLY — ContentNaturalKey.TryDerive is never invoked in the file->row direction; a bare git .md path carries no trusted row metadata to derive a natural key from, so path-inference would be an ad hoc guessing scheme, not a natural-key derivation"
  - "Seed-drift is gated on SeedIndexReadResult.SeedAvailable, checked once at the top of Classify (logs a single skip line) and again per-row before the set-membership check — an unavailable seed emits zero seed-drift while published-orphan/file-orphan/body-hash-mismatch are computed unaffected"
  - "IsPublishedOrphan mirrors GitBodyCoverageAudit's gate exactly (ApprovalStatus==\"approved\" && IsVisible, no IsHidden check) rather than ContentKbOrphanScanner's IsVisible && !IsHidden — the interfaces contract's read_first pointed at GitBodyCoverageAudit as the mining source for this exact loop shape"

patterns-established:
  - "Row-keyed vs path-keyed deterministic ID: BuildId(kind, naturalKeyType, naturalKeyValue, artifactPath) branches on FileOrphan to key by path+literal 'path' token, all other kinds key by kind+type+value, all U+0000-delimited"

requirements-completed: [SYNC-11]

# Metrics
duration: ~25min
completed: 2026-07-09
---

# Phase 91 Plan 04: Reconcile Classifier (SYNC-11 heart) Summary

**Pure, I/O-free `ContentKbReconcileClassifier` emitting all four prod<->git<->seed discrepancy classes from already-loaded collections, with seed-drift hard-gated on `SeedIndexReadResult.SeedAvailable` so an unreadable seed can never mass-flag every seed-owned row.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-09T22:05:00Z
- **Completed:** 2026-07-09T22:30:32Z
- **Tasks:** 2
- **Files modified:** 3 (all created)

## Accomplishments
- `ContentKbReconcileDiscrepancy.cs` — `ContentKbReconcileKind` enum (PublishedOrphan/FileOrphan/SeedDrift/BodyHashMismatch, matching the persisted `kind` TEXT vocabulary from 91-PATTERNS.md's discrepancy-store schema) + the discrepancy record + `BuildId`, a deterministic, order-independent, U+0000-delimited ID builder.
- `ContentKbReconcileClassifier.cs` — pure `Classify(prodRows, existingGitBodyRelPaths, seedIndex, gitBodyByRelPath, logger?)` that mirrors `ContentSyncDiffClassifier`'s static-class/single-entry-point/optional-logger shape, reuses `ContentNaturalKey.TryDerive` and `ContentSiteIndexContentSignature.ComputeBodySha256` verbatim (no second hash path, no second key derivation), and gates seed-drift on `SeedAvailable` per the Codex BLOCK fix (T-91-25).
- 24 new xUnit tests: ID determinism (identical/differing kind/key/path, U+0000 separator, throw-on-missing-key), one `[Fact]` per discrepancy class, the seed-unavailable-gate case plus "other three classes still computed" case, file-orphan path-identity (including a no-front-matter-inference-rescue case), no-natural-key skip+warn, and full order-independence (reversed collections produce the same ID set).

## Task Commits

Tasks 1 and 2 committed together (tightly coupled — see Decisions):

1. **Tasks 1+2: ContentKbReconcileDiscrepancy + ContentKbReconcileClassifier + 24 tests** - `adc472f9` (feat)

_Plan metadata commit and STATE/ROADMAP updates follow this SUMMARY._

## Files Created/Modified
- `DeckFlow.Core/Content/ContentKbReconcileDiscrepancy.cs` - `ContentKbReconcileKind` enum + `ContentKbReconcileDiscrepancy` record + `BuildId` deterministic ID builder
- `DeckFlow.Core/Content/ContentKbReconcileClassifier.cs` - Pure `Classify` static method emitting the four discrepancy classes
- `DeckFlow.Core.Tests/Content/ContentKbReconcileClassifierTests.cs` - 24 tests covering ID determinism + all four classes + the availability gate + order-independence

## Decisions Made
See `key-decisions` in frontmatter. In summary: (1) both tasks landed in one commit per the tightly-coupled-code precedent from 91-01; (2) file-orphan identity is artifact-path-only, never natural-key-inferred; (3) seed-drift gating is double-checked (once for the logged skip, once per-row); (4) published-orphan's gate matches `GitBodyCoverageAudit` (approved+visible), not `ContentKbOrphanScanner`'s slightly different visible+!hidden gate, per the plan's explicit read_first pointer.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed literal NUL bytes written into source files during authoring (repeat of the 91-01 authoring-tool artifact)**
- **Found during:** Task 1/2 combined implementation, three separate occurrences across two source files
- **Issue:** As documented in 91-01-SUMMARY.md, the Write tool intermittently converts the intended `\u0000` C# escape-sequence *text* (and, in one case, even a literal space character typed in that exact syntactic position) into an actual NUL byte embedded directly in the `.cs` source. This happened three times: once in `ContentKbReconcileDiscrepancy.cs` (the `FieldDelimiter` char constant), and twice in `ContentKbReconcileClassifier.cs` (the seed-drift composite-key interpolation). A fourth attempt in the test file was caught proactively by writing a placeholder token first and python-patching it afterward, avoiding the bug entirely.
- **Fix:** Byte-scanned every touched file after each Write/Edit call (`python3` NUL/CR count), located the literal `\x00` bytes, and replaced them with the literal 6-character escape-sequence text `\u0000` so the compiler (not the file bytes) produces the runtime NUL character. Re-verified zero NUL bytes and zero CR bytes in all three files before building.
- **Files modified:** `DeckFlow.Core/Content/ContentKbReconcileDiscrepancy.cs`, `DeckFlow.Core/Content/ContentKbReconcileClassifier.cs`
- **Verification:** `dotnet build` clean (0 warnings) after each fix; final byte-scan of all three touched files confirmed 0 NUL / 0 CR bytes before commit.
- **Committed in:** `adc472f9` (the bytes never reached a prior commit — caught during authoring, before any git add)

**2. [Rule 1 - Bug] Test assertion type mismatch: `ArgumentException` vs `ArgumentNullException`**
- **Found during:** Initial test run (Task 2)
- **Issue:** Two tests asserted `Assert.Throws<ArgumentException>` for `BuildId` calls with `null` (not merely whitespace) natural-key/artifact-path arguments. `ArgumentException.ThrowIfNullOrWhiteSpace` throws `ArgumentNullException` (a subtype) for an actual `null` value, and xUnit's `Assert.Throws<T>` requires an exact type match, not a subtype match.
- **Fix:** Changed both assertions to `Assert.ThrowsAny<ArgumentException>`, which accepts the thrown subtype while still verifying the discrepancy record's fail-fast validation contract.
- **Files modified:** `DeckFlow.Core.Tests/Content/ContentKbReconcileClassifierTests.cs`
- **Verification:** Targeted test run went from 21/24 to 24/24 passing.
- **Committed in:** `adc472f9`

**3. [Rule 1 - Bug] Body-hash-mismatch tests double-counted as published-orphan**
- **Found during:** Initial test run (Task 2)
- **Issue:** Three body-hash-mismatch tests used the shared `EmptyPaths` set for `existingGitBodyRelPaths` while also supplying a matching entry in `gitBodyByRelPath` — an unrealistic combination (a real orchestrator would build both collections from the same file-presence scan) that caused the row to ALSO classify as published-orphan (since its artifact path was absent from `existingGitBodyRelPaths`), breaking the `Assert.Single`/exclusivity expectations.
- **Fix:** Each affected test now builds its own `paths` set containing the row's artifact path (matching what a real orchestrator would produce when the body is present), so only the intended discrepancy class fires.
- **Files modified:** `DeckFlow.Core.Tests/Content/ContentKbReconcileClassifierTests.cs`
- **Verification:** All 24 targeted tests pass; full `DeckFlow.Core.Tests` suite (1201 tests) green.
- **Committed in:** `adc472f9`

---

**Total deviations:** 3 auto-fixed (1 bug class repeated 3x — authoring-tool NUL-byte artifact caught before commit; 2 test-correctness bugs caught by the test run itself before commit)
**Impact on plan:** No scope creep. All fixes are pure correctness to the exact code/tests the plan specified, all caught by build/test verification before any git add.

## Issues Encountered
None beyond the deviations documented above — no build lock, no missing dependency, no architectural surprise.

## User Setup Required
None - no external service configuration required. This plan is pure `DeckFlow.Core` logic with zero I/O, zero DI, zero new dependencies.

## Next Phase Readiness
- `ContentKbReconcileClassifier.Classify` and `ContentKbReconcileDiscrepancy` are ready for 91-06 (the Studio `ContentKbReconcileOrchestrator`) to call directly: it needs only to build `IReadOnlyList<ContentSiteIndexRow>` (via `IProdContentReader`), the `existingGitBodyRelPaths` set (via `Directory.EnumerateFiles` + `ArtifactPathSafety`), a `SeedIndexReadResult` (via `SeedIndexFileReader.Read`), and a `gitBodyByRelPath` map (file reads for rows with a body present) — all I/O the orchestrator already owns per 91-PATTERNS.md's component topology.
- No blockers. Full solution builds clean (`DeckFlow.Core`, `DeckFlow.Core.Tests` 1201/1201, `DeckFlow.Studio`, `DeckFlow.Studio.Tests`, `DeckFlow.CLI` all verified 0 errors / 0 warnings against the new files).

---
*Phase: 91-reconcile-seed-lifecycle*
*Completed: 2026-07-09*

## Self-Check: PASSED

All 3 created files verified present on disk; task commit hash (`adc472f9`) verified present in git log.
