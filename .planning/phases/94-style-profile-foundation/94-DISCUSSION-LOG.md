# Phase 94: Style-Profile Foundation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-11
**Phase:** 94-Style-Profile Foundation
**Areas discussed:** Persistence shape, Profile versioning, Min-deck floor + insufficient behavior, Partial-profile shape

---

## Persistence shape

| Option | Description | Selected |
|--------|-------------|----------|
| JSON-blob columns | Single flat `creator_style_profile` row per slug; scalar meta cols + one JSON column per nested array section; serialized whole via System.Text.Json. | ✓ |
| Normalized child tables | Parent + 3 FK'd child tables (stated_rules, measured_metrics, fused_targets); queryable per-metric but 4x DDL + joins. | |

**User's choice:** JSON-blob columns
**Notes:** Nothing in CS-01..04 or downstream P97/P99 (whole-profile load) needs per-metric SQL; JSON-blob minimizes dialect-guarded DDL and gives trivial round-trip.

---

## Profile versioning

| Option | Description | Selected |
|--------|-------------|----------|
| Overwrite, single row | PK = slug; each recompute UPSERTs; `updated_utc` tracks freshness. | ✓ |
| Versioned history | PK = (slug, version/computed_utc); every recompute inserts; latest wins on read. | |

**User's choice:** Overwrite, single row
**Notes:** Drift-over-time history is a later-cycle concern, not MVP substrate. Matches ContentSiteIndexStore upsert semantics.

---

## Min-deck floor + insufficient behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Floor = 5 | Matches EDHREC threshold in research report; named const, persisted on row. | ✓ |
| Floor = 8–10 | More conservative; risks starving creators with few public decks. | |
| Persist + flag | Store normally with `insufficient_sample=true`; downstream decides trust. | ✓ |
| Refuse to persist | Store throws/returns null below floor; loses partial data. | |

**User's choice:** Floor = 5; Persist + flag
**Notes:** CS-03 says "marked, not silently trusted" — persist+flag honors the wording and keeps P95 crawl output storable before enough decks exist.

---

## Partial-profile shape

| Option | Description | Selected |
|--------|-------------|----------|
| Nullable section columns + empty arrays | Each `*_json` independently nullable; missing section reads as empty `IReadOnlyList<T>` (never null); emptiness IS the signal. | ✓ |
| Explicit presence flags | Add has_measured/has_stated/has_fused bools; redundant with array emptiness. | |

**User's choice:** Nullable section columns + empty arrays
**Notes:** JSON-blob shape makes independent-section nullability natural; follows house "return Array.Empty not null" convention.

---

## Claude's Discretion

- Exact DDL text, `ensureSchema` gate wiring, test-seam `connectionFactoryOverride` ctor — mirror `ContentSiteIndexStore`.
- `ICreatorStyleProfileStore` method signatures (min: upsert + get-by-slug).
- Store namespace (`Content` vs `Knowledge`); records go in `Knowledge` per CS-01.

## Deferred Ideas

- Versioned profile history / say-vs-do drift over time — later cycle.
- Per-metric SQL queryability (normalized tables) — only if a future phase needs it.
- DI registration + first real consumer — belongs to P95+.
