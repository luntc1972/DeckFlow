# Mobile responsiveness sweep — DeckFlow CSS

Scope: `DeckFlow.Web/wwwroot/css/site-common.css` and `site.css` only. Theme forks not touched.

## Plan checklist

- [ ] Add `@media (max-width: 768px)` block — `.page-nav { gap: 0.5rem; }`, `.sync-column { padding: 0.75rem; }`
- [ ] Add `@media (max-width: 480px)` block — `.page-shell { padding: 1rem 0.75rem 2rem; }`, `.hero h1 { font-size: 1.2rem; }`
- [ ] Toolbar full-width buttons at ≤900px — broad rule: every `<button>` inside `.toolbar` goes `width: 100%`; cancel any `flex: 0 0 auto` / `width: auto` on stacked buttons
- [ ] Back-to-top mobile sizing
  - ≤900px: `width/height: 2rem`, `right/bottom: 1.5rem`
  - ≤600px: `display: none`; `.page-footer` padding-right reset to 1rem
- [ ] `.copy-button--icon` at ≤600px → `width/height: 2rem`
- [ ] `.card-picker__add` / `.card-picker__remove` at ≤600px → `width/height: 1.75rem; padding: 0.25rem`

## QA (test each breakpoint)

- [ ] 1024px — desktop unchanged
- [ ] 900px — toolbar stacks, buttons fill row
- [ ] 768px — nav gap tighter, sync-column padding shrinks
- [ ] 600px — back-to-top hidden, footer padding sane, icon buttons shrunk
- [ ] 480px — page-shell tighter, h1 1.2rem, no horizontal scroll

## Decisions locked

- Q1: hide back-to-top entirely on ≤600px (mobile users scroll; button steals tap area)
- Q2: broad selector for toolbar full-width — `.toolbar button` not specific classes

## Review

Cascade discovery: themes load AFTER site-common.css and 13–18 themes redefine the selectors targeted by 6 of the 9 planned rules — those rules would have been dead.

Resolution: introduced `DeckFlow.Web/wwwroot/css/site-mobile.css`, loaded after the theme stylesheet in `_Layout.cshtml` so it always wins cascade. Cascade-conflicting rules moved to that file; cascade-safe rules stayed in `site-common.css`.

Final layout:
- `site-mobile.css` (new): 900px back-to-top resize + toolbar full-width buttons, 768px sync-column padding, 600px back-to-top hide + page-footer padding reset, 480px page-shell padding + hero h1 size
- `site-common.css`: existing 900px theme-picker + new 768px page-nav gap + new 600px card-picker / copy-button-icon shrink
- `_Layout.cshtml`: added `<link>` for site-mobile.css after theme link
- `README.md`: documented the new file and load order

Decisions locked: hide back-to-top entirely on ≤600px; broad `.toolbar button { width: 100% }` selector at ≤900px.

QA: Codex ran two self-QA passes, then verified by reading file contents, grepping selectors, and confirming load order.
