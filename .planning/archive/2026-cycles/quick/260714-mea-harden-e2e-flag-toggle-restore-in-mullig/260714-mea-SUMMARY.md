---
status: complete
---

# Quick Task 260714-mea: e2e flag-restore hardening — SUMMARY

**Branch:** `quick/e2e-flag-restore` (off main `afe26a5f`) · **Commit:** `f8f58586`

## Root cause
`manabase-mulligan.spec.ts` and `manabase-restricted-lands.spec.ts` hard-restored their feature flags to **false** in `afterEach`, but `analysis.manabase.mulligan-eval` and `analysis.manabase.restricted-lands` seed ON. Every run of these specs left the shared SQLite flag store OFF, contaminating later flag-gated specs in the same run (30s flag cache adds races) and every later run. Bit the LOW-8/9 task (260714-kir) hard.

## Fix
Both specs: `beforeEach` captures the flag's current On/Off state from /Admin/Flags (defaulting to true — the seeded state — if the read fails); `afterEach` restores that captured state with one retry and a warn-and-continue fallback so `releaseAdminLockForTest` always runs. Test bodies untouched. Codex gpt-5.4 implemented; plan review skipped (test-infra only, journaled).

## Verification (deterministic)
- Both specs live: **6/6 pass** serial.
- Flag rows byte-identical before and after the run (`sqlite3` pre/post: both `=1`).
- Failure path: restore lives in `afterEach` (runs on test failure); double-fault falls through to lock release. Hard-kill (SIGKILL) of the runner remains unrecoverable — accepted.
- `playwright test --list` parses both files; LF preserved; only the 2 spec files changed.
- Blind verifier skipped: acceptance criteria were proven by the deterministic live run + state diff above (reduced-assurance note journaled in ledger).
