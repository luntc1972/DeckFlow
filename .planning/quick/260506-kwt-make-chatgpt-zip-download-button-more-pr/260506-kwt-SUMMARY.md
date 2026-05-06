---
quick_id: 260506-kwt
slug: make-chatgpt-zip-download-button-more-pr
status: complete
date: 2026-05-06
---

# Quick Task 260506-kwt — SUMMARY

## What shipped

Sticky "Download session (.zip)" bar at the top of all three ChatGPT workflow pages (`/chatgpt-packets`, `/chatgpt-deck-comparison`, `/chatgpt-cedh-meta-gap`). Bar stays visible while scrolling so the user can save current form state at any step without hunting for the result-panel button. Existing per-step inline Download buttons remain as secondary confirmations.

## Files changed

| File | Lines | Change |
|------|-------|--------|
| `DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml` | +8 | Sticky bar markup as first child of `<form>` |
| `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml` | +8 | Sticky bar markup as first child of `<form>` |
| `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml` | +8 | Sticky bar markup as first child of `<form>` |
| `DeckFlow.Web/wwwroot/css/site-common.css` | +31 | `.chatgpt-sticky-download` layout rules (no per-theme touches) |
| `DeckFlow.Web/Help/chatgpt-analysis.md` | ±1 | "Artifact saving" doc line: mention sticky bar location |
| `DeckFlow.Web/Help/chatgpt-deck-comparison.md` | ±1 | Same |
| `DeckFlow.Web/Help/cedh-meta-gap.md` | ±1 | Same |

Total: 7 files, ~58 inserted lines net, 0 deletions of behavior.

## Implementation notes

- Bar uses BEM classes `.chatgpt-sticky-download`, `.chatgpt-sticky-download__label`, `.chatgpt-sticky-download__button`.
- Button wears existing `.run-button` class so theme accent flows through unchanged across all 28 guild themes — no per-theme CSS edits.
- Layout CSS lives in `site-common.css` (project rule: layout never goes in per-theme files; tokens only in theme `:root`).
- No new JavaScript: pure HTML form with `formaction` override pointing at the existing `/chatgpt-*/download` endpoints (shipped in 260506-hgd, commit `5f5764f`).
- Sticky positioning uses `position: sticky; top: 0; z-index: 10` so the bar pins to viewport top when the user scrolls past the workflow step tabs.
- Flex layout with `flex-wrap: wrap` so the bar collapses gracefully on mobile breakpoint.

## Verification

- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` clean: 0 warnings, 0 errors.
- Smoke test (dev server on `localhost:5173`):
  - All three pages return HTTP 200.
  - Each page renders 3 BEM-class matches (one bar with label + button).
  - `formaction` attribute matches page-specific endpoint on all three.
  - Real form submission with antiforgery token round-trips through `ChatGptPacketsDownload` controller cleanly (returns view with ErrorMessage on synthetic deck URL — expected, validation gate intact).
- QA double-pass enforced via Codex prompt; both passes clean.

## Codex routing

Implementation routed through Codex MCP (`mcp__codex__codex`) with model `gpt-5.4` full per project model-selection heuristic (multi-file Razor + theme-aware CSS).

Doc-only updates to `Help/*.md` were applied directly via Edit (per CLAUDE.md, "All coding tasks MUST route through Codex" — markdown docs are not coding).

## Risk surface

- **Z-index conflict:** Bar sits at `z-index: 10`. If any future modal/drawer needs to overlay the bar, that component must use `z-index: 11+`.
- **Mobile copy length:** The label text is 60 chars; if narrowed below ~220px, flex-wrap will stack label above button. No overflow risk.
- **Existing inline buttons:** Untouched. They post to the same endpoint, so behavior is identical between sticky and inline triggers.

## What's next

- Per-page render verification on production after Render auto-deploy from `main` push.
- Optional follow-up: consider promoting the `<details>` "Resume from a saved session (.zip)" disclosure into the sticky bar too, for symmetry. Out of scope for this task.
