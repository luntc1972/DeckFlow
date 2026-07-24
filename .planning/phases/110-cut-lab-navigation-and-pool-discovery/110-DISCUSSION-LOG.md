# Phase 110: Cut Lab Navigation and Pool Discovery - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-23
**Phase:** 110-cut-lab-navigation-and-pool-discovery
**Areas discussed:** Sticky nav vs step tabs, Filter + search mechanics, Collapse persistence key, Card text disclosure scope, Combo evidence depth, Package help copy, Sticky nav vs anchors & back-to-top, Filter/search vs role groups & evidence, Anchor nav a11y semantics

---

## Sticky nav vs step tabs

| Option | Description | Selected |
|--------|-------------|----------|
| New Cut-Lab-only anchor nav | Separate nav on CutLab.cshtml, shared partial untouched | ✓ |
| Enhance the shared partial | Opt-in scroll/sticky in `_WorkflowStepTabs` (used by 5 views) | |
| Replace Cut Lab's step tabs | Anchor nav becomes the only navigation | |

| Option | Description | Selected |
|--------|-------------|----------|
| Mobile only | `position: sticky` under ~640px, static on desktop | ✓ |
| All viewports | Sticky everywhere | |
| Mobile sticky + desktop after threshold | Static until scrolled past origin | |

| Option | Description | Selected |
|--------|-------------|----------|
| 4 step panels + key sub-sections | Steps plus Lock your pool, Structural findings, Role floors, Cut rounds, Tune quantities, Cuts made | ✓ |
| Exactly the 4 step panels | Mirrors tab semantics 1:1 | |
| Conditional — only rendered sections | Server-built list, Manabase `item.Show` pattern | |

| Option | Description | Selected |
|--------|-------------|----------|
| Use the existing `SubmitFormId` seam | `type=button` tabs scroll, `type=submit` tabs submit | ✓ |
| Check target panel presence in DOM | Client re-derives a server fact | |
| New explicit data attribute | Third concept beside `SubmitFormId` and `aria-controls` | |

**User's choice:** All four recommended options.
**Notes:** Zero shared-partial edits was the deciding factor; the `SubmitFormId` seam keeps "safe to scroll" server-authored, consistent with Phase 108.

---

## Filter + search mechanics

| Option | Description | Selected |
|--------|-------------|----------|
| CSS/`hidden`, rows stay in DOM | `getPoolRows()` still serializes full state | ✓ |
| Detach and re-insert rows | Better perf, silently drops locks on submit | |
| Server round-trip | Works no-JS, full reload per change | |

| Option | Description | Selected |
|--------|-------------|----------|
| Controls hidden without JS | No-JS users get today's full table | ✓ |
| Visible but inert | Dead controls | |
| Server-side filter form | Real no-JS filtering, adds round trip | |

| Option | Description | Selected |
|--------|-------------|----------|
| No persistence — reset each load | Avoids stale filter after mutation reload | ✓ |
| Persist in localStorage | Survives reloads, can hide most of the pool | |
| Persist in URL query string | Needs threading through POST redirects | |

| Option | Description | Selected |
|--------|-------------|----------|
| Live match count + empty-state row | "Showing 12 of 87" plus "No cards match" | ✓ |
| Empty-state row only | No signal when matches exist | |
| No extra feedback | Filtered table misreadable as the real pool | |

**User's choice:** All four recommended options.
**Notes:** The DOM-retention decision was driven by `cut-lab.ts:490` / `buildSnapshotFromDom()` — hiding makes "filtering never changes state" true by construction.

---

## Collapse persistence key

| Option | Description | Selected |
|--------|-------------|----------|
| Page-scoped `deckflow.cutlab.sections` | One preference for the page | ✓ |
| Per-commander | Mirrors primer's per-bracket precedent | |
| Per-deck hash | Needs a deck identity the VM lacks | |

| Option | Description | Selected |
|--------|-------------|----------|
| Same list as the anchor nav | One list, one invariant | ✓ |
| All primary result sections | Drifts from the nav list over time | |
| Only the tallest sections | Inconsistent with no visible rule | |

| Option | Description | Selected |
|--------|-------------|----------|
| Preserve today's behavior | Desktop open; mobile auxiliary collapsed | ✓ |
| Everything open everywhere | Discards Phase 107 mobile-collapse work | |
| Mobile: collapse all but current step | Hides primary content by default | |

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-expand, then scroll | Jump always lands on real content | ✓ |
| Scroll to collapsed summary | Extra tap on the mobile surface | |
| Auto-expand without persisting | Confusing re-collapse behavior | |

**User's choice:** All four recommended options.

---

## Card text disclosure scope

| Option | Description | Selected |
|--------|-------------|----------|
| Text-first minimum | Pool-row `<details>` + reuse under evidence chips | ✓ |
| Full approved spec | Adds shared anchored popover | (backlog) |
| Pool rows only | Drops the CLUP-16 reuse clause | |

| Option | Description | Selected |
|--------|-------------|----------|
| View-only lookup dictionary | Mirrors `RoleListByCardName`; keeps `CutLabStateJson` small | ✓ |
| Add fields to `CutLabPoolCard` | Oracle text round-trips on every POST | |
| Separate per-card DOM data island | Third lookup mechanism | |

| Option | Description | Selected |
|--------|-------------|----------|
| Type line · mana cost · set+collector · oracle text | All already on `ScryfallCardData` | ✓ |
| Oracle text only | Loses mana cost | |
| Drop set/collector | Slightly less clutter | |

| Option | Description | Selected |
|--------|-------------|----------|
| Inline disclosure under the finding | Same component, keeps reader in context | ✓ |
| Chip links to the pool-row disclosure | Yanks user away from the finding | |
| Chip opens shared popover | Requires the deferred popover | |

**User's choice:** "1 but 2 on backlog" — text-first minimum ships in Phase 110; the shared chip popover goes to the backlog. Remaining three recommended options accepted.
**Notes:** Discussion established the spec's chip-lock half already shipped in Cycle 18, so only the text half was outstanding.

---

## Combo evidence depth

| Option | Description | Selected |
|--------|-------------|----------|
| Badge on chip + detail in its disclosure | Reuses the Phase 110 disclosure | ✓ |
| Badge only | Doesn't meet CLUP-18's "role/context" | |
| Full detail inline beside the chip | Multiplies Structural findings height | |

| Option | Description | Selected |
|--------|-------------|----------|
| Two distinct badge states | "Combo piece" vs "Needs {MissingCard}" | ✓ |
| One badge, difference in the lead | Lost outside the finding's prose | |
| Near-combo badge only | Leaves half of CLUP-17 unaddressed | |

| Option | Description | Selected |
|--------|-------------|----------|
| Extend the existing weak-floor lead | Reuses the finding kind | |
| New combo-protected finding kind | Own heading and grouping | ✓ |
| Badges only, lead unchanged | Misses the explanatory "why" | |

| Option | Description | Selected |
|--------|-------------|----------|
| Thread full `IncludedCombos` into a card→combo lookup | Replaces the name-only HashSet | ✓ |
| Keep the HashSet, generic badge | Caps what CLUP-18 can deliver | |
| Thread combos, defer results/instructions | Middle cost | |

**User's choice:** Badge + disclosure detail; two badge states **plus** a rule that a complete combo makes round-1 cutting inadvisable; **new combo-protected finding kind**; full `IncludedCombos` threading **plus** exploring template slots (multiple cards satisfying one requirement) and combo variants.
**Notes:** User supplied three screenshots — a Spellbook combo page showing a template slot ("Noncreature permanent castable for {0}" → View 37 Cards) and a "Variants of this combo" view (4 variants sharing two cards, differing in the third, with EDHREC deck counts). Verified `CommanderSpellbookService.cs:251-290` parses only `uses`/`produces`/`description` and drops `requires` and variant grouping entirely.

### Follow-on scoping questions (raised by the above)

| Question | Choice |
|----------|--------|
| Where does "don't cut a complete combo in round 1" live? | **Advisory copy only** — no `CutLabCutRoundEngine` change |
| How far do template slots go in this cycle? | **Research now, implement later** — plus find combo variants |
| How far do variants go? | **Group near-combos and list alternatives** |
| Should Phase 110 be split? | **Split — 110 UX, 110.1 combo intelligence** |

---

## Package help copy

| Option | Description | Selected |
|--------|-------------|----------|
| Both disclosures in 110; 110.1 adds combo layer | CLUP-16 whole in one phase | ✓ |
| Pool rows in 110, evidence reuse in 110.1 | CLUP-16 spans two phases | |

| Option | Description | Selected |
|--------|-------------|----------|
| Top of the Packages section | Collapses with the section | ✓ |
| Near the pool table's package column | Adds copy to the tallest section | |
| Both, shorter at the table | Most copy to maintain | |

| Option | Description | Selected |
|--------|-------------|----------|
| Once above the table, not per row | Select is per row (~100 rows) | ✓ |
| Per-row via `title`/`aria-describedby` | Most users never see it | |
| On the column header | Clipped by mobile card-stack layout | |

| Option | Description | Selected |
|--------|-------------|----------|
| "Grouping doesn't remove cards" | Answers the actual confusion | ✓ |
| "Packages are named lock groups" | Mechanism-first, less direct | |
| Worked example | Longest; example cards age | |

**User's choice:** All four recommended options.

---

## Sticky nav vs anchors & back-to-top

| Option | Description | Selected |
|--------|-------------|----------|
| Cap nav height at ≤4rem | Fits the existing global `scroll-margin-top` | ✓ |
| Page-scoped `scroll-margin` override | Second source of truth | |
| Measure nav height in JS | Adds observer, fails open with no JS | |

| Option | Description | Selected |
|--------|-------------|----------|
| Top of viewport | Clear of back-to-top and mobile chrome | ✓ |
| Bottom bar | Collides with back-to-top reserved space | |

| Option | Description | Selected |
|--------|-------------|----------|
| Horizontally scrollable pill row | Fits 4rem, reuses themed pill styling | ✓ |
| Collapsed "Jump to…" toggle | Extra tap and open/close state | |
| Full wrapped list | Blows past 4rem | |

| Option | Description | Selected |
|--------|-------------|----------|
| Always visible once stuck | No scroll listener, no reduced-motion concern | ✓ |
| Hide on scroll down | Needs hysteresis and a reduced-motion path | |
| Visible from page load | Must render high in document order | |

**User's choice:** All four recommended options.
**Notes:** Grounded in `site-common.css:104-111` (`scroll-margin-top: 4rem` for `[id]`) and the existing fixed back-to-top button with reserved right-edge space at `site-common.css:1065`.

---

## Filter/search vs role groups & evidence

| Option | Description | Selected |
|--------|-------------|----------|
| Table only | No second visibility mirror | ✓ |
| Propagate to role groups | Locked counts would lie or need recomputing | |
| Propagate everywhere | Most stale-filter failure modes | |

| Option | Description | Selected |
|--------|-------------|----------|
| Counts always reflect the whole pool | Core status never filter-dependent | ✓ |
| Counts follow the filter | Export-eligibility story becomes filter-dependent | |
| Show both | Clutters an already-dense heading | |

| Option | Description | Selected |
|--------|-------------|----------|
| Panel heading above the table | Uses an existing layout slot | ✓ |
| Sticky within the table | Competes for the 4rem budget | |
| Inside the anchor nav | Conceptually wrong scope | |

| Option | Description | Selected |
|--------|-------------|----------|
| "Lock your pool" only | Exactly what CLUP-11/12 specify | ✓ |
| All card tables | Multiplies JS and theme verification | |
| Pool + Tune quantities | Tune quantities is rarely long | |

**User's choice:** All four recommended options.

---

## Anchor nav a11y semantics

| Option | Description | Selected |
|--------|-------------|----------|
| `<nav aria-label="Jump to section">` | Distinct landmark beside the tablist | ✓ |
| Plain list, no landmark | Loses landmark navigation | |
| Fold into the tablist container | Semantically wrong | |

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, with distinct wording | Nav is a complete page map | ✓ |
| Sub-sections only | Omits the four biggest landmarks | |

| Option | Description | Selected |
|--------|-------------|----------|
| No current-state tracking | Nothing to keep correct across collapse/filter | ✓ |
| `aria-current` via IntersectionObserver | Observer must survive re-renders | |
| Highlight last-clicked only | Goes stale on manual scroll | |

| Option | Description | Selected |
|--------|-------------|----------|
| Move focus to the target section | Avoids the classic skip-link defect | ✓ |
| Scroll only, focus stays in nav | Keyboard users tab from the wrong place | |
| Native anchor behavior | Breaks once JS must auto-expand first | |

**User's choice:** All four recommended options.

---

## Claude's Discretion

- Theme-fork CSS strategy for the sticky nav (token choice, opacity guarantee, specificity approach
  across the 11 standalone forks). Not discussed; constraints recorded in CONTEXT.md.
- Exact anchor label wording, pill ordering, and section id naming.
- Search matching semantics (case-insensitive substring on card name assumed).
- Precise help-block and hint wording within the agreed angle.

## Deferred Ideas

- Shared chip popover from the full 2026-07-20 card-text spec → backlog.
- Spellbook template-slot candidate matching (enumerating the "37 cards") → after Phase 110.1 research.
- Combo-aware cut-round ranking as an engine rule → its own phase.
- Full variant browser with per-variant EDHREC deck counts → data may not be in our API response.

## Scope Changes Made During Discussion

- **Phase 110.1 (Cut Lab Combo Intelligence) inserted** into ROADMAP.md; CLUP-17 and CLUP-18 remapped
  from Phase 110 to Phase 110.1 in REQUIREMENTS.md. Phase 110 drops from 11 requirements to 9, and
  from 9 success criteria to 7. Phase 111's `Depends on` updated to `Phases 108-110.1`.
