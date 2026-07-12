---
slug: set-picker-date-desc
type: quick
---

# Set picker: flat, date-descending (no grouping)

**Task:** When selecting a set (Deck Analysis Step 4 set-upgrade picker, `/api/set-options`),
list sets in release-date-descending order with NO grouping.

**Finding:** The backend (`ScryfallSetService.GetSetsAsync`) already returns sets
`OrderByDescending(ParseReleasedAt)` and `DisplayLabel` already includes the date. The
client (`deck-sync.ts`) re-groups them into `<optgroup>` by `setType` (SET_TYPE_ORDER),
which destroys the flat date order. Fix is TS-only: render a flat `<option>` list in the
received order; delete the now-dead grouping helpers.

**Change (deck-sync.ts only):**
1. Replace the grouping block (~1573-1658: groupedSets/unknownGroups/otherSets + optgroup loops)
   with a single flat loop appending one `<option>` per set in received order, preserving
   `selectedCodes` selection + the trailing `refreshDfSelect(select)` call.
2. Remove the now-unused helpers `SET_TYPE_LABELS`, `SET_TYPE_ORDER`, `prettifySetType`,
   `getSetTypeLabel` (used only by the removed block). Leave `SetOptionResponse.setType`
   (API still returns it; harmless).

**Verify:** `tsc` clean; set dropdown shows a flat list, newest set first, no group headers.
