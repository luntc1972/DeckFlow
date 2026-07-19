---
quick_id: 260718-rgp
description: AI-framing copy sweep (deterministic-first) + manabase deck-not-pilot voice + AI methodology help page
status: complete
completed: 2026-07-19
---

# Quick Task 260718-rgp: Summary

## What changed

**Commit 8c5b1b93 — AI-framing sweep (approved audit, 16 items):**
- Identity descriptions rewritten deterministic-first: `_Layout` site default, Home meta + hero (option 1 — "one paste, one round-trip", no platform names), About, Help index.
- Five prompt-tool descriptions reordered value-first, keeping the ChatGPT keyword for search (Deck Analysis, Comparison, Primer, Judge Questions, Meta-Gap).
- "Crawled deck history" → "aggregated public deck data" (SuggestCategories, CommanderCategories); KB index description now advertises per-entry crediting.
- deck-primer tile copy in `ToolRegistry` (+ test sync).
- README intro + repository description reordered; GitHub repo About description updated live via `gh repo edit` (297 chars).
- New `DeckFlow.Web/Help/ai-methodology.md` ("How DeckFlow Uses AI") — auto-discovered by HelpContentService, verified rendering at /help/ai-methodology.

**Commit dc3d4847 — manabase voice sweep (user feedback: tool talks at the reader):**
- Analysis output describes the deck, never the pilot: "Reading the deck", "the deck has X vs Y needed", "The deck runs N ramp / M draw", "assumes mana is held open", "the commander's mana value", "the curve's 75th-percentile mana value (no single commander)".
- Applied consistently across `Manabase.cshtml`, `ManabaseDisplay` gloss, `ManabaseReportTextBuilder` (text export), `ManabaseVerdictSynthesizer`, `ManabaseSwapPromptBuilder` (AI prompt artifact).
- Form labels/instructions intentionally stay second-person; anchor id `manabase-reading-your-deck` unchanged for deep links/e2e.
- 9 test files re-synced to the new wording.

## Implementation notes

Codex (gpt-5.4 medium) implemented in three passes; Claude reviewed. Passes 2-3 were needed because the voice-sweep strings had production sources in Core files outside the first fence (`ManabaseVerdictSynthesizer`, `ManabaseSwapPromptBuilder`, `ManabaseDisplay`) — caught by full-suite runs.

## Verification

- Build 0 errors. Core tests 1598/0. Web tests 1583/0 (16 Postgres skips).
- e2e specs grep-verified free of the changed strings.
- EOL clean: full diff == ignore-whitespace diff (29 files, ±113 lines total).
- Hero copy screenshot-verified desktop + mobile; /help/ai-methodology returns 200 with content.
