# Phase 91: Reconcile + Seed Lifecycle - Context

**Gathered:** 2026-07-08
**Status:** Ready for planning
**Source:** Interactive discuss-phase (4 areas, 8 decisions) + ROADMAP + REQUIREMENTS + P89/P90 CONTEXT + research docs

<domain>
## Phase Boundary

**Goal:** Ship the seed-lifecycle safety layer that lets the operator detect and correct
prod↔git↔seed drift, and — for the first time — let a git seed *remove* content from prod
(not just add it), without any risk to prod-only rows.

Three locked requirements, in dependency order:
- **SYNC-17** (hard prereq) — Row-level **seed-ownership marker** distinguishing seed-owned rows
  from prod-only rows. No seed-driven delete may ship without it (Codex HIGH).
- **SYNC-11** — A **NEW** prod↔git↔seed reconciler + persistent discrepancy store (NOT a
  `ContentKbOrphanScanner` extension — that scanner is local-only, CLI-only, and lacks prod
  access / git enumeration / seed awareness). Emits: published-orphans (visible row, no body),
  file-orphans (`.md`, no row), seed-drift (prod row absent from seed), body-hash-mismatch (uses
  SYNC-01 `body_sha256`). Deterministic discrepancy IDs, idempotent re-run (zero dupes),
  resolution-by-absence, scope-tagged partial runs. **Dry-run mode ships first.**
- **SYNC-12** — Seed reload handles **removals**: rows absent from the seed-managed set (per the
  SYNC-17 marker) are hidden intentionally + logged, replacing the additive-only upsert (C2);
  destructive apply gated behind dry-run validation.

Phase 91 is the systematic fix D-11 (P90) pointed at: the P90 git-coverage audit was a read-only
stopgap; this reconciler is the real drift-detection + correction tool. It consumes P89's
`body_sha256` + unified signature and P90's shared seed-export factory.

**Flag:** `sync.reconcile` (gates the destructive apply only — see D-08/D-09).

</domain>

<decisions>
## Implementation Decisions (locked this phase)

### Design stance (inherited P89/P90 — unchanged, MUST honor)
- **git = single source of truth for BODIES** (P90 D-01). The prod `content_site_index` row is
  subordinate and reconstructable from the git seed. Reconcile treats git/seed as authoritative.
- **No CDC / queue / SFTP-body-push** (P90 D-02). Reconcile is idempotent keyed diff + hash
  comparison, run on operator demand. It must NOT introduce any out-of-band body transport.
- **Reuse, do not fork:** P89 `ContentSiteIndexContentSignature.ComputeBodySha256` + unified
  signature for body-hash-mismatch detection; P90 shared `ContentIndexExportRow.From()` seed
  factory for any seed (re)write. No second hash path, no second seed-writer.

### Seed-ownership marker (SYNC-17)
- **D-01: New DB column `seed_managed` (bool) + matching seed JSON field.** Dialect-guarded
  additive DDL (SQLite + Postgres), idempotent `ensure`-style, exactly like SYNC-01's
  `body_sha256` rollout. Rejected: derive-from-seed-set-at-load — a row transiently missing from
  an in-progress/partial seed would read as prod-only and be a false-delete candidate, which is
  unsafe for the exact operation the marker gates. The marker must be an explicit persisted fact,
  not an inference. Set `seed_managed = true` whenever a row is written from the seed-managed set
  (Publish / DirectPush seed export / seed reload).
- **D-02: Backfill legacy rows by CURRENT seed membership.** On first classification, a row whose
  natural key appears in `index-seed.json` → `seed_managed = true`; absent → `seed_managed = false`
  (prod-owned). Principled under git=SoT and immediately enables cleanup of the ~70 prod-only rows.
  The data-safety tail (a genuinely-seed-owned row briefly misclassified) is caught by the
  dry-run + flag gate before any destructive apply — misclassification never deletes silently.
- **Invariant:** seed-driven removal (SYNC-12) applies to `seed_managed = true` rows ONLY.
  Prod-owned (`seed_managed = false`) rows are NEVER hidden/deleted by reconcile — the SYNC-17
  guarantee. This is the whole reason the marker is a hard prereq.

### Removal action (SYNC-12)
- **D-03: Soft-hide only.** When a seed-owned row is absent from the seed-managed set, reconcile
  sets `is_visible = false`, RETAINS the row (marker, `body_sha256`, timestamps, history), and
  logs the discrepancy. Reversible, reconstructable via reseed, full audit trail preserved. This
  is the least-destructive expression of "resolution-by-absence" and matches git=SoT (a truly-gone
  row simply stays hidden; a re-added row un-hides on next reseed). Rejected this phase:
  hard-delete (irreversible in-place, loses local marker/history) and two-stage hide-then-delete
  (extra gated action + more surface than a first destructive ship warrants). Hard-delete can be a
  future lifecycle addition if soft-hide proves insufficient — NOT in scope now.

### Reconciler surface + discrepancy store (SYNC-11)
- **D-04: Reconciler runs as a Studio operator action.** Studio already reads prod
  (`ProdContentReader`), holds the operator's git checkout + `index-seed.json`, and hosts
  DirectPush / Pull + the P90 `IGitBodyCoverageAudit`. It is the only surface with all three
  inputs (prod DB read, git tree, seed) and the established operator UX. Operator-triggered only
  (scheduled/automatic runs are SYNC-F2, explicitly out of this cycle). Rejected: CLI-against-prod
  (re-plumbs prod creds/git/seed into CLI, no operator surface) and web admin endpoint (512MB
  serving tier, lacks the operator's working git tree/seed authoring context, mixes a maintenance
  job into the serving process).
- **D-05: Persistent discrepancy store lives LOCAL to Studio.** A durable operator-side store
  (local content-kb DB / SQLite), consistent with P90 D-10's local awaiting-confirm durability.
  Idempotent upsert keyed by a **deterministic discrepancy ID**; re-run resolves-by-absence
  (a discrepancy no longer present is marked resolved, not re-created); partial/scoped runs are
  scope-tagged so they never false-resolve discrepancies outside their scope. Rejected: a prod DB
  table (violates the minimal-additive-prod-DDL cap — only `body_sha256` + the seed marker are
  sanctioned prod DDL this cycle) and a git-tracked flat file as the store (weak for idempotent
  upsert / resolution-by-absence).
- **D-06: Git-tracked report file is the human-readable dry-run OUTPUT, layered on top of D-05.**
  The dry-run may also emit a readable discrepancy report artifact for operator review; the
  persistent store (D-05) remains the source of truth for state. The file complements, does not
  replace, the local store.
- **D-07: Reconciler is a NEW build (per SYNC-11), not a `ContentKbOrphanScanner` extension.** The
  existing scanner is local-only and lacks prod/git/seed awareness. Mine it (and
  `ContentSyncDiffClassifier`, `ReconciliationReporter`) for reusable classification/reporting
  shapes, but the reconciler is a distinct prod-aware component.

### Dry-run → apply gating + flag (SYNC-11 / SYNC-12)
- **D-08: Two-step re-validated apply.** Dry-run produces the discrepancy report (D-06) + persists
  discrepancies (D-05). A SEPARATE "Apply removals" action RE-RUNS the diff at apply time and
  soft-hides only discrepancies still present then — a stale apply (prod/seed moved since the
  dry-run) is rejected, never blindly applied. Satisfies "destructive apply gated behind dry-run
  validation." Rejected: per-discrepancy confirm (tedious at ~70-row scale; the re-validate
  already prevents blind bulk mistakes) and bulk auto-apply (collapses the mandated review gate).
- **D-09: `sync.reconcile` gates ONLY the destructive (soft-hide) apply.** Detection, dry-run, the
  discrepancy store, and the report are ALWAYS available (read-only, safe) so the operator gets
  full visibility BEFORE flipping the flag — this is the "dry-run ships first" intent and is
  exactly the visibility that would have caught the 63 unpublished rows + CP437 mojibake. Only the
  seed-driven removal write is behind the flag. Rejected: gating the whole reconciler (hides the
  read-only visibility behind an OFF flag, defeating the point).
- **D-10: `sync.reconcile` is a WEB-DB feature flag; Studio reads it via the P90 accessor; seeded
  OFF.** Register in `FeatureFlagCatalog` (persisted in `FeatureFlagStore`), seeded OFF per the
  rollout convention (P90 D-05). Studio reads the same flag value through the minimal read-only
  accessor P90 already built (P90 D-04 pattern) — single source of truth, no duplicate Studio
  config flag. Rejected: a Studio-local config flag (forks a second flag system, breaks the D-04
  convention just established).

### Claude's Discretion
- Exact column name/type nuance for `seed_managed` (bool vs nullable smallint) and the deterministic
  discrepancy-ID scheme — planner/researcher pick the least-invasive form per the code, honoring
  the dialect-guarded additive-DDL house pattern and idempotent-upsert requirement.
- Where the local discrepancy store schema lives (new table in the existing local content-kb DB vs
  a sibling store) — least-invasive per D-05.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, pattern-mapper, planner) MUST read these before planning.**

### Research design (authoritative rationale — read first)
- `docs/research/kb-prod-sync-fix-design.md` — §4 (prod-side/cross-store reconciliation → build one:
  discrepancy classes, idempotent discrepancy IDs, resolution-by-absence, scope tags) + §additive-only
  upsert weakness (C2). The direct source for SYNC-11/12 design.
- `docs/research/kb-prod-sync-roadmap.md` — phase roadmap + requirement derivation (SYNC-11/12/17).

### Requirements + roadmap
- `.planning/REQUIREMENTS.md` — SYNC-17 / SYNC-11 / SYNC-12 full text + design stance (git=SoT,
  idempotent one-way keyed upsert, minimal additive prod DDL cap) + SYNC-F2 (scheduled reconcile
  OUT of cycle).
- `.planning/ROADMAP.md` — Phase 91 line + Cycle 16 framing.

### Prior-phase decisions to honor
- `.planning/phases/90-directpush-correctness-seed-sync/90-CONTEXT.md` — D-01 (git=SoT bodies),
  D-02 (no CDC/SFTP), D-04 (web-DB flag + Studio read-only accessor pattern — reused by D-10 here),
  D-05 (flag seeded OFF), D-10 (local durable state), D-11 (this reconciler is the systematic
  git-coverage fix the P90 audit only reported).
- `.planning/phases/90-directpush-correctness-seed-sync/90-FOLLOWUPS.md` — FU-1/FU-2 are P93
  pre-flip gate items (NOT this phase), but confirm the flag-OFF-until-P93 regime this phase
  ships into.
- `.planning/phases/89-content-hash-foundation/89-CONTEXT.md` — `body_sha256` + unified signature
  + `ComputeBodySha256`; reconcile's body-hash-mismatch class consumes these.
- `.planning/phases/88-index-row-integrity-hotfix/88-CONTEXT.md` — composite natural key +
  approval mirror + schema-ensure-off pattern; the marker DDL follows the same guarded-additive shape.

### Code — reconcile inputs / reuse (Studio)
- `DeckFlow.Studio/Services/IProdContentReader.cs` + `ProdContentReader.cs` — prod DB read seam the
  reconciler builds on.
- `DeckFlow.Studio/Services/IGitBodyCoverageAudit.cs` — P90 read-only git-coverage audit; nearest
  existing analog for the git-tree ↔ row join; extend/generalize toward the full reconciler.
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` + `PublishCoordinator.cs` — operator-action
  coordinator + seed-export patterns to mirror for the reconcile action + any seed (re)write.

### Code — signature / seed / store (Core + Web)
- `DeckFlow.Core/Content/ContentKbOrphanScanner.cs` — local-only scanner; mine for classification
  shapes but do NOT extend it into the reconciler (D-07).
- `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` — existing diff classification; reuse
  vocabulary/shape where it fits the four discrepancy classes.
- `DeckFlow.Core/Reporting/ReconciliationReporter.cs` — existing reporting shape for the D-06
  human-readable report.
- `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` — `ComputeBodySha256` + `AreContentEqual`;
  reuse for body-hash-mismatch (do NOT add a second hash path).
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `content_site_index` columns + upsert +
  `SetVisibilityBySourceAsync`; the write surface for the `seed_managed` column (D-01) and the
  soft-hide apply (D-03).
- `DeckFlow.Core/Orchestration/ContentIndexExportRow.cs` — shared seed-export factory (P90); any
  seed write flows through `From()`.
- `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` — seed load/reconstruct; SYNC-12 changes
  this from additive-only to removal-aware (per the marker).
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` + `FeatureFlagStore.cs` /
  `IFeatureFlagStore.cs` — register `sync.reconcile` (seeded OFF), the store Studio's read-only
  accessor reads (D-10).

### Codebase maps
- `.planning/codebase/ARCHITECTURE.md`, `STRUCTURE.md`, `CONVENTIONS.md`, `TESTING.md` — house
  patterns (dialect-guarded DDL, Studio coordinators, test conventions).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IGitBodyCoverageAudit` (Studio, P90): read-only git-tree ↔ prod-row join — the closest existing
  analog to the reconciler's file-orphan / published-orphan detection; generalize rather than rebuild.
- `ComputeBodySha256` / unified signature (P89): body-hash-mismatch class is a direct consumer.
- Shared `ContentIndexExportRow.From()` seed factory (P90): any seed (re)write goes through it.
- P90 minimal read-only web-flag accessor in Studio: reused verbatim for reading `sync.reconcile`.
- `ContentSyncDiffClassifier` + `ReconciliationReporter` (Core): classification + reporting shapes.

### Established Patterns
- Dialect-guarded, idempotent additive DDL (SYNC-01 `body_sha256`, P88 schema-ensure) → `seed_managed` follows it.
- Web-DB feature flag seeded OFF + Studio read-only accessor (P90 D-04/D-05) → `sync.reconcile` follows it.
- Studio operator-action coordinators (DirectPush/Pull) → the reconcile + apply actions follow that UX.
- Two-phase read-then-write with re-validation (P90 confirm poll) → the dry-run→re-validated-apply gate echoes it.

### Integration Points
- New `seed_managed` column threads through: store schema/upsert, seed JSON export/import, seed loader.
- Seed loader (`ContentKbSeedLoader`) shifts from additive-only to removal-aware under the marker (SYNC-12).
- New Studio reconcile action + local discrepancy store; reads prod via `ProdContentReader`, git via
  the coverage-audit seam, seed via the seed file; reads `sync.reconcile` via the P90 accessor.

</code_context>

<specifics>
## Specific Ideas
- The reconciler's four discrepancy classes map 1:1 to fix-design §4: published-orphan (visible
  row, no body), file-orphan (`.md`, no row), seed-drift (prod row absent from seed), body-hash-mismatch.
- "Resolution-by-absence" is concrete: a discrepancy no longer detected on a re-run is marked
  resolved in the local store (not deleted, not re-created) — idempotent re-run = zero dupes.
- The soft-hide apply reuses `SetVisibility...`-style writes; it must preserve F-51-PG-01
  timestamptz handling on Postgres if it touches any timestamp field.
- Backfill (D-02) and the marker column (D-01) should ship together so no reconcile run ever sees
  unclassified rows.

</specifics>

<deferred>
## Deferred Ideas (explicitly NOT in Phase 91)
- **Hard-delete of seed-absent rows / two-stage hide-then-delete** — deferred; soft-hide only this
  phase (D-03). Revisit only if soft-hide proves insufficient.
- **Scheduled / automatic reconcile runs** — SYNC-F2, explicitly out of this cycle; operator-triggered only.
- **Pull-from-Prod hardening** (per-field master, git-pull-first staleness guard) — Phase 92
  (SYNC-13/14/15). Do not modify Pull semantics here.
- **End-to-end containerized round-trip integration test** — Phase 93 (SYNC-16).
- **Flipping `sync.reconcile` (or `sync.directpush-gitbody`) ON in prod** — gated by Phase 93
  pre-flip checklist; this phase ships the flag OFF.
- Any prod-side discrepancy table or new prod DDL beyond the `seed_managed` marker — rejected by
  the minimal-additive-prod-DDL cap (D-05).

None raised during discussion that required redirecting — the session stayed within phase scope.

</deferred>

---

*Phase: 91-reconcile-seed-lifecycle*
*Context gathered: 2026-07-08 via interactive discuss-phase*
