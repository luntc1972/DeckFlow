---
phase: 65-prod-content-artifact-reconcile
plan: 02
status: operator-gated
requirements: [DATA-02]
completed: partial — code reconcile done; operator deploy + 1-row decision + post-verify outstanding
updated: 2026-06-22
---

# Plan 65-02 Summary — Reconcile Execution (operator-gated)

**Outcome:** Root-cause reconcile delivered as a repo fix (Claude-coded); the residual steps are
genuinely operator-gated (prod deploy + one content decision + post-reconcile verify).

## What was done

- Determined (Plan 01) the live serving base is `/app/content-kb` (committed repo tree) and the
  published-orphan count is **10**.
- Discovered the durable fix mechanism: the startup seed loader upserts `artifact_path` on conflict
  while preserving visibility, so a seed correction auto-fixes prod on deploy (no manual prod SQL).
- **Fixed 9 of 10 orphans at the root:** rewrote the committed seed
  `content-kb/seed/index-seed.json` slug `salubrious-snail` → `salubrioussnail` (19 `artifactPath`
  entries; all ids verified to have committed bodies under `salubrioussnail/`; JSON re-validated,
  LF preserved). Commit `577d3549`.
- Recorded the full decision + operator runbook in `65-DATA02-DECISION.md`.

## Outstanding (operator) — blocks phase close

1. **Deploy the seed fix to prod.** Prod deploys from `main`; the fix is on `cycle11`. Cherry-pick
   `content-kb/seed/index-seed.json` to `main` for an immediate live fix, or let it ship on
   `cycle11 → main`. Until then the 9 pages stay body-less in prod.
2. **Decide the 10th orphan** (`the-command-zone/e3qGnuupp8U`, not in seed, body uncommitted):
   commit the artifact, or unpublish the row (recommended default).
3. **(Optional)** fix the local Studio `content-kb.db` slug so a future DirectPush doesn't
   reintroduce `salubrious-snail`.

## SC3 verification (after deploy + decision)

Re-run prod probe Query C (visible `artifact_path`s) cross-checked against the committed repo
`content-kb/` tree — expect 0 missing — or run `content-kb-check` (Plan 03) against a prod-pulled
local DB with `--artifact-root` = repo root. Expected: 24/25 visible render bodies on deploy; the
25th resolved per the Group-2 choice.

## Verification

- `dotnet build DeckFlow.sln`: **0 errors** (17 pre-existing warnings). Seed change is data-only; no
  test pins the `salubrious-snail` slug.

## Commits

- `577d3549` fix(content-kb): correct seed slug salubrious-snail -> salubrioussnail (+ 65-DATA02-DECISION.md)
