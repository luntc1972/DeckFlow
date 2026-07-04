# Phase 81 — UI Review

**Audited:** 2026-07-03
**Baseline:** No UI-SPEC.md exists — audited against abstract 6-pillar standards + the project's established manabase-lens design convention (the new card is a deliberate sibling of the existing TAP-analyzer `manabase-taplens` card, per 81-03-PLAN.md).
**Screenshots:** not captured (no dev server running on 3000/5173/8080/7173 — code-only audit). Playwright spec `manabase-mulligan.spec.ts` exists but per 81-03-SUMMARY.md was **not executed against a live server in this session** — flagged below as `needs_human_review`.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Hedged consistency-signal tone is correct, but the "not a keep verdict / not a keep/mulligan recommendation" hedge is stated three times in one card (pill, note, implicitly the label) — redundant. |
| 2. Visuals | 3/4 | Card is internally well-composed and a faithful sibling of `manabase-taplens`, but it is the **4th** near-identical stacked lens card on the page, pushing the primary Health/Land verdict further below the fold with no visual differentiation between cards. |
| 3. Color | 4/4 | Zero new hardcoded colors or tokens; reuses `manabase-lens-met`/`-short` (text+color, never color-alone) exactly as the TAP card does. |
| 4. Typography | 4/4 | No new font sizes/weights introduced; reuses the existing `.manabase-lens-*` scale verbatim. |
| 5. Spacing | 4/4 | New CSS block (site-common.css:2814-2845) copies the taplens spacing values verbatim (`-0.25rem 0 1rem`, `1rem 1.5rem` gap, `0.35rem 0` list items) — no arbitrary values. |
| 6. Experience Design | 3/4 | Flag-OFF byte-identity is proven by an `IRazorViewEngine` excision test (excellent rigor) and the mobile-collapse breakpoint exists in CSS, but the live desktop/mobile Playwright smoke was written and never actually run in this session, and no render test covers the zero-representative-openers degrade path. |

**Overall: 21/24**

---

## Top 3 Priority Fixes

1. **Visual hierarchy overload — 4 stacked lens cards bury the Health verdict** — User impact: a user analyzing their deck now scrolls past Karsten-source, cast-rate, tap-analyzer, AND opening-hand cards (all the same box style, same label/big-number/pill/rows/note layout) before reaching the actual pass/fail `manabase-chip` Health verdict at Manabase.cshtml:328-329 — the single most decision-relevant line on the page. Concrete fix: either move the Health/Land verdict block above the lens-card stack, or give each lens card a distinguishing visual weight (e.g., a left accent-color bar keyed to its own pass/fail state, similar to `.manabase-verdict--issues`/`--fine`) so scanning doesn't require reading four uniform gray boxes.
2. **Redundant hedge language dilutes the copy** — User impact: the same "this is a consistency signal, not a keep/mulligan recommendation" idea appears at Manabase.cshtml:277 (pill) and again at line 312 (note), plus "not a keep verdict" is a near-duplicate of "not a keep/mulligan recommendation" — a careful reader notices the repetition and it adds reading friction for a card that's already dense. Concrete fix: keep the hedge in the closing `.manabase-lens-note` only; shorten the pill (line 277) to the keep criterion itself ("2-5 lands with an early play, London mulligan") without repeating the disclaimer.
3. **Live-render verification is outstanding, not just theoretical** — User impact: the `manabase-mulliganlens-split` 640px collapse (site-common.css:2840-2845) and the openers `<ul>` wrap behavior at 390px have never been visually confirmed in this session (SUMMARY.md 81-03 explicitly states the Playwright spec was validated with `--list` only, not run live). Concrete fix: before merge, the operator must run `manabase-mulligan.spec.ts` against the live headless server (`scripts/run-web-test.sh`) at 1280 and 390 widths, flag ON/OFF, across at least 2 themes, per the project's own "UI phases need visual verify" convention — this is already tracked as owed in 81-03-SUMMARY.md but should not be treated as optional.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)

**Evidence:**
- Manabase.cshtml:277 — pill: `"keepable = 2-5 lands with an early play, on the London mulligan; a consistency signal, not a keep verdict"`
- Manabase.cshtml:312 — note: `"First-pass read from the same simulation as the cast rate above, so it never contradicts it — a consistency signal, not a keep/mulligan recommendation."`
- Both sentences independently assert "not a [keep] verdict/recommendation" — the disclaimer is correct and matches the project's honesty contract (Plan 81-02's "EVALUATION consistency signal, never keep/mull advice"), but stating it twice in the same small card is copy bloat, not reinforcement — a user has to read the whole card twice to notice it's the same caveat.
- Positive: the representative-opener line (line 296-307) is specific and non-generic — it names the actual tracked spell (`@opener.TrackedSpellName`) and turn rather than a templated "early plays castable on curve" claim, satisfying MULLIGAN-02's anti-genericism requirement. This is a real strength.
- Positive: `Decision` values from CastabilitySimulator.cs:309-315 ("keep 7" / "mulligan to 6" / "mulligan to 5") are plain, unambiguous MTG terminology — no jargon translation needed.
- No generic `Submit`/`Click Here`/`OK` labels found in the block (grep confirmed clean).

### Pillar 2: Visuals (3/4)

**Evidence:**
- Manabase.cshtml:180-314 — page order is: `.manabase-twolens` (2 cards: Karsten source check, Simulated cast rate) → `.manabase-taplens` (Untapped sources) → `.manabase-mulliganlens` (Opening hand, new in this phase) → THEN the primary `manabase-context`/Health verdict (lines 316-329).
- All four lens cards share identical chrome: `.manabase-lens` background/border/radius, `.manabase-lens-label` uppercase caption, `.manabase-lens-big` headline, `.manabase-lens-pill`, `.manabase-lens-row`, `.manabase-lens-note`. There is no visual signal distinguishing "this is informational context" from "this is the verdict you came here for."
- This is not a new defect introduced by 81-03 in isolation — the taplens card already had this problem after Phase 79/prior work — but Phase 81-03 makes it worse by adding a 4th card of the same weight, and no prior UI review appears to have addressed the cumulative effect (checked project MEMORY.md; the most recent UI-REVIEW for the interaction-audit readout, Phase 79, scored 20/24 but on a different page).
- Positive: within the card itself, hierarchy is correct — a 2.4rem `.manabase-lens-big` keepable-band headline is the clear focal point, followed by supporting rows, matching the TAP-lens convention exactly (site-common.css:2654-2666).
- Positive: icon-only markers (`✓`/`⚠` at Manabase.cshtml:274) are `aria-hidden="true"` and paired with an adjacent `sr-only` text alternative ("meets target"/"below target", line 276) — correctly avoids icon-only ambiguity for assistive tech.

### Pillar 3: Color (4/4)

**Evidence:**
- `git show --stat d1b72048` confirms only `DeckFlow.Web/Views/Deck/Manabase.cshtml` and `DeckFlow.Web/wwwroot/css/site-common.css` were touched — no theme fork (`site-*.css`) modified, satisfying the project's "layout CSS in site-common.css only, tokens in each theme :root" constraint.
- `grep -c ":root" site-common.css` unchanged per 81-03-SUMMARY.md's own verification — no new custom properties invented.
- Severity is conveyed by both text AND color: `manabase-lens-met` (green, "✓" + sr-only "meets target") vs `manabase-lens-short` (gold-warning, "⚠" + sr-only "below target") — ManabaseDisplay.cs:99-102 `KeepableMarker` reuses these exact two classes, no new color introduced for this feature.
- No hardcoded hex/rgb literals added in the diff (the only hex literals in site-common.css belong to the pre-existing `.manabase-health--*` block, untouched by this phase).

### Pillar 4: Typography (4/4)

**Evidence:**
- New CSS rules (site-common.css:2814-2845) declare no `font-size` or `font-weight` at all for `.manabase-mulliganlens`/`-split`; only `.manabase-mulliganlens-openers` sets `font-size: var(--fs-sm, 0.85rem)` (line 2833) — the same variable already used by `.manabase-lens-gloss` (line 2677) and `.manabase-rampdraw-line` (line 2747) elsewhere on the same page. No new size or weight token introduced.
- The card's headline text inherits `.manabase-lens-big` (2.4rem/700) and body text inherits `.manabase-lens-row`/`-muted` (default page body size) exactly as the sibling taplens card does — visually consistent scale.

### Pillar 5: Spacing (4/4)

**Evidence:**
- `.manabase-mulliganlens { margin: -0.25rem 0 1rem; }` (site-common.css:2817-2819) is byte-identical to `.manabase-taplens`'s margin value (line 2795-2797).
- `.manabase-mulliganlens-split { grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr); gap: 1rem 1.5rem; align-items: start; }` (2822-2827) is byte-identical to `.manabase-taplens-split` (2800-2805).
- The mobile collapse breakpoint (`@media (max-width: 640px)`, line 2840-2845) matches the same 640px breakpoint already used for `.manabase-twolens` (2787) and `.manabase-taplens-split` (2807) — consistent breakpoint reuse, not a new arbitrary value.
- No `[...px]`/arbitrary bracket values found in the new block.

### Pillar 6: Experience Design (3/4)

**Evidence:**
- Flag-OFF safety is proven rigorously: `ManabaseViewRenderTests.OffState_MulliganFlagFalse_RendersNoMulliganLensMarkup` (line 68-77) and `OffState_IsByteIdenticalToOnWithMulliganCardExcised` (line 99-122) use an `IRazorViewEngine` render + longest-common-prefix/suffix diff to assert the OFF page's differing region is **byte-for-byte empty** — a stronger guarantee than a substring-absence check, correctly mirroring the precedent tap-analyzer excision test. This is the single strongest piece of evidence in the whole audit.
- Gap: `OnState_MulliganFlagTrue_RendersOpeningHandLensCardWithTrackedSpell` (line 79-96) only exercises the path where `RepresentativeOpeners.Count > 0`. There is no render test for the `Count == 0` branch (Manabase.cshtml:290 `@if` guard) — the degrade is very likely fine (the `<ul>` simply doesn't render) but it is untested, so a future regression that leaves stray markup when the list is empty would not be caught by CI.
- Gap (the top-3 fix above): per 81-03-SUMMARY.md, "`manabase-mulligan.spec.ts` was validated with `npx --no-install playwright test --list` ... but was NOT executed against a live server in this session" — the desktop-1280/mobile-390 visual claim is currently backed only by static analysis of the CSS breakpoints, not an actual rendered screenshot. `needs_human_review: true`.
- Positive: the flag read path (`IsFlagOn(MulliganEvalFlagKey)`, fail-safe OFF) mirrors the established `ShowTapAnalyzer` pattern exactly, and `ShowMulliganEval` was confirmed NOT wired into `DeckAnalysisPacketService`'s cache-bypass registry — correctly scoped out per 81-02-SUMMARY.md's own investigation, since `/manabase` has no cache to go stale.

---

## Files Audited

- `.planning/phases/81-opening-hand-mulligan-evaluator/81-03-PLAN.md`
- `.planning/phases/81-opening-hand-mulligan-evaluator/81-03-SUMMARY.md`
- `.planning/phases/81-opening-hand-mulligan-evaluator/81-02-SUMMARY.md`
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` (full manabase-lens region, lines 180-340, plus a project-wide `Html.Raw` grep)
- `DeckFlow.Web/wwwroot/css/site-common.css` (lines 2543-2900: `.manabase-chip`, `.manabase-health--*`, `.manabase-lens*`, `.manabase-taplens*`, `.manabase-mulliganlens*`, all `@media` breakpoints)
- `DeckFlow.Web/Models/ManabaseDisplay.cs` (full file — `TapMarker`, `KeepableMarker`, `AvgManaValueText`)
- `DeckFlow.Web/Models/ManabaseViewModel.cs` (`ShowMulliganEval`/`ShowTapAnalyzer` flags)
- `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` (lines 1-130: OFF/ON/excision test trio for both tap and mulligan cards)
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` (lines 285-340: `Decision`/`OpeningHandSample` construction)
- `git show --stat d1b72048` (diff scope confirmation: only Manabase.cshtml + site-common.css touched)
