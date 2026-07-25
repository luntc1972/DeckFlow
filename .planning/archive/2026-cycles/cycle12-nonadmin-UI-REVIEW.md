# Cycle 12 (non-admin) — UI Review

**Audited:** 2026-06-27
**Baseline:** Abstract 6-pillar standards + DeckFlow design conventions (no UI-SPEC for this scope)
**Scope:** git `1999ed34..HEAD`, non-admin views only (Manabase, DeckPrimer, DeckAnalysis, CedhMetaGap, DeckConvert) + site-common.css + deck-input-store.ts / deck-sync.ts / primer-selection.ts
**Screenshots:** NOT captured — no dev server on :3000/:5173/:8080. Code-only audit; theme/mobile rendering claims below are flagged for live confirmation.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 4/4 | Clear, specific, transparent heuristic disclaimers; resume-hint kept in sync. One verbose label nit. |
| 2. Visuals | 3/4 | Commander ★ vs companion + glyphs use different color/alignment; advisory "cards" lose their fill in ~11 themes. |
| 3. Color | 2/4 | `--panel-soft-bg` undefined in 11 standalone themes and `--surface` defined nowhere → advisory box backgrounds collapse to transparent. |
| 4. Typography | 3/4 | New sub-headings/gloss at 0.72–0.8rem in `--muted`; sizes hardcoded instead of `--fs-*` tokens; one no-fallback `var(--fs-sm)`. |
| 5. Spacing | 3/4 | Consistent rem rhythm, but commander glyph lacks the `min-width` the companion glyph has → misaligned castability lines. |
| 6. Experience Design | 3/4 | Strong silent-restore + clear affordance + busy-overlay fixes; dynamically-inserted `role=status` and duplicated `aria-label` are SR nits. |

**Overall: 18/24**

---

## Top 3 Priority Fixes

1. **Advisory boxes render with no background fill in ~11 of 24 themes (HIGH, Color).** `.manabase-verdict / -beta-notice / -cmd-castability / -rampdraw` use `background: var(--panel-soft-bg, var(--surface, transparent))`. `--panel-soft-bg` is only defined by site.css + the 11 `@import`-ing guild themes; the 11 standalone forks (abzan, bant, esper, grixis, jund, mardu, naya, nyx, planeswalker-dark, sultai, commander-table) do **not** define it, and `--surface` is defined in **no** file. So the fallback chain bottoms out at `transparent`, and the tinted variants color-mix against transparent (a faint wash). The flagship plain-language-verdict box loses its panel fill in nearly half of themes. **Fix:** change the fallback to the universal token — `var(--panel-soft-bg, var(--panel))` (`--panel` is defined in every theme) — and delete the dead `--surface` reference. Or add `--panel-soft-bg` to the 11 standalone `:root`s per the project's "tokens go in each theme file" rule.

2. **Commander ★ and companion + glyphs don't align (MED, Spacing/Visuals).** `.manabase-cmd-companion-glyph` has `min-width: 0.9rem; margin-right: 0.2rem`; the commander `.manabase-cmd-glyph` (site-common.css:2467) only sets `margin-right: 0.2rem` with no `min-width`, so the two castability lines start their card name at different x-offsets and the glyphs read as two unrelated treatments. **Fix:** give `.manabase-cmd-glyph` the same `min-width: 0.9rem; display:inline-block` (or extract a shared `.manabase-cmd-line-glyph` base class for both).

3. **Small muted text contrast + token inconsistency (MED, Typography).** `.manabase-lens-gloss` and `.manabase-rampdraw-note` are 0.8rem in `--muted` (a deliberately low-contrast color); `.manabase-verdict-heading` is 0.72rem uppercase `--muted`. At those sizes `--muted` likely fails WCAG AA in some themes. The new rules also hardcode 0.72/0.8/0.85rem instead of the `--fs-sm/--fs-xs` tokens the sibling `.deck-restored-notice__clear` uses. **Fix:** confirm contrast live across themes; bump the smallest gloss to ≥0.8rem with `--ink`-adjacent color or tokenize via `--fs-sm`.

---

## Detailed Findings

### Pillar 1: Copywriting (4/4)
- **GOOD** — `DeckPrimer.cshtml:92` resume hint updated to add "primer style" alongside bracket/AI target/sections; copy kept in sync with the new feature (matches the project's "update copy when behavior changes" norm).
- **GOOD** — `Manabase.cshtml:199` beta notice ("Treat the results as a guide, not gospel - numbers may be inaccurate") and the rampdraw/cmd-castability disclaimers ("Community heuristic, not Karsten math", "includes the +3 generic 'to hand' tax as an approximation") are transparent about model limits — strong for a serious deck-builder audience.
- **GOOD** — No generic labels in scope (no bare "Submit/OK/Save"); CTAs are specific ("Start Over", "Download analysis (.txt)", "Clear").
- **LOW** — `DeckAnalysis.cshtml:174` and `Manabase.cshtml` companion `<label>` is a full descriptive sentence used as the input's *only* accessible name ("Auto-detected from Moxfield; name it here for Archidekt or pasted lists, or to override."). It reads as help text, not a label — the accessible name a screen reader announces is a long run-on. **Fix:** split a short `<span>` label ("Companion") from a separate hint paragraph, or wire `aria-describedby` to the sentence.
- **LOW** — Companion blurb is near-duplicated between Manabase and DeckAnalysis. Acceptable under the project's intentionally-decoupled prose convention, but keep both in sync on edits.

### Pillar 2: Visuals (3/4)
- **MED** — Glyph treatment is inconsistent: commander ★ uses `var(--commander-gold, #d4af37)` with no width reservation, companion + uses `var(--accent-strong, var(--accent))` with `min-width`/bold. Two different colors *and* two different alignments on adjacent lines weakens the "these are the same kind of row" read. (See Top Fix #2.)
- **MED** — The transparent-background issue (Top Fix #1) flattens the visual hierarchy of the advisory boxes in ~11 themes: the verdict/rampdraw/cmd-castability blocks are meant to read as distinct soft "cards" but degrade to border-only outlines, competing less with surrounding body text.
- **GOOD** — Hierarchy is otherwise sound: uppercase letter-spaced `.manabase-verdict-heading` label + `--gold-warning`/`--success` left-border color coding (issues vs fine) gives an at-a-glance status cue.
- **GOOD** — Decorative glyphs carry `aria-label`/`title` ("commander"/"companion"); icon meaning is exposed, not icon-only-without-label.

### Pillar 3: Color (2/4)
- **HIGH** — `site-common.css` `.manabase-verdict, .manabase-beta-notice, .manabase-cmd-castability, .manabase-rampdraw { background: var(--panel-soft-bg, var(--surface, transparent)); }`. Verified token coverage: `--panel-soft-bg` defined only in site.css + the 11 `@import` themes; **missing** in abzan, bant, esper, grixis, jund, mardu, naya, nyx, planeswalker-dark, sultai, commander-table. `--surface` defined in **zero** files. Net: transparent fill in those 11 themes, and `color-mix(... , transparent)` faint washes for the tinted variants. (See Top Fix #1.) Contradicts the project rule that token additions belong in each theme's `:root`.
- **MED** — `.manabase-verdict--fine` left-border `var(--success, #2f855a)`: the 11 `@import` themes inherit site.css's green and the standalone themes mostly define their own, but commander-table falls to the hardcoded `#2f855a`, which can clash with that theme's palette. Acceptable as a status color, but it is a generic green dropped into guild themes that don't tune `--success`.
- **GOOD** — `.deck-restored-notice` (the other new component) correctly uses only universal tokens (`--panel`, `--line`, `--accent`, `--muted`, `--ink`) — all confirmed defined in every standalone theme. This is the correct pattern the manabase boxes should have followed.
- **GOOD** — No raw hex without a `var()` fallback path in the changeset; every literal sits behind a token default.

### Pillar 4: Typography (3/4)
- **MED** — Smallest new text is 0.72rem (`.manabase-verdict-heading`, uppercase) and 0.8rem (`.manabase-lens-gloss`, `.manabase-rampdraw-note`) all colored `var(--muted)`. Low-contrast muted at sub-0.85rem risks WCAG AA failure; needs live per-theme contrast check (flagged below).
- **MED** — Size tokenization is inconsistent: new manabase rules hardcode `0.72/0.8/0.85rem`, while the sibling `.deck-restored-notice__clear` uses `var(--fs-sm)`. Pick one; prefer the `--fs-sm/--fs-xs` tokens so themes can scale type.
- **LOW** — `.deck-restored-notice__clear { font-size: var(--fs-sm); }` has **no fallback**. `--fs-sm` is defined everywhere except commander-table (standalone, no `@import`), where the Clear button's font-size silently falls back to inherited. Add `var(--fs-sm, 0.85rem)` to match the rest of the file's defensive pattern.
- **GOOD** — Weight usage is restrained (600 for headings/labels/emphasis, 700 only on the companion glyph); no weight sprawl.

### Pillar 5: Spacing (3/4)
- **MED** — Commander glyph alignment gap (Top Fix #2): companion line reserves `min-width: 0.9rem`, commander line does not, so names don't line up.
- **GOOD** — New rules use a consistent rem rhythm that matches neighbors (0.35 / 0.45 / 0.75 / 0.85rem margins/padding; 12px radius shared with existing `.manabase-*` panels). No arbitrary/odd pixel values introduced.
- **GOOD** — `.deck-restored-notice` flex row (`space-between`, `gap: 0.75rem`, `flex-shrink:0` on the button) is a sane reflow pattern; short "Restored your last deck." + pill won't crowd at narrow width.
- **LOW** — The 3-pill "Primer style" control reuses `.manabase-pills` (`flex-wrap: wrap`) and the existing `@media (max-width:640px)` segmented block, so the added Full-cEDH pill should wrap rather than overflow — confirm live (see below).

### Pillar 6: Experience Design (3/4)
- **GOOD** — `deck-input-store.ts` restores cross-tool deck input **only into empty fields** (`restoreSplitFields` bails if url/text already populated) — POST-rendered values and user input correctly win; no clobber.
- **GOOD** — Two clear paths kept consistent: the "Start Over" `[data-clear-cache]` handler and the restored-notice `Clear` button both call `clearLastDeck()` + remove the notice. `deck-sync.ts` `hasRenderedResultOnLoad()` + `abortBridgeBusy()` fix stale/stuck busy overlays — real UX hardening.
- **GOOD** — Deck-text persistence is capped at 100KB to avoid sessionStorage quota errors, and all storage access is wrapped in try/catch (degrades silently when storage disabled).
- **MED** — The restored notice is created with `role="status"` **after** load and inserted populated. ARIA live regions generally must exist in the DOM *before* their content changes to be announced; a pre-populated injected `role=status` may not announce in some screen readers. Consider inserting an empty live region first, then setting text, or use `role="status"` on a container that already exists.
- **LOW** — `.manabase-verdict` sets `aria-label="@verdict.Headline"` *and* renders the same headline as visible `.manabase-verdict-heading` text → screen readers may read the headline twice (region name + content). Drop the redundant `aria-label` or `aria-hidden` the visible duplicate.
- **NOTE** — Loading/error/empty states are out of scope for these additions (server pre-renders results); the busy-overlay handling is the relevant interaction state and it improved.

---

## Needs Live Visual Confirmation (code-only audit)
1. **Plain-language verdict box** (`Model.ShowPlainLanguage` / `Model.PlainLanguageVerdict`) — flag-gated `manabase.plain-language-verdict`; confirm the issues vs fine border-color + (after Fix #1) background reads correctly. **Confirm specifically in a standalone theme that lacks `--panel-soft-bg`** (e.g. Nyx, Abzan, Planeswalker Dark) — that's where the transparent-fill regression shows.
2. **Command-zone castability section** (`Model.ShowCommanderCastability && Model.ShowCastability`) — confirm ★ vs + glyph alignment/color and the companion line wrapping.
3. **Deck-analysis companion `<details>`** (`Model.CommandZoneAwarenessEnabled`) — flag-gated `analysis.command-zone-awareness`; confirm summary/disclosure styling parity with `.manabase-overrides`.
4. **Ramp/draw advisory** — confirm the multi-`<strong>` inline line wraps cleanly at 320–375px.
5. **3-pill "Primer style" segmented control** — confirm wrap + the hidden Full-cEDH pill toggling by bracket (JS `syncCedhStyleVisibility`) on desktop and mobile.
6. **Restored-notice flex row** — confirm "Restored your last deck. [Clear]" doesn't crowd at 320px.
7. **Contrast** — measure `--muted` gloss at 0.72/0.8rem against panel/page background across light and dark themes (Planeswalker Dark, Nyx especially).
8. **Per-theme sweep** — the transparent-background and hardcoded-`#2f855a`/`--success` findings should be eyeballed across the 11 standalone forks + commander-table.

---

## Files Audited
- `DeckFlow.Web/Views/Deck/Manabase.cshtml`
- `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml`
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml`
- `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml`
- `DeckFlow.Web/Views/Deck/DeckConvert.cshtml`
- `DeckFlow.Web/wwwroot/css/site-common.css` (new `.deck-restored-notice(+__clear)`, `.deck-analysis-overrides`, `.manabase-lens-gloss`, `.manabase-verdict(--issues/--fine)`, `.manabase-beta-notice`, `.manabase-cmd-castability(+line/companion-glyph)`, `.manabase-rampdraw(+line/note)`; existing `.manabase-cmd-glyph`, `.manabase-segmented/pills/pill`)
- `DeckFlow.Web/wwwroot/ts/deck-input-store.ts`
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts`
- `DeckFlow.Web/wwwroot/ts/primer-selection.ts`
- Cross-checked token coverage across all 24 theme CSS files + `_Layout.cshtml` CSS link order.

_Registry audit: skipped — no `components.json` / shadcn in this repo._
