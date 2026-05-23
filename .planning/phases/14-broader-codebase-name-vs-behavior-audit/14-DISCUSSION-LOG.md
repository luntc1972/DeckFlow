# Phase 14: Broader Codebase Name-vs-Behavior Audit — Discussion Log

**Date:** 2026-05-17
**Mode:** interactive `discuss`
**Areas selected:** all 4 of 4 (Audit method + rename threshold, GenerateDocumentationFile scope, Test-double prefix canonicalization, Wave decomposition)

---

## Pre-discussion state

- Phase 13 closed 2026-05-17, verifier PASS, UAT T1-T8 signed off
- `.editorconfig` + `.gitattributes` committed earlier this session (`0f38cce` + `dfa73ed`)
- Branch `v1.3` at commit `dfa73ed`, 141 commits ahead of `main`
- No prior CONTEXT.md, RESEARCH.md, PATTERNS.md, or plans for Phase 14
- Phase 14 dir created: `.planning/phases/14-broader-codebase-name-vs-behavior-audit/`

## Codebase scout (pre-discussion)

- Public type counts: Core=26, Web=188, CLI=0 (internal-static only), Core.Tests=10, Web.Tests=55
- `<GenerateDocumentationFile>` state: ON in DeckFlow.Web (with NoWarn 1591;1573;1587); OFF in other 4
- Test-double prefix distribution: 55 Fake / 8 Throwing / 2 Stub / 1 Null / 1 Test / 1 Configurable / 1 Capturing

## Carried forward from prior phases

- Plain-author commits, no `Co-Authored-By` trailer (CLAUDE.md)
- VSTest unreliable in WSL → `dotnet build --configuration Release` is the gate
- XML doc-comment tone anchor: `CardLookupService.cs` / `CommanderSpellbookService.cs` (Phase 13 D-03)
- `.editorconfig` pins style; do NOT run Format Document / Code Cleanup (preserves `init` accessors, separate-line attributes, raw-string indent)
- Preservation list from Phase 10 + 12 + 13 D-07 carried to D-10 below

---

## Area 1: Audit method + rename threshold

### Q1.1 — How rigorous should the name-vs-behavior audit be?

**Options presented:**

1. **Scripted + targeted manual review** — grep for smells, then manual review of flagged + REQUIREMENTS.md candidates
2. Fully manual file-by-file (read every public class summary across 5 projects)
3. Targeted only (REQUIREMENTS.md candidates only)

**User selected:** 1 — Scripted + targeted manual review
**Rationale:** Bounded scope, high signal-to-noise. The 188-type Web surface is too large for fully-manual; the 3-candidate-only path is too narrow to catch other gaps.
**→ Captured as D-01**

### Q1.2 — What triggers a rename?

**Options presented:**

1. Strict: name OMITS a load-bearing responsibility
2. **Loose: name COULD be clearer**
3. Strict + record-borderline-in-deferred

**User selected:** 2 — Loose
**Rationale:** User prioritizes long-term readability over git-blame preservation. Future readers benefit more from descriptive names than blame-spelunkers lose from a one-commit-of-rename hop.
**→ Captured as D-02**

---

## Area 2: GenerateDocumentationFile scope

### Q2.1 — Enablement strategy?

**Options presented:**

1. **Enable in all 5 projects, backfill to clean build**
2. Enable in Core + CLI only (production code; test projects keep NoWarn)
3. Leave OFF in Core + CLI, backfill opportunistically

**User selected:** 1 — Enable in all 5 projects, backfill to clean
**Rationale:** Strongest guarantee. Test projects' public surface (Web.Tests=55, Core.Tests=10) is not exempt; even test classes deserve `<summary>` for IntelliSense quality.
**→ Captured as D-03 + D-04 (CLI included for future-proofing)**

### Q2.2 — Enable on CLI even with 0 public types?

**Options presented:**

1. **Yes — enable for consistency**
2. No — skip CLI entirely

**User selected:** 1 — Yes
**Rationale:** Zero cost today (0 public types), prevents future drift if a public type is added later.
**→ Captured as D-04**

---

## Area 3: Test-double prefix canonicalization

### Q3.1 — How to handle the 4 one-off prefixes (Null, Test, Configurable, Capturing)?

**Options presented:**

1. **Consolidate into Fake/Stub/Throwing per CONVENTIONS.md**
2. Add Capturing + Null to CONVENTIONS.md as legitimate (rename only Test* + Configurable*)
3. Leave alone, capture in deferred

**User selected:** 1 — Consolidate
**Rationale:** CONVENTIONS.md already defines the 3-prefix taxonomy. One-off prefixes (one instance each) don't justify expanding the documented vocabulary. Cheaper to rename 4 doubles than to maintain a 7-prefix taxonomy.
**→ Captured as D-05 (4 renames with concrete mappings)**

---

## Area 4: Wave decomposition

### Q4.1 — How to decompose Phase 14 into plans?

**Options presented:**

1. **By surface type (4 plans)** — baseline+report / renames / doc-backfill / GenDocFile flip+gate
2. By project (5 plans) — Core / Web / CLI / Core.Tests / Web.Tests
3. Hybrid (3 plans) — baseline / production sweep / test sweep

**User selected:** 1 — By surface type
**Rationale:** Cleanest separation of concerns; each plan has one verb (audit / rename / document / flip). Easy to roll back if any plan blows up. The 5-by-project alternative would duplicate the 3 task types in every plan.
**→ Captured as D-07**

Follow-on automatic decisions (not asked, derived):

- **D-08:** Plan 14-02 builds must stay GREEN throughout (per-rename build-clean check). Phase 13 D-05's intermediate-red was a DeckController.cs / Program.cs convergence necessity that Phase 14 doesn't have.
- **D-09:** Plan 14-01 captures pre-phase warning count baseline so SC3 is verifiable as a strict "zero new warnings" diff.
- **D-10:** Preservation list carried from Phase 13 D-07 + Phase 10 invariants (no edits to `"ChatGPT"` AI keys, `targetAiPlatform` form field, `"chatgpt"` zip fallback, CSS theme forks, internal HTML/JS identifiers).

---

## Deferred ideas (raised but out-of-scope)

- **`NoWarn 1591;1573;1587` removal from DeckFlow.Web.csproj** — would force 88 v1.1-era undoc'd public types to all get summaries. Out of Phase 14 scope.
- **Responsibility splits** — if Plan 14-01 audit identifies a class whose name-vs-behavior gap is too large to fix by rename alone (e.g., `ScryfallTaggerService` actually doing three things), capture as deferred refactor candidate in `14-AUDIT-REPORT.md`. Phase 14 renames only.
- **Internal-only class summaries** — public-only scope per D-06.

## Claude's Discretion items deferred to executor

- Specific old-name → new-name mapping for production candidates surfaced by Plan 14-01 audit (within D-02 trigger).
- Order of file renames within Plan 14-02 — alphabetical fine.
- Whether to fold a discovered class-name-vs-behavior mismatch into deferred (split needed) vs renaming to a "best single-line summary".

---

*Discussion concluded 2026-05-17. CONTEXT.md written. Ready for `/gsd-plan-phase 14`.*
