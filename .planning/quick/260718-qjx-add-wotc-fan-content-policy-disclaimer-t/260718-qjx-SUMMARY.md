---
quick_id: 260718-qjx
description: Add WotC Fan Content Policy disclaimer to site footer
status: complete
completed: 2026-07-19
---

# Quick Task 260718-qjx: Summary

## What changed

- `DeckFlow.Web/Views/Shared/_Layout.cshtml` — added `<p class="page-footer__legal">` inside the shared `page-footer`, containing the standard Wizards of the Coast Fan Content Policy disclaimer with "Fan Content Policy" linked to https://company.wizards.com/en/legal/fancontentpolicy (target _blank, noopener noreferrer). Renders on every page.
- `DeckFlow.Web/wwwroot/css/site-common.css` — new `.page-footer__legal` rule (muted `--muted` token, 0.78rem, 70ch max-width, right-aligned block) plus `flex-wrap: wrap` on `.page-footer` and `flex-basis: 100%` on the legal block so it occupies its own row.
- `README.md` — UI styling section notes the footer disclaimer.

## Implementation notes

- Codex (gpt-5.4 medium) implemented both passes; Claude reviewed.
- Review catch: first pass left the disclaimer as a competing flex item in the non-wrapping footer flex row — on 390px mobile it rendered as a ~114px-wide squished column beside the footer links. Fix pass added `flex-wrap: wrap` + `flex-basis: 100%`.
- EOL verified: both files LF before and after; diff contains only content lines.

## Verification

- `dotnet build DeckFlow.sln` — 0 errors (9 pre-existing warnings in DeckFlow.Core.Tests, unrelated).
- Web test suite: 1583 passed / 0 failed / 16 skipped (Postgres integration skips).
- Playwright screenshots at 1280px and 390px confirm layout on both viewports; user eyeballed the running test server.
- No theme CSS files touched; layout CSS confined to site-common.css per project constraint.
