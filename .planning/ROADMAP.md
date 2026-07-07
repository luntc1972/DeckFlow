# Roadmap: DeckFlow

## Milestones

- 🚧 **Cycle 16 — Content-KB Prod↔Git↔Studio Sync Hardening** — Phases 88-93 (target `2026.07.3`)
- ✅ **2026.07.2 Cycle 15 — Cleanup, Refactor & Visual Polish** — Phases 82–87 (shipped 2026-07-05) → see .planning/milestones/2026.07.2-ROADMAP.md
- ✅ **Cycle 14 — Deeper Deck Evaluation** — Phases 79-81 (shipped 2026-07-03, `2026.07.1`) — see `.planning/milestones/cycle14-ROADMAP.md`
- ✅ **Cycle 13 — Deck Evaluation & Creator Output** — Phases 75-78 (shipped 2026-06-30, `2026.06.10`) — see `.planning/milestones/cycle13-ROADMAP.md`
- ✅ **Cycle 12 — Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence** — Phases 70-74 + flag-key namespacing (shipped 2026-06-27, `2026.06.9`)
- ✅ **Cycle 11 — Security, Visibility Control & Creator-Lens** — Phases 64-69 (shipped 2026-06-25, `2026.06.8`) — see `.planning/milestones/cycle11-ROADMAP.md`
- ✅ **Cycle 10 — Studio Automation, Sync & Polish** — Phases 59-63 (shipped 2026-06-21, `2026.06.6`) — see `.planning/milestones/cycle10-ROADMAP.md`
- ✅ **Cycle 9 — Content Pipeline & Publish-Tracking** — Phases 55-58 (shipped 2026-06-19, `2026.06.5`) — see `.planning/milestones/cycle9-ROADMAP.md`
- ✅ **Cycle 8 — Hardening & Backlog Burn-down** — Phases 51-54 (shipped 2026-06-17, `2026.06.4`) — see `.planning/milestones/cycle8-ROADMAP.md`
- ✅ **v1.7 Local Harvest & Publish Studio** — Phases 41-50 (shipped 2026-06-17) — see `.planning/milestones/v1.7-ROADMAP.md`
- ✅ **v1.6 Content KB Retrieval Fix + Value Re-Validation** — Phases 34-40 (shipped 2026-06-12) — see `.planning/milestones/v1.6-ROADMAP.md`
- ✅ **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-33 (shipped 2026-06-10) — see `.planning/milestones/v1.5-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`

## Phases

**Phase Numbering:**
- Integer phases (88, 89, ...): Planned milestone work
- Decimal phases (88.1, 88.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order. Numbering continues from Cycle 15's Phase 87.

### 🚧 Cycle 16 — Content-KB Prod↔Git↔Studio Sync Hardening (target `2026.07.3`)

- [x] **Phase 88: Index-Row Integrity Hotfix** - DirectPush stops writing visible-while-pending rows and collision-prone PinId diffing (ships first — live prod bugs)
- [ ] **Phase 89: Content-Hash Foundation** - One unified body-inclusive hash everywhere; corrupt/stale bodies become detectable instead of silently served
- [ ] **Phase 90: DirectPush Correctness + Seed Sync** - Bodies reach prod only via git; DirectPush re-exports the seed and survives a redeploy without reverting (flag `sync.directpush-gitbody`)
- [ ] **Phase 91: Reconcile + Seed Lifecycle** - Seed-ownership marker, then a prod↔git↔seed reconciler (dry-run first), then gated seed-driven removal (flag `sync.reconcile`)
- [ ] **Phase 92: Pull Hardening** - Pull-from-Prod adopts prod state without clobbering operator fields or acting on a stale checkout
- [ ] **Phase 93: Round-Trip Integration Test** - One end-to-end test locks the whole distill→publish→prod→serve→reseed→pull→reconcile loop

<details>
<summary>✅ 2026.07.2 Cycle 15 (Phases 82–87) — SHIPPED 2026-07-05</summary>

- [x] Phase 82 — Refactor-Review Sweep & UI Baseline Audit (completed 2026-07-04)
- [x] Phase 83 — Packet-Service SRP Split (completed 2026-07-04)
- [x] Phase 84 — Theme Semantic-Token Migration (completed 2026-07-05)
- [x] Phase 85 — `chatgpt-*` Naming Cleanup (completed 2026-07-05)
- [x] Phase 86 — UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout (completed 2026-07-05)
- [x] Phase 87 — Creator-Source Model Hardening (completed 2026-07-05)

</details>

## Phase Details

### Phase 88: Index-Row Integrity Hotfix
**Goal**: Kill the two live prod correctness bugs — a row can never be publicly visible while `pending`, and sync diffing can never cross-match rows on a colliding surrogate key.
**Depends on**: Nothing (first phase — live prod bugs, ships ahead of the hash foundation per Codex-revised sequencing)
**Requirements**: SYNC-04, SYNC-05, SYNC-06
**Success Criteria** (what must be TRUE):
  1. A row DirectPush inserts or updates always carries `approval_status='approved'` — it is never publicly visible while `pending` (C1 closed).
  2. Sync diff classification matches rows by the `(natural_key_type, natural_key_value)` composite instead of `PinId`, so a YouTube/podcast key collision can no longer cross-match two different rows (C4-collision closed).
  3. The DirectPush diff-read path runs no unexpected DDL against prod, and the code comment describing that behavior is corrected to match reality (C4-comment closed).
**Plans**: 3 plans (2 waves)
- [x] 88-01-PLAN.md — Store approved-write mirror + serve-side approval filter + schema-ensure OFF switch (SYNC-04, SYNC-06 store)
- [x] 88-02-PLAN.md — Shared ContentNaturalKey helper + composite-key classifier + stored vocabulary + skip-log (SYNC-05 core)
- [x] 88-03-PLAN.md — Studio coordinator dedup + ProdStoreFactory schema-ensure-off + no-DDL comment sweep (SYNC-05 studio, SYNC-06 factory)

### Phase 89: Content-Hash Foundation
**Goal**: Every row's body content is hashed end-to-end on one unified signature, so drift is a single indexed comparison and body corruption (e.g. mojibake) is detectable instead of silently served.
**Depends on**: Phase 88
**Requirements**: SYNC-01, SYNC-02, SYNC-03
**Success Criteria** (what must be TRUE):
  1. `content_site_index` carries a `body_sha256` column (SQLite + Postgres + seed JSON) computed from the on-disk `.md` body at publish time.
  2. DirectPush, Pull, and reconcile all compare rows using one unified body-inclusive signature — the two previously divergent schemes (`ContentSiteIndexContentSignature` and the `ContentSyncDiffClassifier` fingerprint) are gone.
  3. The web app refuses to render a row whose on-disk body hash does not match its stored `body_sha256`, logging the mismatch instead of serving stale/corrupt content.
**Plans**: 6 plans (3 waves)
- [x] 89-01-PLAN.md — Shared ComputeBodySha256 helper + body-inclusive BuildSignature + ContentSiteIndexRow.BodySha256 (SYNC-01, SYNC-02) [wave 1]
- [ ] 89-02-PLAN.md — Store body_sha256 DDL/model/upsert plumbing + null-only backfill setter (SYNC-01) [wave 2]
- [ ] 89-03-PLAN.md — Delete Fingerprint, switch classifier to unified signature + one-signature-surface guard test (SYNC-02) [wave 2]
- [ ] 89-04-PLAN.md — index-seed.json export/load bodySha256 round-trip + golden fixture (SYNC-01) [wave 3]
- [ ] 89-05-PLAN.md — Publish-time hash compute + detail-render fail-open guard with structured warning (SYNC-01, SYNC-03) [wave 3]
- [ ] 89-06-PLAN.md — One-time deterministic startup backfill pass (idempotent, DDL-free) (SYNC-01) [wave 3]

### Phase 90: DirectPush Correctness + Seed Sync
**Goal**: DirectPush converges to the same consistent end-state as Publish — bodies reach prod only through git, and a redeploy can never revert or leave a DirectPush'd row half-consistent.
**Depends on**: Phase 89 (needs the body hash to hash-gate the ordering)
**Requirements**: SYNC-07, SYNC-08, SYNC-09, SYNC-10
**Success Criteria** (what must be TRUE):
  1. With `sync.directpush-gitbody` on, a DirectPush'd row's body is served exclusively from the git-shipped `/app` tree — the `/data` SFTP-first overlay is no longer part of the serving path.
  2. DirectPush re-exports `index-seed.json` (like Publish already does), so a fresh prod reseed reconstructs the DirectPush'd row instead of reverting it.
  3. `is_visible` flips only after the body has been committed, deployed, and hash-verified at `/app` — a row is never visible before its body is reachable.
  4. `pushed_to_prod_utc` is stamped only after prod confirms the deployed body, so a live DirectPush'd row never shows a "Never published" badge.
**Plans**: TBD

### Phase 91: Reconcile + Seed Lifecycle
**Goal**: Prod-side drift is detectable and reconcilable, and rows removed from the seed actually leave prod — safely, gated behind a seed-ownership marker so a bad seed can't mass-delete live rows.
**Depends on**: Phase 90 (shares the seed contract DirectPush now maintains)
**Requirements**: SYNC-17, SYNC-11, SYNC-12
**Success Criteria** (what must be TRUE):
  1. Every `content_site_index` row carries a seed-management marker distinguishing seed-owned rows from prod-only rows.
  2. A reconcile dry-run enumerates published-orphans (visible row, no body), file-orphans (`.md`, no row), seed-drift (prod row absent from seed), and body-hash-mismatch discrepancies with deterministic IDs — re-running it produces zero duplicate entries and resolves discrepancies by absence.
  3. With `sync.reconcile` on, a seed reload hides or deletes seed-owned rows that are absent from the current seed set, logging the removal as intentional, instead of leaving them to accumulate as orphans.
  4. The destructive seed-delete apply path cannot run unless a dry-run has already validated the same discrepancy set.
**Plans**: TBD

### Phase 92: Pull Hardening
**Goal**: Pull-from-Prod adopts prod's state field-by-field without ever clobbering operator-owned data or acting on a stale local checkout.
**Depends on**: Phase 91 (reuses the composite-key diffing + reconcile discrepancy vocabulary)
**Requirements**: SYNC-13, SYNC-14, SYNC-15
**Success Criteria** (what must be TRUE):
  1. On Pull, body and content are sourced from the git tree while `is_visible`/`is_hidden`/`approval_status` are sourced from prod and preserved — neither side clobbers the other's authoritative fields.
  2. Pull warns or refuses to proceed when the local checkout is behind (a `git pull` staleness guard), rather than silently reading a stale git tree.
  3. Any body-vs-index divergence discovered during Pull is surfaced to the operator for a decision — it is never silently adopted.
**Plans**: TBD

### Phase 93: Round-Trip Integration Test
**Goal**: The entire sync loop — distill through reconcile — is locked by one automated end-to-end test so future changes can't silently reintroduce any of the fixed classes of drift.
**Depends on**: Phase 92 (exercises every prior phase's fix)
**Requirements**: SYNC-16
**Success Criteria** (what must be TRUE):
  1. An integration test spanning distill → Publish/DirectPush → prod store → web body resolution → deploy/reseed → PullFromProd → reconcile runs against containerized Postgres + a real git tree.
  2. The test asserts served body == published body and `body_sha256` matches at every hop in the chain.
  3. The test asserts a published/DirectPush'd row is not reverted after a reseed (no-revert-after-reseed).
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 88 → 89 → 90 → 91 → 92 → 93

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 82. Refactor-Review Sweep & UI Baseline Audit | 2026.07.2 | 3/3 | Complete | 2026-07-04 |
| 83. Packet-Service SRP Split | 2026.07.2 | 7/7 | Complete | 2026-07-04 |
| 84. Theme Semantic-Token Migration | 2026.07.2 | 2/2 | Complete | 2026-07-05 |
| 85. `chatgpt-*` Naming Cleanup | 2026.07.2 | 5/5 | Complete | 2026-07-05 |
| 86. UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout | 2026.07.2 | 5/5 | Complete | 2026-07-05 |
| 87. Creator-Source Model Hardening | 2026.07.2 | 1/1 | Complete | 2026-07-05 |
| 88. Index-Row Integrity Hotfix | Cycle 16 | 3/3 | Complete | 2026-07-06 |
| 89. Content-Hash Foundation | Cycle 16 | 1/6 | In Progress | - |
| 90. DirectPush Correctness + Seed Sync | Cycle 16 | 0/TBD | Not started | - |
| 91. Reconcile + Seed Lifecycle | Cycle 16 | 0/TBD | Not started | - |
| 92. Pull Hardening | Cycle 16 | 0/TBD | Not started | - |
| 93. Round-Trip Integration Test | Cycle 16 | 0/TBD | Not started | - |

---

## Carry-forward backlog (not in Cycle 16)

- Scheduled/bulk harvest (AUTO-03/04)
- SEO/growth lane (SEO-01..05)
- Matchup / meta-threat read (deferred — deepens cedh-meta-gap, a separate lane)
- **ADMIN-01** — `/Admin/Flags` sortable by on/off (enabled) state (descoped from Cycle 15, user decision 2026-07-05; view-only, no flag semantics change)
- Manabase engine refactor (CastabilitySimulator / ManabaseAnalyzer / ManabaseClassifier SRP split) — deferred out of Cycle 15: behavior-critical Monte-Carlo + Karsten scoring, no byte-identical gate, just heavily worked in Cycles 12/14. Needs a numeric-parity harness built FIRST. Candidate for a dedicated future refactor cycle.
- **KB "commander advice" content class for filtered videos** — the distill classifier filters out videos that lack actionable deckbuilding decisions (slot/cut/synergy on a real list), discarding them entirely. But many are still valuable *general commander advice*: meta/format philosophy, budget-building mindset, card evaluations. Give these a distinct KB content type/home instead of dropping them, so they can be surfaced (and pasted into ChatGPT) as advice rather than deckbuilding lessons. Needs: a second classifier verdict ("advice" vs "filtered"), its own artifact shape/prompt, and a browse surface. Observed 2026-07-04 re-distill filtered 3 such videos: `D5XXv7BzmZw` (The Midrange-ification of Commander — format meta essay), `GGoQxBP3DcE` (budget-deck pep talk / "Rock Lee of Commander"), `s_B1wCIWGR0` (Top 10 Lands for EDH — card eval + pricing).
- **SYNC-F1** — Retire DirectPush entirely (fold into Publish) — this cycle makes the two paths consistent; retirement is a later-cycle decision.
- **SYNC-F2** — Scheduled/automatic reconcile runs (this cycle ships operator-triggered reconcile only).
