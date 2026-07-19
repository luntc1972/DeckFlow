---
quick_id: 260718-qjx
description: Add WotC Fan Content Policy disclaimer to site footer
created: 2026-07-19
mode: quick
---

# Quick Task 260718-qjx: Add WotC Fan Content Policy disclaimer to site footer

## Goal

Every page shows the standard Wizards of the Coast Fan Content Policy disclaimer in the shared footer, satisfying the FCP's mandatory-notice condition for fan tools.

## Context

- Footer lives in `DeckFlow.Web/Views/Shared/_Layout.cshtml` lines 114-125 (`<footer class="page-footer">` with `.page-footer__link` entries).
- Layout CSS must go in `DeckFlow.Web/wwwroot/css/site-common.css` (never `site.css`); `.page-footer` styles are at ~line 1008 with a mobile media query at ~1053.
- Required disclaimer text (exact): "DeckFlow is unofficial Fan Content permitted under the Fan Content Policy. Not approved/endorsed by Wizards. Portions of the materials used are property of Wizards of the Coast. ©Wizards of the Coast LLC."
- "Fan Content Policy" must link to https://company.wizards.com/en/legal/fancontentpolicy (target _blank, rel noopener noreferrer).
- Themes are standalone CSS forks but share site-common.css for layout; use existing color tokens (muted/secondary text token) so all guild themes render it acceptably.

## Tasks

### Task 1: Footer markup + CSS

**Files:** `DeckFlow.Web/Views/Shared/_Layout.cshtml`, `DeckFlow.Web/wwwroot/css/site-common.css`

**Action:** Inside the existing `<footer class="page-footer">`, after the link row, add a `<p class="page-footer__legal">` containing the disclaimer with the FCP hyperlink. Add `.page-footer__legal` styles in site-common.css adjacent to the existing `.page-footer` block: small font size, muted color via an existing CSS token, centered to match footer alignment, constrained max-width, comfortable line-height, small top margin. Ensure mobile media query keeps it readable (it should inherit; adjust only if the existing footer query requires it).

**Verify:** `dotnet build` clean; disclaimer visible on any page.

**Done:** All pages render the disclaimer; no theme-specific overrides needed.

### Task 2: Build + test verification

**Action:** `dotnet build DeckFlow.sln` clean; run Web test suite; confirm no render/layout tests break (fix any that assert on footer markup).

**Done:** Build 0 errors/0 new warnings; Web tests green.

## must_haves

- truths:
  - Every public page shows the FCP disclaimer in the footer
  - "Fan Content Policy" links to the official WotC policy page
- artifacts:
  - `DeckFlow.Web/Views/Shared/_Layout.cshtml` (disclaimer markup)
  - `DeckFlow.Web/wwwroot/css/site-common.css` (`.page-footer__legal`)
