# Phase 74 - UI Review

**Audited:** 2026-06-27
**Baseline:** Abstract 6-pillar standards (no UI-SPEC.md for this phase)
**Screenshots:** Not captured — dev server confirmed running at localhost:5173, but phase 74 is behavior-only; static screenshots of existing form fields do not exercise the new feature. Behavioral proof lives in the Playwright e2e suite (74-02).

---

## Phase Framing

Phase 74 adds client-side sessionStorage persistence that silently prefills **existing** deck-source fields when a user navigates between single-deck tools. It introduces no new visual components, no new CSS, no new page copy, and no new layout. The five wired views (DeckAnalysis, Manabase, CedhMetaGap, DeckConvert, DeckPrimer) add only a `<script>` tag before `deck-sync.js`. All five pillars that cover visual surface (copywriting, visuals, color, typography, spacing) have zero new auditable area and inherit their prior state unchanged. Scoring those pillars numerically would penalize the implementation for things outside its scope; they are marked N/A and excluded from the total.

The only pillar with new surface to grade is **Experience Design**.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | N/A (inherited) | No new in-app copy; README note is clear and scoped correctly |
| 2. Visuals | N/A (inherited) | No new components, layout, or visual elements |
| 3. Color | N/A (inherited) | No CSS changes, no new tokens |
| 4. Typography | N/A (inherited) | No new text elements |
| 5. Spacing | N/A (inherited) | No new margin, padding, or layout |
| 6. Experience Design | 3/4 | Silent prefill contracts solid; DeckPrimer untested; no debounce on per-keystroke writes |

**Effective scored total: 3/4 (one pillar in scope)**

---

## Top 3 Priority Fixes

1. **DeckPrimer is unproven as a source or destination in e2e** — The view has `deck-input-store.js` loaded (74-01 confirmed), but no Playwright test routes to or from `/deck-primer`. The plan required coverage of all five in-scope views. Fix: add a test case `page.goto('/deck-analysis')` + paste deck + `page.goto('/deck-primer')` + assert `textarea[name="DeckText"]` has value, mirroring the manabase test.

2. **No debounce on the `persist` callback** — `setLastDeck` fires synchronously on every `input` event on DeckText and DeckSource. For a user pasting a large deck list near the 100KB cap, each rendered character triggers `TextEncoder().encode(value).length` + `JSON.stringify()` + `sessionStorage.setItem()` inline. At median deck sizes (under 10KB) this is imperceptible. At 80-100KB paste operations it adds measurable synchronous overhead per keystroke. Fix: wrap the `persist` closure in a simple 300ms debounce.

3. **Silent prefill leaves users with no affordance to understand or dismiss the restored value** — The locked CONTEXT decision chose "no notice, no restored banner, no prompt." This is an acceptable v1 tradeoff, but it means a user who navigates to a tool expecting a fresh form silently receives a pre-populated deck with no indication of origin or a one-click way to clear it. The deferred idea in CONTEXT.md ("Restored your last deck — clear" affordance) should be scheduled as a near-term follow-up. Until then, any user who notices the pre-fill has no discoverable explanation.

---

## Detailed Findings

### Pillar 1: Copywriting (N/A — inherited)

No new in-app strings, CTAs, empty states, or error messages were introduced. The feature is silent by design. The README addition at line 334 ("Cross-tool single-deck carry-over") is factually correct, appropriately scoped to single-deck tools, names the deferred two-deck tools, and uses no em/en dashes. No auditable defects in new copy.

### Pillar 2: Visuals (N/A — inherited)

No new components. No new icons, illustrations, or visual hierarchy changes. The form fields being prefilled are existing Razor-rendered inputs that were already styled by prior phases. No auditable defects in new visual surface.

### Pillar 3: Color (N/A — inherited)

Zero CSS added. Zero token changes. No hardcoded hex or rgb values introduced by this phase.

### Pillar 4: Typography (N/A — inherited)

No new font-size or font-weight classes. No new text nodes in any view.

### Pillar 5: Spacing (N/A — inherited)

No new margin, padding, gap, or layout classes. The `<script>` tag additions to the five views introduce no rendered DOM elements.

### Pillar 6: Experience Design (3/4)

**What was delivered and works correctly:**

- Fill-if-empty guarantee is correctly implemented: `restoreSplitFields` guards `(urlValue !== '' || textValue !== '')` and returns early on the first non-empty field — POST-rendered values survive intact. Playwright Test 3 (postback) proves this with `addInitScript` seeding a competing deck before the form POST.
- Both storage calls (`setLastDeck`, `getLastDeck`) are wrapped in independent `try/catch` blocks (lines 27/35 and 41/57). Storage quota, private browsing, and disabled storage degrade silently with no thrown exception.
- `TextEncoder().encode(value).length` is used for accurate byte counting (handles multibyte UTF-8) rather than naive `.length`.
- Script order is correct and documented: `deck-input-store.js` at line 945 in DeckAnalysis, immediately before `deck-sync.js` at line 946. The `// Why:` comment explains the dependency.
- Double-bootstrap guard (lines 155-158: `DOMContentLoaded` listener + immediate call when `readyState !== 'loading'`) prevents a missed boot if the script loads after the document is ready.
- `window.DeckFlow` registration uses `win.DeckFlow = win.DeckFlow ?? {}` (merge, not overwrite), correctly co-existing with `deck-sync.ts`'s own registration.
- Deferred views confirmed clean: `DeckComparison.cshtml` and `DeckSync.cshtml` do not reference `deck-input-store.js`.
- e2e spec has 13 `toHaveValue` assertions across 5 tests; plan required 6+. Tests run on both `chromium-desktop` and `chromium-mobile` projects. Theme coverage loops 3 representative themes using `localStorage` init scripts before navigation.
- Combined-field URL heuristic (`/^https?:\/\//i`) correctly classifies Moxfield/Archidekt URLs from CedhMetaGap's `textarea[name="DeckSource"]` and maps them to the `PublicUrl` inputSource on split-field tools.

**Defects and gaps:**

**WARNING — DeckPrimer not tested as source or destination (e2e gap):**
The e2e spec exercises four of five in-scope views as either a source or destination: DeckAnalysis (source, Test 1/2/3), Manabase (destination, Test 1/4), DeckConvert (destination, Test 2; POST vehicle, Test 3), and CedhMetaGap (source, Test 5). DeckPrimer appears in neither a `page.goto('/deck-primer')` nor as a store source. The view has the script tag (confirmed in view at line 298), and DeckPrimer uses the same split-field shape as DeckAnalysis, so the risk of behavioral defect is low. But the plan acceptance criteria required proving "all five in-scope views" and that contract is not met by the spec as written.

**WARNING — Per-keystroke `setLastDeck` with no debounce:**
Lines 112-114 attach `input` listeners that call `persist()` synchronously on every keystroke. `persist()` calls `setLastDeck`, which calls `getDeckTextBytes(state.deckText)` (a `new TextEncoder().encode(value).length` call) then `JSON.stringify` then `sessionStorage.setItem`. For the typical case (short URL or paste of under 5KB) this is imperceptible. For a user who types a 80KB+ deck text character by character, each keystroke triggers measurable synchronous work. No debounce is present. Fix: 300ms trailing debounce on the `persist` closure.

**WARNING — Silent prefill discoverability (locked design tradeoff, future UX debt):**
Users who land on a tool expecting a fresh form receive a pre-populated deck with no visual indicator that the value originated from their prior session. The CONTEXT.md explicitly deferred the "Restored your last deck — clear" affordance. This is the correct v1 call (shipping bias) but creates two practical risks: (a) a user who navigates to start a fresh analysis may submit the wrong deck accidentally, and (b) a user who hasn't used the feature before may be confused about why the field is populated. The feature works silently in both the success case (the right deck appears) and the failure-to-intent case (the wrong deck appears). A minimal deferred affordance — e.g., a small `(prefilled - clear)` link appended to the label when a restore fires — would resolve both risks without adding visual weight.

**INFO — Combined-field save fires on partial URLs:**
Every `input` event on CedhMetaGap's `textarea[name="DeckSource"]` stores the current partial value. A user typing "https://moxfield.com" character-by-character stores each intermediate state. Once the string starts with "https://" the heuristic classifies it as a URL and stores it in `deckUrl`. This is expected behavior but means a user who erases the field mid-edit stores an empty state, so the next tool gets no prefill. Not a defect but worth documenting as the designed behavior.

---

## Files Audited

- `/mnt/c/users/chrislunt/source/personal/deckflow-phase74/DeckFlow.Web/wwwroot/ts/deck-input-store.ts` (new — primary implementation)
- `/mnt/c/users/chrislunt/source/personal/deckflow-phase74/DeckFlow.Web/e2e/cross-tool-deck-persistence.spec.ts` (new — e2e regression coverage)
- `/mnt/c/users/chrislunt/source/personal/deckflow-phase74/README.md` (modified — cross-tool carry-over note, line 334)
- `.planning/phases/74-cross-tool-input-persistence/74-CONTEXT.md` (design decisions)
- `.planning/phases/74-cross-tool-input-persistence/74-01-PLAN.md` and `74-01-SUMMARY.md` (wave 1)
- `.planning/phases/74-cross-tool-input-persistence/74-02-PLAN.md` and `74-02-SUMMARY.md` (wave 2)
- DeckAnalysis.cshtml line 945, Manabase.cshtml line 570, CedhMetaGap.cshtml line 641, DeckConvert.cshtml line 102, DeckPrimer.cshtml line 298 (view wiring additions verified via grep)
- DeckComparison.cshtml and DeckSync.cshtml (confirmed clean — no store reference)
