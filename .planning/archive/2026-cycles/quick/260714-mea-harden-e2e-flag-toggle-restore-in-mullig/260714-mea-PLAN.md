# Quick Task 260714-mea: e2e flag-restore hardening — PLAN

**Date:** 2026-07-14 · **Branch:** `quick/e2e-flag-restore` (off main `afe26a5f`)
**Trigger:** LOW-8/9 run (260714-kir): `manabase-mulligan.spec.ts` afterEach hard-restores `analysis.manabase.mulligan-eval` to **false** unconditionally; `manabase-restricted-lands.spec.ts` identical for its flag. Flags seed ON, so every run of these specs leaves the shared SQLite flag store OFF, contaminating later flag-gated specs (observed: lens-visual mobile) and later runs.

## Side Effects Report
**Files (direct):** the two spec files only. **Transitive/shared state:** the local `feature_flags` table via /Admin/Flags UI — end state changes from always-OFF to restored-original. **External surfaces / contracts / prod:** none (test infra). **Tests:** these ARE tests. **Compat risk:** none. **Open questions:** hard-kill (SIGKILL) of the runner still can't restore — accepted, documented.

## Change (both specs, same shape)
1. `beforeEach`: after acquiring the admin lock, read the flag's CURRENT status from /Admin/Flags (same row/status locators `setFlagEnabled` uses) → store `originalEnabled` in the module-level state next to `heldLock`.
2. `afterEach`: restore to `originalEnabled` (not hardcoded false); wrap restore in one retry (2 attempts total, small delay) since the test's page may be mid-failure; keep lock release in `finally`.
3. If the status read in beforeEach fails, default `originalEnabled = true` (seeded default) rather than skipping restore.

## Acceptance
- Both specs pass serially (live), and after a full run the DB rows for both flags equal their pre-run values (verify with sqlite before/after).
- Simulated failure path: flag restore still runs when a test body fails (afterEach semantics — verify by inspection).
- LF endings preserved; no other files.
