---
quick_id: 260712-l1z
slug: fix-deck-analysis-mobile-overflow-prompt
status: complete
completed: 2026-07-12
branch: quick/deck-analysis-ux-fixes
commits: [aac3b4dd, 98ed77a1]
---

# Summary: Deck-analysis + sibling pages UX fixes

Source: UX research 2026-07-12 (`.planning/ui-design/deck-analysis-ux-research.md` in main tree)
plus mid-task visual audit of /deck-primer and /deck-comparison (user-requested scope extension).

## Commit 1 — `aac3b4dd` fix(deck-analysis)

- HIGH-1: Deck Summary `<pre>` mobile overflow (479px doc on 390px viewport) → scoped
  `.deck-summary-pre` wrap rule in site-common.css. Live-verified 390/390.
- MED-2: Expand/Collapse toggles on analysis.txt / reference.txt / set-upgrade textareas
  (240→~60vh); generic `data-expand-target` handler added to deck-sync.ts.
- MED-3: "Analysis context" details default-open.
- New e2e guard `deck-analysis-mobile.spec.ts` (overflow + context-open assertions).

## Commit 2 — `98ed77a1` fix(deck-primer,deck-comparison)

Visual audit confirmed same bugs on siblings:
- DeckPrimer:329 — LIVE overflow (425/390) → `.deck-summary-pre`. Verified 390/390.
- DeckComparison:392 — latent (pre bled 35px past panel) → same fix.
- Expand toggles: primer-output (20.9KB), comparison prompt/context/follow-up outputs.
- DeckPrimer step-3 tab hardcoded `false` → reflects PrimerPromptText (cosmetic
  first-incomplete auto-highlight shift accepted; primer tabs are scroll-jump nav).
- New e2e guard `sibling-pages-mobile.spec.ts` (both pages, live generation, 390x844).

## Verification

- Codex gpt-5.4 implemented; Claude reviewed diffs; blind verifier PASS (commit 1) +
  PASS_WITH_NOTES (commit 2). EOL clean (LF, 0 CR). site.css untouched; no global pre restyle.
- Build 0 warn/0 err. Web.Tests 1349 pass. New specs green ×3 independent runs (real
  generation, not skip). Adjacent deck-analysis specs 15 pass.
- Visual: deck-analysis + primer screenshotted post-fix at 390px; comparison covered by
  spec assertion (same class/rule as the two visually verified pages).
- Tests skipped for xUnit: view/CSS/TS-only change, no C# logic — e2e covers behavior.

## Follow-ups (not done, noted)

- LOW: lede copy promises Gemini regardless of DECKFLOW_GEMINI_ENABLED
  (DeckPrimer.cshtml:42, DeckComparison.cshtml:147).
- LOW: primer-chat-title input clips on mobile (Copy mitigates).
- LOW: comparison mobile micro-bleeds (.prompt-sticky-download +12px, step-2 panel +9px).
- MED (deck-analysis research): "jump to results" shortcut on long result states — unaddressed.
