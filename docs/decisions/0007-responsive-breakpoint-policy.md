# 0007 — Responsive breakpoint policy: 1024px workspace, 900px mobile chrome

Date: 2026-09-19

## Context

The mobile UI redesign sweep (2026-09-18) flagged several pages as breakpoint-inconsistent, on the
premise that a 600/900px contract exists. No written contract existed. The stylesheets in fact use a
mixed set of values: 1024px for desktop workspace layouts (9 `min-width` rules), 900/901px for the
shared mobile chrome (nav, back-to-top, touch targets), and page-local values (760/761px Deck
Modules, 720px Cut Lab constraints, 640px History compact, 600px and 480px small-phone rules).

The band between 901px and 1023px was checked on `/sync` and `/deck-history` at 900, 901, 960, 1023
and 1024px: it renders the base (single-column) layout with no horizontal overflow, matching the
≤900px workspace chrome. It is a deliberate tablet band, not a defect.

## Decision

Keep the current values and record them as the policy:

- **1024px** — where a workspace switches to its desktop layout (wider hero padding, multi-column
  result grids, sticky rails). Use `min-width: 1024px` for the desktop side and
  `max-width: 1023.98px` for any rule that must complement it.
- **900px** — the shared mobile chrome boundary in `site-mobile.css` (nav, touch targets, footer
  reserve).
- **≤600px** — small-phone tuning inside `site-mobile.css`; **≤480px** only for the tightest phones.
- **Page-local breakpoints** (760/761, 720, 640, 520, 768) stay where a component's own content
  forces a reflow. A new one needs a `Why:` comment naming the content that forces it; it is not to
  be added just to mirror 600/900.
- The 901–1023px band uses base styles by design. Do not add rules to "close" it unless a real
  overflow or touch-target defect is shown at a specific width.
- Layout rules go in `site-common.css`; `site-mobile.css` is for overrides of selectors the guild
  themes redefine.

## Consequences

Findings that only observe "this page uses 1024/760/720/640 instead of 900/600" are closed as
by-design. A change of a workspace breakpoint (for example 1024 to 900) is a redesign decision and
needs desktop and mobile Playwright checks on every page that shares the rule.
