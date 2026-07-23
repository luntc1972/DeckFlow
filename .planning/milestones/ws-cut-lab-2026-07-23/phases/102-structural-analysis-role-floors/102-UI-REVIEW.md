# Phase 102 — UI Review

**Audited:** 2026-07-19
**Baseline:** `.planning/workstreams/cut-lab/phases/102-structural-analysis-role-floors/102-UI-SPEC.md` (approved design contract)
**Screenshots:** captured (inherited from 102-05's phase-execution sweep — `.planning/ui-design/cut-lab/screenshots/structure-{classic,azorius,nyx}-{desktop,mobile}.png`, 6 files, all dated 2026-07-19 same-session as this audit). A dev server was live at `localhost:5173` during this audit; the existing 3-theme × 2-viewport sweep was used as the evidence baseline instead of re-capturing, since it already covers the full matrix the UI-SPEC's Validation Notes require and was pixel-inspected directly (crops + color sampling) as part of this review.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Every prescribed heading/sub-line/CTA/empty-state string matches the UI-SPEC verbatim, but the "weak floor cases" finding reads as nonsensical at count=0 ("Payoffs is at 0 ... every card in this role is effectively protected already") and dominates the findings panel (6 of 7 rendered findings are this one type). |
| 2. Visuals | 2/4 | Commander-lock badge and helper text visibly overlap/collide on mobile in the pool table's "Card" cell, across all 3 audited themes — a legibility-breaking defect. The intended post-import "focal point" (findings count chip, A8) is visually indistinguishable from the pre-existing lock-count chip, so no chip actually reads as elevated. |
| 3. Color | 3/4 | Accent (`--accent`) and gold-warning tokens are used exactly where the contract reserves them (verified by pixel sampling in the Nyx dark theme: gold border = rgb(200,160,64), matching `#c8a040`) and zero new `--danger` surfaces exist — but with 6/7 rendered findings sharing the identical gold-warning treatment, the findings panel becomes one long monochrome block with no differentiation, undercutting the 60/30/10 intent for this panel. |
| 4. Typography | 4/4 | Zero new font sizes or weights — only `--fs-base`/`--fs-sm` at 400/600 are used anywhere in the new CSS, matching the contract's Body/Label roles exactly. |
| 5. Spacing | 2/4 | Pixel-measured the "Lock all {role}" pill at ~21px tall on the rendered desktop page — under half the UI-SPEC's explicit 44×44px minimum touch target, which names "per-group 'Lock all {role}' pills" specifically. All other new controls (accordion summary, floor input, reset button) do carry the required `min-height: 44px`. |
| 6. Experience Design | 4/4 | Full state coverage confirmed: fail-open degradation lines for both combo and category data (rendered, not banners), per-role empty state, all-clear zero-findings state, disabled+explained commander checkbox, preserved `window.confirm()` on package delete, reversible no-confirm floor reset — all present in code and screenshots. |

**Overall: 18/24** *(cross-AI adjusted: 17/24 — see Cross-AI Verification Addendum)*

---

## Top 3 Priority Fixes

1. **Commander badge/helper-text overlap on mobile (all 3 themes)** — In the pool table's "Card" cell, `<span class="kb-chip cutlab-lock-badge--commander">Commander · Always locked</span>` renders on top of / overlapping `<div>Your commander is always locked and can't be cut.</div>` on the mobile responsive-table layout (confirmed in `structure-classic-mobile.png`, `structure-azorius-mobile.png`, `structure-nyx-mobile.png` — all three show the same collision). Root cause: the generic responsive-table rule at `site-common.css:1230` (`td:not([data-label="Select"]):not([data-label="#"]):not([data-label="Player"]) { display: grid; grid-template-columns: 6.5rem 1fr; }`) assumes each `<td>` holds one label:value pair, but the commander "Card" cell holds three children (`<strong>`, badge `<div>`, helper-text `<div>`) that get auto-placed into the 2-column grid and collide. Fix: wrap the commander badge + helper text in a single child element with `grid-column: 1 / -1;` (or give `.cutlab-lock-badge--commander`'s containing divs their own `grid-column: 1/-1` rule) so they stack in their own full-width row instead of auto-flowing into the label column. This predates Phase 102 (the badge markup at `CutLab.cshtml:206-212` was untouched by this phase) but is directly adjacent to and visible within the same row Phase 102 extended with the role-list cell, and it breaks legibility of the single most safety-critical message on the page (commander cannot be cut) on every mobile theme.

2. **"Lock all {role}" pill misses the 44px touch-target minimum the contract mandates by name** — UI-SPEC Spacing Scale exceptions explicitly list "per-group 'Lock all {role}' pills" among the controls requiring a 44×44px minimum hit area. Pixel-measuring the rendered "Lock all interaction" pill in `structure-classic-desktop.png` gives a height of ~21px (top border ≈ y=3202, bottom border ≈ y=3223 in the full-page screenshot) — under half the required minimum, and confirmed by CSS: `.cutlab-role-group__body .manabase-pill` (`site-common.css:4178-4180`) only sets `margin-bottom`, with no `min-height` override, unlike the other three new touch targets in this phase (`.cutlab-role-group__summary` at `:4163`, `.cutlab-floor-reset` at `:4231`, `input[data-cut-lab-floor]` at `:4241`, all of which correctly carry `min-height: 44px`). Fix: add `min-height: 44px; display: inline-flex; align-items: center;` (or equivalent) to `.cutlab-role-group__body .manabase-pill`.

3. **Findings panel reads as one repetitive block, and the intended new "focal point" doesn't visually differentiate itself** — Two related issues in the "Structural findings" section: (a) 6 of the 7 findings rendered in the captured fixture pool are all "Weak floor cases" with the identical static heading, back-to-back, including a nonsensical instance at count=0 ("Payoffs is at 0 against a floor of 6 — every card in this role is effectively protected already" — zero cards cannot be "protected"); (b) the UI-SPEC's stated new focal point ("Focal point of the results view is the findings summary chip... supersedes the 101 focal point," A8) is rendered with the exact same `.prompt-size-note` muted small-text class as the pre-existing "105 cards in pool · 1 locked" chip above it — nothing distinguishes it as elevated. Fix: special-case zero-count phrasing in the weak-floor-case finding generator (e.g., "{Role} has no cards yet against a floor of {floor} — nothing here to protect."), and give the findings-count chip a distinguishing accent treatment (color or weight) if the "new focal point" intent from A8 is meant to be felt, not just asserted in the contract prose.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)

**What matches exactly (verified against `102-UI-SPEC.md` Copywriting Contract via screenshot + `CutLab.cshtml` read):**
- Section headings: "How your pool competes" (`CutLab.cshtml:310`), "Structural findings" (`:356`), "Role floors" (`:405`) — all present, exact case.
- All three sub-lines match verbatim (`:311`, `:357`, `:406`).
- Findings chip copy `"{n} structural findings"` (`:362`) and zero-findings all-clear text (`:388`) match verbatim.
- Both degradation notices (`:393`, `:398`) match verbatim, rendered as muted inline lines, never a banner — correct per the "fail-open, never a confident false-negative" contract (Pitfall 6).
- Per-group empty state `"No {role} cards detected in this pool."` (`:341`) matches.
- Interaction help line (`:346`) and the amended play-experience help copy (`:101`) both match verbatim — closing 101-VERIFICATION open item 3 as the plan claimed.
- "Recalculate analysis" CTA label (`:455`) matches; "Reset to default" (`:447`) matches.
- Weak-floor-case lead pattern confirmed rendering exactly as specified: *"Ramp is at 2 against a floor of 12 — every card in this role is effectively protected already."* (screenshot, all 3 themes).

**Deductions:**
- The weak-floor-case copy is nonsensical at count=0 (screenshot: *"Payoffs is at 0 against a floor of 6 — every card in this role is effectively protected already."* and *"Win conditions is at 0 against a floor of 3 — every card in this role is effectively protected already."*). Zero cards cannot be "effectively protected already" — there is nothing to protect. This is flagged in the orchestrator's context as a known product call, but it is real, user-visible, and appears twice in the captured fixture.
- 6 of the 7 rendered findings are the identical "Weak floor cases" heading — this is spec-compliant (the UI-SPEC hardcodes that heading regardless of role) but produces a copy experience that reads as one repeated message rather than 6 distinct findings, undercutting the panel's own stated purpose ("Measured observations about your pool's shape").

### Pillar 2: Visuals (2/4)

**Confirmed via direct screenshot pixel inspection (crops in this audit's scratch directory):**
- **BLOCKER-adjacent defect:** the commander lock badge (`kb-chip cutlab-lock-badge--commander`, "Commander · Always locked") overlaps the helper text ("Your commander is always locked and can't be cut.") in the pool table's responsive mobile layout. Reproduced identically in `structure-classic-mobile.png`, `structure-azorius-mobile.png`, and `structure-nyx-mobile.png` — this is a cross-theme, 100%-reproducible mobile bug, not a theme-specific rendering artifact. Desktop is unaffected (table isn't grid-collapsed there). See Top 3 Fix #1 for root-cause analysis.
- The eight role-group accordions, 8-row floor table, and finding blocks all render cleanly with no other overlap, clipping, or truncation issues across all 6 screenshots (3 themes × 2 viewports).
- Icon-only controls (`+`/`×` package-add/cancel buttons) carry `aria-label` (`CutLab.cshtml:300-301`) — correct.
- The A8 "new focal point" intent (findings-count chip supersedes the lock-count chip once results render) is not realized visually: both chips share the identical `.prompt-size-note` class with no differentiating color, weight, or size (`CutLab.cshtml:130` vs `:362`). A user scanning the page has no visual cue that the findings count is now the more important number.
- Visual hierarchy inside the findings panel is flat: all 5 possible finding types (curve congestion, stranded subthemes, redundant finishers, weak floor cases, enabler-starved cards) share the exact same block treatment (gold left-border, same heading size) with no icon or type-marker differentiating them — compounding the repetition problem in Pillar 1 into a visual one as well.

### Pillar 3: Color (3/4)

**Confirmed via CSS read + pixel color-sampling in the Nyx dark theme:**
- `.cutlab-finding` left-border color sampled at rgb(200,160,64) in `structure-nyx-mobile.png`, exactly matching the contract's `var(--gold-warning, var(--warning, #c8a040))` fallback — the advisory idiom resolves correctly across themes, not just in light ones.
- `--accent` is used only in the three declared new places: `.cutlab-role-chip--locked` (locked chip in a role group, `:4188`), `.cutlab-floor-badge--adjusted` (Adjusted badge, `:4223`), plus the inherited `.cutlab-package--locked` (`:4145`) — matches the contract's accent reservation list; not used on card names, counts, or finding text anywhere in the new CSS.
- Zero new `--danger` surfaces added (confirmed: grep count for `var(--danger` in `site-common.css` is unchanged from the plan's acceptance gate, and no finding/floor-state code path references it).
- No hardcoded raw hex/`rgb()` values were introduced outside `var(..., #fallback)` chains, which mirror the exact pre-existing sitewide idiom (same fallback chain reused verbatim, not fabricated).

**Deduction:** because all 5 finding types share one color treatment and the captured fixture triggers the same finding type 6 times in a row, the findings panel is, in practice, a solid block of gold-tinted rows with no color variation to signal "these are different observations" — a real (if data-dependent) departure from a balanced color distribution within that panel, even though every individual color decision matches the contract.

### Pillar 4: Typography (4/4)

- Grep of all new `site-common.css` cutlab rules (`:4141-4244`) shows only two font-size tokens in use: `var(--fs-base)` (role list column, degradation notes, floor source/reset text) and `var(--fs-sm)` (role-group summary, finding heading) — matching the contract's Body (0.95rem/400) and Label (0.85rem/600) roles exactly, with zero new sizes introduced.
- Only one new weight value (`600`) appears, applied consistently to Label-role text (summary, finding heading, at-floor marker) — matches the contract; no new weight classes invented.
- No inline `style="font-size:..."` or arbitrary Razor-injected typography found in `CutLab.cshtml`.

### Pillar 5: Spacing (2/4)

- **Confirmed violation:** pixel-measured the "Lock all interaction" pill in `structure-classic-desktop.png` — top border at y≈3202, bottom border at y≈3223 (full-page coordinates), giving a rendered height of ~21px. The UI-SPEC's Spacing Scale "Exceptions" section explicitly requires 44×44px minimum on "per-group 'Lock all {role}' pills" by name. CSS confirms the gap: `.cutlab-role-group__body .manabase-pill` (`site-common.css:4178-4180`) sets only `margin-bottom: 0.75rem`, with no `min-height` override — unlike the other three Phase 102 touch targets, all of which correctly declare `min-height: 44px` (`.cutlab-role-group__summary` `:4163`, `.cutlab-floor-reset` `:4231`, `input[data-cut-lab-floor]` `:4241`). No mobile media-query override compensates either (`:3235` only sets `justify-content`).
- Everything else checks out: the sub-scale values used (`0.75rem`, `0.85rem`, `0.35rem`, `0.55rem`) are not on the UI-SPEC's simplified 6-token scale, but they are reused verbatim from pre-existing sitewide conventions already used 20-58 times each elsewhere in `site-common.css` (and `0.85rem` pill padding is explicitly called out in the UI-SPEC itself as "reused verbatim from `.manabase-pill`") — this is precedent-matching, not fabricated arbitrary spacing, so it is not counted as a deviation.
- No `[...]px`/`[...]rem` bracket-arbitrary values were introduced anywhere in the new markup or CSS.
- The other three explicitly-required 44px targets (accordion summary row, floor number input, Reset-to-default button) all correctly implement the minimum.

### Pillar 6: Experience Design (4/4)

- **Loading:** inherits the unchanged busy-indicator flow (`data-busy-*` attributes, `CutLab.cshtml:25-27`) — no new loading-state gap introduced.
- **Error:** top-level `@Model.ErrorMessage` banner unchanged; no new error-banner surfaces added for findings degradation (correctly avoided per the "never an error banner" contract).
- **Fail-open degradation:** both `Model.ComboDataUnavailable` and `Model.CategoryDataUnavailable` render independently as muted inline lines (`:391-399`) — confirmed actually firing in the captured screenshot ("Community category data unavailable — subtheme detection fell back to card-text heuristics only.").
- **Empty states:** per-role-group `"No {role} cards detected in this pool."` (`:341`) and the all-clear zero-findings message (`:388`) are both implemented and reachable.
- **Disabled state:** commander checkbox is `disabled` with adjacent explanatory copy (`:202`, `:211`).
- **Destructive confirmation:** `window.confirm()` preserved for package deletion (`cut-lab.ts:659`) — the one destructive action on this page keeps its guard; the new "Reset to default" floor action correctly has none (reversible value change, matches contract).
- State transitions for floor edits (Adjusted badge show/hide, at-floor marker, Recalculate round-trip) are exercised by the `cut-lab-structure.spec.ts` Playwright suite per the 102-05 summary (14/14 passing), even though the static screenshot sweep didn't happen to capture an "Adjusted" state — functional coverage is present even where visual evidence isn't.

---

## Registry Safety

`components.json` not found in the repository root — shadcn is not initialized. Registry audit skipped per the audit protocol (DeckFlow has no component registry; confirmed by the UI-SPEC's own Registry Safety table: "not applicable — DeckFlow has no component registry; all UI is hand-authored CSS/Razor").

---

## Files Audited

- `DeckFlow.Web/Views/Deck/CutLab.cshtml` (474 lines, full read)
- `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (targeted grep of roleFloors/hasRoleToken/requestSubmit/confirm/getForm surfaces)
- `DeckFlow.Web/wwwroot/css/site-common.css` (lines 484-609, 1113-1263, 2492-2540, 4130-4244 — full read of all new `.cutlab-*` rules plus the responsive-table and pill/chip precedents they reuse)
- `.planning/workstreams/cut-lab/phases/102-structural-analysis-role-floors/102-UI-SPEC.md`
- `.planning/workstreams/cut-lab/phases/102-structural-analysis-role-floors/102-0{1..5}-PLAN.md` (102-04 read in full; others skimmed for scope/interfaces)
- `.planning/workstreams/cut-lab/phases/102-structural-analysis-role-floors/102-0{1..5}-SUMMARY.md`
- Screenshots: `.planning/ui-design/cut-lab/screenshots/structure-{classic,azorius,nyx}-{desktop,mobile}.png` (all 6, viewed full-size plus targeted pixel crops for the commander-overlap and touch-target measurements)

---

## Cross-AI Verification Addendum (Codex gpt-5.5, 2026-07-19)

Independent read-only cross-review by Codex (screenshots attached, all 3 themes × 2 viewports). Full output: dispatched via `codex exec -s read-only`.

### Verdicts on this review's findings

| Finding | Verdict | Evidence |
|---|---|---|
| P1 commander-lock badge/helper mobile overlap | CONFIRMED | 2-col mobile grid `site-common.css:1230`; Card cell has 3 real children `CutLab.cshtml:204`; nowrap `.kb-chip` (`site-common.css:569`) overflows the 6.5rem label column |
| P2 "Lock all {role}" pill below 44px | CONFIRMED | Spec names per-group pills in 44×44 list (`102-UI-SPEC.md:49`); `.cutlab-role-group__body .manabase-pill` (`site-common.css:4178`) adds only margin; base `.manabase-pill` (`:2492`) has no min-height |
| P3 findings repetition + count=0 copy + chip not elevated | CONFIRMED | Weak-floor emitted for `count <= floor+1` incl. 0 (`CutLabStructuralFindings.cs:248`); lead copy unconditional (`:253`); findings count uses same `.prompt-size-note` as lock count (`CutLab.cshtml:129`, `:359`) |
| Minor: copy strings verbatim | CONFIRMED | Headings/CTA verbatim in Razor (`CutLab.cshtml:310/356/405/455`) |
| Minor: all finding types share one gold treatment | CONFIRMED | Single `.cutlab-finding` class (`CutLab.cshtml:371`; `site-common.css:4192`) |
| Minor: typography contract-compliant | CONFIRMED | Token-only sizes/weights (`site-common.css:4149`) — no further deduction |

### Findings Codex added (missed in original audit)

- **MED — bulk "Lock all {role}" does not toggle.** UI-SPEC says click toggles underlying checkboxes (`102-UI-SPEC.md:127`); implementation only sets them `true`, never unlocks (`cut-lab.ts:507`). Selected pill state is a dead-end action.
- **LOW — role pill state not exposed to AT.** No `aria-pressed` in Razor (`CutLab.cshtml:322`); TS toggles `.is-selected` visual only (`cut-lab.ts:267`).
- **LOW — locked evidence chips can't show accent border per spec** (`102-UI-SPEC.md:140`): Razor renders plain `.kb-chip` (`CutLab.cshtml:376`); view model flattens evidence to strings (`CutLabViewModel.cs:94`).

### Score adjustment

Experience Design 4/4 → **3/4** (bulk-lock toggle contract violation + missing state exposure). **Adjusted overall: 17/24.**

### Cross-AI bottom line

Claude's review sound on the three big visual/copy issues; missed one real interaction contract bug. Pre-flag-flip fix list: mobile commander cell collision, 44px role pill target, weak-floor zero-copy/repetition, bulk-role toggle + `aria-pressed`.
