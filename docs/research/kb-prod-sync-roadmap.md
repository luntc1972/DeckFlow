# Cycle 17 (proposed) — Content-KB Prod↔Git↔Studio Sync Hardening

*Fix-cycle roadmap from the combined audit + web-research + Codex cross-check (`kb-prod-sync-fix-design.md`). 12 weaknesses → 6 phases. Planning artifact only — not executed.*

## ⚠ Priority note
Several of these are **live prod-drift bugs**, not future risks: rows visible while `approval_status='pending'` (C1), DirectPush metadata reverted on every deploy (C3), body corruption/mojibake undetectable (M5), and DirectPush'd rows with unreachable bodies if `MTG_DATA_DIR` unset (M1). **Recommend this cycle ships BEFORE Cycle 16 creator-style** — creator-style adds KB-derived features on top of a KB that currently drifts. Phase numbers assigned at `/gsd-new-milestone` (likely 94-99, or slotted ahead of Cycle 16's 88-93).

## Design stance
Git = single source of truth for bodies; prod DB index row is subordinate and reconstructable from git. All sync = idempotent one-way keyed upsert. Every prod state change reflected in the git seed. Body content-hashed end-to-end. No CDC/Kafka — upsert + hash + expand-contract ordering, fits 512MB/Render.

## Phase map (6 phases, 3 waves)

| Phase | Name | Fixes | Live-bug? | Flag |
|---|---|---|---|---|
| **P1** | Content-hash foundation | M5 | drift-blind | — |
| **P2** | Index-row integrity bugs | C1, C4 | ✅ live | — |
| **P3** | DirectPush correctness + seed sync | M1, M2, M3, C3, M6a | ✅ live | `sync.directpush-gitbody` |
| **P4** | Reconcile + seed lifecycle | M4, C2 | drift | `sync.reconcile` |
| **P5** | Pull hardening | M7 | drift | — |
| **P6** | Round-trip integration test | M8 | — | — |

**Waves:** W1 = P1 ‖ P2 (independent; P2 is small live-bug fixes). W2 = P3 (needs P1 hash) ‖ P4 (needs P1). W3 = P5 (needs P2 composite-key) → P6 (needs all).

---

## P1 — Content-hash foundation
**Goal:** one body-inclusive content hash, stored everywhere, so drift is one indexed comparison and body corruption is detectable.
- SYNC-01: add `body_sha256` column to `content_site_index` (dialect-guarded DDL, local + prod + seed JSON); compute from the `.md` at publish.
- SYNC-02: unify on ONE signature that includes `body_sha256` + the canonical row columns; replace the two divergent schemes (`ContentSiteIndexContentSignature` + `ContentSyncDiffClassifier` fingerprint) so DirectPush, Pull, and reconcile share it.
- SYNC-03: app may **refuse to render** a row whose on-disk body hash ≠ stored hash (guards the residual unreachable/stale-body window); log mismatch.
- Tests: signature incl. body, hash-mismatch render guard, migration both dialects.
**Touchpoints:** `ContentSiteIndexStore.cs`, `ContentSiteIndexContentSignature.cs`, `ContentSyncDiffClassifier.cs`, `ContentKbController.cs`, seed exporter.

## P2 — Index-row integrity bugs (live drift, small)
**Goal:** kill the correctness bugs Codex found; can ship fast.
- SYNC-04: **mirror approval to prod** — stop hardcoding `approval_status='pending'` on the DirectPush insert path (`ContentSiteIndexStore.cs:991-1027`); `WritePublishAsync` propagates local approval (C1). Decide approval ownership (local authoritative for DirectPush).
- SYNC-05: **composite-key diffing** — key `ContentSyncDiffClassifier` by `(natural_key_type, natural_key_value)` not `PinId` (`:76-93`), matching DirectPush (C4-collision).
- SYNC-06: fix the "no DDL against prod" false comment + ensure the diff read doesn't run unexpected DDL on prod, or make it explicit/guarded (`DirectPushCoordinator.cs:88-97`) (C4-comment).
- Tests: approval propagation, YouTube/podcast key-collision regression.
**Touchpoints:** `ContentSiteIndexStore.cs`, `ContentSyncDiffClassifier.cs`, `DirectPushCoordinator.cs`.

## P3 — DirectPush correctness + seed sync (the big one)
**Goal:** DirectPush converges to the same consistent end-state as Publish; bodies reach prod via git; no every-deploy revert.
- SYNC-07: **bodies reach prod only via git `/app`** — drop reliance on the `/data` SFTP overlay for prod bodies (kills M1 unreachable-body + M3 `/app`-shadows-`/data`). Behind flag `sync.directpush-gitbody`.
- SYNC-08: **DirectPush re-exports `index-seed.json`** (like `PublishCoordinator.cs:97`) so git fully reconstructs prod and the next deploy can't revert DirectPush'd rows (M2, C3).
- SYNC-09: **hash-gated expand-contract ordering** — commit body → deploy/verify body at `/app` (hash present) → then flip `is_visible` + stamp (M3).
- SYNC-10: **stamp `pushed_to_prod_utc` only after prod confirms** the deployed body — not at local commit time (M6a; also fixes the "23 Never-published-but-live" symptom).
- Tests: seed re-export on DirectPush, ordering (no visible-before-body), no-revert-after-reseed.
**Touchpoints:** `DirectPushCoordinator.cs`, `PublishCoordinator.cs`, `ContentKbSeedLoader.cs`, resolver.

## P4 — Reconcile + seed lifecycle
**Goal:** prod-side drift is detectable + reconcilable; removed items actually leave prod.
- SYNC-11: **prod-side idempotent reconcile command** — join prod `content_site_index` ↔ git tree ↔ `index-seed.json`, emitting: published-orphans (visible row, no body), file-orphans (`.md`, no row), seed-drift (prod row absent from seed), **body-hash mismatch** (uses P1). Deterministic discrepancy IDs upserted, resolution-by-absence, partial runs scope-tagged. Extend `ContentKbOrphanScanner` to run against prod + git + seed (today it's local-only, CLI-only).
- SYNC-12: **seed reload handles removals** — `ContentKbSeedLoader` hides/deletes rows absent from a "seed-managed" set instead of additive-only upsert (C2); intentional, logged.
- Tests: 4 orphan classes, idempotent re-run (zero dupes), scope-tag no-false-resolve, seed-delete path.
**Touchpoints:** `ContentKbOrphanScanner.cs`, `ContentKbSeedLoader.cs`, CLI, reconcile store.

## P5 — Pull hardening
**Goal:** Pull adopts prod state without clobbering DB-only operator fields; bodies from current git (NOT prod — adjudicated).
- SYNC-13: **per-field master** — body+content ← git tree; DB-only operator fields (`is_visible`/`is_hidden`/`approval_status`) ← prod, preserved not clobbered (`PullFromProdCoordinator.cs:165-169`).
- SYNC-14: **`git pull` first / staleness guard** — Pull warns or refuses if the local checkout is behind (bodies resolve from git tree, so a stale checkout mis-reports). Do NOT SFTP-download prod bodies (Codex's rebuild rejected — prod `/data` empty by design, `0dd49f19`).
- SYNC-15: surface body-vs-index divergence to the operator instead of silent adopt.
- Tests: field-preservation on adopt, stale-checkout guard.
**Touchpoints:** `PullFromProdCoordinator.cs`.

## P6 — Round-trip integration test
**Goal:** lock the whole loop.
- SYNC-16: end-to-end test spanning distill → Publish/DirectPush → prod store → web body resolution → deploy/reseed → PullFromProd → reconcile, on a containerized Postgres + real git tree. Assert served-body == published body and `body_sha256` matches end to end; assert no-revert-after-reseed.
**Touchpoints:** new integration test project/harness.

---

## Requirement→weakness trace
- **P1** ⇐ M5 (body-blind checksums)
- **P2** ⇐ C1 (approval pending), C4 (PinId collision, DDL comment)
- **P3** ⇐ M1 (unreachable body), M2 (seed omission), M3 (`/app` shadow / ordering), C3 (every-deploy revert), M6a (premature stamp)
- **P4** ⇐ M4 (no reconcile), C2 (seed no-delete)
- **P5** ⇐ M7 (Pull clobber/stale)
- **P6** ⇐ M8 (no round-trip test)

## Session incidents this cycle closes
- 63 unpublished prod rows (deleted manually) → P4 reconcile would have flagged.
- CP437 mojibake live on prod → P1 body-hash would have caught.
- 23 "Never published" badge but live → P3 confirmed-stamp fixes.
- Pull-from-Prod git-decouple (`0dd49f19`) → P5 keeps git-SoT (Codex rebuild rejected).

## Decisions owed at plan time
1. Approval ownership: local-authoritative for DirectPush (SYNC-04) — confirm.
2. Keep DirectPush at all, or fold into Publish? P3 makes them consistent; a later cycle could retire DirectPush.
3. Flag rollout for `sync.directpush-gitbody` + `sync.reconcile` (seed OFF, operator flips).

---

## Plan-review adjustments (Codex gpt-5.4-high + live prod audit, 2026-07-05)

### Live prod drift audit — validates the cycle with real numbers
Read-only against prod (`dpg-d7oj8...`) + git tree:
- **106 prod rows, only 36 in the approved git seed → 70 rows NOT reconstructable from seed** (M2/C2 live; a DB reset loses 70 rows).
- **57 hidden+pending rows re-accumulated** (we deleted 63 last session; regenerated by a non-seed path) — M4 live.
- **434 git bodies vs 106 index rows → ~328 file-without-row orphans** — M4.
- **32 files with CP437 mojibake, 15 prod-visible** — M5 confirmed live (body drift invisible to index checksums). Repaired out-of-band on `fix/kb-mojibake-emdash` `60b5eda0`; the *systemic* fix is P1 body-hash.
- 1 visible-but-unstamped (M6); visible_but_pending currently 0 (C1 path exists but not triggered right now).

### Codex adjustments (fold into phases)
- **HIGH — P4 seed-delete unsafe as written.** No seed-owned/origin marker exists; a bad seed at boot could delete live rows (`ContentKbSeedLoader.cs:43`, `Program.cs:263`). → add a row-level seed-management marker FIRST; ship delete as dry-run/reconcile-only before destructive apply. Split SYNC-12.
- **HIGH — P3 is a re-architecture, not a bugfix.** Current DirectPush is "prod DB + /data live first, git `[skip render]` later". → split into **P3a DirectPush architecture flip** (git `/app` authoritative, drop /data-first) + **P3b seed/body ordering + confirmation stamping**.
- **MED — pull P2 ahead of P1 as the first hotfix slice.** visible-pending is immediately user-visible (public filters on `is_visible` only, ignores approval). Ship SYNC-04/05 first.
- **MED — SYNC-04 narrower than written.** DirectPush only reads approved local rows → fix = "DirectPush **writes approved** on insert/update", not mirror arbitrary state. Collapse SYNC-04.
- **MED — reconciler is NEW work, not a scanner extension.** `ContentKbOrphanScanner` has no prod access / git enumeration / seed awareness / discrepancy persistence. Define a new reconciler + discrepancy store explicitly.
- **LOW — `sync.*` flags not in current plumbing; Studio doesn't register the web flag system.** Decide web-DB flag vs Studio config vs both; budget admin/test work (`FeatureFlagCatalog.cs`, `Studio/Program.cs`).

### Revised sequencing (supersedes §"Recommended sequencing")
1. **P2** (SYNC-04 approved-write + SYNC-05 composite key) — hotfix slice, ship first.
2. **P1** content-hash foundation.
3. **P3a** DirectPush architecture flip → **P3b** ordering/stamping.
4. **P4** seed-owned marker → reconciler+discrepancy store (dry-run) → seed delete (destructive, gated).
5. **P5** Pull hardening → **P6** round-trip test.
