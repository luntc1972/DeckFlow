# Phase 91: Reconcile + Seed Lifecycle - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-08
**Phase:** 91-reconcile-seed-lifecycle
**Areas discussed:** Marker + legacy backfill, Removal action, Reconciler surface + discrepancy store, Dry-run → apply gating

---

## Marker + legacy backfill (SYNC-17)

### How to mark seed-ownership

| Option | Description | Selected |
|--------|-------------|----------|
| New DB column + seed field | Nullable `seed_managed` bool, dialect-guarded additive DDL (SQLite+Postgres+seed JSON), set true when written from seed-managed set | ✓ |
| Derive from seed set at load | No column; ownership inferred from natural-key presence in index-seed.json at reconcile time | |

**User's choice:** New DB column + seed field.
**Notes:** Derive-at-load is fragile — a row transiently missing from an in-progress/partial seed reads as prod-only → false-delete candidate. Marker must be an explicit persisted fact.

### Legacy backfill default (~106 existing prod rows)

| Option | Description | Selected |
|--------|-------------|----------|
| By current seed membership | Present in index-seed.json → seed-owned; absent → prod-owned. Enables cleanup of ~70 prod-only rows | ✓ |
| All existing = prod-owned | Fail-safe: reconcile can never delete a pre-existing row until re-published via seed | |

**User's choice:** By current seed membership.
**Notes:** Principled under git=SoT; dry-run + flag gate catch misclassification before any destructive apply.

---

## Removal action (SYNC-12)

| Option | Description | Selected |
|--------|-------------|----------|
| Soft-hide only | is_visible=false, retain row+marker+hash+timestamps, log discrepancy. Reversible, reconstructable | ✓ |
| Hard-delete row | DELETE the row outright. Irreversible in-place, loses local history | |
| Two-stage hide-then-delete | Hide first pass; later explicit pass hard-deletes still-hidden+seed-absent rows | |

**User's choice:** Soft-hide only.
**Notes:** Least-destructive first behavior; matches resolution-by-absence + git=SoT. Applies to seed_managed=true rows ONLY; prod-owned rows never touched.

---

## Reconciler surface + discrepancy store (SYNC-11)

### Execution surface

| Option | Description | Selected |
|--------|-------------|----------|
| Studio operator button | Studio has prod read (ProdContentReader) + git checkout + seed + DirectPush/Pull + P90 coverage audit | ✓ |
| CLI against prod | Prod-aware CLI reconcile command | |
| Web admin endpoint | Run in web app behind admin BasicAuth | |

**User's choice:** Studio operator button.
**Notes:** Only surface with all three inputs + operator UX. Scheduled runs are SYNC-F2, out of cycle.

### Discrepancy store home

| Option | Description | Selected |
|--------|-------------|----------|
| Local Studio store | Durable local content-kb DB; idempotent upsert by deterministic ID, resolution-by-absence | ✓ |
| Prod DB table | Cross-host visible but adds prod DDL for operator-only data | |
| Git-tracked report file | Human-readable, but weak as an idempotent persistent store | |

**User's choice:** Local Studio store (git report file kept as the human-readable dry-run output on top).
**Notes:** Matches P90 D-10 local durability; respects the minimal-additive-prod-DDL cap.

---

## Dry-run → apply gating (SYNC-11 / SYNC-12)

### Apply-promotion mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Two-step re-validated apply | Separate "Apply removals" re-runs the diff; applies only discrepancies still present (rejects stale apply) | ✓ |
| Per-discrepancy confirm | Operator ticks each removal individually | |
| Bulk auto-apply | One action runs dry-run then applies all | |

**User's choice:** Two-step re-validated apply.

### What `sync.reconcile` gates

| Option | Description | Selected |
|--------|-------------|----------|
| Only destructive apply | Detection/dry-run/store always available; flag gates only the soft-hide apply | ✓ |
| Whole reconciler | Flag gates the entire feature incl. dry-run | |

**User's choice:** Only destructive apply.

### Flag home

| Option | Description | Selected |
|--------|-------------|----------|
| Web-DB flag, Studio reads | Register in FeatureFlagCatalog (OFF), Studio reads via P90 accessor | ✓ |
| Studio-local config flag | A Studio-side setting | |

**User's choice:** Web-DB flag, Studio reads.
**Notes:** Single source of truth; reuses the P90 D-04 accessor. Seeded OFF per rollout convention.

---

## Claude's Discretion
- Exact `seed_managed` column form (bool vs nullable smallint) + deterministic discrepancy-ID scheme.
- Local discrepancy store schema location (new table in existing local content-kb DB vs sibling store).

## Deferred Ideas
- Hard-delete / two-stage hide-then-delete → future, if soft-hide insufficient.
- Scheduled/automatic reconcile → SYNC-F2, out of cycle.
- Pull-from-Prod hardening → Phase 92.
- Round-trip integration test → Phase 93.
- Flipping `sync.reconcile` ON in prod → Phase 93 pre-flip gate.
