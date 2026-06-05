---
phase: 30-content-kb-integration
plan: 01
status: complete
requirements: [KBI-01]
one_liner: "Content KB live in prod (flag ON, clips visible) after Salubrious Snail top-10 harvest; prod tag-distribution audit captured for plan 02 calibration."
key_files:
  created:
    - .planning/phases/30-content-kb-integration/30-TAG-AUDIT.md
    - content-kb/salubrious-snail/ (10 artifacts)
  modified:
    - content-kb/seed/index-seed.json (10 -> 20 rows)
---

# 30-01 Summary — Flip-first + Tag Audit

## What happened

- **Task 1 (harvest, D-11 — deviated per user direction):** Instead of an incremental top-up of the existing 5 channels, the user directed harvesting the **top 10 most-watched public videos of @salubrioussnail** (new channel, source id 6, from the prepared `artifacts/salubrious-snail-videos.txt` list; members-only videos excluded — no public captions). Harvest 10/10 manual captions, distill 10/10 via claude CLI backend (30 LLM calls, $0.00, 0 failures). Seed regenerated 10 → 20 rows. Commit **665c236** (plain default-author), pushed on `v1.5`. User deployed the `v1.5` branch to Render directly.
- **Task 2 (curation + flag flip, D-09):** Operator curated rows visible in `/Admin/ContentKb`, flipped `content.kb.enabled` ON via live `/Admin/Flags`, confirmed the public `/content-kb` browse page renders clips. User confirmed "all passes" — **SC1 / KBI-01 TRUE**.
- **Task 3 (tag audit):** Run by orchestrator via Render MCP read-only SQL (accepted per T-30-03) instead of operator-manual. Results in `30-TAG-AUDIT.md`.

## Key numbers for downstream plans

- **Corpus size: 2 visible rows / 20 total** — far below the ~50 threshold; plan 02 uses bare per-request artifact reads, NO IMemoryCache path needed.
- **Empty bracket: 45% of all rows (50% of visible)** — bracket-match must be a **score bonus, not a hard gate**.
- **Schema correction for plan 02:** prod columns are `bracket_tags` / `archetype_tags` / `card_category_tags` (JSON-array text), not RESEARCH.md's `tags_bracket` / `tags_archetype`.
- Ubiquitous archetype tags (value-engine 12/20, ramp 11/20) are weakly discriminating; rare-tag matches should outweigh them.
- Tiny visible corpus means zero-match is a common, normal case for relevance scoring.

## Deviations

1. Harvest scope changed from "top-up existing 5 channels" to "new channel @salubrioussnail top-10 most-watched" — explicit user direction at the Task 1 checkpoint.
2. Deploy went out from branch `v1.5` (user choice), not the push-to-main v1.4 model.
3. Task 3 audit executed by orchestrator (Render MCP, read-only) rather than operator-manual.

## Verification

- SC1 operator-confirmed live (flag ON + clips visible on public browse page).
- `30-TAG-AUDIT.md` contains all required sections with real numeric counts.
- App started clean post-deploy (operator-confirmed; prod serving and KB pages render).
