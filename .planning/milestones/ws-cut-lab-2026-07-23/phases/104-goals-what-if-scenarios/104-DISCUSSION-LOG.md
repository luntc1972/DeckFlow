# Phase 104: Goals & What-If Scenarios - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-20
**Phase:** 104-goals-what-if-scenarios
**Areas discussed:** Goal definition model, Scenario storage, What-if swap UX, Goal results + engine coupling

---

## Goal definition model (GOAL-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Editable turns on fixed families | Seed turn targets per bracket for the existing metric families; edit turn numbers; no custom categories (stays within 103 sim families) | ✓ |
| Add/remove/reorder custom goal rows | Free custom goals; risk = categories outside 103 families need new sim math | |
| Preset bundles only | Pick a cEDH/casual preset, no per-turn edit | |

**User's choice:** Editable turns on fixed families (recommended).
**Notes:** Keeps every goal backed by an existing 103 metric — honors SIM "no new simulation math." Seeds from CedhMulliganCalibration caps (the D-17 fixed model becomes editable).

---

## Scenario storage (GOAL-02)

| Option | Description | Selected |
|--------|-------------|----------|
| localStorage named slots, full snapshot | Save/load/delete named scenarios in browser localStorage; capture goals + locks + intent + working state | ✓ |
| Named session-JSON download/upload | Extend session-JSON export to named files; portable but manual | |
| Both (slots + export) | localStorage + file export/import; more surface | |

**User's choice:** localStorage named slots, full snapshot (recommended).
**Notes:** No user accounts → client-side only. Reuse CutLabState serializer as the snapshot payload. File export deferred.

---

## What-if swap UX (GOAL-03)

| Option | Description | Selected |
|--------|-------------|----------|
| Non-destructive preview | A from working list, B from cuts-made/original-pool; see deltas via 103 engine; Keep or Discard | ✓ |
| Committed edit | Swap mutates the working list directly; no try-before-commit | |

**User's choice:** Non-destructive preview (recommended).
**Notes:** "Keep/Discard" mirrors the accept/reject vocabulary from 103. B-source = already-resolved cut-pile/original-pool cards (no new Scryfall); card-search deferred.

---

## Goal results + engine coupling

| Option | Description | Selected |
|--------|-------------|----------|
| Display-only | Per-goal pass/miss + %-vs-target in metrics/compare; 103 cut ordering unchanged | ✓ |
| Feed back into cut ordering | Editable goals re-rank the 103 cut rounds; changes the shipped engine + determinism | |

**User's choice:** Display-only (recommended).
**Notes:** Smaller, safer; goals inform the user without re-driving cuts or touching the 103 determinism guard. Revisit only if UAT shows goal-driven ordering is wanted.

## Claude's Discretion

- Goal seed defaults + exact exposed families (D-02).
- Whether card-search is an in-scope swap source (D-06).
- localStorage slot schema/versioning + quota handling.
- Per-goal results widget style (badge vs bar vs %-vs-target).

## Deferred Ideas

- Named session-JSON file export/import for cross-browser scenario portability.
- What-if swap card B via Scryfall search (arbitrary new card).
- Goals feeding back into cut-round ordering (would touch 103 determinism).
- Custom goal categories beyond the 103 metric families (needs new sim math).
