# Phase 112: Cycle 17 Code Port - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-24
**Phase:** 112-Cycle 17 Code Port
**Areas discussed:** Shared-infra build order, Port mechanism, Modified-file hunk policy, Test port + DI proof

---

## Shared-infra build order

### Q1 — 112 must build clean, but its Web code calls four Scryfall helpers that live only on the stale branch

| Option | Description | Selected |
|--------|-------------|----------|
| Port the 4 files as new | Bring `ScryfallCollectionResolver`/`ScryfallLimits`/`CachedNameResolution`/`ScryfallBatching` in as new files (147 lines total); zero existing main callsites touched; 113 keeps the manabase dedup + archidekt pipeline | ✓ |
| Rewrite callsites against main | Leave all four for 113; rewrite creator-style against main's `ScryfallCardResolver` — throwaway work, behavior-drift risk | |
| Swap phase order (113 first) | Front-load the riskiest work with no ported code exercising it | |

**User's choice:** Port the 4 files as new
**Notes:** Scouting established the files are tiny and additive; the real conflict surface is the `ManabaseAnalysisService.cs:560` callsite, which Cut Lab edited across Cycles 18/19 and which stays in 113.

### Q2 — `ArchidektOwnerClient` resolves a named `archidekt` pipeline that main doesn't register

| Option | Description | Selected |
|--------|-------------|----------|
| Register it at 112 | One additive entry matching the existing five named pipelines | |
| Defer to 113, rely on the fallback | Ported code has `?? ResiliencePipeline.Empty`, but Polly throws `KeyNotFoundException` on unregistered keys — needs verification | ✓ |
| You decide | Let the planner pick after verifying throw-vs-null | |

**User's choice:** Defer to 113, rely on the fallback
**Notes:** Risk was flagged before the choice and reaffirmed after. Verification obligation recorded as D-03: if the DI-resolution test resolves `IArchidektOwnerClient` and the missing key throws at construction, success criterion 3 fails and the pipeline must be registered at 112 after all.

### Q3 — Does 112 port Cycle 17's edits to main's existing Scryfall files?

| Option | Description | Selected |
|--------|-------------|----------|
| No — new files only | Main's `CardLookupService`/`ScryfallCardResolver`/`ScryfallDtos`/`ScryfallReferenceResolver` stay byte-identical; all rewires belong to 113 | ✓ |
| Yes — port the rewires too | Fewer commits, but pulls 113's whole conflict surface into 112 | |
| Case-by-case | Per-file judgment with written justification | |

**User's choice:** No — new files only
**Notes:** Third consecutive choice favoring a zero-regression-risk 112.

### Q4 — The changed-lines format gate will judge every line of ~120 newly ported files

| Option | Description | Selected |
|--------|-------------|----------|
| Gate must pass, Codex fixes | `scripts/format-check-changed.sh staged` before each commit; violations fixed in the same commit; five carve-outs honored | ✓ |
| Port first, format-fix as its own commit | Clean mirror of the branch for review, at the cost of one red CI run | |
| You decide | Planner picks after a dry run | |

**User's choice:** Gate must pass, Codex fixes
**Notes:** The `{ get; init; }` carve-out is the highest-stakes one — converting it to `{ get; }` silently breaks System.Text.Json deserialization and has caused a real bug in this repo before.

---

## Port mechanism

### Q1 — How does code physically move from the stale branch?

| Option | Description | Selected |
|--------|-------------|----------|
| Path-allowlist checkout | `git checkout <branch> -- <explicit paths>`; deterministic, reviewable, structurally can't drag in Cycle-16 work already on main | ✓ |
| Cherry-pick c17 commits | Preserves authorship, but each pick carries extra changes and conflicts against 777 commits of drift | |
| Codex re-authors from branch as reference | Highest fidelity to main's conventions, but discards 6,000+ lines of already-verified engine code | |

**User's choice:** Path-allowlist checkout
**Notes:** Driven by the scouting finding that the branch diff contains Cycle-16 Content-KB files (`ContentBodyHashBackfill.cs`, `SeedManagedBackfill.cs`, `SeedIndexFileReader.cs`, `WebSeedKeyMembershipSource.cs`) that already landed on main independently.

### Q2 — Where's the allowlist boundary, given `CreatorStylePacketService` needs `Models/CreatorStyleRequest`?

| Option | Description | Selected |
|--------|-------------|----------|
| Compile closure rule | Take exactly what compiles: `CreatorStyleRequest` yes, `CreatorStyleViewModel` no; controller/views/help stay at 114 | ✓ |
| All Models at 112 | 114 never touches `Models/`, but ports a view model shaped for the old public page | |
| Split the request type | Move it to `Services/CreatorStyle/` — cleaner layering, but a code change inside a "ported unmodified" port | |

**User's choice:** Compile closure rule

### Q3 — Commit shape

| Option | Description | Selected |
|--------|-------------|----------|
| Keep 2 commits | Core engine, then Web services — matches the approved design spec | ✓ |
| Split commit 2 further | Separate the Scryfall helpers and DI for tighter review | |
| One commit per c17 phase (94-98) | Best traceability, but boundaries would be reconstructed by hand | |

**User's choice:** Keep 2 commits

### Q4 — What proves nothing extra came along?

| Option | Description | Selected |
|--------|-------------|----------|
| Diff-vs-main path audit | `git diff --name-status main` limited to allowlisted paths, plus a grep for dropped-plumbing strings | ✓ |
| Build + tests as the only gate | Catches breakage but not silent regression of Cycle 18/19 work | |
| Claude review of the full diff | Thorough but a poor use of review attention on already-verified code | |

**User's choice:** Diff-vs-main path audit

---

## Modified-file hunk policy

### Q1 — Default rule for files that already exist on main

| Option | Description | Selected |
|--------|-------------|----------|
| Deny by default, hunk-apply | Touch only what the compile closure demands; apply hunks to main's current version by hand; one-line justification each | ✓ |
| Take branch version, then reconcile | Faster, but silently reverts 18 days of main changes | |
| Whole-file for Core, hunk-apply for Web | Trades safety for speed in the wrong place — Cycle 16 landed heavily in Core/Content | |

**User's choice:** Deny by default, hunk-apply

### Q2 — Where creator-style DI registrations land

| Option | Description | Selected |
|--------|-------------|----------|
| New dedicated extension | `AddDeckFlowCreatorStyle()` + one line in Program.cs; matches the `AddDeckFlowResiliencePipelines()` precedent | ✓ |
| Mirror Cycle 17's placement | Truest to the branch, but spreads edits across four contested files | |
| All inline in Program.cs | Simplest to trace, but grows the most-edited file in the repo | |

**User's choice:** New dedicated extension

### Q3 — Seed loader and the `[]` seed placeholders

| Option | Description | Selected |
|--------|-------------|----------|
| Loader wired + `[]` placeholders | Real startup hydration path exercised at 112; 115 just overwrites contents | ✓ |
| Loader registered but not invoked | Leaves hydration unexercised until the cycle's last phase | |
| No seed files at 112 | Only the graceful-absence branch ever gets tested | |

**User's choice:** Loader wired + `[]` placeholders

---

## Test port + DI proof

### Q1 — What proves success criterion 3 (DI resolves at startup)?

| Option | Description | Selected |
|--------|-------------|----------|
| DI-resolution xUnit test | Builds the real provider and resolves every creator-style interface; runs in CI forever | ✓ |
| Local app boot smoke | Exercises the true startup path, but one-time and unrepeatable by later phases | |
| Both | Belt and braces, at the cost of a manual step in the DoD | |

**User's choice:** DI-resolution xUnit test

### Q2 — Test scope (asked as part of the closing gate)

**User's choice:** Accepted the proposed defaults — tests travel with their code (Core engine + Web-service tests at 112; Phase-100 public-surface suites never ported), and the Postgres integration test files (`PostgresContainerFixture`, `PostgresFactAttribute`, `CreatorStyleProfileStorePostgresTests`) stay out because Postgres migration is out of scope for Cycle 20.
**Notes:** User asked to move directly to the closing question rather than run the full 4-question loop for this area; the test-scope defaults were stated explicitly in that question and accepted.

---

## Claude's Discretion

- Exact composition of the path allowlist (mechanically derivable; the plan must publish it).
- Ordering of file groups within each of the two commits.

## Deferred Ideas

- `ManabaseAnalysisService.cs:560` dedup onto `ScryfallCollectionResolver` — Phase 113.
- `archidekt` resilience pipeline registration — Phase 113, unless D-03 verification forces it into 112.
- Rewiring `CardLookupService` / `ScryfallCardResolver` / `ScryfallDtos` / `ScryfallReferenceResolver` — Phase 113.
- Admin controller, views, `/Admin` landing personal-tools section, `CreatorStyleViewModel` — Phase 114.
- Repo-wide deletion proof for Phase-100 public plumbing — Phase 114 (PTOOL-02).
- Real seed data, `creator-style-import-stated` CLI, `fuse-profile` run — Phase 115.
- Postgres migration of the creator-style stores — out of scope for Cycle 20.
