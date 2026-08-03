---
created: 2026-08-03T21:34:35.224Z
title: Card Lookup Card List mode is unreachable on mobile
area: ui
severity: minor
files:
  - DeckFlow.Web/Views/Deck/CardLookup.cshtml:9,19,76
  - DeckFlow.Web/wwwroot/css/site-common.css:5741-5744
  - DeckFlow.Web/wwwroot/css/site-commander-table.css:1074
---

## Problem

Open design decision carried over from the 2026-08-03 Batch A + G UI-audit UAT. It was recorded
only as item 14 in `.planning/HANDOFF.json`, which was overwritten when that add/add conflict was
resolved during the `gsd/cycle21-cut-lab` rebase — so this file is now its only record.

Card Lookup has two modes. The second one disappears on phones with no trace:

- `CardLookup.cshtml:19` — the mode picker (`Single Card` / `Card List (download)`) carries
  `.desktop-only`.
- `CardLookup.cshtml:76` — the entire list panel, including the bulk `.txt` / `.json` download
  form that POSTs to `~/card-lookup/download`, also carries `.desktop-only`.
- `CardLookup.cshtml:9` — even the lede sentence that *mentions* the list mode is wrapped in
  `.desktop-only`.
- `site-common.css:5741-5744` — `@media (max-width: 600px) { .desktop-only { display: none
  !important; } }`.

Below 600px the result is not a degraded experience but a silently absent one: no picker, no
panel, no explanatory copy. A phone user has no way to learn that bulk Scryfall text export
exists, and nothing tells them to come back on a desktop. Every other `.desktop-only` use on the
site hides *supplementary* copy (`DeckAnalysis.cshtml:399,405,501`) or a redundant layout control
(`:85`) — this is the only one that hides a whole primary feature of the page.

Note the wrinkle: `.desktop-only` is defined twice, in `site-common.css:5742` and
`site-commander-table.css:1074`. Any change to the breakpoint or the rule has to touch both, and
per the theme rules the layout side belongs in `site-common.css`.

## Solution

TBD — this is a product decision, not a defect with one right fix. Two candidates:

1. **Make it responsive.** The list panel is a textarea plus a submit button; the constraint is
   plausibly the download UX and the 100-card cap rather than anything about the layout. If the
   form works acceptably at ~390x844, drop `.desktop-only` from `:19` and `:76` and let it render.
   Confirm the download actually completes in mobile Safari and Chrome before committing to this —
   a form POST that returns a file attachment is the part most likely to misbehave, and that is
   presumably why it was gated in the first place.

2. **Say so explicitly.** Keep the feature desktop-only but replace the silent hide with a visible
   mobile-only note on the page ("Bulk card-list export is available on desktop"), so the
   capability is at least discoverable. Cheaper, and it removes the "feature does not exist"
   reading without needing any mobile download testing.

Whichever is chosen, decide it for the `.desktop-only` *pattern* on this page as a whole, so
`:9`'s lede copy stops contradicting whatever the picker does.
