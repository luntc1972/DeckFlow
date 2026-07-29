# Phase 5: Archidekt Bracket Capture - Discussion Log

**Date:** 2026-07-29
**Workstream:** `cycle21-cut-lab`
**Command:** `/gsd:discuss-phase 5`

## Areas Discussed

### Phase Selection

The user redirected from Phase 4 to Phase 5:

- Phase 4 remains untouched: Functional-Twins Detector.
- Phase 5 is now the active discussion target: Archidekt Bracket Capture.

### Metadata Scope

Question: Besides Archidekt's bracket, should Phase 5 capture any other top-level payload fields while touching the deck payload?

Options presented:
- Bracket only - keep Phase 5 tight.
- Bracket + format - also persist `deckFormat`.
- Metadata bundle - capture bracket, format, theorycrafted, and created/updated fields if present.

User selected:
- Metadata bundle.
- Freeform note: "all availble fields"

Follow-up question: For "all available fields," what should Phase 5 actually persist from the Archidekt payload?

Options presented:
- Curated columns - persist stable deck-level fields, skip raw payload storage.
- Raw metadata JSON - store top-level non-card payload JSON.
- Both - curated query columns plus raw metadata JSON.

User selected:
- Curated columns.

Captured decision:
- Persist a curated deck-level metadata set as named columns.
- Do not store raw payload JSON in this phase.

### Captured vs. Absent

Question: How should the database distinguish pre-change rows from post-change rows where Archidekt had no bracket?

Options presented:
- Captured flag - add `archidekt_metadata_captured_utc`; null means pre-change, non-null plus null bracket means captured absent.
- Boolean flag - add `archidekt_bracket_checked`.
- Sentinel value - store a special bracket value for absent.

User selected:
- Captured flag.

Captured decision:
- Use a captured timestamp, not a boolean or sentinel value.

### Write Paths

Question: Should one-off URL imports write the bracket metadata too, or only the background bulk harvest?

Options presented:
- Both paths - every successful Archidekt deck import updates the same deck_queue metadata.
- Bulk only - admin URL import remains smaller.
- URL only later - defer URL handling.

User selected:
- Both paths.

Captured decision:
- Bulk harvest and admin URL import must update the same metadata columns.

## Deferred

- Raw metadata JSON storage.
- Backfill of old deck rows.
- Commander x bracket floor derivation.
- User-facing bracket coverage UI.

## Next Step

Run `/gsd:plan-phase 5` in the `gsd/cycle21-cut-lab` worktree.
