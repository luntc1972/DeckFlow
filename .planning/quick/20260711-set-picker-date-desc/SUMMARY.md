---
slug: set-picker-date-desc
type: quick
status: complete
---

# Summary

Set-selection dropdown (`data-set-options-select`, Deck Analysis Step 4) now renders a
FLAT `<option>` list in release-date-descending order, with NO grouping.

Root cause: backend already returned sets date-desc (`ScryfallSetService.GetSetsAsync`
`OrderByDescending(ParseReleasedAt)`), but the client (`deck-sync.ts`) re-grouped them into
`<optgroup>` by setType, destroying the flat order. Fixed client-side only: replaced the
grouping block with a single flat loop appending options in received order, and removed the
now-dead helpers (SET_TYPE_LABELS, SET_TYPE_ORDER, prettifySetType, getSetTypeLabel).

Verified live: 0 optgroups, 657 options, newest set first (2026-11-20 → …). tsc --noEmit clean.
1 file changed (deck-sync.ts), 6 ins / 112 del. No backend/API change.
