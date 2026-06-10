# Requirements: DeckFlow v1.5

**Defined:** 2026-06-03
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT, Claude, or Gemini and get back a useful answer in one round-trip — without the user reformatting anything.

## v1.5 Requirements

Requirements for this milestone. Each maps to roadmap phases.

### Deck Primer Generator

- [x] **PRM-01**: Combo-data spike decision recorded before prompt-builder build — Spellbook `Instructions` richness verdict (sufficient / needs enrichment / fallback) + representative cEDH primer prompt byte-size measured against paste caps
- [x] **PRM-02**: User can open a Deck Primer page (fourth workflow tab, peer of DeckAnalysis/DeckComparison/CedhMetaGap) and load a decklist via the existing import flow (URL or paste)
- [x] **PRM-03**: User can select bracket (1–5); bracket choice pre-applies a section preset (cEDH or Casual/Upgraded) and gates bracket-scoped sections (cEDH-only #24/#25 vs casual-only #26)
- [x] **PRM-04**: User can toggle individual sections from the 31-section catalog rendered as 5 collapsible groups (Identity / Combos / Gameplay / Matchups / Maintenance)
- [x] **PRM-05**: Generated prompt injects Commander Spellbook combos as ground truth, structurally separated from a fenced speculative-synergies ask, with explicit disclosure when Spellbook is unavailable (null return)
- [x] **PRM-06**: Matchup sections route on bracket — EdhTop16 named archetypes for bracket 5 WHEN the EdhTop16 GraphQL schema exposes a meta-wide archetype query (verified by the PRM-01 spike); otherwise bracket 5 falls back to the five generic strategy buckets (accepted degradation). Brackets 1–4 always use the five generic strategy buckets (Aggro/Control/Midrange/Combo/Stax-Hate)
- [x] **PRM-07**: Prompt grounds identity/engine/mulligan sections with category-knowledge distribution numbers (ramp/draw/interaction/tutor counts)
- [x] **PRM-08**: Combo lines ranked by priority (piece count, assembly cost, immediacy) when spike confirms data sufficiency; AI-ranked fallback otherwise
- [x] **PRM-09**: User can generate per-AI artifact variants (ChatGPT/Claude/Gemini) stored via PacketArtifactStore with zip round-trip — primer entries added to the zip allowlist with a round-trip regression test
- [x] **PRM-10**: Section selection persists per bracket preset in localStorage across visits
- [x] **PRM-11**: Collapsed group headers show selected-count badges ("3/7 sections selected")
- [x] **PRM-12**: Each section exposes help text explaining what good AI output for that section looks like

### Content KB Integration

- [x] **KBI-01**: `content.kb.enabled` flag flipped ON in prod with published KB content verified live (prerequisite step inside this milestone, not post-ship)
- [x] **KBI-02**: Deck-analysis prompt artifact includes an Expert Context block of top-K relevant curated clips — tag-based relevance (commander name, archetype, bracket filter, card-category tiebreak), `is_kept = true` only, ≤ ~150 words/clip, K=5 default
- [x] **KBI-03**: Injected clips formatted as block-quote pull-quotes with source attribution so the AI treats them as authoritative context
- [x] **KBI-04**: "What Experts Say" panel on the DeckAnalysis result page shows injected clips with attribution, timestamp deep-link, and harvest/publication date — collapsed by default, grouped by source channel (diversity indicator)
- [x] **KBI-05**: Graceful empty state — prompt omits the Expert Context block and the panel shows a friendly empty message when no relevant clips match
- [x] **KBI-06**: Admin sources view shows per-clip relevance match score (operator-only) to support `is_kept` curation tuning

### Expert Context Selection

- [x] **SEL-01**: Pin a video / follow a creator from the browse page and the analysis-form chip area (typeahead-backed), hidden-field bound into the analysis form
- [x] **SEL-02**: Layered tier-fill of the Expert Context set — pinned → followed → auto-relevance → evergreen, within K=5 + char budget; pins trimmed last and survive a parse-failed artifact (fixed 2026-06-09, a106c6a)
- [x] **SEL-03**: One-shot video pins, sticky creator follows, persisted in localStorage across visits
- [x] **SEL-04**: Selection persisted in the packet zip (`expert-selection.json`); re-upload restores it (allowlist-gated, corrupt-JSON degrades to empty)
- [x] **SEL-05**: Artifact-level Evergreen admin flag (`is_evergreen`, self-healing SQLite+Postgres migration), max 1 evergreen clip in the merged set
- [x] **SEL-06**: Panel origin markers per clip — pinned / followed / auto / evergreen — surfaced via `ClipOrigin`

### Admin Content KB Curation UX

- [x] **KBUX-01**: Admin can filter/search the Content KB entries list (`AdminContentKb.Index`) by tags, title/name, and creator/source to quickly locate an entry to publish/unpublish in a list that has grown long
- [x] **KBUX-02**: Content KB entries list readability improvements (zebra rows, sticky header on page scroll, hover/focus row highlight, clean tag wrapping, mobile-safe) so a long list scans comfortably

### Housekeeping

- [x] **HSK-01**: DeckFlow.Core XML-doc backfill (186 sites) complete and doc-warning gate widened to `[DeckFlow.Core/**.cs]` in the final commit — build clean, 0 new warnings
- [x] **HSK-02**: KB-12 codex distill backend — `distill` CLI verb works end-to-end with codex backend (`CliEnvelopeKind.Raw`), replacing the `NotSupportedException` stub — RE-DEMOTED to backlog 2026-06-04 per D-03 (Phase 28-03 discovery: no provable read-isolation boundary in codex 0.136.0; see `28-DISCOVERY.md`)
- [x] **HSK-03**: VERIFICATION.md hygiene — 7 missing v1.4 phase VERIFICATION files back-filled and stale UAT labels corrected
- [x] **HSK-04**: v1.4 artifact hygiene — P26 missing SUMMARYs, P24 quick-fix artifact chain, and dual artifact-tree drift items from the milestone audit resolved

## Future Requirements (v1.6+)

Deferred. Tracked but not in current roadmap.

### Deck Primer Generator

- **PRM-F01**: Minimal preset (4-section quick primer)
- **PRM-F02**: Smart section auto-detection from deck analysis

### Content KB Integration

- **KBI-F01**: Expert panel on DeckComparison, CedhMetaGap, and DeckPrimer result pages (DeckAnalysis-only in v1.5)
- **KBI-F02**: Expert Context injection into all 5 prompt builders (analysis-only in v1.5)
- **KBI-F03**: Embedding-based semantic clip retrieval (when corpus > ~1000 clips)
- **KBI-F04**: Scheduled (cron) KB harvest cadence

### Multi-AI

- **GEM-F01**: Gemini paste-limit unblock (split-message prompt or direct API) — deferred again from v1.5; stays flag-gated via `DECKFLOW_GEMINI_ENABLED`

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Gemini paste-limit unblock | User-deferred to v1.6 at v1.5 scoping (2026-06-03); third consecutive deferral — stays flag-gated |
| Automatic primer publishing to Moxfield | No public write API; scraping violates ToS; Core Value is paste-ready output |
| EDHREC integration for matchup data | No API access; design note explicitly excludes; EdhTop16 + generic buckets suffice |
| Per-card narrative for all 99 cards | Exceeds AI context window; real primers cover 15–20 key cards — prompt instructs accordingly |
| Real-time primer preview / streaming AI responses | DeckFlow is a prompt-artifact tool, not an analysis service; would require SSE + API key management |
| Primer version history | PacketArtifactStore regeneration suffices; history adds schema + admin UI complexity |
| Primer quality scoring / feedback loop | No user account model; cannot correlate feedback |
| Embedding-based clip retrieval | No vector DB infra; tag matching sufficient at current corpus size |
| Real-time KB query on page load | Clip selection happens at prompt-build time, cached with artifact |
| Public user clip add/edit | Single-operator admin model; moderation surface not warranted |
| Per-user channel weighting | No user accounts |
| Clip translation (non-English) | KB scope English-first per v1.4 decision |
| "Inject all clips" mode | Prompt bloat; top-K bounded selection only |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| PRM-01 | Phase 31 | Complete |
| PRM-02 | Phase 31 | Complete |
| PRM-03 | Phase 31 | Complete |
| PRM-04 | Phase 31 | Complete |
| PRM-05 | Phase 31 | Complete |
| PRM-06 | Phase 31 | Complete |
| PRM-07 | Phase 31 | Complete |
| PRM-08 | Phase 31 | Complete |
| PRM-09 | Phase 31 | Complete |
| PRM-10 | Phase 31 | Complete |
| PRM-11 | Phase 31 | Complete |
| PRM-12 | Phase 31 | Complete |
| KBI-01 | Phase 30 | Complete |
| KBI-02 | Phase 30 | Complete |
| KBI-03 | Phase 30 | Complete |
| KBI-04 | Phase 30 | Complete |
| KBI-05 | Phase 30 | Complete |
| KBI-06 | Phase 30 | Complete |
| KBUX-01 | Phase 33 | Complete |
| KBUX-02 | Phase 33 | Complete |
| SEL-01 | Phase 32 | Complete |
| SEL-02 | Phase 32 | Complete |
| SEL-03 | Phase 32 | Complete |
| SEL-04 | Phase 32 | Complete |
| SEL-05 | Phase 32 | Complete |
| SEL-06 | Phase 32 | Complete |
| HSK-01 | Phase 29 | Complete |
| HSK-02 | Phase 28 | Re-demoted to backlog (D-03, 2026-06-04) |
| HSK-03 | Phase 28 | Complete |
| HSK-04 | Phase 28 | Complete |

**Coverage:**
- v1.5 requirements: 30 total
- Mapped to phases: 30 (100%)
- Unmapped: 0 ✓

---
*Requirements defined: 2026-06-03*
*Last updated: 2026-06-09 — added SEL-01..06 (Phase 32 Expert Context Selection, previously orphaned); flipped PRM/KBI/KBUX/SEL to Complete after v1.5 milestone-audit backfill; coverage 24 → 30*
