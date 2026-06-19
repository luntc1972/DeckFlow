# Requirements (ARCHIVED): DeckFlow — Cycle 9 (Content Pipeline & Publish-Tracking)

**Defined:** 2026-06-18 · **Shipped:** 2026-06-19 (`2026.06.5`) · **Status:** COMPLETE — 12/12 requirements Met
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT, Claude, or Gemini and get back a useful answer in one round-trip — without the user reformatting anything.

> Archived at milestone close. All Cycle 9 requirements met across Phases 55-58 (each verified; Phase 58 dogfood validated the whole pipeline end-to-end and surfaced + fixed the DirectPush publish-visible gap).

## Cycle 9 Requirements — all Met

### Channel Browse + Per-Video Status (BROWSE)
- [x] **BROWSE-01**: Select a YouTube channel in Studio, see its video list. — Phase 56
- [x] **BROWSE-02**: Each video shows DeckFlow pipeline status (Not harvested / Harvested / Distilled / Approved / Published / Blocked) computed across the videos store, `content_site_index`, and `blocked_videos`. — Phase 56
- [x] **BROWSE-03**: Multi-select videos from the channel list and harvest the selected set. — Phase 56

### KB Removal & Block (REM)
- [x] **REM-01**: Block a video from the Studio UI — hard-deletes video + KB artifacts AND records it in the blocklist so future harvests skip it. — Phase 56
- [x] **REM-02**: See the blocked-videos list in Studio and Unblock any of them. — Phase 56

### Publish Tracking (PUB)
- [x] **PUB-01**: Content index records when each entry was pushed to production. **Adjusted in planning:** new distinctly-named `pushed_to_prod_utc` column (the existing `published_utc` holds the video's YouTube date and is part of the byte-stable seed contract); idempotent dialect-guarded migration (SQLite + Postgres); stamped by both publish paths. — Phase 55
- [x] **PUB-02**: Derives a single publish-state — Never published / Pushed-hidden / Published / Local-newer — via a shared `PublishStateDeriver`. — Phase 55
- [x] **PUB-03**: See each entry's derived publish-state in Studio's Review and Publish pages. — Phase 56

### Site Admin Visibility (SITE)
- [x] **SITE-01**: Same derived publish-state column on `/Admin/ContentKb`. — Phase 57

### Add Single Video (ADD)
- [x] **ADD-01**: Add a single video by URL/ID in Studio and harvest it. — Phase 56

### Distill Prompt Quality (DIST)
- [x] **DIST-01**: Reworked transcript→KB distill prompt produces measurably better paste-ready entries (current providers, no swap; JSON contract unchanged). Validated by Phase 58 dogfood (tag discipline 3 vs 12, cleaner clips). — Phase 57

### Dogfood / Validation (DOGFOOD)
- [x] **DOGFOOD-01**: Real in-cycle harvest + distill run exercised the improved prompt end-to-end through review → publish with publish-state surfacing correctly in Studio and `/Admin/ContentKb`, within spend caps. Surfaced + fixed the DirectPush "publishes visible" gap (`4cb333e`). — Phase 58

## Outcomes / Adjustments
- **PUB-01** column renamed `published_utc` → `pushed_to_prod_utc` to protect the seed contract (documented in planning).
- **DOGFOOD-01** found a real cross-surface gap (Studio stuck Pushed-hidden after DirectPush) — fixed in-cycle (DirectPush now publishes visible, prod-then-local) + Codex-reviewed + secured (T-58-09).

## Deferred (Future Requirements — not this cycle)
- **SEO-01..05** (Search Console/Bing, backlinks, analytics/monitoring, on-site SEO, Core Web Vitals)
- **AUTO-01..02** (reduce manual pipeline gates; add creator sources at scale)

## Traceability (final)

| Requirement | Phase | Status |
|-------------|-------|--------|
| PUB-01 | 55 | Met |
| PUB-02 | 55 | Met |
| BROWSE-01 | 56 | Met |
| BROWSE-02 | 56 | Met |
| BROWSE-03 | 56 | Met |
| REM-01 | 56 | Met |
| REM-02 | 56 | Met |
| ADD-01 | 56 | Met |
| PUB-03 | 56 | Met |
| SITE-01 | 57 | Met |
| DIST-01 | 57 | Met |
| DOGFOOD-01 | 58 | Met |
