---
quick_id: 260802-m6s
description: fix nine verified UI defects site-wide
date: 2026-08-02
branch: feat/ui-audit-batch-a
status: complete
committed: false
executor: claude (Codex unavailable; user authorized direct implementation)
---

# Quick Task 260802-m6s — Summary

All eleven Batch A fixes landed on `feat/ui-audit-batch-a`. Uncommitted pending your manual UAT.

## What changed

| # | Defect | Fix |
|---|--------|-----|
| D1 | 3 tiles rendered "?" | `_ToolTileIcon.cshtml` keyed off `Key` not `HelpSlug`; added `deck-history` arm |
| D2 | Landing page had no `<h1>` | visible `<h1 class="hub-title">` + spacing rule |
| D3 | 404s bypassed the branded page | `UseStatusCodePagesWithReExecute`, `/api` excluded; `ErrorPageModel` branches copy |
| D4 | Feedback validation inert | dropped `novalidate` |
| D5 | Step tabs unnamed on mobile | `aria-label="@step.Label"` |
| D6 | Disabled tabs broke roving-tabindex | dropped `disabled`; capture-phase guard in `site.ts` |
| D7 | Nav toggle controlled itself | `#deck-tool-nav-groups` wrapper (`display: contents`); CSS-gated visibility |
| D8 | Primer "Start Over" silently wiped the deck | `data-cache-key="deck-primer"` |
| D9 | Sub-44px tap targets, iOS auto-zoom | 44px floors; `font-size: max(16px, 1em)` on feedback inputs |
| D10 | `.table-wrapper` had no CSS rule | rule added + `tabindex="0" role="region"` |
| D11 | "found in 0 reference deck(s)" | `RefCount > 0` |

## Amendments to the plan, and why

**D6 — the prescribed fix broke Cut Lab, and the e2e baseline caught it.**
Dropping `disabled` alone would let an incomplete `type="submit"` tab post the form, so the plan also
degraded blocked tabs to `type="button"` with no `form` binding. That broke
`cut-lab-export.spec.ts:141`: Cut Lab renders step 4 disabled server-side and **enables it
client-side** once the deck hits 100 cards, and that JS only flips `disabled`/`aria-disabled` — it
never restores `type` or `form`. The tab looked enabled and submitted nothing.
Final shape: `type`/`form` stay unconditional, only `disabled` is dropped, and the capture-phase
guard in `site.ts` blocks activation while `aria-disabled="true"` — releasing the moment JS clears it.

**D7 — the `hidden` attribute was not dead code.** It was the desktop hiding mechanism; nothing in
`site-common.css` set `display` on `.tool-nav__menu-toggle` and `site-mobile.css` only overrode
`[hidden]` below 600px. Removing it without a replacement rule would have leaked a "Tools" button
onto every desktop page. Visibility now lives in CSS in both sheets.

**D3 — added an `/api` carve-out** not in the plan: a bare re-execute would have returned HTML to
`fetch()` callers on any API 404.

## Pre-existing defect found and fixed

`HomeTilesViewTests.ToolTileIcon_PartialContainsIconArm` was **asserting the bug**: its hand-written
`InlineData` listed `"ask-a-judge"` and `"category-suggestions"` (help slugs, not icon keys) and
omitted `deck-history`, `cut-lab`, and `bracket` entirely — 13 cases covering the wrong population.
It now derives its cases from `ToolRegistry.All` (16 keys) and asserts `case "<key>":` rather than a
bare quoted string, so a key in a comment cannot satisfy it.

## Verification

- `dotnet build DeckFlow.sln` — **0 errors, 9 warnings**, matching the documented CS8629 baseline
- `DeckFlow.Web.Tests` — **2269 passed, 0 failed**, 16 skipped
- `DeckFlow.Core.Tests` — **2011 passed, 0 failed**
- New `e2e/ui-audit-batch-a.spec.ts` — 20 passed across desktop + mobile viewports
- Affected existing specs (cut-lab-export/smoke/structure, tool-toggles, content-kb-public,
  deck-analysis-render, layout-mode-interaction) — **93 passed, 0 failed** single-worker
- **Baseline comparison:** a throwaway worktree at `origin/main` was built and run to separate my
  regressions from pre-existing flake. The full parallel suite fails ~13-15 specs on *both* trees
  with a ~1.5m timeout signature (Debug build + parallel workers on this machine); single-worker,
  baseline passes 35/35 and the branch passes the same 35 plus the new 18.
- **Both new guards mutation-proven:** neutering the `site.ts` listener fails the step-tab test;
  renaming one icon `case` fails the totality test.

## Not done / follow-ups

- **Not committed** — awaiting your manual UAT per the commit rule.
- The full parallel e2e suite still has ~13 pre-existing timeout-shaped failures on this machine.
  Unrelated to this change (reproduced on `origin/main`), but worth a separate look.
- Batch B (CSS-location + icon-key CI guards), D (deep-linking), E (panel segmentation),
  F (tab partial split) remain as separate todos. The icon-key guard is now partly satisfied by the
  registry-derived unit test, but there is still no CSS-location guard.
- Two judgment calls to confirm during UAT: the landing page now shows a **visible** `<h1>`
  (say the word and it becomes `.sr-only`), and the Deck Primer now **persists form state** across
  reloads as a side effect of the `data-cache-key` fix the audit prescribed.
