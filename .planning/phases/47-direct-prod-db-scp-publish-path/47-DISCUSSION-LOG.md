# Phase 47: Direct Prod-DB + SCP Publish Path - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-16
**Phase:** 47-direct-prod-db-scp-publish-path
**Areas discussed:** SCP transport, Prod store handle, Diff + partial-failure, Step gating

---

## SCP transport

| Option | Description | Selected |
|--------|-------------|----------|
| Shell out to system scp | Process.Start the OS scp/ssh binary; zero new packages | |
| Add SSH.NET package | Renci.SshNet in-process SCP; cleaner errors; violates no-new-packages | ✓ |
| Manual SCP, DB-only in Studio | Operator SCPs by hand; Studio does DB only | |

**User's choice:** Add SSH.NET package
**Notes:** Genuinely-new package; deliberate operator-approved exception to the v1.7
"no new packages" constraint. In-process structured errors chosen specifically because they
serve the per-file SC4 reconcile better than parsing shell-scp output. Exact version pinned
at plan time; Codex plan-review to scrutinize the public-repo supply-chain add.

---

## Prod store handle

| Option | Description | Selected |
|--------|-------------|----------|
| Build Postgres store on demand | Construct from user-secrets conn string inside the publish action only | ✓ |
| Register prod store at startup | DI-register a keyed Postgres store at boot when configured | |

**User's choice:** Build Postgres store on demand
**Notes:** Minimizes always-live accidental-write surface. Research item: confirm
ContentSiteIndexStore can be constructed against a Postgres conn (provider detection /
ctor overload via IRelationalDialect); add an overload if only a SQLite path ctor exists.

---

## Diff + partial-failure

| Option | Description | Selected |
|--------|-------------|----------|
| In-memory key diff + per-item status list | Natural-key compare → New/Updated; per-file (SCP) + per-row (DB) success/fail | ✓ |
| Counts only | Aggregate counts + single overall success/fail | |

**User's choice:** In-memory key diff + per-item status list
**Notes:** Mirrors Phase 46 D-11. Raw textual git-diff not meaningful (target is live DB +
remote disk, not repo tree). Per-item detail required for SC4 manual reconcile.

---

## Step gating

| Option | Description | Selected |
|--------|-------------|----------|
| SCP all → then DB upsert, gated | Step 1 SCP every file; Step 2 disabled until Step 1 fully succeeds | ✓ |
| Per-row SCP+upsert pairs | Per row: SCP file then upsert it | |

**User's choice:** SCP all → then DB upsert, gated
**Notes:** Artifact-first so no DB row references a missing file (PUB-04). Per-row pairing
rejected — interleaves the two systems and complicates the gate.

---

## Claude's Discretion

- Exact `Studio:Scp:*` user-secrets key names and SSH key-auth presentation.
- `Renci.SshNet` exact version; SFTP vs SCP subsystem.
- StateHasChanged / async bridging + button-lock state machine (mirror Phase 45/46).
- One vs two on-demand store instances for prod read + upsert.
- Resolving the local artifact file set from approved rows' `artifact_path`.

## Deferred Ideas

- Shell-out to system scp (rejected in favor of SSH.NET).
- Always-live prod store DI singleton (rejected for accidental-write surface).
- Per-row interleaved SCP+upsert (rejected for gate simplicity).
- Page/nav layout, expand-vs-modal markup, visual styling → 47-UI-SPEC.md.
- Reviewed-not-folded todos: combo-data spike, user-selectable expert context, KB-value A/B
  (all generic-keyword matches, unrelated to publish flow).
