# Phase 47: Direct Prod-DB + SCP Publish Path - Context

**Gathered:** 2026-06-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a **second, direct publish path** to the standalone local `DeckFlow.Studio` app
(v1.7 milestone): the operator publishes approved Content-KB entries **straight to
production** — markdown artifacts SCP'd to the Render `/data` disk **first**, then the
approved rows upserted into the **prod Render Postgres** site-index — bypassing the
git-commit → Render-auto-deploy cycle that Phase 46 delivered.

The operator can:

1. **Preview the prod diff (PUB-05 / SC1):** Studio queries the prod Postgres site-index
   via the user-secrets connection string, natural-key-compares it against the approved
   local rows, and shows exactly which rows/artifacts are **New** vs **Updated** and that
   the target is **prod** — **no write happens until the operator explicitly confirms.**
2. **Two-stage gated push (PUB-04 / SC2):** **Step 1** SCPs *all* artifact files to Render
   `/data`; **Step 2** (prod Postgres upsert) is unreachable until Step 1 fully succeeds —
   artifact-first ordering so no DB row ever references a missing file.
3. **Reconcile partial failure (PUB-05 / SC4):** if Step 1 or Step 2 fails partway, the UI
   lists which files (SCP) and which rows (DB) succeeded vs failed, in enough detail to
   reconcile manually without re-running the whole set.

**This phase is a UI + thin-wiring wrapper over the existing data layer.** Phase 43 already
ships `UpsertContentColumnsOnlyAsync` (the *only* upsert this path may use — preserves
`is_visible`/`is_evergreen`) and `GetApprovedRowsAsync`. The NEW surface is: (a) a prod
Postgres store handle built on demand from `Studio:ProdConnectionString`, (b) an SCP upload
service (new dependency — see D-01), and (c) a new Studio publish page mirroring the Phase
45/46 page patterns.

**UI hint = yes.** Page/nav structure, layout, spacing/type/color are deferred to a
`47-UI-SPEC.md` (run `/gsd-ui-phase 47` next). Decisions below cover transport, data, and
operator-behavior choices the UI-SPEC leaves open.

**Out of scope (own phases / already shipped):** the git-commit publish path (Phase 46,
shipped); Studio executing `git push` (deliberately excluded, Phase 46 D-01); branch
switching / merge-to-main (Phase 46 D-02); UI visual styling (47-UI-SPEC).

</domain>

<decisions>
## Implementation Decisions

### SCP transport (PUB-04 — artifact upload to Render /data)
- **D-01:** Upload artifacts to Render `/data` via the **SSH.NET (`Renci.SshNet`) NuGet
  package** using its SCP/SFTP client in-process. **This is a genuinely new package and a
  deliberate, operator-approved exception to the v1.7 "no new packages" constraint** — the
  lighter shell-out-to-system-`scp` alternative was explicitly offered and rejected in favor
  of in-process, structured error surfacing (which directly serves the per-file
  success/fail reconcile in SC4). The exact version is pinned at plan/impl time; Codex
  plan-review must scrutinize the supply-chain addition on the public repo. SCP-over-SSH is
  the *only* `/data` write mechanism — there is no Render file-write REST API
  (REQUIREMENTS.md upstream-constraint table).
- **D-02:** SSH connection parameters (host, port, username, private-key path/passphrase,
  and the remote `/data` artifact target path) are read from **user-secrets / environment
  alongside `Studio:ProdConnectionString`** (proposed keys under a `Studio:Scp:*` section —
  exact key names are Claude's discretion / planner's call). Presence-only logging like the
  prod conn string (see D-07); never log the host, key, or path values.

### Prod Postgres store handle (PUB-04 — diff-read + upsert)
- **D-03:** Studio builds a **Postgres-backed `ContentSiteIndexStore` on demand inside the
  publish action** from `Studio:ProdConnectionString` — **not** a startup DI singleton. This
  minimizes the always-live accidental-write surface (the prod handle exists only during a
  confirmed publish). **Research item:** confirm `ContentSiteIndexStore` can be constructed
  against a Postgres connection string (provider detection / ctor overload) the same way the
  web app's dual-provider path does; if the current ctor only accepts a SQLite file path, the
  plan must add a Postgres-conn constructor/overload behind the existing
  `IRelationalDialect`/`RelationalDatabaseConnection` abstraction — not a new ad-hoc client.

### Diff preview + reconcile (PUB-05 / SC1 / SC4)
- **D-04:** The pre-write diff is computed **in-memory by natural-key comparison**: query
  prod rows from the on-demand prod store, compare against `GetApprovedRowsAsync` (local) →
  **New / Updated** row lists + counts, plus the matching artifact-file list. This mirrors
  Phase 46 D-11's in-memory key compare (a raw textual `git diff` is **not** meaningful here —
  the target is a live DB + remote disk, not the repo working tree).
- **D-05:** Partial failure surfaces a **per-item status list**: per **file** for the SCP
  step and per **row** for the DB-upsert step (succeeded / failed + reason), so the operator
  can reconcile exactly the failed subset manually (SC4). Counts-only was rejected as too
  coarse for reconcile.

### Step ordering + gating (PUB-04 / SC2)
- **D-06:** **Step 1 = SCP every approved artifact** to Render `/data`; **Step 2 = prod
  Postgres upsert.** The Step 2 button stays **disabled until Step 1 reports full success**,
  enforcing artifact-first ordering so no committed DB row references a missing file.
  Per-row-paired SCP+upsert was rejected (interleaves the two systems, complicates the gate).
  Each step shows its own success/failure before the next is reachable.

### Safety invariants (carried from prior phases — locked, not re-discussed)
- **D-07:** **Secret redaction (SC5):** the prod connection string, SSH host/user/key, and
  remote paths **never** appear in any log line, UI text, or error message. Logs/UI show only
  "Prod connection: configured / not configured" (and an analogous "SCP: configured / not
  configured") — extends the existing `StudioConfig.IsProdConfigured` + startup-log pattern.
- **D-08:** **Upsert is `UpsertContentColumnsOnlyAsync` exclusively (SC3)** — no full-row
  upsert path may touch prod; `is_visible` and `is_evergreen` on pre-existing prod rows are
  preserved across a direct push (operator can verify by querying prod before/after).
- **D-09:** **Explicit confirmation gate before any write (SC1):** the diff preview must be
  acknowledged ("I have reviewed what will be written to PROD") before Step 1 is enabled —
  mirrors Phase 46 D-04's reviewed-the-diff checkbox gate. Disable the whole flow with a
  clear message when `IsProdConfigured` (or SCP config) is false.

### Claude's Discretion
- Exact `Studio:Scp:*` user-secrets key names and how SSH key auth is presented
  (key-file path vs agent) — planner/researcher pick the simplest that works against Render.
- `Renci.SshNet` exact version + whether SFTP or SCP subsystem is used (SFTP is often more
  reliable through SSH.NET) — implementation detail, as long as files land at the `/data`
  artifact path.
- StateHasChanged / async-bridging + button-lock state machine for the two in-flight steps —
  mirror Phase 45/46 progress-sink patterns.
- Whether the prod-diff read and the upsert reuse one on-demand store instance or two.
- Resolving the local artifact file set to upload from the approved rows' `artifact_path`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope
- `.planning/ROADMAP.md` §"Phase 47: Direct Prod-DB + SCP Publish Path" — goal + 5 success
  criteria (SC1 diff+confirm, SC2 gated steps, SC3 safe-upsert-only, SC4 partial-failure,
  SC5 secret redaction).
- `.planning/REQUIREMENTS.md` — PUB-04, PUB-05 (this phase). Upstream-constraint table notes
  "A Render file-write REST API — Does not exist; SCP-over-SSH is the only `/data` write
  mechanism (PUB-04)."

### Data layer (Phase 43 — already built; this phase consumes)
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — `UpsertContentColumnsOnlyAsync`
  (the ONLY upsert allowed on prod, SC3), `GetApprovedRowsAsync` (the approved local set),
  `GetAllRowsAsync`/`GetPublishedRowsAsync` (for prod-side read if needed).
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — current ctor (SQLite path); the upsert
  SQL + dialect usage to confirm a Postgres-conn construction path exists or must be added.
- `.planning/phases/43-approval-status-safe-upsert/43-CONTEXT.md` — `approval_status`
  semantics, admin-preserved field set (`is_visible`/`is_evergreen`), safe-upsert rationale.

### Dual-provider storage abstraction (for the on-demand prod Postgres store — D-03)
- `DeckFlow.Core/Storage/` — `IRelationalDialect`, `RelationalDatabaseConnection`,
  `SqliteRelationalDialect`, `PostgresRelationalDialect`; how `DeckFlow.Web` selects Postgres
  via `DECKFLOW_DATABASE_PROVIDER` / connection string is the precedent to mirror.

### Studio host + prior publish path (mirror Phase 45/46)
- `DeckFlow.Studio/Program.cs` — DI wiring; `Studio:ProdConnectionString` read from
  user-secrets (@38-39), `StudioConfig(isProdConfigured)` presence-only (@47), startup log
  "Studio prod connection: configured/not configured" (@110) — the SC5 redaction template.
- `DeckFlow.Studio/StudioConfig.cs` — presence-only record to extend for SCP-config presence.
- `DeckFlow.Studio/Pages/Publish.razor` — Phase 46 two-stage gated publish page; closest
  template for the new direct-push page (diff preview → reviewed-checkbox → gated steps).
- `DeckFlow.Studio/Pages/Review.razor`, `DeckFlow.Studio/Pages/Harvest.razor` — status-driven
  rows, button-lock state machine, progress bridging.
- `DeckFlow.Studio/Services/ActionOrchestratorProgress.cs` — progress-sink bridge pattern.
- `.planning/phases/46-review-queue-commit-publish-path/46-CONTEXT.md` — commit-publish
  decisions; D-04 (reviewed-diff checkbox gate) and D-11/12 (in-memory key diff) are the
  direct precedents for SC1/SC2/SC4 here.
- `.planning/phases/45-harvest-distill-ui/45-CONTEXT.md` + `45-UI-SPEC.md` — Studio UI wiring
  + the UI-SPEC design-contract pattern this phase's `47-UI-SPEC.md` will follow.

### Process / shell-out + git precedent (alternative transport reference)
- `DeckFlow.Core/Integration/CliCommandSpec.cs`, `GitRepository.cs`/`IGitRepository.cs` —
  existing `Process.Start` patterns (the rejected shell-`scp` alternative; useful reference
  for error-surfacing shape if SSH.NET proves awkward).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Phase 43 `UpsertContentColumnsOnlyAsync` + `GetApprovedRowsAsync` — the entire prod-write
  + approved-set data layer already exists; this phase only points them at a prod store.
- `Publish.razor` (Phase 46) — two-stage gated publish UI with diff preview + reviewed-diff
  checkbox; nearly the same shape as this direct-push page.
- `Studio:ProdConnectionString` + `StudioConfig.IsProdConfigured` + startup presence-log —
  the SC5 secret-redaction pattern is already established and just needs extending for SCP.

### Established Patterns
- Studio = storage-agnostic UI over host-wired Core stores; Studio must NOT reference
  `DeckFlow.CLI`; Core stays console-free.
- Dual-provider storage behind `IRelationalDialect`/`RelationalDatabaseConnection`
  (`DeckFlow.Web` Postgres path) — the prod store must go through this, not a raw Npgsql/ADO
  client.
- Two-stage gated action with optimistic/locked buttons + progress bridge (Phase 45/46).

### Integration Points
- NEW: on-demand Postgres `ContentSiteIndexStore` from `Studio:ProdConnectionString`
  (possibly a new Postgres-conn ctor/overload).
- NEW: SSH.NET-based SCP/SFTP upload service (new package — D-01) + `Studio:Scp:*` config.
- NEW: Studio direct-push page + NavMenu entry (exact structure per 47-UI-SPEC).
- Reuse: `UpsertContentColumnsOnlyAsync`, `GetApprovedRowsAsync`, presence-only logging.

</code_context>

<specifics>
## Specific Ideas

- Artifact-first: SCP **all** files (Step 1) before **any** prod DB write (Step 2); Step 2
  gated on Step 1 full success.
- Prod diff = in-memory natural-key compare (prod rows vs approved local) → New/Updated +
  per-row & per-file lists; explicit "writing to PROD" confirm before any write.
- Partial failure → per-file (SCP) + per-row (DB) success/fail list for manual reconcile.
- Prod store built **on demand**, not a startup singleton.
- Secrets (conn string, SSH host/user/key, remote path) never logged/shown — "configured /
  not configured" only.
- `UpsertContentColumnsOnlyAsync` is the sole prod write; `is_visible`/`is_evergreen`
  preserved.

</specifics>

<deferred>
## Deferred Ideas

- **Shell-out to system `scp`** — considered and rejected in favor of SSH.NET (D-01); revisit
  only if the new-package addition is later reversed.
- **Always-live prod store DI singleton** — rejected (D-03) to shrink accidental-write
  surface; revisit only if on-demand construction proves too costly.
- **Per-row interleaved SCP+upsert** — rejected (D-06) for gate simplicity.
- **Page/nav layout, expand-vs-modal markup, visual styling** — deferred to `47-UI-SPEC.md`
  (`/gsd-ui-phase 47`).

### Reviewed Todos (not folded)
- *Spike — combo data richness for primer pilot lines* — unrelated (primer/combo data).
- *User-selectable Expert Context — pin a KB video/tag into the analysis prompt* — out of
  scope (deckflow.gg prompt feature, not the Studio publish flow).
- *Validate Content KB value — A/B ChatGPT output with vs without expert context* — unrelated
  (KB value validation). All three matched only on generic keywords (score 0.6), same as
  Phase 46.

</deferred>

---

*Phase: 47-direct-prod-db-scp-publish-path*
*Context gathered: 2026-06-16*
