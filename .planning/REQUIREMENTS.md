# Requirements: DeckFlow — Cycle 16 (Content-KB Prod↔Git↔Studio Sync Hardening)

**Defined:** 2026-07-06
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything. This cycle protects the Content-KB half of that promise: the knowledge the site serves must be exactly what the operator published — no drift, no corruption, no ghost rows.

**Sources:** `docs/research/kb-prod-sync-roadmap.md` + `docs/research/kb-prod-sync-fix-design.md` (3-way audit + Fable5 web research + Codex gpt-5.4-high cross-check), 2026-07-05 live prod drift audit. Codex plan-review adjustments folded in: SYNC-04 narrowed to approved-write, SYNC-12 split with SYNC-17 marker prereq, P3 split into architecture flip + ordering/stamping.

**Design stance:** Git = single source of truth for bodies; the prod DB index row is subordinate and reconstructable from git. All sync = idempotent one-way keyed upsert. Every prod state change reflected in the git seed. Body content-hashed end-to-end. No CDC/queues — upsert + hash + expand-contract ordering fits 512MB/Render.

## Cycle 16 Requirements

### Integrity Hotfix (live prod bugs — ships first)

- [ ] **SYNC-04**: DirectPush writes `approval_status='approved'` on its insert/update path (it only reads approved local rows) — a prod row can never be publicly visible while `pending` (C1)
- [ ] **SYNC-05**: Sync diffing keyed by the `(natural_key_type, natural_key_value)` composite, replacing `PinId`, matching DirectPush — YouTube/podcast key collisions can't cross-match rows (C4-collision)
- [ ] **SYNC-06**: DirectPush diff read runs no unexpected DDL against prod; the false "no DDL against prod" comment corrected and the path explicitly guarded (C4-comment)

### Content-Hash Foundation

- [x] **SYNC-01**: `content_site_index` gains `body_sha256` (dialect-guarded DDL, SQLite + Postgres + seed JSON), computed from the `.md` body at publish (M5)
- [x] **SYNC-02**: ONE unified body-inclusive row signature replaces the two divergent schemes (`ContentSiteIndexContentSignature` + `ContentSyncDiffClassifier` fingerprint); DirectPush, Pull, and reconcile all share it
- [x] **SYNC-03**: Web app detects and logs (structured warning) when a row's on-disk body hash ≠ stored `body_sha256` — mojibake/stale-body corruption becomes visible instead of silently served. Fail-open + log this phase per D-05; the fail-closed refuse-to-render tightening is deferred to a future phase once the D-08 backfill guarantees every live row is hashed.

### DirectPush Correctness + Seed Sync (flag `sync.directpush-gitbody`)

- [x] **SYNC-07**: Bodies reach prod only via git `/app` — the `/data`-SFTP-first overlay is dropped (architecture flip; kills M1 unreachable-body + M3 `/app`-shadows-`/data`)
- [x] **SYNC-08**: DirectPush re-exports `index-seed.json` (like Publish) so git fully reconstructs prod and a redeploy cannot revert DirectPush'd rows (M2, C3)
- [x] **SYNC-09**: Hash-gated expand-contract ordering — body committed + deployed + hash-verified at `/app` before `is_visible` flips (M3)
- [x] **SYNC-10**: `pushed_to_prod_utc` stamped only after prod confirms the deployed body, not at local commit time (M6a; fixes "Never published" badge on live rows)

### Reconcile + Seed Lifecycle (flag `sync.reconcile`)

- [ ] **SYNC-17**: Row-level seed-management marker distinguishing seed-owned rows from prod-only rows (Codex HIGH — hard prereq before any seed-driven delete can ship)
- [ ] **SYNC-11**: NEW prod↔git↔seed reconciler + persistent discrepancy store (not an `ContentKbOrphanScanner` extension — it lacks prod access/git enumeration/seed awareness): emits published-orphans (visible row, no body), file-orphans (`.md`, no row), seed-drift (prod row absent from seed), body-hash-mismatch (uses SYNC-01); deterministic discrepancy IDs, idempotent re-run (zero dupes), resolution-by-absence, scope-tagged partial runs; dry-run mode ships first
- [ ] **SYNC-12**: Seed reload handles removals — rows absent from the seed-managed set (per SYNC-17 marker) are hidden/deleted intentionally + logged, replacing additive-only upsert (C2); destructive apply gated behind dry-run validation

### Pull Hardening

- [ ] **SYNC-13**: Pull-from-Prod per-field master — body+content ← git tree; DB-only operator fields (`is_visible`/`is_hidden`/`approval_status`) ← prod, preserved not clobbered (M7)
- [ ] **SYNC-14**: Pull warns/refuses when the local checkout is behind (`git pull` first staleness guard); never SFTP-downloads prod bodies (Codex rebuild rejected — prod `/data` empty by design, `0dd49f19`)
- [ ] **SYNC-15**: Body-vs-index divergence surfaced to the operator instead of silently adopted

### Round-Trip Proof

- [ ] **SYNC-16**: End-to-end integration test spanning distill → Publish/DirectPush → prod store → web body resolution → deploy/reseed → PullFromProd → reconcile, on containerized Postgres + a real git tree; asserts served body == published body, `body_sha256` matches end-to-end, and no-revert-after-reseed (M8)

## Future Requirements

Deferred — tracked but not in this roadmap.

### Sync follow-ons

- **SYNC-F1**: Retire DirectPush entirely (fold into Publish) — this cycle makes the two paths consistent; retirement is a later-cycle decision
- **SYNC-F2**: Scheduled/automatic reconcile runs (this cycle ships operator-triggered reconcile only)

### Carry-forward backlog (unchanged from prior cycles)

- Scheduled/bulk harvest (AUTO-03/04); SEO/growth lane (SEO-01..05); matchup/meta-threat read (cedh-meta-gap lane); ADMIN-01 `/Admin/Flags` on/off sorting; manabase-engine refactor (needs numeric-parity harness); KB "commander advice" content class

## Out of Scope

| Feature | Reason |
|---------|--------|
| Cycle 17 creator-style features (P87-93) | Ships after this cycle — creator-style builds KB-derived features on a KB that currently drifts |
| CDC / queue-based sync (Kafka, outbox) | Overkill for a single-operator 512MB Render deployment; upsert + hash + ordering suffices |
| SFTP-downloading prod bodies in Pull | Adjudicated against (Codex rebuild rejected) — prod `/data` is empty by design; git tree is the body source |
| Public-app feature changes | Studio/ops cycle; only public surface changes are the SYNC-03 render guard and the SYNC-04 visibility fix |
| Prod-DB schema rework beyond `body_sha256` + seed marker | Minimal additive DDL only; both dialect-guarded and idempotent per house pattern |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| SYNC-04 | 88 | Pending |
| SYNC-05 | 88 | Pending |
| SYNC-06 | 88 | Pending |
| SYNC-01 | 89 | Complete |
| SYNC-02 | 89 | Complete |
| SYNC-03 | 89 | Complete |
| SYNC-07 | 90 | Complete |
| SYNC-08 | 90 | Complete |
| SYNC-09 | 90 | Complete |
| SYNC-10 | 90 | Complete |
| SYNC-17 | 91 | Pending |
| SYNC-11 | 91 | Pending |
| SYNC-12 | 91 | Pending |
| SYNC-13 | 92 | Pending |
| SYNC-14 | 92 | Pending |
| SYNC-15 | 92 | Pending |
| SYNC-16 | 93 | Pending |

**Coverage:**
- Cycle 16 requirements: 17 total
- Mapped to phases: 17
- Unmapped: 0 ✓

---
*Requirements defined: 2026-07-06*
*Last updated: 2026-07-06 after roadmap creation (Phases 88-93, 100% coverage)*
