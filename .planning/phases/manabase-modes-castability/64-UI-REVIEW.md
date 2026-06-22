# Phase 64 — UI Review

**Audited:** 2026-06-21
**Baseline:** `64-UI-SPEC.md` (design contract)
**Screenshots:** Captured (live dev server at `http://localhost:5173/manabase`, Classic + Azorius themes, desktop 1280 + mobile 390, empty / Casual-result / cEDH-result states)

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 4/4 | CTA, helper text, caveat, empty/error copy all match the SPEC contract verbatim; methodology prose is specific and credits Karsten. |
| 2. Visuals | 3/4 | Clear hierarchy and chips, but the commander pin/crown styling (SPEC §4) cannot be confirmed for the common paste-input path and the weakest-color accent rail only fires on under-supported decks. |
| 3. Color | 3/4 | All severity uses semantic tokens + text labels (never color alone); one hardcoded `color:#fff` on the selected pill is a latent light-accent-theme contrast risk. |
| 4. Typography | 4/4 | Uses only existing `--fs-*` tokens and 600 weight; no arbitrary sizes introduced. |
| 5. Spacing | 3/4 | Consistent rem rhythm and a clean <480px stack; a few arbitrary one-off values (`0.45rem`, `0.85rem`) sit outside the page's prevailing 0.25/0.5/0.75/1rem step. |
| 6. Experience Design | 4/4 | All six SPEC states present and verified live; mobile table scrolls within its own wrapper with no page-level horizontal scroll; cEDH correctly swaps the table for the note; panels gate correctly. |

**Overall: 21/24**

---

## Top Priority Fixes

1. **Commander row is not pinned/flagged when the deck is pasted as a flat list** (WARNING) — SPEC §4 requires the commander row pinned to the top with a crown glyph + `manabase-row--commander` highlight "regardless of sort." The view code is correct (`Manabase.cshtml:188-194`) and the Core analyzer pins commander-first (`ManabaseAnalyzer.cs:170-171`), but in the live paste-path test (Brago deck) `IsCommander` was false for every spell, so Brago rendered as a plain row at position 7 with no glyph and no highlight, and the color-findings table showed no ★ on Blue/White. The castability `Cast on curve` column also rendered no commander emphasis. **Fix:** mark the first card of a pasted Commander decklist (or a card matching the optional commander field) as `IsCommander` in the paste import path so the SPEC's pinned-row + crown contract actually surfaces for the dominant input mode; until then the commander affordances are dead code for paste users. Confirm with an end-to-end render that has a detected commander.

2. **Hardcoded `color: #fff` on the selected pill risks failing contrast on light-accent themes** (WARNING) — `site-common.css:2307` (`.manabase-pill.is-selected { color: #fff }`) pairs white text with `var(--accent)`. On themes whose accent is a pale/light hue, white-on-light fails WCAG AA. Verified fine on Classic and Azorius (blue accent), but SPEC §6/§8 require all 24 themes. **Fix:** replace `#fff` with the existing `var(--on-accent, var(--ink-on-accent, #fff))` token used elsewhere (e.g. `.feedback-submit`, `.maintenance-page__action`) so each theme supplies a legible on-accent color.

3. **Commander-color crown glyph uses the accent token, not a distinct identity color** (WARNING) — `.manabase-cmd-glyph { color: var(--accent, #b8860b) }` (`site-common.css:2331`). The fallback is gold (a real crown color) but the resolved value is the theme accent (blue on Classic), so the "crown/star" reads as a generic accent ★ rather than the gold identity glyph SPEC §3 implies. It is also reused verbatim for the pinned-commander glyph in the castability table. **Fix:** give the identity glyph its own token (or keep the gold literal) so commander colors are visually distinguishable from ordinary accent emphasis, and confirm the ★ is legible on dark themes.

### Additional recommendations
4. **Weakest-color emphasis is unverifiable on healthy decks** (WARNING) — `.manabase-row--weakest` (left danger rail) and the "Biggest fix" callout only render when a color is under-supported. The healthy Brago deck produced "every color is adequately supported," so the SPEC §3 emphasis path was not visually exercised. Add a deliberately color-short fixture to the e2e so the danger rail + callout are regression-covered.
5. **A handful of arbitrary spacing values** (WARNING) — `0.45rem`, `0.85rem`, `0.6rem 0.75rem 0.75rem` padding on `.manabase-segmented` / pills sit off the page's prevailing 0.25/0.5/0.75/1rem step. Cosmetic, but tighten to the scale for consistency with surrounding chrome.
6. **`.manabase-chip--ok` text color diverges from its siblings** — low/good chips color the text with their own danger/success token; the `--ok` (warning) chip uses `var(--ink)` instead of `var(--warning)` (`site-common.css:2383`). Intentional for amber legibility, but it makes the three chips inconsistent; verify the amber chip text stays readable on dark themes.

---

## Detailed Findings

### Pillar 1: Copywriting (4/4)
Contract met verbatim and with specificity:
- Mode helper "cEDH = lower land count, fast-mana heavy…" — `Manabase.cshtml:64` matches SPEC §1.
- Commander-importance options + Brago example — `Manabase.cshtml:83` matches SPEC §1 helper text exactly.
- Castability heading "Castability — chance to cast on curve" + caveat "Estimate, on the play, on its mana-value turn. Counts ramp; mana rocks/dorks aren't listed." — `Manabase.cshtml:171-172` matches SPEC §4.
- cEDH note "Castability view is available in Casual mode." — `Manabase.cshtml:209` matches SPEC §4 (verified live in cEDH submission).
- Mode echo "Mode: Casual · Commander: Standard" — `Manabase.cshtml:105-108`, SPEC §2 (verified rendered).
- Methodology panel prose names the regression terms, the Monte-Carlo model, London mulligan, joint mana+color check, credits Karsten, and links Salubrious Snail — `Manabase.cshtml:242-245`, satisfies SPEC §4b. `LimitingText` maps tokens to "mana" / "color: U" / "mana + color" — `ManabaseDisplay.cs:16-43`, SPEC §4. No generic "Submit/OK" labels; CTA is "Analyze Mana Base."

### Pillar 2: Visuals (3/4)
- Clear focal hierarchy: hero → form → Result panel → two tables → formula disclosures (verified in desktop/mobile shots).
- Cast% chips give a strong scannable signal (green "good" column rendered correctly).
- Glyphs carry `title` + `aria-label="commander"` / `"commander color"` — `Manabase.cshtml:145,192`.
- **Gap:** the SPEC §4 pinned-commander row (highlight + crown) and SPEC §3 commander-color ★ did not appear for the live paste deck — `cmdGlyphs:0`, no `manabase-row--commander`, no `manabase-row--weakest` in the rendered DOM. The view branches exist but were inert because the input path did not flag a commander. The pinned/highlight visual contract is therefore unverified for the primary input mode (see Top Fix 1).

### Pillar 3: Color (3/4)
- Severity always paired with text: chip renders `"91% · good"`, limiting renders "color: Blue" (verified). SPEC §4 / §7 "never color alone" satisfied.
- All chips and the weakest rail use semantic tokens (`--danger/--warning/--success`) with hex only as token fallbacks — `site-common.css:2374-2390, 2337`. Acceptable under the theme constraint.
- Accent confined to pills, glyph, links — no accent overuse.
- **Issue:** one true hardcode `color:#fff` on the selected pill (`site-common.css:2307`) — light-accent-theme contrast risk (Top Fix 2). Identity glyph resolves to accent not gold (Top Fix 3).

### Pillar 4: Typography (4/4)
- Sizes use only `var(--fs-sm)` / inherited base; no new `text-*` scale, no arbitrary `px`/`rem` font sizes introduced in the Phase 64 block (`site-common.css:2251-2428`).
- Single added weight: `600` (legends, pills, chips, glyph). Within the 2-weight ceiling. No font-family changes.

### Pillar 5: Spacing (3/4)
- `.manabase-options` gap `1rem 1.5rem`, table cell padding `0.35rem 0.6rem`, and the `@media (max-width:480px)` stack are consistent and match SPEC §6 (verified: pills `flex-direction: column` at 390px).
- Off-scale one-offs: `0.45rem`, `0.85rem`, asymmetric `0.6rem 0.75rem 0.75rem` (`site-common.css:2264, 2290, 2319`) sit outside the prevailing 0.25/0.5/0.75/1rem step used by neighboring components. Minor (Recommendation 5).

### Pillar 6: Experience Design (4/4)
All six SPEC §5 states confirmed live:
- **Empty** — form only; "How the analysis works" present, "This deck's numbers" absent, no castability/result block (`cast:false, result:false`). SPEC §4b gating correct.
- **Casual success** — full color table + castability table rendered, sorted worst-cast% first (91→100%). Verified.
- **cEDH success** — mode echoes "cEDH", castability table absent, replaced by the note; pill selection persists on postback (`pillSelected:"cEDH"`). SPEC §4 met.
- **Error** — existing `error-banner role="alert"` retained (`Manabase.cshtml:20`).
- **Unresolved cards** — notice retained (`Manabase.cshtml:216-219`).
- **No-commander success** — table renders without a pinned row (observed; the no-op importance path behaves as SPEC §5 specifies).
- **Responsive (SPEC §6 acceptance):** desktop `scrollWidth==innerWidth==1280`; mobile `scrollWidth==innerWidth==390` with the castability table overflowing only inside `.castability-scroll` (467 > 340 clientWidth) — exactly the contained-scroll behavior SPEC §6 requires. No page-level horizontal scroll at either viewport.
- Accessibility: `<fieldset>/<legend>` per radio group, `<th scope="col">`, hidden-radio focus forwarded to the pill `span` via `:focus-visible + span` (`site-common.css:2312`). SPEC §7 met.
- Native `<details>` panels (keyboard-accessible, no JS) per SPEC §4b. No destructive actions; loading uses the existing `_BusyIndicator`.

---

## Files Audited
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` (view markup, all states)
- `DeckFlow.Web/wwwroot/css/site-common.css` (Phase 64 block, lines 2224-2429)
- `DeckFlow.Web/Models/ManabaseDisplay.cs` (chip / limiting / label presentation helpers)
- `DeckFlow.Web/Models/ManabaseViewModel.cs` (`ShowCastability` gate)
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` (commander-first sort, color findings) — referenced for root-cause of Top Fix 1
- `.planning/phases/manabase-modes-castability/64-UI-SPEC.md` (baseline)
- `.planning/phases/manabase-modes-castability/64-02-PLAN.md` (intent)
- Live render: Classic + Azorius themes; empty / Casual-result / cEDH-result; 1280 + 390 viewports

_Registry audit: skipped — no `components.json` / shadcn in this ASP.NET + Razor project._
