---
phase: 14-broader-codebase-name-vs-behavior-audit
plan: "01"
subsystem: planning
tags:
  - audit
  - baseline
  - documentation
dependency_graph:
  requires: []
  provides:
    - 14-BASELINE.md
    - 14-AUDIT-REPORT.md
    - 14-BASELINE-PUBLIC-TYPES.txt
  affects:
    - 14-02-PLAN.md
    - 14-03-PLAN.md
    - 14-04-PLAN.md
tech_stack:
  added: []
  patterns:
    - XML coverage diff (Option A) as AUDIT-03 verification gate
    - grep-derived public type census across 5 projects
    - Fake/Stub/Throwing test-double taxonomy canonicalization
key_files:
  created:
    - .planning/phases/14-broader-codebase-name-vs-behavior-audit/14-BASELINE.md
    - .planning/phases/14-broader-codebase-name-vs-behavior-audit/14-BASELINE-PUBLIC-TYPES.txt
    - .planning/phases/14-broader-codebase-name-vs-behavior-audit/14-AUDIT-REPORT.md
  modified: []
decisions:
  - "ScryfallTaggerLookupService chosen as rename target for ScryfallTaggerService (D-02 loose trigger; 3-responsibility class; split deferred)"
  - "DeckPageTab enum opted-in to 5 one-line summaries in Plan 14-03 backfill"
  - "TestServiceFactory explicitly allowlisted as NOT a rename target (legitimate factory pattern)"
  - "FakeMetaGapService L810 preliminary rename to StubMetaGapService (Option A) selected over qualifier-preserving Option B"
  - "AUDIT-03 gate overridden: XML coverage diff (Option A) replaces D-04 broken warning-gate"
  - "9 total test-double renames (8 non-canonical prefix + 1 mis-prefixed canonical)"
  - "Worktree build note: D-09 command must be run from main repo (node_modules not in worktree)"
metrics:
  duration: "7 minutes"
  completed: "2026-05-17T23:59:13Z"
  task_count: 3
  file_count: 3
---

# Phase 14 Plan 01: Baseline + Audit Report Summary

**One-liner:** Pre-phase baseline captured (0 warnings, 318 public types, 487 tests) and authoritative audit report emitted with 1 production rename, 9 test-double renames, 83 doc-backfill targets, and XML coverage diff as AUDIT-03 gate.

---

## Baseline numbers captured

| Metric | Value |
|--------|-------|
| Build warning count | 0 |
| Build error count | 0 |
| HEAD SHA | `421108589f91712967ba9ab2420a14c59357c9cd` |
| `DeckFlow.Core` public types | 44 |
| `DeckFlow.Web` public types | 208 |
| `DeckFlow.CLI` public types | 0 |
| `DeckFlow.Core.Tests` public types | 10 |
| `DeckFlow.Web.Tests` public types | 56 |
| Total public types | 318 |
| Canonical test doubles (Fake/Stub/Throwing) | 65 |
| Non-canonical test doubles (rename targets) | 8 |
| Mis-prefixed canonical double (FakeMetaGapService L810) | 1 |
| Total test-double renames | 9 |
| Baseline test count (`--list-tests`) | 487 |
| `DeckFlow.Web.xml` member count | 825 |

---

## Rename count from 14-AUDIT-REPORT.md

### Production code renames (Plan 14-02)

1 rename:
- `ScryfallTaggerService` → `ScryfallTaggerLookupService`

### Test-double renames (Plan 14-02 D-05 canonicalization)

9 renames (rows a–i in report; row a must execute before row c):

| Row | Old name | New name |
|-----|----------|----------|
| (a) | `FakeMetaGapService` (L810) | `StubMetaGapService` |
| (b) | `NullTempDataProvider` | `StubTempDataProvider` |
| (c) | `ConfigurableMetaGapService` | `FakeMetaGapService` |
| (d) | `CapturingDeckAnalysisPacketService` | `FakeDeckAnalysisPacketService` |
| (e) | `SuccessfulCardLookupService` | `StubSuccessfulCardLookupService` |
| (f) | `SuccessfulSingleCardLookupService` | `StubSuccessfulSingleCardLookupService` |
| (g) | `SuccessfulMechanicLookupService` | `StubSuccessfulMechanicLookupService` |
| (h) | `DummyCommanderSearchService` | `StubCommanderSearchService` |
| (i) | `FailingRecentDecksImporter` | `ThrowingRecentDecksImporter` |

---

## Doc-backfill count from 14-AUDIT-REPORT.md

| Project | Files needing type-level summary |
|---------|--------------------------------:|
| `DeckFlow.Core` | 37 |
| `DeckFlow.Web` | 2 (DeckPageTab + renamed ScryfallTaggerLookupService via Plan 14-02) |
| `DeckFlow.CLI` | 0 |
| `DeckFlow.Core.Tests` | 9 |
| `DeckFlow.Web.Tests` | 37 |
| **Total** | **85 files** |

Note: Test-double files renamed in Plan 14-02 (rows a–i) get summaries in the rename commit
(lockstep); outer test class summaries are Plan 14-03's responsibility.

---

## Decision log

### ScryfallTaggerService rename name choice

**Decision:** Rename to `ScryfallTaggerLookupService`.

Rationale: Class legitimately does three things — (1) Scryfall REST card resolution, (2) Tagger
GraphQL query, (3) CSRF session lookup + kill-switch enforcement. The name `ScryfallTaggerService`
describes only responsibility #2. `ScryfallTaggerLookupService` is the best single-line summary
of the primary operation (looking up oracle tags via the Tagger). Responsibility split into
`IScryfallTaggerLookup` + `ITaggerSessionGate` is deferred per CONTEXT.md AUDIT-01 boundary.

### DeckPageTab opt-in

**Decision:** Opt-in — add 5 one-line summaries (enum type + all 11 values) in Plan 14-03.

Rationale: Phase 14 D-03 scope is every public class + interface across 5 projects. `DeckPageTab`
is in `DeckFlow.Web`; `NoWarn 1591` in `DeckFlow.Web.csproj` means the build won't fail without
summaries, but the XML coverage diff (Plan 14-04 gate) will flag the gap. Per D-02 loose trigger
(any reader benefits), the enum members benefit from readable summaries. Cheap to add at
doc-backfill time.

### TestServiceFactory allowlist

**Decision:** NOT a rename target. `TestServiceFactory` is a legitimate factory class scoped to
the test assembly (`internal` modifier). The `Test` prefix here is meaningful — it signals
"factory for test scenarios" rather than communicating stub/fake/throwing behavior semantics.
Allowlisted in `14-AUDIT-REPORT.md ## Allowlist`.

### FakeMetaGapService L810 collision resolution

**Decision:** Option A — preliminary rename L810 `FakeMetaGapService` → `StubMetaGapService`
before renaming L831 `ConfigurableMetaGapService` → `FakeMetaGapService`.

Rationale: L810's `FakeMetaGapService` has canned-response semantics (returns hardcoded result)
= Stub, not Fake per CONVENTIONS.md. Option B (qualifier-preserving → `FakeConfigurableMetaGapService`)
leaves a known-wrong prefix in place. Option A is one extra rename for full semantic correctness
and results in a clean Stub/Fake/Throwing three-pattern set for the MetaGap test surface.

### AUDIT-03 gate (XML coverage diff vs warning gate)

**Decision:** AUDIT-03 verification uses XML coverage diff (RESEARCH.md Option A) instead of
D-04's CS1591/1573/1587 warning gate.

Reason: `.editorconfig` lines 93-96 (committed `0f38cce`) suppress CS1591/1573/1587 repo-wide.
A live probe confirmed that even with `GenerateDocumentationFile=true` and deliberately-missing
summaries, the build produces 0 warnings. The XML coverage diff diffs source-grep'd public types
against generated `.xml` member names — accurate signal for missing docs regardless of suppression.

---

## Artifacts committed

- `.planning/phases/14-broader-codebase-name-vs-behavior-audit/14-BASELINE.md` — pre-phase build state, public type counts, test-double census, HEAD SHA, D-09 literal command
- `.planning/phases/14-broader-codebase-name-vs-behavior-audit/14-BASELINE-PUBLIC-TYPES.txt` — full grep-derived public type list (318 types across 5 projects)
- `.planning/phases/14-broader-codebase-name-vs-behavior-audit/14-AUDIT-REPORT.md` — authoritative rename + backfill worklist for Plans 14-02 and 14-03

Commit: `a3892b4` — `docs(14-01): capture baseline + audit report`

Plans 14-02, 14-03, and 14-04 can begin against these committed artifacts.

---

## Deviations from Plan

None — plan executed exactly as written, with one environment note documented:

**Environment note (not a deviation):** The git worktree for this plan does not contain
`DeckFlow.Web/node_modules/` (gitignored; not present in worktree checkout). The D-09 build
baseline was captured from the main repo checkout at the same HEAD SHA. This is documented in
`14-BASELINE.md` so Plans 14-02/03/04 know to run build verification from the main repo or
run `npm install` in their working copy. No source code was affected.

---

## Self-Check: PASSED

Files created:
- FOUND: `.planning/phases/14-broader-codebase-name-vs-behavior-audit/14-BASELINE.md`
- FOUND: `.planning/phases/14-broader-codebase-name-vs-behavior-audit/14-BASELINE-PUBLIC-TYPES.txt`
- FOUND: `.planning/phases/14-broader-codebase-name-vs-behavior-audit/14-AUDIT-REPORT.md`

Commit verified: `a3892b4` exists in `git log --oneline`

No source code (*.cs, *.cshtml, *.csproj) modified in this plan.
LF line endings confirmed (no CRLF in new markdown files).
No Co-Authored-By trailer in commit.
