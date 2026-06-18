---
phase: 52-live-prod-publish-verification
status: passed
verified: 2026-06-17
requirements:
  HARD-02: passed
---

# Phase 52 Verification — Live prod-publish verification

**Goal:** Exercise the P47 direct prod-publish path (SCP → Render `/data` + content-columns-only
Postgres upsert) live end-to-end, confirming is_visible/is_evergreen on pre-existing rows are
preserved — or waive HARD-02 with reason.

**Outcome: PASSED** (run live, not waived).

## HARD-02 — PASSED
- Studio wired to prod (new credential `deckflow_28g4_user`); boot log `prod connection: configured` + `SCP: configured`.
- Operator ran DirectPush against prod: diff new=8/updated=0 → SCP artifacts to `/data` → content-columns-only upsert. AI performed no prod write (read-only snapshots only).
- **Admin-field preservation proven:** admin_fingerprint over the 86 pre-existing rows identical before/after (`6074848f279dbcc76452f498d609d3ed`); whole-table visible/evergreen/hidden/approved unchanged (24/0/42/24). content_fingerprint over the 86 also identical (updated=0).
- 8 new rows inserted (total 86→94), all `approval_status=pending` / `is_visible=false` — upsert never wrote admin fields even on insert.
- All 8 artifacts confirmed on `/data/content-kb/…` via read-only SSH ls (timestamped today).
- Caveat: live UPDATE-preserves-admin path not hit (updated=0, no overlapping local approved key); covered by P47 automated `UpsertContentColumnsOnlyAsync` tests.

## Follow-ups
- (operator, security) Finish credential rotation: redeploy `DeckFlow` web onto `deckflow_28g4_user`, confirm `deckflow_admin` open-connections → 0, delete `deckflow_admin` (kills the in-session-exposed password). Tracked separately from HARD-02.

## Evidence
`52-PROD-PUBLISH-RESULTS.md`.
