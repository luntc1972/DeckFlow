---
phase: 30-content-kb-integration
plan: 04
status: complete
requirements: [KBI-04, KBI-05, KBI-06]
one_liner: "What Experts Say panel + admin live score preview shipped; 3 UAT fix rounds (short-name matching, typeahead, filter/sort UX); human visual verify PASSED 2026-06-07 on prod."
key_files:
  created:
    - DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml
  modified:
    - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
    - DeckFlow.Web/Controllers/DeckController.cs
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/Models/AdminContentKbViewModel.cs
    - DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs
    - DeckFlow.Web/Views/AdminContentKb/Index.cshtml
    - DeckFlow.Web/Services/ContentKbRelevanceService.cs
    - DeckFlow.Web/wwwroot/css/admin-common.css
    - DeckFlow.Web/wwwroot/css/admin-mobile.css
    - DeckFlow.Web/wwwroot/ts/content-kb-admin.ts
---

# 30-04 Summary — Panel + Admin Preview + UAT Hardening

## What happened

- **Task 1 (f8f3a3f):** `_ContentKbPanel.cshtml` — collapsed `<details>` "What Experts Say" panel, clips grouped by source channel, block-quoted excerpts, prebuilt timestamp deep-links rendered directly (`target="_blank" rel="noopener noreferrer"`), harvest dates. Hidden entirely when null/empty. Placed inside the Step-3 Analysis Summary stack after the Overview section. Fresh-analysis success path maps `ExpertContextClips`; CSS in site-common.css only.
- **Task 2 (57d0eab):** Admin live score preview — GET form (commander + bracket), `ScoreAllAsync` through the production scoring path, "Score (artifact)" column (clips inherit artifact score per D-01, labeled honestly), bracket allowlist validation + commander normalization (T-30-11).
- **Task 3 (checkpoint): PASSED 2026-06-07** — user verified on prod (deploy `8cfd258` line): panel rendering, no-match hide, admin preview, mobile/Kindle layout. User screenshot of preview-active admin table captured during UAT.

## UAT fix rounds (user feedback during checkpoint)

1. **Round 1 (306a17b):** (a) Commander short-name matching — full names ("Kinnan, Bonder Prodigy") derived archetypes but missed the free-text dimension because artifacts say "Kinnan"; `NormalizeCommander` now adds the pre-comma token (>=4 chars) as a match candidate. Root cause of the all-0.00 scores the user hit. (b) Commander typeahead on the preview input (df-typeahead + `/commander-categories/search`; _AdminLayout doesn't load site scripts, so the page loads df-typeahead.js itself + admin-scoped panel CSS). (c) Mobile kb-tag pill wrapping in the card layout.
2. **Round 2 (8cfd258):** Preview panel moved directly above the Entries table; server-side All/Published/Hidden visibility filter; score-descending sort via column header (preview-active only, nulls last); all query params compose.

## Operational decisions made during UAT

- **Gameplay videos excluded from KB (user decision):** Playing With Power source disabled (`content-source-set-enabled --id 5 --enabled false` on local harvest DB `artifacts/uat-content-kb.db`) — gameplay VODs are not citable expert advice. Existing 2 PWP prod rows to be unpublished by operator.
- **Evergreen flag chosen** for general-advice videos (always-eligible filler) — implemented in Phase 32, not here.
- **Phase 32 spawned:** Expert Context Selection (pins/follows/evergreen) — spec `.planning/specs/2026-06-07-expert-context-selection-design.md`, added to v1.5 roadmap (3a7a5e7), runs before Phase 31.

## Verification

- Windows builds 0/0 throughout; full Web suite at close: **592 pass / 0 fail / 5 PG-skips** (597 total).
- Human-verify checkpoint PASSED (prod, 2026-06-07): panel + empty-state + admin preview + mobile.
- Acceptance greps green across both tasks (panel guard, prebuilt href, kb-expert in site-common only, ScoreAllAsync + bracket validation).

## Commits

- f8f3a3f feat(30-04): add What Experts Say panel to deck analysis results
- 57d0eab feat(30-04): add live relevance-score preview to admin KB view
- 306a17b fix(30-04): UAT round 1 — short-name commander match, admin typeahead, mobile tags
- 8cfd258 fix(30-04): UAT round 2 — admin preview placement, visibility filter, score sort
