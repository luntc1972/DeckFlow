# Deck History (/deck-history) — UI Review

**Audited:** 2026-07-16
**Branch:** `feat/deck-history` @ `c681ef7c`
**Baseline:** `.planning/deck-history-ui-spec.md` (binding design contract) + `.planning/deck-history-design.md` states/error copy
**Screenshots:** Pre-captured Task 8 set used (12 PNGs: form + results × Classic/Azorius/Nyx × 1280/390, `.planning/ui-design/deck-history/screenshots/`). No live dev server during audit; all visual findings verified against these captures plus code.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Spec strings verbatim, but identical-deck state shows two contradictory notices at once |
| 2. Visuals | 3/4 | Compare panel abandons the spec's inline "older → newer" row; Notes textarea dwarfs the form |
| 3. Color | 3/4 | Clean token discipline incl. Nyx dark surfaces, but spec's green success notice renders amber |
| 4. Typography | 4/4 | No new sizes/weights; house hierarchy (h1→h2→h3) intact across themes |
| 5. Spacing | 2/4 | Mobile timeline Notes column shreds words mid-word in all themes; form vertical rhythm crowded |
| 6. Experience Design | 3/4 | Full state coverage per spec, but form screenshots show a select/panel mode desync |

**Overall: 18/24**

---

## Top 3 Priority Fixes

1. **Mobile timeline word-shredding (WARNING, worst visible defect)** — On 390px every theme renders Notes as "Swap ped Brains torm for Mystic Remor a." because the inherited `.result-panel { overflow-wrap: anywhere; word-break: break-word; }` (site.css:999-1004) collapses the Notes column's min-content width, so the spec-mandated `overflow-x: auto` scroll (`.history-timeline__wrap`, site-common.css:3950) never engages — and the Δ column's "−1" still clips at the panel edge. Spec: "Notes cell wraps, never truncates." **Fix:** add `.history-timeline td { overflow-wrap: normal; word-break: normal; }` and give the table a `min-width` (e.g. `28rem`) so the wrapper scrolls as designed.
2. **Contradictory identical-deck notices (WARNING)** — All six results screenshots show, in one banner: "Deck has 34 cards — … Snapshot saved anyway." directly above "The imported deck is identical to the latest version - no new snapshot was added." The card-count warning is emitted before the append outcome is known (`DeckHistoryPageService.cs:154-160`). **Fix:** move the count warning after `DeckHistoryAppender.Append` and drop/vary the "Snapshot saved anyway." suffix when `Appended == false`; also change the ASCII hyphen in `DeckHistoryAppender.cs:74` to the em dash the spec states table and the sibling string use.
3. **Success notice rendered as amber warning (WARNING)** — Spec states table: "New history created → **Green-tinted** notice 'Started a new history — version 1 saved.'" Implementation funnels success notices and repair warnings into the single amber `.warning-banner` (`DeckHistory.cshtml:118-125`, `DeckHistoryViewModel.BuildWarnings`), so "everything worked" is visually indistinguishable from "something needed repair." **Fix:** render the append/new-history notice in a success-tinted banner (e.g. `var(--success)` left border) separate from the amber warning list.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)

**Passes (verified verbatim against spec "Copy (exact strings)"):**
- Intro lede — `DeckHistory.cshtml:44` matches spec exactly.
- File hint "First visit? Skip this — import your deck below and download your new history file." — `:64`.
- Notes label "Notes — why did the deck change?" — `:101`.
- Download panel "Download the updated file and keep it with your deck. Re-upload it next time to add the next version." — `:232`.
- Empty-compare "Add a second version to compare." — `:218`.
- No generic labels: CTA is "Update history", secondary "Start over", "Compare", "Copy" (house-standard) — no bare Submit/OK/Cancel.
- Neither-input error "Upload a history file, import a deck, or both." (`DeckHistoryPageService.cs:165`) is actionable, better than a generic error.

**Findings:**
- **WARNING — contradictory simultaneous notices.** `DeckHistoryPageService.cs:159` appends "…Snapshot saved anyway." unconditionally when count ≠ 100, before the append runs; when the deck is identical, `DeckHistoryAppender.cs:74` adds "…no new snapshot was added." Both render in one list (visible in all 6 results screenshots). The first bullet is factually false in that state.
- **WARNING (minor) — dash inconsistency.** `DeckHistoryAppender.cs:74` uses "- no new snapshot" (ASCII hyphen); the spec states table and the sibling warning at `DeckHistoryPageService.cs:159` use an em dash. Visible in screenshots.
- **Minor — download button label.** Spec wireframe shows the concrete filename ("Download deck-history-….json"); implementation says generic "Download deck history (.json)" (`:234`). The filename is not in the exact-strings section, so this is advisory — but naming the file would reinforce the "you own this file" hook the page is built on.

### Pillar 2: Visuals (3/4)

**Passes:**
- Page skeleton mirrors Bracket verbatim: hero h1 + lede, `_DeckToolTabs`, single `result-panel` form, result panels — confirmed side-by-side with `Bracket.cshtml`.
- Timeline zebra striping, green/red Δ coding, and three bordered diff cards give the results section clear scannable structure in all three themes.
- Focal point on results is the Timeline panel, as designed.

**Findings:**
- **WARNING — compare row layout deviates from wireframe.** Spec: `[V1 ▾] → [V3 ▾] [Compare]` on one desktop row. Implementation stacks two full-width labeled selects plus a full-width toolbar row (desktop screenshots) — the directional "older → newer" reading is lost and the panel is ~3× the designed height. No compare-row CSS exists in site-common.css. Fix: a two-column grid (or inline-flex) row for the selects ≥900px with an → separator, collapsing to stacked on mobile.
- **Minor — Notes textarea dominates the form.** `rows="3"` is overridden by `.result-panel textarea { min-height: 16rem; }` (site.css:1006-1008), making the least-important field the largest element on the page and pushing the "Update history" CTA well below the fold at 1280×900 (form screenshots). A scoped `#deck-history-notes { min-height: 6rem; }` would restore proportion.
- **Minor — no "How it works" hero disclosure.** Bracket's hero carries a `hero-detail` explainer; this page's novel ownership model (file-based history) gets none. Not spec-required; advisory.

### Pillar 3: Color (3/4)

**Passes:**
- New CSS (site-common.css:3950-4039) uses only theme tokens with fallbacks: `--line`, `--panel-soft-bg`/`--panel`, `--success`, `--danger`, `--muted` — all defined in site.css `:root` (lines 7, 13, 44, 53) and overridden per theme (`--success` present in every theme file). Zero raw hex outside fallback positions. No accent overuse — accent appears only on the primary CTA and links, per house 60/30/10.
- Nyx dark theme: panels, inputs, table, diff cards, warning banner all on dark surfaces — **no `--theme-surface` light bleed** (both Nyx screenshots verified). Spec's dark-theme rule met.
- Δ colors `+1`/`−1` legible against dark and light surfaces in all six results captures.

**Findings:**
- **WARNING — missing green success state.** Spec states table requires a green-tinted notice for "Started a new history — version 1 saved."; `BuildWarnings` merges it into the amber `.warning-banner`. Success and repair warnings share one amber treatment (contract deviation; see Top Fix 3).
- **Informational** — spec declared a new `.history-warnings` class (amber left border, `var(--warning, var(--accent))` chain); implementation reused the existing `.warning-banner` instead. Visually consistent with house notices, acceptable reuse — but it is what forecloses the green/amber split above.

### Pillar 4: Typography (4/4)

- No new font sizes or weights introduced: h1 (hero), h2 (panel headings), h3 (diff columns — matching the spec's a11y requirement), body text, `small.manabase-help` hint. Table header/delta weight 600 within house range; label-inline uses `<strong>`.
- Distribution verified across all 12 screenshots — hierarchy consistent per theme, headings scale correctly at 390px ("AI prompt — 'How has this deck evolved?'" wraps cleanly on two lines).
- Minor (no score impact): the file hint reuses the `manabase-help` class on a non-manabase page — naming drift only, identical rendering; a shared `.field-help` alias would be cleaner.

### Pillar 5: Spacing (2/4)

**Findings:**
- **WARNING (top fix) — mobile timeline column crush.** All three 390px results captures show the Notes column at ~10ch with mid-word breaks and the Δ value clipped at the panel's right edge. Root cause is the inherited `overflow-wrap: anywhere` from `.result-panel` (site.css:1002) defeating the `overflow-x: auto` containment strategy the spec mandates — the table shrinks-to-fit instead of scrolling. Page-level horizontal scroll is correctly absent (spec ✓), but the in-panel result is unreadable. See Top Fix 1 for the concrete CSS.
- **WARNING — form vertical rhythm.** In every form capture the hint "First visit? Skip this…" sits flush against the following "Input method" label (~0px gap), and on mobile the decklist textarea bottom touches the "Deck name" label. `small.manabase-help` carries no bottom margin and `.field` spacing doesn't compensate, so field groups don't read as groups. Fix: `margin-bottom` on the hint (or `margin-top` on `.field + .field`) — ~0.75rem restores the house rhythm visible on Bracket.
- **Passes:** new CSS spacing values (0.5/0.65/0.85/1rem paddings, 0.85rem grid gap, 12px radius) sit on the house scale; diff grid `repeat(3, minmax(0,1fr))` → `1fr` at 640px matches the spec breakpoint exactly; download button goes full-width on mobile ✓.

### Pillar 6: Experience Design (3/4)

**State coverage vs spec table — verified in code + screenshots:**

| Spec state | Status |
|---|---|
| First visit (GET): form only, URL preselected | ✓ (form screenshots; but see desync finding) |
| New history: notice + panels, compare hidden, muted "Add a second version to compare." | ✓ structure (`DeckHistory.cshtml:181-219`) — ✗ notice color (Pillar 3) |
| Appended: "Version N added.", compare defaults N−1 → N | ✓ (`BuildWarnings`, `SelectPair` at `DeckHistoryPageService.cs:269-272`) |
| Inspect (file only): no append notice, deltas recomputed | ✓ (`Appended=false` path, `RecomputeDeltas`) |
| Identical deck: amber notice, panels still render | ✓ render — ✗ contradictory copy (Pillar 1) |
| Repair warnings: non-blocking amber list | ✓ (serializer warnings flow into banner) |
| Errors: red alert above form, values preserved, no panels | ✓ (`error-banner` `role="alert"`, `Request` echo, `HasResult=false`) |

**Also passes:** busy indicator with staged progress copy; download via `data-prompt-download-submit` fetch/blob intercept (mobile pull-to-refresh safe, house pattern); compare re-POSTs the hidden `HistoryJson` without re-upload; a11y contract fully met (`<label for>` on file/selects/textareas, `<th scope="col">`, diff columns as `<section>`+`<h3>` in mobile stack order, `role="status"` on notices, Copy button carries text).

**Findings:**
- **WARNING (needs_human_review) — restored-state mode desync.** All six form captures show select = "Use public deck URL" while the Paste-text panel is visible with restored content and the URL field hidden. `deck-input-store.ts:146-148` sets `inputSelect.value` on restore without dispatching `change`, and whichever script toggled the panels disagreed with the select's displayed value. May be an e2e-fixture artifact of shared localStorage, but as captured the page presents an incoherent input mode. Reproduce manually: paste a deck on another tool, then GET /deck-history.
- **Minor — "Snapshot saved anyway." asserted pre-append** (`DeckHistoryPageService.cs:154-160`) — state-logic side of Top Fix 2.
- **Minor — Compare button gives no feedback** (`data-no-busy`, no disabled state) during the re-POST; consistent with house behavior for cheap posts, advisory only.

---

## Registry Safety

Not applicable — no `components.json` (ASP.NET Razor app, no shadcn); UI-SPEC lists no third-party registries. Registry audit skipped per protocol.

## Screenshot storage note

Prior cycles commit UI screenshots under `.planning/ui-design/` (e.g. cycle13 Bracket PNGs are git-tracked); the deck-history set is currently untracked (`??`). `.planning/ui-reviews/.gitignore` gate verified present for any future review captures.

---

## Files Audited

- `.planning/deck-history-ui-spec.md`, `.planning/deck-history-design.md`, `.planning/deck-history-plan.md` (Tasks 1-7 read), `.foreman/ledger.md`
- `DeckFlow.Web/Views/Deck/DeckHistory.cshtml`
- `DeckFlow.Web/Views/Deck/Bracket.cshtml` (house-pattern baseline)
- `DeckFlow.Web/Models/DeckHistoryViewModel.cs`
- `DeckFlow.Web/Services/DeckHistoryPageService.cs`
- `DeckFlow.Core/History/DeckHistoryAppender.cs` (warning strings)
- `DeckFlow.Web/wwwroot/css/site-common.css` (lines 3950-4039), `site.css` (tokens + `.result-panel` rules)
- `DeckFlow.Web/wwwroot/ts/deck-input-store.ts`
- `DeckFlow.Web/Views/Shared/_AiSelector.cshtml` (Gemini flag-gated — by design, not a finding)
- 12 screenshots under `.planning/ui-design/deck-history/screenshots/` (form + results × Classic/Azorius/Nyx × desktop/mobile)
