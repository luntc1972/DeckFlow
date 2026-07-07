# Phase 90: DirectPush Correctness + Seed Sync - Context

**Gathered:** 2026-07-07
**Status:** Ready for planning
**Source:** Fast-path decision capture (3 decisions via AskUserQuestion) + ROADMAP + research docs

<domain>
## Phase Boundary

**Goal:** DirectPush converges to the SAME consistent end-state as Publish — a DirectPush'd
Content-KB row's body reaches prod ONLY through git (`/app`), and a redeploy can never revert
or leave a DirectPush'd row half-consistent (visible-before-reachable, or "Never published"
badge on a live row).

Phase 89 delivered the body hash + unified signature this phase consumes: `body_sha256` is now
computed at publish time, persisted end-to-end, round-tripped through seed JSON, and backfilled
on both hosts. Phase 90 uses that hash to **hash-gate** the DirectPush visibility/stamp ordering
and to **verify** the deployed `/app` body before flipping state.

**Requirements (4):**
- **SYNC-07** — Bodies reach prod only via git `/app`; drop the `/data`-SFTP-first serving overlay (kills M1 unreachable-body + M3 `/app`-shadows-`/data`). *[serving flip — web]*
- **SYNC-08** — DirectPush re-exports `index-seed.json` (like Publish already does) so git fully reconstructs prod and a redeploy cannot revert DirectPush'd rows (M2, C3). *[Studio + shared export]*
- **SYNC-09** — Hash-gated expand-contract ordering: body committed + deployed + hash-verified at `/app` before `is_visible` flips (M3). *[ordering — Studio drives, web verifies]*
- **SYNC-10** — `pushed_to_prod_utc` stamped only after prod confirms the deployed body, not at local commit time (M6a; fixes "Never published" badge on live rows). *[stamp — Studio]*

**Codex-recommended split (from plan-review of the original roadmap):** DirectPush is a
re-architecture, not a tweak. Treat SYNC-07 (git-body serving flip) as one coherent unit and
SYNC-08/09/10 (re-export + hash-gated ordering + confirmed stamp) as a second unit that depends
on the flip. The planner should reflect this in wave/plan structure (e.g. a serving-flip plan
gating an ordering/stamp plan), not cram all four into one plan.

</domain>

<decisions>
## Implementation Decisions (locked)

### Design stance (inherited, unchanged)
- **D-01: git = single source of truth for BODIES.** The prod `content_site_index` row is
  subordinate and reconstructable from the git seed. Bodies are served from the git-shipped
  `/app` tree; prod `/data` holds no authoritative body (the `0dd49f19` decouple was deliberate).
  This phase makes DirectPush obey that stance the way Publish already does.
- **D-02: No CDC / queue / SFTP-body-fetch.** Idempotent one-way keyed upsert + body hash +
  expand-contract deploy ordering fits the 512MB Render / single-operator scale. DirectPush must
  NOT start SFTP-uploading bodies to prod `/data` to "fix" reachability — the fix is to serve
  from `/app` and reconstruct via seed, not to push bodies out-of-band.

### Approval ownership (ANSWERED — fast-path Q2)
- **D-03: Approval is LOCAL-authoritative for DirectPush, mirrored to prod.** Studio/git is the
  source of truth for a row's approval state; DirectPush mirrors approval INTO prod (consistent
  with the git-SoT stance and P88's approval-mirror plumbing). Prod never independently flips
  approval. This closes the SYNC-04 follow-through and prevents the visible-but-pending class
  (C1) from re-appearing through the DirectPush path.

### Flag home (ANSWERED — fast-path Q3)
- **D-04: `sync.directpush-gitbody` is a WEB-DB feature flag (authoritative), Studio reads/mirrors.**
  The behavior the flag gates — serve a DirectPush'd row's body from `/app` instead of the
  `/data`-SFTP-first overlay — is a WEB serving concern, so the flag lives in the existing
  web feature-flag system (`DeckFlow.Web/Services/FeatureFlags/`, registered in
  `FeatureFlagCatalog`, persisted in `FeatureFlagStore`). Studio does not register the web flag
  system today; plumb a MINIMAL read-only accessor so Studio's DirectPush path can read the same
  flag value from the prod feature-flag store (Studio already reads prod via `ProdContentReader`).
  Single source of truth = the web-DB flag; no duplicate Studio config flag.
- **D-05: Flag seeded OFF.** `sync.directpush-gitbody` ships OFF, matching every prior cycle's
  convention (operator flips ON after the prod deploy is confirmed healthy). With the flag OFF,
  DirectPush serving behavior is unchanged from today — zero risk during rollout.

### Ordering / stamp correctness (SYNC-09 / SYNC-10)
- **D-06: Hash-gated expand-contract ordering.** `is_visible` for a DirectPush'd row flips to
  true ONLY after: (a) the body is committed to git, (b) the deploy carrying it is live at `/app`,
  and (c) the deployed `/app` body's recomputed `body_sha256` matches the stored hash (reuse the
  Phase 89 `ComputeBodySha256` helper + the render-guard comparison — do NOT hand-roll). Until all
  three hold, the row stays hidden. This is the expand (ship body) → verify → contract (flip
  visible) sequence; never contract-before-expand.
- **D-07: `pushed_to_prod_utc` stamped only after prod confirms the deployed body.** Move the
  stamp from local-commit time to post-confirmation (after the `/app` hash-verify in D-06). A live
  DirectPush'd row must never show a "Never published" badge (M6a). Preserve the F-51-PG-01
  timestamptz handling from prior phases when writing the stamp on Postgres.

### Seed re-export (SYNC-08)
- **D-08: DirectPush re-exports `index-seed.json` via the SHARED export factory.** DirectPush must
  emit the seed exactly like Publish does, through the shared `ContentIndexExportRow.From()`
  factory (Phase 89 already routed both CLI export and DirectPush through it and added
  `body_sha256` to it), so a fresh prod reseed reconstructs the DirectPush'd row instead of
  reverting it (M2, C3). Do NOT fork a second seed-writer.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, pattern-mapper, planner) MUST read these before planning.**

### Research design (authoritative rationale — read first)
- `docs/research/kb-prod-sync-fix-design.md` — the fix design (M1..M8 + C1..C4 weaknesses, git-SoT stance, expand-contract ordering).
- `docs/research/kb-prod-sync-roadmap.md` — phase roadmap + requirement derivation.

### DirectPush / Publish / Pull coordinators (Studio)
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` — the DirectPush path to converge onto Publish's end-state; currently stamps `pushed_to_prod_utc` / flips visibility at the wrong time and does not re-export the seed.
- `DeckFlow.Studio/ViewModels/PublishCoordinator.cs` — the reference end-state DirectPush must match (already re-exports seed, orders correctly). Mine for the pattern to replicate.
- `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` — consumer of the shared signature/classifier; do not regress F-51-PG-01 direction branches (Pull hardening itself is P92, out of scope here).
- `DeckFlow.Studio/Services/ProdContentReader.cs` — how Studio reads prod DB; the seam to extend for the read-only prod flag accessor (D-04).

### Shared export + seed (Core / Web)
- `DeckFlow.Core/Orchestration/ContentIndexExportRow.cs` — shared export factory `From()` (already carries `body_sha256`); DirectPush seed re-export flows through this (D-08).
- `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` — seed load/reconstruct side (already round-trips `bodySha256`).

### Web serving + flag + hash guard
- `DeckFlow.Web/Controllers/ContentKbController.cs` — Content-KB detail serving path; the Phase 89 fail-open render guard lives here; SYNC-07 flips serving to `/app`-only under the flag.
- `DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs` — resolves `/app` (git) vs `/data` (SFTP overlay) artifact paths; SYNC-07 drops the `/data`-first overlay from the serving resolution.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — register `sync.directpush-gitbody` here, seeded OFF (D-04/D-05).
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` + `IFeatureFlagStore.cs` — flag persistence; the store Studio's read-only accessor reads from.
- `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` — `ComputeBodySha256` + `AreContentEqual` (Phase 89); reuse for the D-06 `/app` hash-verify. Do NOT add a second hash path.
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `body_sha256`, `pushed_to_prod_utc`, `is_visible` columns + upsert variants; the write surface for D-06/D-07.

### Prior-phase decisions to honor
- `.planning/phases/89-content-hash-foundation/89-CONTEXT.md` — D-01..D-09 (body hash, one signature, fail-open render guard, dual-host backfill). Phase 90 builds directly on the hash + shared export factory.
- `.planning/phases/88-index-row-integrity-hotfix/88-CONTEXT.md` — approval mirror + composite natural key + schema-ensure-off; D-03 here extends P88's approval mirror.

</canonical_refs>

<specifics>
## Specific Ideas
- The `/app` hash-verify (D-06) should reuse the exact render-guard comparison Phase 89 added in
  `ContentKbController` — recompute `ComputeBodySha256` on the deployed `/app` body and compare to
  the stored `body_sha256`. This is the same byte-identical helper, so a match is a real proof the
  deployed body is the intended one.
- DirectPush should call the same seed-export code path Publish calls (shared factory), not a
  parallel one — the whole point of Phase 89's `ContentIndexExportRow.From()` consolidation.
- Flag gating should make the ENTIRE new DirectPush behavior (serving flip + ordering + stamp)
  rollout-atomic under `sync.directpush-gitbody` where practical, so operator flips one flag.
</specifics>

<deferred>
## Deferred Ideas (explicitly NOT in Phase 90)
- **Prod-side reconciler + seed-ownership marker + gated seed-driven deletes** → Phase 91 (SYNC-17, flag `sync.reconcile`).
- **Pull-from-Prod hardening** (per-field master, git-pull-first) → Phase 92. Do not modify Pull semantics here beyond not regressing the shared signature.
- **End-to-end containerized round-trip integration test** → Phase 93 (SYNC-16).
- Any SFTP-body-push to prod `/data` — rejected by D-02; never in scope.
</deferred>

<scope_fence>
## Scope Fence
**In scope:** DirectPush serving flip to `/app` under a web-DB flag (SYNC-07); DirectPush seed
re-export via the shared factory (SYNC-08); hash-gated expand-contract visibility ordering
(SYNC-09); post-confirmation `pushed_to_prod_utc` stamp (SYNC-10); a minimal read-only Studio
accessor for the web-DB flag; register `sync.directpush-gitbody` OFF.

**Out of scope:** the reconciler / seed lifecycle / deletes (P91), Pull hardening (P92), the
round-trip integration test (P93), any new SFTP body transport, any framework migration, any
change to the Phase 89 hash helper or unified signature (reuse only).
</scope_fence>

---

*Phase: 90-directpush-correctness-seed-sync*
*Context gathered: 2026-07-07 via fast-path decision capture*
