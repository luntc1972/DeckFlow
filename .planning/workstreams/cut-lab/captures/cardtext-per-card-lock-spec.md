# Cut Lab — Card Text View + Per-Card Lock in Role Groups

**Date:** 2026-07-20
**Workstream:** cut-lab (Cycle 18)
**Status:** Design approved — pending spec review → implementation plan
**Scope note:** This is a 101/102 intake/lock-surface feature. It is NOT part of Phase 103 (simulation engine & guided cut rounds). It lands as its own small phase in the `cut-lab` workstream, executed after Phase 103 closes. The `tool.cut-lab.enabled` flag stays OFF through this work.

## Problem

Two gaps surfaced during Phase 103 UAT:

1. **No way to read a card's rules text** while working the pool. The oracle text is resolved and cached at intake but never surfaced in the UI.
2. **Role groups only support bulk locking.** The role-group accordions (Phase 102) render each member as a display-only chip with a single "Lock all {role}" pill. There is no way to lock/unlock an *individual* card from within a role group.

The Phase 102 design deliberately made the pool table the **single canonical lock surface** and kept role groups display-only, because a card can belong to multiple role groups — duplicating lock state across groups multiplies the desync surface (102 "Pitfall 8"). Any per-card locking in groups must preserve that single-source invariant.

## Approach (chosen)

**Embed oracle text once, read locally.** The server renders each card's cached text into its pool-table row once (hidden until opened). Both the pool grid and the role-group chip popover read that embedded text — no fetch, no new endpoint, no CSP change, instant on load. Rejected alternatives: an on-demand JSON endpoint (adds endpoint + same-origin guard + per-open round-trip + loading state for data already in memory), and card images (new `img-src` CSP entry + external requests; the ask was card *text*).

## Design

### 1. Data (server)

- **Source:** the resolved-card cache already holds `ScryfallCardData.OracleText`, `TypeLine`, `ManaCost`, **`Set` (set code, e.g. "iko")**, and **`CollectorNumber`** per pool card (populated at intake; `ScryfallCardDataMapper.ToCardData` maps `Set = card.SetCode` + `CollectorNumber`; DFC faces merged).
- **Change:** thread `TypeLine`, `ManaCost`, `OracleText`, `Set`, `CollectorNumber` onto each `CutLabViewModel` pool row, sourced from the cache.
- **Set display:** show the printing as **set code + collector number** (e.g. "IKO · #211") — both are already in hand, zero new fetch. The full set *name* ("Ikoria: Lair of Behemoths") is NOT on `ScryfallCardData`; if a friendlier label is wanted later, map the code via the existing `IScryfallSetService` (code→name) — treat that as optional enrichment, not required for this feature (avoids an extra lookup/cache on the hot intake path).
- **Fail-open:** a card the cache could not resolve renders with its text/set simply absent — never a crash, never a blank-required field. A resolved card missing `Set` (rare) omits the set line only.
- Text + set are rendered **once per card**, in the pool-table row (a card appears once in the table, but in N role groups — embed at the single table location).

### 2. View (`CutLab.cshtml`)

- **Pool "Lock your pool" grid:** each row gains a native `<details>`/`<summary>` "card text" disclosure showing type line · mana cost · **set code + collector number** · oracle text. Native element ⇒ readable with **no JS**.
- **Role-group chips:** each `data-cut-lab-chip-card` member becomes a real `<button>`. With JS it opens **one shared popover element** anchored to the clicked chip, showing that card's text + **set/printing** (read from the embedded per-card block, matched by card name) plus a **Lock / Unlock** button.
- **Commander:** text viewable everywhere; the lock control is absent/disabled with the existing "Commander · Always locked" treatment.

### 3. Client (`cut-lab.ts`) — single-source lock model

The canonical lock state remains the **pool-table checkbox** `input[data-cut-lab-lock-card="{name}"]` — unchanged, and still the value that serializes into `CutLabStateJson` on submit. A chip's Lock button holds **no state of its own**. On click it:

1. Finds the canonical checkbox by card name and toggles it.
2. Calls the **same group-resync routine the bulk "Lock all {role}" pill already uses** (the `getPoolRows().filter(row => api.hasRoleToken(row.dataset.cutLabRole, roleKey))` path). That routine re-renders every reflection of the card:
   - the same card's chips in **all** role groups (`cutlab-role-chip--locked` add/remove),
   - each affected group's locked count (`data-cut-lab-group-locked`),
   - the affected groups' bulk-pill state.

Result: a multi-membership card locked from group A immediately shows locked in group B and in the table. One source, many mirrors — no new persistence, no new desync surface.

**Popover:** a single reused DOM element. Opens near the clicked chip; ESC and click-outside close it; focus returns to the chip on close.

### 4. No-JS / accessibility / mobile / theme

- **No-JS:** the pool-row `<details>` shows card text natively. Role-group chips stay display-only; locking falls back to the pool table (today's behavior). The popover and chip-lock are pure JS enhancement — nothing regresses with JS off.
- **Accessibility:** chip is a `<button>` with `aria-expanded`; the lock button carries `aria-pressed` reflecting current lock state; the commander lock control is `aria-disabled` with a reason; ESC dismiss + focus return.
- **Mobile:** 44×44px minimum hit targets (WIG convention already applied in 101/102); popover clamped to the viewport, rendered as a bottom sheet on narrow widths.
- **Theme:** reuse `.kb-chip`, the existing panel/popover tokens, and `cutlab-role-chip--locked`. Lock uses the existing accent family — **no new accent semantics** (respects the 101/102 accent-reservation rules). All layout CSS goes in `site-common.css` per the project theme constraint.

### 5. Testing

- **Vitest (`cut-lab-proposal.test.ts` or a sibling):** chip-lock flips the canonical checkbox; a multi-membership card locked in group A updates its chip in group B and both group counts; a commander chip exposes no lock toggle; the popover renders the correct card's text.
- **xUnit (`CutLabPageServiceTests` / view-model tests):** pool row carries type line/mana/oracle text/**set code + collector number** from the cache; an unresolved card (or one missing `Set`) yields absent text/set with no throw; commander flagged non-lockable.
- **e2e (`cut-lab-structure.spec.ts`):** open a chip popover, lock one card, assert the canonical checkbox + pool-table row + the card's chip in another group + the group locked-count all sync; open card text from a pool-table row; commander shows always-locked; capture theme×viewport screenshots.

### 6. Scope & boundaries

- **Touches:** `DeckFlow.Web/Models/CutLabViewModel.cs`, `DeckFlow.Web/Views/Deck/CutLab.cshtml`, `DeckFlow.Web/wwwroot/ts/cut-lab.ts`, `DeckFlow.Web/wwwroot/css/site-common.css` (+ the three test files above).
- **Out of scope:** card images; add/edit/delete cards; changing lock persistence or the canonical lock surface; package pills (separate surface); the primary/secondary plan-textbox helper text (separate backlog item).
- **Phasing:** its own small phase in the `cut-lab` workstream, executed after Phase 103 closes. Flag stays OFF; UAT then flip, per the milestone's standing rule.

## Invariants (must not regress)

- Pool table stays the single canonical lock surface; chips and the pool row are proxies/reflections only.
- No-JS lock path (pool-table form POST) unchanged.
- Multi-membership cards keep one lock state reflected everywhere.
- Oracle text is read from already-cached data — no new upstream fetch, no new endpoint, no CSP change.
- `site-common.css` holds all layout CSS; no new accent semantics.
