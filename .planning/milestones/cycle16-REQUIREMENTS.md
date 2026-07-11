# Requirements: DeckFlow — Cycle 16 (Content-KB Prod<->Git<->Studio Sync Hardening)

**Status:** SHIPPED 2026-07-11 (CalVer 2026.07.3)
**Defined:** 2026-07-06
**Outcome:** 17/17 requirements code-satisfied and verified. Two feature flags
(`sync.directpush-gitbody`, `sync.reconcile`) ship OFF by design; their live
prod flip is gated behind the operator pre-flip walk (`93-PREFLIP-CHECKLIST.md`).

**Core Value:** Every supported workflow must produce output the user can paste
into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip. This
cycle protects the Content-KB half of that promise: the knowledge the site
serves must be exactly what the operator published — no drift, no corruption,
no ghost rows.

**Design stance:** Git = single source of truth for bodies; the prod DB index
row is subordinate and reconstructable from git. All sync = idempotent one-way
keyed upsert. Every prod state change reflected in the git seed. Body
content-hashed end-to-end. No CDC/queues (fits 512MB/Render).

## Cycle 16 Requirements (all complete)

### Integrity Hotfix (live prod bugs — shipped first)

- [x] **SYNC-04**: DirectPush writes `approval_status='approved'` — a prod row can never be publicly visible while `pending` (C1). Phase 88.
- [x] **SYNC-05**: Sync diffing keyed by `(natural_key_type, natural_key_value)`, replacing `PinId` (C4-collision). Phase 88.
- [x] **SYNC-06**: DirectPush diff read runs no unexpected DDL against prod; false "no DDL" comment corrected + path guarded (C4-comment). Phase 88.

### Content-Hash Foundation

- [x] **SYNC-01**: `content_site_index` gains `body_sha256` (dialect-guarded DDL, SQLite + Postgres + seed JSON), computed from the `.md` body at publish (M5). Phase 89.
- [x] **SYNC-02**: ONE unified body-inclusive row signature replaces the two divergent schemes; DirectPush/Pull/reconcile share it. Phase 89.
- [x] **SYNC-03**: Web app detects + logs (structured warning) when on-disk body hash != stored `body_sha256`. Fail-open + log this cycle per D-05; fail-closed refuse-to-render deferred to a future phase once the D-08 backfill guarantees every live row is hashed. Phase 89.

### DirectPush Correctness + Seed Sync (flag `sync.directpush-gitbody`, OFF)

- [x] **SYNC-07**: Bodies reach prod only via git `/app`; `/data`-SFTP overlay dropped under flag (kills M1/M3). Phase 90.
- [x] **SYNC-08**: DirectPush re-exports `index-seed.json` so git reconstructs prod and a redeploy cannot revert DirectPush'd rows (M2, C3). Phase 90.
- [x] **SYNC-09**: Hash-gated expand->contract ordering — body committed + deployed + hash-verified at `/app` before `is_visible` flips (M3). Phase 90.
- [x] **SYNC-10**: `pushed_to_prod_utc` stamped only after prod confirms the deployed body (M6a; fixes "Never published" badge). Phase 90.

### Reconcile + Seed Lifecycle (flag `sync.reconcile`, OFF)

- [x] **SYNC-17**: Row-level seed-management marker distinguishing seed-owned from prod-only rows (hard prereq before any seed-driven delete). Phase 91.
- [x] **SYNC-11**: NEW prod<->git<->seed reconciler + persistent discrepancy store: published-orphans, file-orphans, seed-drift, body-hash-mismatch; deterministic IDs, idempotent re-run, resolution-by-absence, scope-tagged; dry-run always-on. Phase 91.
- [x] **SYNC-12**: Seed reload handles removals — seed-owned rows absent from the seed set are soft-hidden intentionally + logged (C2); destructive apply gated behind dry-run validation + flag. Phase 91.

### Pull Hardening (no flag — Pull writes local-only)

- [x] **SYNC-13**: Pull per-field master — body+content <- git tree; operator fields (`is_visible`/`is_hidden`/`approval_status`) <- prod, preserved not clobbered (M7). Phase 92.
- [x] **SYNC-14**: Pull warns/refuses when the local checkout is behind (fetch/behind staleness guard); never SFTP-downloads prod bodies. Phase 92.
- [x] **SYNC-15**: Body-vs-index divergence surfaced to the operator instead of silently adopted. Phase 92.

### Round-Trip Proof

- [x] **SYNC-16**: End-to-end integration test spanning distill -> Publish/DirectPush -> prod store -> web body resolution -> deploy/reseed -> PullFromProd -> reconcile, on containerized Postgres + a real git tree; asserts served body == published body, `body_sha256` matches end-to-end, no-revert-after-reseed (M8). Phase 93. (Harness green locally; the real Render-deploy round-trip is an operator gate, not CI.)

## Traceability (final)

| Requirement | Phase | Status |
|-------------|-------|--------|
| SYNC-04 | 88 | Complete |
| SYNC-05 | 88 | Complete |
| SYNC-06 | 88 | Complete |
| SYNC-01 | 89 | Complete |
| SYNC-02 | 89 | Complete |
| SYNC-03 | 89 | Complete (fail-open+log; fail-closed deferred) |
| SYNC-07 | 90 | Complete (flag OFF at ship) |
| SYNC-08 | 90 | Complete |
| SYNC-09 | 90 | Complete |
| SYNC-10 | 90 | Complete |
| SYNC-17 | 91 | Complete |
| SYNC-11 | 91 | Complete |
| SYNC-12 | 91 | Complete (flag OFF at ship; live walk = FU-3) |
| SYNC-13 | 92 | Complete |
| SYNC-14 | 92 | Complete |
| SYNC-15 | 92 | Complete |
| SYNC-16 | 93 | Complete (harness; real-deploy leg = operator gate) |

**Coverage:** 17 requirements, 17 mapped, 0 unmapped.

## Deferred (tracked, not this cycle)

- **SYNC-F1**: Retire DirectPush entirely (fold into Publish) — later-cycle decision.
- **SYNC-F2**: Scheduled/automatic reconcile runs (this cycle ships operator-triggered only).

---
*Requirements defined 2026-07-06; shipped 2026-07-11 as CalVer 2026.07.3.*
