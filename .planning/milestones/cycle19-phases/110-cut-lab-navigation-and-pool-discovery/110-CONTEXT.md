# Phase 110: Cut Lab Navigation and Pool Discovery - Context

**Gathered:** 2026-07-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Make the Cut Lab page navigable and its pool searchable, and surface card rules text as text-first
disclosures — **without** disturbing the canonical lock/package workflow or the no-JS path.

Phase 110 is **view-layer only**: Razor markup (`CutLab.cshtml`), TypeScript (`cut-lab.ts`),
`site-common.css`, and one view-model lookup dictionary. It touches **no** analysis engine, **no**
upstream API client, and **no** shared partial.

**In scope:** CLUP-06, CLUP-07, CLUP-08, CLUP-11, CLUP-12, CLUP-13, CLUP-14, CLUP-15, CLUP-16.

**Explicitly moved out during this discussion:** CLUP-17 and CLUP-18 were split into the newly
inserted **Phase 110.1 — Cut Lab Combo Intelligence**. That work is data-layer (Commander Spellbook
parsing, a new finding kind, a changed `CutLabFindingEvidence` record, and a ripple into the Phase
108 `CutLabUiPatchBuilder` DTO) and carries a different risk profile than this phase's view work.
Phase 110 delivers the reusable disclosure component; Phase 110.1 adds the combo layer inside it.

</domain>

<decisions>
## Implementation Decisions

### Jump navigation (CLUP-06, CLUP-07, CLUP-14)
- **D-01:** Build a **new Cut-Lab-only anchor nav** in `CutLab.cshtml`, patterned on the
  `.manabase-anchor-nav` idiom (`Views/Deck/Manabase.cshtml:420`, `site-common.css:2970`). The shared
  `Views/Shared/_WorkflowStepTabs.cshtml` partial is **NOT modified** — it is consumed by
  `CedhMetaGap`, `DeckPrimer`, `DeckAnalysis`, and `DeckComparison`, and the backlog explicitly
  flagged touching it as the regression risk.
- **D-02:** Sticky on **mobile only**. Static block on desktop, matching how `.manabase-anchor-nav`
  already collapses to a single column under 640px.
- **D-03:** Anchor targets = the **4 step panels (Process, Decide, Goals, Export) plus key
  sub-sections**: Lock your pool, Structural findings, Role floors, Cut rounds, Tune quantities,
  Cuts made. Anchoring only the 4 steps does not meaningfully help, because the sub-section tables
  dominate the ~14,000px mobile scroll.
- **D-04:** "Safe to scroll" is a **server-authored fact**, not a client inference. The partial
  already renders `type="@(step.SubmitFormId is null ? "button" : "submit")"`
  (`_WorkflowStepTabs.cshtml:25`) — tabs with a null `SubmitFormId` need no server work and may be
  scrolled by JS; `type=submit` tabs keep submitting untouched. Do **not** re-derive this by probing
  the DOM; that is the exact anti-pattern Phase 108 removed.
- **D-05:** The sticky nav must fit within **≤4rem** so the existing global
  `h1,h2,h3,[id] { scroll-margin-top: 4rem; }` (`site-common.css:104-111`) keeps holding. Do not
  override that rule for this page and do not measure height in JS.
- **D-06:** Sticky nav sits at the **top** of the viewport — clear of the fixed back-to-top button
  and its reserved right-edge space (`site-common.css:1065`, `wwwroot/ts/site.ts:41`) and clear of
  mobile browser bottom chrome.
- **D-07:** When stuck, the nav renders as a **horizontally scrollable single row of pills**, reusing
  chip/pill styling already themed across all 11 forks. Not a wrapped list (blows the 4rem budget),
  not a collapsed "Jump to…" toggle.
- **D-08:** The nav is **always visible once stuck** — no hide-on-scroll-down/reveal-on-scroll-up.
  No scroll listener, no hysteresis, nothing extra for `prefers-reduced-motion` to handle.

### Anchor nav accessibility (CLUP-06, CLUP-14)
- **D-09:** Render as `<nav aria-label="Jump to section">` — a distinct navigation landmark beside
  the step tabs' own `role="tablist"`/`aria-label`, mirroring Manabase's `<nav aria-label="On this
  page">`.
- **D-10:** Anchors **do** duplicate the four step-panel targets, with wording that distinguishes
  navigation from workflow control. The nav is a complete map of the page; omitting the four biggest
  landmarks would make its coverage look arbitrary.
- **D-11:** **No current-section tracking.** Plain links — no `aria-current`, no IntersectionObserver,
  no scroll listener. Nothing to keep correct across collapse/expand, filtering, or post-mutation
  re-renders. Matches Manabase's equivalent.
- **D-12:** After a jump, **move keyboard focus to the target section** (`tabindex="-1"` + focus)
  so keyboard and screen-reader users land in the section rather than leaving focus behind in the
  nav. Honor `prefers-reduced-motion` for the scroll, matching `wwwroot/ts/admin-harvest.ts:174`.

### Pool filter and search (CLUP-11, CLUP-12)
- **D-13:** Non-matching rows are hidden via a `[hidden]` attribute / class — **rows stay in the
  DOM**. This is load-bearing: `getPoolRows()` (`cut-lab.ts:490`) is a live `querySelectorAll` over
  the pool table and `buildSnapshotFromDom()` walks it to build `CutLabStateJson`. Detaching rows
  would silently drop lock/package state on the next submit. Hiding makes CLUP-11's "without
  changing card state" true **by construction**.
- **D-14:** Filter/search controls are **hidden without JS** (revealed or injected on script init).
  No-JS users get today's full unfiltered table — nothing regresses, and there are no dead controls.
- **D-15:** **No persistence.** Filter and search combine with AND while active and both clear on
  page load. Prevents the "why is my pool empty" failure after a server mutation reload — a real
  hazard on a page that reloads after every decide/adjust.
- **D-16:** Show a **live match count** (e.g. "Showing 12 of 87 cards") near the controls plus an
  explicit **"No cards match" empty-state row** at zero. Makes it unmistakable that hidden cards
  still exist and are still locked.
- **D-17:** Filtering is **scoped to the "Lock your pool" table only**. Role-group accordions and
  Structural evidence chips are different lenses on the same pool and stay whole — no second
  visibility mirror to keep in sync alongside the lock-state mirror Cycle 18 already stabilized.
- **D-18:** Status counts (`data-cut-lab-lock-count`, role-group locked counts) **always reflect the
  whole pool**, never the filtered view. Only the new match count describes the filtered subset.
  Those counts feed the export-eligibility story and must not become filter-dependent.
- **D-19:** Controls live in the **existing panel-heading block above the table**, alongside the pool
  status text, so they collapse with the section. Not sticky (would compete for the 4rem budget).
- **D-20:** Only the "Lock your pool" table gets filter/search — not Tune quantities, not Cuts made.

### Section collapse (CLUP-13)
- **D-21:** localStorage key is **page-scoped**: `deckflow.cutlab.sections`. Collapse is a viewing
  preference, not deck data. No new view-model field, no unbounded key growth. Follows the
  `deckflow.primer.sections.{bracket}` naming convention (`wwwroot/ts/primer-selection.ts:27`).
- **D-22:** The **collapsible section list is identical to the anchor list** (D-03). One list to
  maintain, one invariant to test, and the two features reinforce rather than diverge. Includes the
  three sections that already collapse (Packages, Scenarios, What-if swap).
- **D-23:** Defaults preserve today's behavior — **desktop: everything open; mobile: the three
  auxiliary sections collapsed** as they are now, primary sections open. Nothing regresses for
  existing users and the Cycle 18 mobile-collapse e2e assumptions stay valid.
- **D-24:** Jumping to a collapsed section **auto-expands it, then scrolls**. Without this, mobile
  users land on a one-line summary and conclude the nav is broken — the same "looks like jump-nav
  but doesn't jump" complaint that motivated this phase.
- **D-25:** Reuse the existing `getLocalStorage()` try/catch wrapper (`cut-lab.ts:254`) and
  `isQuotaExceededError` handling from the Phase 104 scenario code. Do not add a parallel mechanism.

### Card text disclosure (CLUP-16)
- **D-26:** Build the **text-first minimum**: a native `<details>` disclosure on pool-table rows
  **and** an inline disclosure under Structural evidence chips. The shared anchored popover from the
  approved 2026-07-20 spec is **deferred** (see Deferred Ideas). Note the spec's other half —
  per-card lock from role-group chips — already shipped in Cycle 18 (`cut-lab.ts:2907-2910`), so
  only the text half was outstanding.
- **D-27:** The evidence-chip disclosure ships in **Phase 110** showing plain card text, which needs
  no combo data at all. Phase 110.1 later adds combo badges/context **inside that same disclosure**.
  This keeps CLUP-16 whole in one phase and CLUP-17/18 whole in the next.
- **D-28:** Oracle text lives in a **view-only lookup dictionary** on `CutLabViewModel` (a
  `CardTextByCardName`-style map, mirroring the existing `RoleListByCardName` /
  `RoleKeysByCardName` patterns). It must **NOT** go on `CutLabPoolCard`
  (`Models/CutLab/CutLabState.cs:129`) — that record is the serialized state that round-trips through
  `CutLabStateJson` on every mutation, and full oracle text for ~100 cards would bloat every POST,
  directly at odds with the Phase 108 timeout work.
- **D-29:** Disclosure contents: **type line · mana cost · set code + collector number · oracle
  text**. All five are already on `ScryfallCardData` from intake — zero new fetches, no new endpoint,
  no CSP change.
- **D-30:** Native `<details>`/`<summary>` so the text is readable with **no JS**.
- **D-31:** Fail open — a card the cache could not resolve renders with its text simply absent.
  Never a crash, never a blank required field.

### Package assignment help (CLUP-15)
- **D-32:** Static help block goes at the **top of the Packages section**, inside the existing
  `<details class="cutlab-collapsible">` (`CutLab.cshtml:354`), above the package cards — so it
  collapses with the section once understood.
- **D-33:** The inline hint renders **once above the table, not per row**. The package `<select>` is
  per pool row (`cut-lab.ts:2555` reads `getPackageSelect(row)`); repeating a hint across ~100 rows
  would be noise and a real page-height cost on the section being shortened.
- **D-34:** Copy leads with **"grouping doesn't remove cards from the pool"** — assigning a card to
  a package leaves it in the pool and cuttable unless the package is locked. What a package *does*
  (lock/unlock together) comes second.

### Claude's Discretion
- **Theme-fork CSS strategy for the sticky nav.** Not discussed. Constraint to respect: the sticky
  element needs an opaque background or content scrolls through it, and 11 standalone theme forks
  load **after** `site-common.css`, so equal-specificity rules lose on source order. Prior sessions
  hit both the transparent-chip trap and `--theme-surface` reading light in dark themes — prefer
  `--panel` / `--panel-soft-bg` with explicit fallbacks, as `.manabase-anchor-nav` already does.
- Exact anchor label wording, pill ordering, and section id naming.
- Search matching semantics (case-insensitive substring on card name is assumed).
- Precise help-block and hint wording, within the D-34 angle.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase source and scope
- `.planning/milestones/ws-cut-lab-2026-07-23/BACKLOG-cut-lab-followups-2026-07-22.md` — items 3 and
  4 are the origin of this phase; item 3 documents the ~14,000px mobile scroll, the
  step-tabs-don't-jump defect, and why the shared partial must be handled carefully.
- `.planning/ROADMAP.md` — Phase 110 and the newly inserted Phase 110.1 with split rationale.
- `.planning/REQUIREMENTS.md` — CLUP-06/07/08/11/12/13/14/15/16 text and traceability table.

### Approved design specs
- `.planning/milestones/ws-cut-lab-2026-07-23/captures/cardtext-per-card-lock-spec.md` — the approved
  2026-07-20 card-text design. **Phase 110 implements its pool-row `<details>` half only.** Its
  "Invariants (must not regress)" section (§Invariants) applies in full. Its popover half is
  deferred; its chip-lock half already shipped in Cycle 18.

### Codebase patterns to follow
- `DeckFlow.Web/Views/Deck/Manabase.cshtml:420-431` — the anchor-nav markup idiom to pattern after.
- `DeckFlow.Web/wwwroot/css/site-common.css:2970-3006` — `.manabase-anchor-nav` styling and token use.
- `DeckFlow.Web/wwwroot/css/site-common.css:104-111` — the global `scroll-margin-top: 4rem` rule that
  bounds the sticky nav height (D-05).
- `DeckFlow.Web/wwwroot/ts/primer-selection.ts:27` — the `deckflow.*.sections.*` localStorage naming
  convention.
- `DeckFlow.Web/wwwroot/ts/admin-harvest.ts:174` — the `prefers-reduced-motion` scroll pattern.

### Files this phase modifies
- `DeckFlow.Web/Views/Deck/CutLab.cshtml`
- `DeckFlow.Web/wwwroot/ts/cut-lab.ts`
- `DeckFlow.Web/wwwroot/css/site-common.css`
- `DeckFlow.Web/Models/CutLabViewModel.cs`
- Plus the Cut Lab test files (`cut-lab-*.test.ts`, `CutLabPageServiceTests`, `cut-lab-*.spec.ts`)

### Project constraints
- `CLAUDE.md` — theme system (layout CSS in `site-common.css`, never `site.css`), LF line endings,
  changed-lines format gate, UI testing must never open a browser on the Windows host.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `.manabase-anchor-nav` (`site-common.css:2970`) — anchor-list styling with theme tokens and a
  640px single-column rule. Pattern source for the new nav; note it is **not** sticky today.
- `cutlab-collapsible` + `data-cutlab-mobile-collapse` `<details>` — already wraps Packages,
  Scenarios, and What-if swap (`CutLab.cshtml:354`, `:813`, `:835`). CLUP-13 extends this pattern
  rather than inventing one.
- `getLocalStorage()` / `isQuotaExceededError` (`cut-lab.ts:254-263`) — storage wrappers from the
  Phase 104 scenario work.
- `RoleListByCardName` / `RoleKeysByCardName` on `CutLabViewModel` — the established view-only
  lookup-dictionary pattern that D-28 mirrors.
- Structural evidence chips are **already** lockable `<button>` elements with `aria-pressed` and
  `data-cut-lab-chip-card` (`CutLab.cshtml:474-520`, handler at `cut-lab.ts:2907`) — CLUP-18's
  lock-behavior half is done; only combo context is missing (Phase 110.1).

### Established Patterns
- **Single canonical lock surface** (Phase 102 "Pitfall 8"): the pool-table checkbox is the source of
  truth; chips and rows are reflections. Filtering must not create a second competing state.
- **Server-authored state** (Phase 108): the client renders patch DTOs rather than re-deriving
  domain rules. D-04 and D-28 both follow from this.
- **Progressive enhancement**: every JS feature must degrade to a working no-JS path.
- **Theme-fork source order**: `site-common.css` loads before 11 standalone theme forks, so
  equal-specificity rules lose. Compound selectors required to win.

### Integration Points
- `getPoolRows()` / `buildSnapshotFromDom()` (`cut-lab.ts:490`, `:910`) — the serialization path
  filtering must not break (D-13).
- `_WorkflowStepTabs.cshtml` — read-only for this phase; its `SubmitFormId` distinction is consumed
  (D-04) but the partial is not edited.
- `data-scroll-on-load` handling lives in `deck-sync.ts:2205-2218`, not `cut-lab.ts` — relevant if
  scroll behaviors interact.

</code_context>

<specifics>
## Specific Ideas

- The user supplied Commander Spellbook screenshots showing a **template slot** ("Noncreature
  permanent castable for {0}" → *View 37 Cards*) and a **"Variants of this combo"** view (4 variants
  sharing Valley Floodcaller + Banishing Knack, differing only in the third card, each with EDHREC
  deck counts). These drove the Phase 110.1 split and are recorded in that phase's scope — **not**
  Phase 110's.
- Verified during discussion: `Services/CommanderSpellbookService.cs:251-290` parses only `uses`,
  `produces`, and `description` (plus `popularity`/`manaValueNeeded`). It drops `requires` entirely
  and has no concept of variant grouping. Phase 110.1's researcher must confirm the API shape.

</specifics>

<deferred>
## Deferred Ideas

- **Shared chip popover** (the full 2026-07-20 card-text spec): one reused anchored popover element
  with ESC/click-outside dismiss, focus return, viewport clamping, and a mobile bottom sheet. User
  decision: build the text-first minimum now, **put the popover on the backlog**.
- **Spellbook template-slot candidate matching** — enumerating a template's candidate cards (the "37
  cards") and checking them against the pool. Deferred until Phase 110.1 research confirms the API
  shape.
- **Combo-aware cut-round ranking** — "if at least one full combo is complete, don't recommend
  cutting it in round 1" as an *engine* rule in `CutLabCutRoundEngine`. Phase 110.1 ships **advisory
  copy only**; changing proposal ranking is its own phase.
- **Full variant browser** — per-variant results and EDHREC deck counts inline, mirroring the
  Spellbook page. That data may not be in our response at all.

</deferred>

---

*Phase: 110-cut-lab-navigation-and-pool-discovery*
*Context gathered: 2026-07-23*
