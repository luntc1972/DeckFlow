# UI-SPEC — Phase 76: Bracket Classifier + Balancer (`/bracket`)

**Status:** Design contract (initial UI). NET-NEW flag-gated tool tile (`tool.bracket.enabled`, seeded OFF).
**Mockup:** `.planning/ui-design/cycle13/phase76-bracket-mockup.html`
**Closest analog:** `Views/Deck/Manabase.cshtml` (deck-input → pills → result with chip/verdict/list).
**Requirements covered:** BRACKET-01 (auto-classify), BRACKET-03 (target + floor-violations + starter cuts), BRACKET-05 (effective-date stamp + re-confirm). BRACKET-02 (data migration to Core) and BRACKET-04 (3-variant parity) are non-UI / prompt-text concerns referenced where they touch the page.

---

## 1. Surface overview

A new standalone tool page at `/bracket`, registered in the tool system exactly like `/manabase`. It adds:

- **One new tool page** — `Views/Deck/Bracket.cshtml` (`@model BracketViewModel`), reachable at `/bracket`, included in the Deck workflow tab strip via `_DeckToolTabs`.
- **One new Home tile** — auto-rendered in the **Analyze** section of the hub grid once the registry entry exists and `tool.bracket.enabled` is ON. Non-primary tile (`isPrimaryTile:false`).
- **One new nav entry** — appears in the `_DeckToolTabs` strip (Analyze section) alongside Deck Analysis / Deck Comparison / cEDH Meta Gap / Mana Base.
- **Optional help topic** — `Views/Help/Bracket.*` behind `helpSlug:"bracket"`.

Flag seeded **OFF** in prod → the tile is absent from Home, the tab is absent from the strip, and `/bracket` is unreachable. With the flag off, every other page must render **byte-identical** to today (the registry filters disabled tools before render; no markup leaks).

The page is a deck-evaluation tool whose deliverable is a paste artifact (per Core Value): it classifies the deck into the official 5-tier bracket and, when a target is chosen, emits a balancer prompt the user pastes into ChatGPT / Claude / Gemini for fair-swap refinement.

---

## 2. Tool-registration checklist (UI integration list)

Mirror the `/manabase` registration end-to-end:

1. **`Services/Tools/ToolRegistry.cs`** — add to `Definitions`:
   ```
   Create("bracket", "Bracket", "/bracket", ToolNavSection.Analyze,
       "tool.bracket.enabled", false /*core*/,
       "Bracket Check",
       "Classify a Commander deck into its official 1-5 bracket from Game Changers, two-card combos, and mass-land-denial — then generate a balancer prompt to hit a target bracket. No tutor-counting.",
       "bracket", DeckPageTab.Bracket, false /*isPrimaryTile*/),
   ```
   Place it in the Analyze block, after `manabase` (keeps Analyze section grouped). `core:false` so it is flag-gated and OFF-able like Mana Base.
2. **`Models/DeckPageTab.cs`** — add `Bracket = 15,` (next free value; do not renumber existing members — the enum has gaps already, e.g. 6 is unused, but append at 15 to avoid churn).
3. **`Controllers/BracketController.cs`** — new controller; GET `/bracket` renders the empty form, POST `/bracket` classifies + (if target chosen) builds the balancer artifact. Set `Model.ActiveTab = DeckPageTab.Bracket`. Follow `ManabaseController` shape (load vs analyze can collapse to a single POST since there are no cost-override pre-detection steps).
4. **`Views/Deck/Bracket.cshtml`** — `@model BracketViewModel`; render `_BusyIndicator` + `_DeckToolTabs` (passing `Model.ActiveTab`) per the page shape in §4.
5. **`Views/Shared/_ToolTileIcon.cshtml`** — add a `case "bracket":` SVG (a layered/tiered bars glyph — five ascending steps, matching the 20×20 stroke convention of the other icons). Proposed:
   ```
   <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><line x1="4" y1="16" x2="6" y2="16"/><line x1="8" y1="13" x2="10" y2="13"/><line x1="12" y1="10" x2="14" y2="10"/><line x1="16" y1="7" x2="16" y2="7"/><polyline points="4,16 6,16 6,13 10,13 10,10 14,10 14,7 17,7"/></svg>
   ```
   (`IconKey` defaults to the registry `key`, so `"bracket"` resolves automatically.)
6. **Flag** — register `tool.bracket.enabled` in the flag seed (the namespaced `tool.*` convention from the flag-key namespacing work), default **FALSE** in prod state, with the idempotent migration carrying any operator toggle across deploys.
7. **Help (optional)** — `Views/Help/Bracket.cshtml` + markdown; wire `helpSlug:"bracket"`.

No changes needed to `_DeckToolTabs.cshtml` itself — it iterates the registry; the new tab appears once registered. The Home tile likewise auto-renders from the registry.

---

## 3. ASCII wireframe — full `/bracket` page

```
┌──────────────────────────────────────────────────────────────────────┐
│ HERO                                                                   │
│  Bracket Check                                                         │
│  Classify a Commander deck into its official 1-5 bracket and balance   │
│  it toward a target — computed locally, no AI needed to classify.      │
│  ▸ How it works  (details.hero-detail)                                 │
├──────────────────────────────────────────────────────────────────────┤
│ _DeckToolTabs  [ Deck Analysis | Comparison | Meta Gap | Mana Base |   │
│                  Bracket‹active› | … ]                                  │
├──────────────────────────────────────────────────────────────────────┤
│ error-banner (hidden unless ErrorMessage)                              │
├──────────────────────────────────────────────────────────────────────┤
│ FORM  (form.result-panel, POST /bracket)                               │
│   Input method  [ Use public deck URL ▾ ]                              │
│   Archidekt or Moxfield deck URL  [ https://moxfield.com/decks/… ]     │
│      (deckflow-bridge hint)                                            │
│   Deck name (optional)  [ Najeela Stax ]                               │
│                                                                        │
│   ┌ fieldset.manabase-segmented — "Target bracket (optional)" ──────┐  │
│   │  ( ) B1 Exhibition  ( ) B2 Core  (•) B3 Upgraded               │  │
│   │  ( ) B4 Optimized   ( ) B5 cEDH                                 │  │
│   │  Leave unset to just classify. Pick a target to get cut         │  │
│   │  suggestions to reach it.                                        │  │
│   └─────────────────────────────────────────────────────────────────┘ │
│                                                                        │
│   [ Classify deck ]  [ Start over ]                                    │
├──────────────────────────────────────────────────────────────────────┤
│ RESULT  (section.result-panel, data-scroll-on-load)                    │
│                                                                        │
│   ┌ .bracket-badge.bracket-badge--b5 ─────────────────────────────┐    │
│   │  THIS DECK CLASSIFIES AS                                       │    │
│   │   B5   cEDH                                                    │    │
│   │  Metagame-tuned competitive Commander. Games can end any turn. │    │
│   └────────────────────────────────────────────────────────────────┘   │
│                                                                        │
│   Target: B3 Upgraded — this deck is 2 brackets over.                  │
│                                                                        │
│   WHY THIS BRACKET  (.manabase-verdict.manabase-verdict--issues)       │
│    • 6 Game Changers (B5 has no cap; B3 allows ≤ 3)                     │
│    • 1 two-card win combo: Thassa's Oracle + Demonic Consultation       │
│    • 1 mass land denial: Armageddon                                    │
│    • 0 extra-turn loops · 0 mass extra-card draw                        │
│    (NOT counted: tutors — removed from the official rubric Oct 2025)   │
│                                                                        │
│   FLOOR VIOLATIONS — exceed target B3   (.bracket-violation rows)       │
│    ⬡ Mana Crypt              [Game Changer]                            │
│    ⬡ The One Ring            [Game Changer]                            │
│    ⬡ Jeweled Lotus           [Game Changer]                            │
│    ⬡ Cyclonic Rift           [Game Changer]                            │
│    ⬢ Thassa's Oracle         [Combo half]                              │
│    ⬢ Demonic Consultation    [Combo half]                              │
│    ▦ Armageddon              [Mass land denial]                        │
│                                                                        │
│   STARTER CUTS to reach B3  (.manabase-verdict-list)                   │
│    • Cut Thassa's Oracle or Demonic Consultation (breaks the win combo)│
│    • Cut Armageddon (no mass land denial at B3)                        │
│    • Cut 3 of: Mana Crypt, Jeweled Lotus, The One Ring, Cyclonic Rift  │
│      (B3 allows ≤ 3 Game Changers; you run 6)                           │
│                                                                        │
│   ┌ .bracket-stamp ──────────────────────────────────────────────────┐ │
│   │  Game Changers list effective 2025-10-08. The pasted prompt asks  │ │
│   │  the AI to re-confirm current membership before swapping.          │ │
│   └────────────────────────────────────────────────────────────────────┘│
│                                                                        │
│   ▸ Want fair swaps? Copy this prompt for ChatGPT / Claude / Gemini     │
│      (details.result-panel.nested-panel)                               │
│        [ Copy ]   ┌ textarea (readonly) — balancer artifact ─────────┐ │
│                   │ You are refining a Commander (EDH) deck from      │ │
│                   │ bracket 5 down to bracket 3 … (3 decoupled        │ │
│                   │ ChatGpt/Claude/Gemini variants) …                 │ │
│                   └───────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────────────┘
 ▸ How brackets are determined  (details.result-panel.nested-panel, always shown)
```

---

## 4. Page shape (slot order)

`.hero` (h1 + `.lede` + `details.hero-detail`) → `_BusyIndicator` → `_DeckToolTabs` → `.error-banner` → `form.result-panel` (input-method select · URL/paste field · deck name · **target-bracket `manabase-segmented` pills B1-B5** · actions `Classify deck` + `Start over`) → `section.result-panel` result (bracket badge · target line · why-this-bracket verdict · floor-violations · starter cuts · effective-date stamp · copy-prompt collapsible) → always-on `How brackets are determined` methodology `details`.

---

## 5. Components reused (exact classes) + net-new

### Reused as-is (no new CSS)
| Slot | Classes |
|------|---------|
| Page skeleton | `.hero`, `.lede`, `details.hero-detail`, `.result-panel`, `.toolbar` |
| Deck input | `.field` + `label` + `select[data-df-select]` + `input[type=url]` + `textarea`; `_DeckFlowBridgeHint` partial; `data-sync-panel` show/hide pattern (copy verbatim from Manabase) |
| Target picker | `fieldset.manabase-segmented` > `legend` + `.manabase-pills` > `label.manabase-pill` > `input[type=radio]` + `span`; `.manabase-help` caption |
| Reasons / cuts lists | `.manabase-verdict` + `--issues` / `--fine`, `.manabase-verdict-heading`, `.manabase-verdict-list` |
| Status chips | `.manabase-chip` + `.manabase-health--{excellent,solid,workable,needswork}` (reused for the "meets target" success state and small inline tags) |
| Tab strip | `_DeckToolTabs` (`.tool-nav`) |
| Buttons | `.run-button` (Classify), `.clear-cache-button` (Start over), `.copy-button` (Copy prompt) |
| Copy artifact | `details.result-panel.nested-panel` > `summary` + `.panel-heading` + `.copy-button[data-copy-target]` + `textarea[readonly]` (verbatim from Manabase swap-prompt block) |
| Notices | `.error-banner role=alert`; `.deck-restored-notice` shape only if a "deck changed" stale banner is later added (not required Phase 76) |
| Disclosure | `details` for "How it works" and "How brackets are determined" methodology |

### Net-new classes (3 base, tokenized — defined in `site-common.css` under a `/* Phase 76 — bracket tool */` block; previewed inline in the mockup)

1. **`.bracket-badge`** — the prominent classified-tier block (bigger than a chip; this is the page's headline verdict). Soft card with a 4px left accent and a large tier label.
   - children: `.bracket-badge__eyebrow` (uppercase muted "THIS DECK CLASSIFIES AS"), `.bracket-badge__tier` (the `B5` token, `--fs-2xl`, bold), `.bracket-badge__name` (the tier name, e.g. "cEDH"), `.bracket-badge__meta` (the one-line description / turn expectation, `--muted`).
   - level modifiers `--b1 … --b5` set the accent color by reusing the **existing** health palette tokens so the five tiers read consistently with the rest of the app and stay legible on every theme:
     `--b1`→success/`#166534`, `--b2`→`#1d4ed8`, `--b3`→accent-strong, `--b4`→`#f59e0b`, `--b5`→`#b91c1c`. Border/background derived via `color-mix(... 14%, transparent)` over `--panel-soft-bg`, matching `.manabase-chip` construction. No raw colors outside these (all already used by `.manabase-health--*`).

2. **`.bracket-violation`** — one floor-violation row: a flex row (`space-between`, soft separator using `--line`) with the card name on the left and a category **tag pill** on the right.
   - children: `.bracket-violation__name`, `.bracket-violation__tag` (small pill, `border-radius:999px`, `--fs-xs`, uppercase) with kind modifiers `--gamechanger`, `--combo`, `--mld` (mass land denial), `--extraturns`, `--extracards`. Tag colors map to the same tokenized palette (Game Changer = warning, Combo = danger, MLD/extra = info), each via `color-mix` so it tints to the theme.
   - wrapped in `.bracket-violation-list` (an `<ul>` reset; the wrapper is layout-only, no color).

3. **`.bracket-stamp`** — the effective-date + re-confirm advisory line (BRACKET-05). Small muted note with a left rule (`--accent` 3px), like a quieter `.bracket-callout`. `--fs-xs`, `--muted`. (`.bracket-callout` already exists and could be reused; `.bracket-stamp` is its lighter sibling so the stamp doesn't compete visually with the badge.)

> Tokenized rule: every net-new color references an existing `var(--…)` token or one of the four baked health hex values already shipped for `.manabase-health--*`. No new raw colors are introduced. Layout/cross-cutting CSS lives in `site-common.css`; theme files are not forked for Phase 76.

---

## 6. States

| State | Trigger | Rendering |
|-------|---------|-----------|
| **Flag OFF** | `tool.bracket.enabled` false | Tool absent everywhere: no Home tile, no tab, `/bracket` unreachable. All other pages **byte-identical** to pre-Phase-76. |
| **Empty** | GET `/bracket`, no submit | Hero + tabs + form only. No result panel. Target pills all unset. |
| **Classified, no target** | POST with a deck, no target pill chosen | `.bracket-badge--bN` + "WHY THIS BRACKET" reasons. **No** floor-violations / cuts panels. Effective-date stamp shown. Copy-prompt = a classification-confirmation artifact ("here is the auto-classification; re-confirm the bracket and Game Changers membership"). |
| **Target selected, violations** | POST with deck classifying **above** target | Full result: badge + "N brackets over" line + reasons + **floor-violations** + **starter cuts** + stamp + balancer copy-prompt. (Primary mockup scenario: B5 deck, target B3.) |
| **At or below target** | Deck classifies **≤** target | Badge + success line "Meets your B3 target — nothing to cut." rendered via `.manabase-verdict--fine` / `.manabase-health--excellent` chip. No floor-violations, no cuts. Copy-prompt = a lighter "confirm and tighten" artifact, not a cut list. |
| **Combo data unavailable** | Commander Spellbook returns null (graceful-degradation, same as `CommanderSpellbookService.FindCombosAsync` → null) | Classification still renders from Game Changers + MLD/extra-turn/extra-card detection. A `role="note"` disclosure under the reasons: "Combo detection is temporarily unavailable, so a two-card win combo could push this deck a bracket higher than shown — the pasted prompt asks the AI to double-check combos." Never silently claim "no combos." |
| **Error** | Import/parse failure | `.error-banner` populated; form re-rendered with submitted values; no result panel. Distinguish `OperationCanceledException` (timeout copy) from generic upstream failure, matching controller convention. |

---

## 7. Exact microcopy

**Hero h1:** `Bracket Check`
**Hero lede:** `Classify a Commander deck into its official 1-5 bracket and balance it toward a target — the classification is computed locally, no AI needed.`
**Hero "How it works" body:** `The official bracket system (effective late 2025) sorts decks by what they do, not how strong each card is: how many Game Changers they run, whether they pack a two-card win combo, and whether they lean on mass land denial, extra turns, or mass extra-card draw. Tutors are no longer counted. Pick a target bracket and we list the specific cards pushing you over it, plus starter cuts — then a copy-ready prompt has an AI refine them into fair swaps.`

**Target picker legend:** `Target bracket (optional)`
**Target picker help:** `Leave unset to just classify. Pick a target to get the cards that exceed it plus suggested cuts.`

**Bracket tier names + descriptions** (migrated from `CommanderBracketCatalog`, BRACKET-02 — used in pills, badge, and prompt):
- **B1 — Exhibition:** `Theme-first showcase decks; optimization takes a back seat. Expect 9+ turns before a win or loss.`
- **B2 — Core:** `Unoptimized, straightforward decks with incremental, disruptable wins. Expect 8+ turns.`
- **B3 — Upgraded:** `Strong synergy and card quality with meaningful interaction; explosive but earned wins. Expect 6+ turns.`
- **B4 — Optimized:** `Fast, lethal, efficient decks with Game Changers, fast mana, and explosive lines. Expect 4+ turns.`
- **B5 — cEDH:** `Metagame-tuned competitive Commander built for maximum efficiency and consistency. Games can end any turn.`

**Badge eyebrow:** `THIS DECK CLASSIFIES AS`
**Target-comparison line (over):** `Target: B3 Upgraded — this deck is 2 brackets over.`
**Target-comparison line (meets):** `Meets your B3 Upgraded target — nothing to cut.`

**Reasons heading:** `WHY THIS BRACKET`
**Reasons items (example):**
- `6 Game Changers (B5 has no cap; B3 allows up to 3).`
- `1 two-card win combo: Thassa's Oracle + Demonic Consultation.`
- `1 mass land denial: Armageddon.`
- `0 extra-turn loops · 0 mass extra-card draw.`
**Reasons footnote:** `Not counted: tutors — removed from the official bracket rubric in October 2025.`

**Floor-violations heading:** `FLOOR VIOLATIONS — cards that exceed B3`
**Tag labels:** `Game Changer`, `Combo half`, `Mass land denial`, `Extra turns`, `Mass card draw`

**Starter-cuts heading:** `STARTER CUTS to reach B3`
**Starter-cuts items (example):**
- `Cut Thassa's Oracle or Demonic Consultation — breaks the two-card win.`
- `Cut Armageddon — no mass land denial at B3.`
- `Trim 3 of: Mana Crypt, Jeweled Lotus, The One Ring, Cyclonic Rift — B3 allows up to 3 Game Changers; you run 6.`
**Cuts caption:** `A starting point, not a verdict — the prompt below has an AI turn these into power-equivalent swaps.`

**Effective-date stamp (BRACKET-05):** `Game Changers list effective 2025-10-08. The copied prompt asks the AI to re-confirm current Game Changers membership before suggesting swaps, so a stale list degrades gracefully instead of misclassifying silently.`

**Combo-unavailable note:** `Combo detection is temporarily unavailable, so a two-card win combo could place this deck a bracket higher than shown. The copied prompt asks the AI to double-check for combos.`

**Copy-prompt summary (over target):** `Want fair swaps? Copy this prompt for ChatGPT / Claude / Gemini`
**Copy-prompt sub:** `The cuts above are a starting point. Paste this to have an AI replace each over-bracket card with a power-equivalent legal swap and re-confirm the bracket.`

**Buttons:** `Classify deck` · `Start over` · `Copy`

**Always-on methodology summary:** `How brackets are determined`

---

## 8. Theme tokens

Use only the documented tokens: `--bg`, `--panel`, `--panel-soft-bg`, `--ink`, `--muted`, `--line`, `--accent`, `--accent-strong`, `--warning`, `--error`, `--info`, `--success`, `--danger`, `--focus`, `--on-accent`, and the type scale (`--fs-xs … --fs-2xl`). The five bracket-level accents reuse the four `.manabase-health--*` baked hex values plus `--accent-strong` (B3), so the badge and tags read consistently across all 22 themes with AA contrast. No theme file is forked; no raw color outside the existing health palette.

## 9. Responsive

- Form fields and pills inherit the existing single-column-on-mobile behavior already defined for `.manabase-pill` (`site-common.css` ~line 2808 media query) — no new rules.
- `.bracket-badge`: tier token and name stack with the eyebrow at all widths (it is already a vertical block); on narrow screens the `__meta` line wraps under.
- `.bracket-violation`: the name + tag flex row wraps the tag beneath the name below ~480px (`flex-wrap:wrap`), so long card names never clip the tag pill.
- Copy-prompt `textarea` is full-width and scrolls; matches Manabase.

## 10. Accessibility

- Result panel `data-scroll-on-load` + `role="status"` on the post-submit landing summary (same as Manabase loaded-hint).
- `.bracket-badge` has `aria-label="Classified as bracket 5, cEDH"` so the visual `B5` token is announced in full.
- Floor-violation tag pills carry their kind in text (`Game Changer`, etc.) — not color-only — satisfying non-color-reliance.
- Target pills are a real `fieldset[role=radiogroup] > legend`; checked state is keyboard-reachable and uses the `:has(> input:checked)` styling already shipped; `:focus-visible` outline `--focus` inherited.
- Reasons/cuts are semantic `<ul>` lists; the combo-unavailable disclosure is `role="note"`.
- Copy button announces success via the existing copy-to-clipboard live-region behavior.

---

## 11. Open questions

1. **Control-axis dependency (Phase 77 SCORE).** The multi-axis score (Phase 77) needs an interaction/removal classifier (SCORE-02 "Control"). The bracket tool already detects MLD / extra-turns / extra-cards. Should the bracket classifier's category-detection live in the same Core service the score will consume, to avoid two divergent "what counts as interaction/MLD" definitions? Recommend: build the detector in `DeckFlow.Core` (per BRACKET-02 migration) with a shared shape Phase 77 imports. Flagged for plan-phase.
2. **Combo-timing threshold.** The official rubric distinguishes "early-game two-card combo" (pushes to B4/B5) from a combo that only assembles very late. Commander Spellbook gives combo presence but Phase 76 may not have reliable turn-timing. Decision needed: does any two-card win combo force ≥ B4, or only "compact/early" ones? Default for the mockup: any detected two-card *win* combo counts; surface it as a reason and let the AI prompt nuance timing. Confirm in discuss/plan.
3. **Two-target affordance.** Should the page also let the user pick the *deck's intended* bracket separately from the *balance target* (i.e. "I built this as B4 but it's classifying B5")? Phase 76 scope = single target picker (classify vs target). Noted as possible future enhancement; out of scope now.
