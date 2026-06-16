# Requirements: DeckFlow v1.7 Local Harvest & Publish Studio

**Defined:** 2026-06-13
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT / Claude / Gemini and get a useful answer in one round-trip — without reformatting. (The Studio serves this by making the curated Content KB easy to grow and publish.)

## v1.7 Requirements

A standalone local Blazor Server tool that wraps the existing local-CLI Content KB pipeline with a UI to discover YouTube videos, harvest + distill them, review/approve the output, and publish approved entries to deckflow.gg via two paths — plus an independent admin commander-grid perf fix. Grounded in `.planning/research/SUMMARY.md` (HIGH confidence, code-verified). No new NuGet packages.

### Studio Foundation (STU)

- [ ] **STU-01**: A standalone local Blazor Server project exists in `DeckFlow.sln`, references `DeckFlow.Core`, and launches a browser UI via `dotnet run` (decoupled from the deployed site; not included in the Render container build).
- [ ] **STU-02**: Operator secrets (prod Postgres connection string, Render SSH target) are stored via `dotnet user-secrets` and never written to any git-tracked file; `.gitignore` excludes any local Studio data/config artifacts.
- [ ] **STU-03**: Secrets and connection strings never appear in logs or UI output (referenced by name only), honoring the public-repo no-secrets rule.

### Orchestration Extraction (ORCH)

- [ ] **ORCH-01**: The harvest / distill / seed-export orchestration is extracted from `DeckFlow.CLI` into `DeckFlow.Core` (`IContentKbOrchestrator` + implementation) so both the CLI and the Studio consume identical logic (closes the v1.6 `ContentKbCommandRunners` god-class backlog item).
- [ ] **ORCH-02**: `DeckFlow.CLI` command runners become thin adapters over the extracted orchestrator; existing CLI command behavior and tests are unchanged (route/behavior parity).

### Discovery, Harvest & Distill (HARV)

- [ ] **HARV-01**: Operator can paste a YouTube channel URL/handle/ID and see a list of that channel's recent videos to select from (via YoutubeExplode; no API key; lister concurrency stays serialized).
- [ ] **HARV-02**: Operator can paste individual YouTube video URLs/IDs to queue specific videos.
- [x] **HARV-03**: Selected videos display a harvested/distilled status badge and are de-duplicated against already-harvested entries (no accidental re-harvest).
- [ ] **HARV-04**: Operator can trigger harvest + distill on selected videos from the UI, with live progress, without blocking the UI/circuit.
- [x] **HARV-05**: Estimated + actual LLM distill spend is shown before and after a distill run, gated by the existing spend ledger/cap; already-distilled videos are not silently re-distilled (re-distill is explicit/opt-in).

### Review & Approve Queue (REVQ)

- [ ] **REVQ-01**: A new `approval_status` column (`pending` / `approved` / `rejected`) is added to the content site-index via the established self-healing ALTER migration pattern (Sqlite + Postgres), defaulting to `pending`.
- [ ] **REVQ-02**: Operator can review each distilled entry in a queue — preview the summary, timestamped clips, and tags — and approve, reject, or leave pending.
- [ ] **REVQ-03**: Queue supports batch approve/reject and filters by status; status transitions are visible (harvested → distilled → approved/rejected → published).

### Publish Paths (PUB)

- [ ] **PUB-01**: A content-only-columns upsert overload exists in `ContentSiteIndexStore` that updates display/navigation fields WITHOUT clobbering admin-set `is_visible` / `is_evergreen` / pin state (prerequisite for any direct write).
- [ ] **PUB-02**: Seed export is filtered to `approved` rows only, so `pending`/`rejected` content never ships to the public repo or prod.
- [x] **PUB-03**: Commit-then-deploy publish (primary): from the UI the operator can export the approved seed, see a diff of what will change, and stage + commit the seed + markdown artifacts (LF-normalized) for push → Render auto-deploy. (Push to `main` remains the operator's explicit action.)
- [ ] **PUB-04**: Direct prod-DB push (secondary): operator can write approved rows straight into the Render Postgres site-index via the safe content-only upsert and upload the matching markdown to Render `/data` via SCP — artifact-first ordering (SCP before DB) so no DB row references a missing artifact.
- [ ] **PUB-05**: Direct push requires a dry-run/preview + explicit confirmation showing exactly which rows and artifacts will be written and to which target (prod), and surfaces partial-failure state for reconcile.

### Admin Grid Performance (GRID)

- [ ] **GRID-01**: The `/Admin/Harvest` commander-deck grid loads pages on demand (AJAX partial endpoint, same-origin guarded, numbered pages) instead of computing the full grid on initial page load.
- [ ] **GRID-02**: The underlying slow query is fixed at the source — add the missing partial expression index on `LOWER(commander_name)` so the distinct-count + paged read no longer full-scan on every load.

### UI Review (UIR)

- [ ] **UIR-01**: An updated 6-pillar visual audit of the deployed deckflow.gg public site is produced and scored, with prioritized findings (re-scoring the v1.0 16/24 baseline that has been deferred every milestone since; Color + Typography were the lowest pillars).
- [ ] **UIR-02**: High and medium findings from UIR-01 are remediated to reach the ≥20/24 target bar; theme-system constraints honored (layout CSS in `site-common.css`, tokens in each theme `:root`).
- [ ] **UIR-03**: Remediation is visually verified with browser screenshots at ≥2 viewports (mobile + desktop) before close — grep-only verification is insufficient for CSS/layout changes.

## Future Requirements (deferred)

### Discovery

- **DISC-FUT-01**: YouTube Data API v3 keyword "find a creator by name" search (deferred — adds a secured API key + 10k-units/day quota risk; channel-URL browse + paste covers the v1.7 workflow).

### Studio

- **STU-FUT-01**: Multi-operator / auth on the Studio (deferred — single-operator local tool).
- **STU-FUT-02**: Scheduled/automated harvest cadence (deferred every milestone; Studio stays operator-triggered).

## Out of Scope

| Feature | Reason |
|---------|--------|
| Rebuilding the harvest/distill/transcription pipeline | Exists and works; Studio is a UI wrapper + orchestration extraction, not a rewrite |
| New LLM distill provider/backend | Pluggable backend already shipped (v1.4 KB-12 / 21.2); out of scope |
| YouTube Data API v3 (any use) | Deferred to Future; v1.7 uses YoutubeExplode only — no key, no quota |
| A Render file-write REST API | Does not exist; SCP-over-SSH is the only `/data` write mechanism (PUB-04) |
| Changing the Dockerfile to solution-level restore | Would pull Studio/Blazor into the container; Dockerfile restore stays project-scoped |
| Migrating the deployed site or HTTP/resilience stack | Pinned by PROJECT.md constraints |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| STU-01 | Phase 41 | Pending |
| STU-02 | Phase 41 | Pending |
| STU-03 | Phase 41 | Pending |
| ORCH-01 | Phase 42 | Pending |
| ORCH-02 | Phase 42 | Pending |
| REVQ-01 | Phase 43 | Pending |
| PUB-01 | Phase 43 | Pending |
| PUB-02 | Phase 43 | Pending |
| GRID-01 | Phase 44 | Pending |
| GRID-02 | Phase 44 | Pending |
| HARV-01 | Phase 45 | Pending |
| HARV-02 | Phase 45 | Pending |
| HARV-03 | Phase 45 | Complete |
| HARV-04 | Phase 45 | Pending |
| HARV-05 | Phase 45 | Complete |
| REVQ-02 | Phase 46 | Pending |
| REVQ-03 | Phase 46 | Pending |
| PUB-03 | Phase 46 | Complete |
| PUB-04 | Phase 47 | Pending |
| PUB-05 | Phase 47 | Pending |
| UIR-01 | Phase 48 | Pending |
| UIR-02 | Phase 48 | Pending |
| UIR-03 | Phase 48 | Pending |
