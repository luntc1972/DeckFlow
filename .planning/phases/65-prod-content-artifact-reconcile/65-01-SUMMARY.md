---
phase: 65-prod-content-artifact-reconcile
plan: 01
status: complete
requirements: [DATA-01]
completed: 2026-06-22
---

# Plan 65-01 Summary — DATA-01 Decision + Read-Only Prod Probe

**Outcome:** DATA-01 settled and live-confirmed; read-only prod probe complete with a definitive,
root-caused published-orphan count. All three tasks (incl. the two operator checkpoints) resolved
autonomously via read-only Render MCP — no prod writes.

## What was found

**DATA-01 (serving source) — corrected by the live log.** The live site serves content-KB body from
`/app/content-kb/{slug}/{id}.md` — the committed repo `content-kb/` tree baked into the Docker image
— NOT from `/data`, and NOT from any DB column (no content/body column exists). The prod startup log
confirms the resolver base: `Content KB content base resolved to /app; content-kb exists: True`
(two most recent boots). The resolver candidate walk hits `/app/content-kb` before `/data`, so the
SFTP `/data` uploads are not the serving source. The Phase-60 "/data gap" was the wrong location.

**Prod probe (read-only).** 109 rows; 25 visible (published), 84 not-visible (cosmetic).
`content.kb.enabled = TRUE` → route live. Of the 25 visible rows, 15 have their `.md` in the
committed repo tree and 10 do not → **published-orphan count = 10**:
- 9 × `salubrious-snail/*` — slug mismatch (repo dir is `salubrioussnail`; all 9 bodies exist there).
- 1 × `the-command-zone/e3qGnuupp8U.md` — genuinely uncommitted (P58 dogfood distill).

## Artifacts created

- `.planning/phases/65-prod-content-artifact-reconcile/65-DATA01-DECISION.md` — serving-path decision
  with code citations (`ContentKbController.cs:109/115/121`, `ContentSiteIndexStore.cs` DDL) + the
  live `/app` resolver-base confirmation + the serving-source correction.
- `.planning/phases/65-prod-content-artifact-reconcile/65-PROBE-RESULTS.md` — Query A/B/C output,
  the 25-vs-repo cross-check, the 10 published orphans (root-caused), and the Plan-02 decision-tree
  branch.

## Task resolution

- Task 1 (DATA-01 doc) — done.
- Task 2 (operator checkpoint: Render resolver-base log) — **resolved by AI** via Render MCP
  `list_logs` (read-only). Base = `/app`, `content-kb exists: True`.
- Task 3 (operator/AI-read: prod probe + published-orphan count) — **resolved by AI** via Render MCP
  `query_render_postgres` (read-only) cross-checked against the committed repo tree. The `/data`
  SFTP listing was intentionally skipped as non-authoritative (resolver = `/app`, not `/data`).

## Notes / deviations

- The published-orphan severity gate (RESEARCH assumption A3) was wrong: there ARE published
  orphans (10), but bodies for 9 already exist in the image (slug mismatch), so the reconcile is a
  targeted re-point, not a mass re-upload. Recorded for Plan 02.
- No secrets/connection strings/exception text recorded (D-07; T-65-01-INFO mitigated).
- No prod writes (T-65-01-PRODWRITE non-threat preserved).

## Commits

- `<this commit>` docs(65): DATA-01 decision + prod probe results (DATA-01)
