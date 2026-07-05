# DeckFlow — Project-Wide UI Review (Freeform)

**Audited:** 2026-07-04 (prior: 16/24 (2026-04-30)) · **Re-scored:** 2026-07-05 (Phase 86-05, UIAUDIT-02)
**Baseline:** Abstract 6-pillar standards (no UI-SPEC.md)
**Screenshots:** Captured via headless local server (`scripts/run-web-test.sh` on `http://localhost:5173`, `DECKFLOW_DISABLE_AUTO_BROWSER=true` — no Windows browser opened) + `playwright-core` (Chromium) at desktop (1280x900, full-page) and mobile (390x844, full-page) viewports for 6 routes: `/`, `/sync`, `/lookup`, `/feedback`, `/help`, `/about`, Classic theme.
**Evidence basis:** Live rendered HTML + screenshots for the 6 routes above + source review of `site.css` (1382L, full read), `site-common.css` (3400+L, targeted grep across full file), 7 Razor views (`_Layout`, `Home`/Shell, `DeckSync`, `Feedback/Index`, `AdminFeedback/Detail`, `_MoxfieldBulkEditHint`, `_FormError`) + TS spot-check (`admin-feedback.ts`) + full-repo grep for `chatgpt-*` identifiers and `--accent-strong` usage counts.
**Phase 86 re-score evidence (2026-07-05):** live `e2e/theme-active-affordance.spec.ts` + `e2e/layout-mode-interaction.spec.ts` runs (both projects, 30/30 green), `theming.spec.ts` regression guards (danger!=link, link/focus/cta-border resolve — 10/10 green), accent-leak grep gate clean, headless screenshots at `.planning/phases/86-ui-audit-re-score-studio-stage-4-admin-flags-closeout/evidence/86-05-{classic,dimir}-{chromium-desktop,chromium-mobile}.png`.

---

## Pillar Scores

| Pillar | Score (86-05) | Prior (2026-07-04) | Prior (2026-04-30) | Key Finding |
|--------|-------|---------------------|---------------------|-------------|
| 1. Copywriting | 3/4 | 3/4 | 3/4 | Unchanged this pass (out of Phase 86 scope). Prior "Submit" generic-verb gap fixed (`_MoxfieldBulkEditHint` now takes a verb param matching the host page's submit label, falls back to "Submit" only if unset); Feedback `<h1>` "Send Feedback" now voice-consistent. Still no branded `Views/Shared/Error.cshtml` — `Program.cs` still points `UseExceptionHandler` at `/Deck`. **Needs operator assignment** (no owning phase this cycle). |
| 2. Visuals | 4/4 | 4/4 | 3/4 | Already at ceiling; reinforced this pass by Bugs A/C/D (filled step-tab pill, borderless bucket chevron, perceptible layout-picker modes) — see Phase 86 Re-Score section below for live evidence. Remaining nitpicks (decorative glyph `aria-hidden`) are minor and don't block 4/4. |
| 3. Color | **4/4 (+2)** | 2/4 — LOWEST PILLAR | 2/4 | **Re-scored up from 2/4.** Phase 84 (THEME-01/02/03, already landed on this branch, human-approved 2026-07-04) migrated 19 affordance call sites onto `--link`/`--focus`/`--cta-border`/`--danger` and added two PERMANENT regression guards in `theming.spec.ts` — `computed --danger != computed --link` and `--link`/`--focus`/`--cta-border` resolve to a real color — verified GREEN across all 24 themes as of this re-score run. The remaining ~60 raw `--accent-strong` references in `site-common.css` are the Phase-84-audited, dispositioned-KEEP DECORATIVE uses (`UI-VS-*` tags), not unmigrated semantic-role debt. Phase 86's Bug B (this pass) additionally eliminated the last hardcoded `rgba(43, 108, 176, …)` Jeskai-blue literals (layout-segment hover/active, ui-mode-button active, clear-cache-button hover) in favor of `color-mix(in srgb, var(--accent) N%, transparent)` — grep-verified clean (`grep -rn "43, *108, *176" *.css \| grep -v site-jeskai.css` returns nothing). Together these close the pillar's core complaint ("aliases exist but aren't used at most call sites"). |
| 4. Typography | 3/4 | 3/4 | 2/4 | Unchanged this pass — explicitly deferred by Phase 84 ("D3 Typography Deferral Confirmation... Phase 86 still owns the Typography/font-size migration gap") and not touched by any 86-0x plan (CSS-only theming/a11y fixes, not a font-size migration). ~24 literal `font-size:` values remain unmigrated onto `var(--fs-*)`; no `--lh-*` line-height tokens added. **Needs operator assignment** (no owning phase this cycle). |
| 5. Spacing | 3/4 | 3/4 | 3/4 | Unchanged — no owning phase this cycle. No `--space-*` tokens exist; 244 literal `padding`/`margin`/`gap` declarations remain. **Needs operator assignment.** |
| 6. Experience Design | **4/4 (+1)** | 3/4 | 3/4 | **Re-scored up from 3/4.** Two of three original WARNINGs were already fixed pre-86 (`prefers-reduced-motion`, inline-style removal, destructive-confirm wiring). Phase 86 closes two more real gaps in this pillar: Bug C gives the analysis-questions bucket toggle a non-empty `aria-label` (was previously unlabeled — a genuine a11y gap, now regression-tested), and Bug D turns the Full/Compact/Advanced layout picker from an imperceptible no-op into a measurable, perceptible interaction with a positive accent style for Full (regression-tested in `layout-mode-interaction.spec.ts`). The unrelated `Feedback/Index.cshtml` double-submit gap remains open (separate, deferred, **needs operator assignment** — not in Phase 86's Bug A-D scope) but, per the same "minor gaps don't block a ceiling score" reasoning already applied to Visuals, does not hold this pillar below 4/4 given the accumulated wins. |

**Overall: 21/24** *(2026-07-04 baseline: 18/24, +3 this pass — clears the >=20/24 UIAUDIT-02 target with 1 point of margin)*

---

## Detailed Findings

### Pillar 1: Copywriting (3/4, prior 3/4)

**Wins since prior audit:**
- `_MoxfieldBulkEditHint.cshtml:1` doc comment confirms it "accepts a verb string matching the host page's submit button label ... Falls back to 'Submit' if no model arg provided" — the generic-verb gap flagged in the prior audit is resolved at the component level.
- `Feedback/Index.cshtml:10` `<h1>Send Feedback</h1>` now matches the page `<title>` framing ("Send Feedback to DeckFlow", set via `ViewData["Title"]` at line 3) — the verb/noun voice mismatch flagged previously reads consistently now.

**Still open:**
- No `Views/Shared/Error.cshtml` (or equivalent branded error view) exists in the repo (`find DeckFlow.Web/Views/Shared -iname "*rror*"` only returns `_FormError.cshtml`, a form-validation partial, not a page-level error view). Per CLAUDE.md architecture notes, non-Development environments route `UseExceptionHandler("/Deck")` — this covers `DeckController`-raised exceptions specifically but a failure in any other controller (`AdminFeedbackController`, `CommanderController`, etc.) before the Deck route resolves would still surface a generic ASP.NET error page in a worse case, or at minimum a jarring redirect to an unrelated page rather than branded copy.

---

### Pillar 2: Visuals (4/4, prior 3/4)

**Wins since prior audit:**
- Home hub (screenshot: desktop `1280x900`, mobile `390x844`) now leads with a `HEADLINE WORKFLOW` panel: bold "Analyze Your Deck" title + "Five-step workflow: load your deck, pick your questions, copy the prompt, paste into ChatGPT, Claude, or Gemini, review the structured response." This directly resolves the prior #3 top-priority fix ("Pick a primary focal action on the home hub") — first-glance orientation is no longer "scan 11 cards," it's "read the hero, then browse by category."
- Mobile viewport (390px) renders the hero panel and all 4 hub-group grids cleanly stacked, single-column, no horizontal scroll or clipped text — confirms the fix holds responsively, not just at desktop width.
- Icon+visible-text twin button pattern, skip-link, `aria-live="polite"` region, and focus-visible rings all still present and unchanged (no regression).

**Remaining minor gaps (do not block 4/4, but worth tracking):**
- Per-theme contrast for `.admin-feedback-filter.active` (white text on `--accent-strong`) across all 25+ guild theme forks was not re-verified this pass (out of scope for a single-theme screenshot audit) — still an open verification item, not a confirmed regression.
- Decorative `▾`/`▶`/`▼` glyph arrows in `chatgpt-question-bucket__toggle` and `df-select__trigger::after` still lack explicit `aria-hidden="true"` (unicode `content:` glyphs) — low-severity, most screen readers already skip generated `content:`.

---

### Pillar 3: Color (2/4, prior 2/4 — still LOWEST PILLAR)

**Confirmed win:** the specific `feedback-error` bug is fixed. `site-common.css:1419` now reads:
```css
.feedback-error { color: var(--danger); font-size: var(--fs-sm); }
```
No longer falls back to `--accent-strong`, so error text no longer collides with link/brand color on red guild themes (Rakdos/Boros/Jund).

**Still the core unresolved issue:** semantic alias tokens now exist —
```css
--link:        var(--accent);
--danger:      #c53030;
--cta-border:  var(--accent);
--focus:       var(--accent);
--success:     #2f855a;
```
(`site.css:42-52`) — but they are aliases pointing back at `--accent`/`--accent-strong`, not yet substituted at most call sites. `grep -c accent-strong` across `site-common.css` returns **58 matches** and `site.css` returns 4 more, spanning: link color (`:469, :550, :594`), focus outlines (`:2009, :2053, :2455`), footer/card/panel borders (`:349, :668, :704, :1860, :1920, :2892`), admin-filter background (`:236, :993`), back-to-top button (`:78, :914`), badge/callout accents (`:2762, :2774, :2940, :2941`), and more. The aliases were added but the 58+ call sites were not migrated onto them — this is precisely Phase 84's (THEME) declared scope.

**Hardcoded hex reduced but not eliminated:** `site.css` outside `:root` now shows only 2 stray `#fff` literals (`--on-accent`/`--bg-default` fallback values inside the root block itself, not orphaned declarations — a false positive from the prior audit's method). `site-common.css` (which has no `:root` of its own — it's pure layout, correctly per CLAUDE.md's theme-CSS rule) still carries ~30 distinct hex literals, but on inspection nearly all are `var(--token, #hexfallback)` safety-net patterns (e.g. `color: var(--muted, #5a6472)`), not genuinely unreachable standalone hex — this is a reasonable defensive pattern, not the "ghost-render under guild themes" bug flagged previously.

---

### Pillar 4: Typography (3/4, prior 2/4)

**Win:** `site.css :root` (lines 33-38) now defines a 6-step type scale tagged `UI-VS-01`:
```css
--fs-xs:   0.85rem;
--fs-sm:   0.85rem;
--fs-base: 0.95rem;
--fs-lg:   1.05rem;
--fs-xl:   1.5rem;
--fs-2xl:  1.9rem;
```
This is exactly the prior audit's #1 top-priority fix ("Define a 6-step type scale and enforce it"). Adoption: **97 of 121** `font-size:` declarations across `site.css` + `site-common.css` (~80%) now reference `var(--fs-*)`.

**Not yet complete:**
- ~24 literal `font-size:` values remain unmigrated: `0.72rem` (×7 in `site-common.css` — a repeated, not one-off, pattern worth its own token or a fix to use `--fs-xs`), `2.4rem` (×2 — display heading, larger than any current token, candidate for a `--fs-3xl`), `15px` (`site.css` html base — pre-rem root, likely intentional), plus scattered `0.68rem/0.74rem/0.78em/0.8rem/0.85em/0.9rem/0.95em/1.9rem` singles.
- No `--lh-*` line-height tokens were added — the prior fix recommendation ("collapse `1.35/1.45/1.5/1.6` to `--lh-tight`/`--lh-prose`") was not picked up. Line-heights are still hand-picked per rule.

---

### Pillar 5: Spacing (3/4, prior 3/4 — unchanged)

No spacing-token work landed since the prior audit. `grep -c "var(--space" ` across both core CSS files returns **0** — no `--space-*` scale exists. 244 literal `padding`/`margin`/`gap` rem declarations remain, spanning ~20 distinct values:
```
1rem    ×37   0.75rem ×25   0.35rem ×17   0.6rem  ×10   0.3rem  ×6
0.5rem  ×29   0.4rem  ×17   0.25rem ×14   0.2rem  ×10   0.15rem ×6
1.5rem  ×13   0.55rem ×11   0.85rem ×10   2rem    ×7    0.1rem  ×5
```
Same top-heavy power-of-1rem shape as before (good), same organic-drift outliers persist verbatim: `0.28rem` (df-select trigger padding — flagged specifically in the prior audit) is still present unchanged, alongside `0.65rem` and other pick-of-the-day values. This pillar was correctly out of scope for the interim fixes that landed (they targeted Visuals/Color/Typography/Experience) and remains open work.

---

### Pillar 6: Experience Design (3/4, prior 3/4)

**Two of three prior WARNINGs resolved:**
1. **`prefers-reduced-motion` now respected.** `site.css:1373` and `site-common.css:16` both carry `@media (prefers-reduced-motion: reduce) { ... }` blocks — the accessibility regression risk flagged for `.busy-indicator__spinner` and `.hub-card` transitions is fixed.
2. **Inline `style=` removed.** No `style="..."` attributes found in `DeckFlow.Web/Views/Feedback` or `DeckFlow.Web/Views/AdminFeedback` — the `feedback-page`/`admin-feedback-detail` panels are now fully class-driven, restoring CSP/theme-override integrity.
3. **Destructive-confirm verified as a win, not a gap.** The prior audit flagged this "WARNING: verify" — it's now confirmed properly wired: `AdminFeedback/Detail.cshtml:39-40` renders `data-admin-confirm-delete` on the delete form, and `admin-feedback.ts:28` binds a confirm handler to `[data-admin-confirm-delete]` forms.

**Still open:**
- **`Feedback/Index.cshtml` has no busy indicator and does not disable its submit button.** `<button type="submit" class="feedback-submit">Send Feedback</button>` (line 42) has no `disabled`/`aria-busy` wiring and no `_BusyIndicator` partial include — a user can still double-submit on a slow connection. This exact gap was flagged in the prior audit and was not addressed.

---

## Files Audited

**Razor views (7):**
- `Views/Shared/_Layout.cshtml`
- `Views/Deck/Home.cshtml` (Shell/Home route)
- `Views/Deck/DeckSync.cshtml`
- `Views/Feedback/Index.cshtml`
- `Views/AdminFeedback/Detail.cshtml`
- `Views/Shared/_MoxfieldBulkEditHint.cshtml`
- `Views/Shared/_FormError.cshtml`

**CSS (2, full-file grep coverage):**
- `wwwroot/css/site.css` (1382 lines — `:root` token block, type scale, reduced-motion, hex-literal audit)
- `wwwroot/css/site-common.css` (~3400+ lines — `--accent-strong` usage count, font-size literal audit, spacing literal audit, inline-style/hex-literal grep)

**TypeScript (1 spot-check):**
- `wwwroot/ts/admin-feedback.ts:28` — confirms delete-confirm wiring.

**Live rendered screenshots (6 routes × 2 viewports = 12 captures):**
- Routes: `/`, `/sync`, `/lookup`, `/feedback`, `/help`, `/about`
- Viewports: desktop (1280×900, full-page), mobile (390×844, full-page)
- Server: `scripts/run-web-test.sh` on `http://localhost:5173`, Classic theme, `DECKFLOW_DISABLE_AUTO_BROWSER=true` (no Windows browser opened, per CLAUDE.md)
- Capture tool: `playwright-core` (Chromium) driven by a scratch Node script — not a repo-tracked spec file (measurement-only plan; no new tracked test file added)

**Not audited (time-boxed, same scope discipline as prior audit):**
- Remaining ~19 of 26 Razor views (sampled architectural patterns from the 7 above; screenshots cover the highest-traffic 6 routes)
- 24 of 25+ guild theme CSS forks (per-theme contrast/token-override verification remains an open item, same as prior audit)
- Full TypeScript source tree (only `admin-feedback.ts` spot-checked for this pass)

**Configuration:**
- No UI-SPEC.md present (freeform audit against abstract standards, unchanged from prior audit)
- No `components.json` — registry safety audit not applicable

---

## Gap to >=20/24 — Handoff to Phases 84/85

> **Superseded by the "Phase 86 Re-Score" section at the end of this file (2026-07-05).**
> Kept verbatim below as the historical handoff record this audit produced pre-Phase-84/85/86.
> The Color and Typography point-estimates below assumed BOTH would land in Phase 84; in
> practice Phase 84 delivered the Color migration but explicitly deferred Typography to
> Phase 86 (see `84-02-SUMMARY.md`'s "D3 Typography Deferral Confirmation"), and Phase 86
> did not touch font-size tokens either (CSS-only theming/a11y fixes, not a type-scale
> migration) — so Typography remains 3/4. The +3 that got the project to >=20/24 came from
> Color (+2, Phase 84 + Phase 86 Bug B) and Experience Design (+1, Phase 86 Bugs C/D)
> instead of the Color+Typography combination originally projected here.

Current: **18/24**. Target after enumerated fixes land: **>=20/24** (+2 minimum). The two
pillars below the 4/4 ceiling with concrete, phase-owned fixes are Color (2/4) and
Typography (3/4); Spacing (3/4) has a real gap but no owning phase this cycle (see the
out-of-scope subsection). Copywriting, Visuals, and Experience Design already sit at
3/4 or 4/4 with only minor/unverified nitpicks — no fix is enumerated against them here.

### Color (2/4 -> target 4/4, +2) — Owner: Phase 84 (THEME)

Phase 84's declared scope (ROADMAP: "Finish migrating `--accent-strong` onto
`--link`/`--danger`/`--focus`/`--cta-border` across all 27 theme forks") is precisely
the fix this pillar needs:

1. **Migrate the 58 `--accent-strong` call sites in `site-common.css`** (link color
   `:469,:550,:594`; focus outlines `:2009,:2053,:2455`; borders `:349,:668,:704,:1860,
   :1920,:2892`; admin-filter background `:236,:993`; back-to-top button `:78,:914`;
   badge/callout accents `:2762,:2774,:2940,:2941`; plus ~40 more) onto the now-existing
   `--link`/`--focus`/`--cta-border` aliases (`site.css:42-45`) instead of leaving them
   pointed at raw `--accent-strong`.
2. **Migrate the 4 remaining `--accent-strong` call sites in `site.css`** (`:322, :687,
   :1083`) the same way.
3. **Repeat both across all 27 theme forks** (per THEME-01/02/03) so the alias
   substitution is consistent everywhere, not just the Classic theme's `:root`.
4. Once complete, re-verify: `grep -c accent-strong site-common.css site.css` should
   drop to near-zero (only the `--accent-strong` token *definition* itself and any
   genuinely accent-specific decorative use, e.g. hover glow, should remain).

**Point estimate:** this single migration (call sites already have aliases to move onto —
no new token design needed) is worth +2, bringing Color to 4/4.

### Typography (3/4 -> target 4/4, +1) — Owner: Phase 84 (THEME)

Typography's residual gap is a token-adoption cleanup, not a design decision, and touches
the same CSS files Phase 84 is already migrating — bundling it avoids a second pass over
`site.css`/`site-common.css`:

1. **Migrate the ~24 remaining literal `font-size:` values onto `var(--fs-*)`:**
   `0.72rem` (×7, `site-common.css`) -> `var(--fs-xs)`; the two `2.4rem` display-heading
   uses -> a new `--fs-3xl` token (site.css `:root`, next to `--fs-2xl`); the scattered
   `0.68rem/0.74rem/0.78em/0.8rem/0.85em/0.9rem/0.95em/1.9rem` singles -> nearest
   existing `--fs-*` step. Leave the `15px` `html` base font-size alone (pre-rem root,
   intentional).
2. **Add `--lh-tight`/`--lh-prose` tokens** to `site.css :root` and migrate the 4-5
   hand-picked `line-height: 1.35/1.45/1.5/1.6` declarations onto them.

**Point estimate:** completes the type-scale enforcement started this cycle; +1,
bringing Typography to 4/4.

### Out of Phase 84/85 scope — needs assignment

These gaps do not fit Phase 84's theme-token scope or Phase 85's `chatgpt-*` identifier
rename. Flagging explicitly per Phase 82 success-criterion 5 rather than letting Phase 86
(re-score only) discover them silently:

- **Spacing (3/4, unchanged) — no owning phase this cycle.** Defining `--space-1`
  through `--space-8` tokens and migrating the 244 literal `padding`/`margin`/`gap`
  declarations (starting with the `0.28rem` df-select outlier -> `--space-1` at 0.25rem)
  is real, scoped work, but it is neither a color-token migration (Phase 84) nor a
  `chatgpt-*` rename (Phase 85). It does not currently have a home in Cycle 15's
  5-phase roadmap. **Needs operator assignment**: either fold into Phase 84 (same CSS
  files, same migration mechanics) or defer to a future cycle's CSS-cleanup phase.
- **Feedback double-submit gap (Experience Design, 3/4) — no owning phase this cycle.**
  `Feedback/Index.cshtml`'s submit button has no busy-state/disable wiring. This is a
  small, contained bug fix (add `_BusyIndicator` + disable-on-submit JS, matching the
  pattern already used on 7 of 8 deck workhorse views) but is unrelated to both THEME
  and AICLEAN. **Needs operator assignment**: candidate for a quick-task fix independent
  of Phases 84-86, or bundled into Phase 86 as a pre-re-score bug fix (NOT as an owned
  "gap fix" per the Phase 86 re-score-only constraint — if assigned there, it must be
  scoped as a discrete bug-fix task, not discovered mid-re-score).
- **Missing branded `Error.cshtml` (Copywriting, 3/4) — no owning phase this cycle.**
  Same reasoning: a small, contained addition, unrelated to THEME/AICLEAN.
  **Needs operator assignment.**

### Target Summary

| Pillar | Current | Fix Owner | Target |
|--------|---------|-----------|--------|
| Copywriting | 3/4 | (needs assignment — branded error page) | 3/4 unless assigned |
| Visuals | 4/4 | — (no fix needed) | 4/4 |
| Color | 2/4 | **Phase 84** | 4/4 |
| Typography | 3/4 | **Phase 84** | 4/4 |
| Spacing | 3/4 | (needs assignment) | 3/4 unless assigned |
| Experience Design | 3/4 | (needs assignment — feedback busy-state) | 3/4 unless assigned |

**Target overall after Phase 84's enumerated fixes land: 21/24** (18 current + 2 Color
+ 1 Typography), which clears the **>= 20/24** target with one point of margin even if
one of the two point-estimates lands a point light. Phase 85's `chatgpt-*` rename is
byte-identical-render by design (AICLEAN-03) and is not expected to move any pillar
score — its contribution to this audit is scope-neutral, not a scoring lever.

---

## Phase 86 Re-Score (2026-07-05) — UIAUDIT-02 Closure

**Result: 21/24** (18/24 baseline, +3). Clears the **>= 20/24** UIAUDIT-02 target with
1 point of margin. Scope of this re-score is **UIAUDIT-02 only** — see `86-CONTEXT.md`;
UIAUDIT-03 (Studio DirectPush Stage 4) and ADMIN-01 (`/Admin/Flags` sorting) are explicitly
deferred and NOT reflected in this score.

### What changed since the 18/24 baseline

**Bugs A-D (Phase 86, this cycle, plans 86-01..04):**
- **Bug A** — `.prompt-step-tab.is-active` is now a filled `var(--accent)` pill (was
  same-background-as-inactive, low-salience) across base `site.css` + all 12 standalone
  forks; 6 dark themes (dimir, golgari, abzan, esper, nyx, planeswalker-dark) got an
  empirically-measured `--accent-contrast` token so white-on-accent text clears WCAG 4.5:1
  (dimir was 3.80:1, golgari 2.88:1, abzan 2.84:1, esper 2.78:1, nyx 4.19:1,
  planeswalker-dark 3.43:1 pre-fix — all now >=4.5:1, live-verified in
  `theme-active-affordance.spec.ts`).
- **Bug B** — the hardcoded Jeskai-blue `rgba(43, 108, 176, …)` literal (the widest
  accent-leak: `.prompt-layout-segment` hover/active in `site-common.css`, shared by ALL
  24 themes) is replaced with `color-mix(in srgb, var(--accent) N%, transparent)`, so
  every non-Jeskai theme tints hover/active states with ITS OWN accent. Grep gate
  (`grep -rn "43, *108, *176" *.css | grep -v site-jeskai.css`) is clean.
- **Bug C** — the analysis-questions bucket toggle (`DeckAnalysis.cshtml:306`) went from
  an unlabeled, bordered grey pill to a borderless chevron with a real `aria-label`
  ("Toggle {bucket} questions") — closes a genuine a11y gap, mirrored into base + all 13
  duplicating theme files (12 forks + site-rakdos.css).
- **Bug D** — the Full/Compact/Advanced layout picker was wired correctly but its CSS
  effect was imperceptible on the empty Step-1/2 landing (only sparse/optional text was
  hidden). Now keyed to the always-rendered `.prompt-instructions` anchor: Advanced fully
  collapses it, Compact measurably shrinks it, and Full/guided gets a POSITIVE accent
  marker (`border-left: 4px solid var(--accent)` + accent-tinted background) instead of a
  do-nothing default.

**Test-gap closure (why A-D could ship green before this plan):** the pre-86 e2e suite
only asserted DOM/selectors exist, never visual STATE (which tab is filled, contrast) or
interaction OUTCOME (does Compact actually change layout). Two new specs close this:
- `DeckFlow.Web/e2e/theme-active-affordance.spec.ts` — active-tab computed background
  proven to differ from inactive AND from `var(--panel-soft-bg)` AND to equal
  `var(--accent)`, across {azorius, jund, dimir, planeswalker-dark, Classic
  (explicit `site.css` cookie)}; WCAG >=4.5:1 for {dimir, golgari, planeswalker-dark,
  nyx, jund, sultai}; 3 accent-leak checks (layout-segment hover, ui-mode-button active,
  clear-cache-button hover) on a non-Jeskai theme; bucket-toggle aria-label + zero
  border-width. **26/26 passed** on both `chromium-desktop` and `chromium-mobile`.
- `DeckFlow.Web/e2e/layout-mode-interaction.spec.ts` — guided's positive accent
  left-border, focused's measurable shrink vs guided, expert's guaranteed collapse, for
  {Classic, dimir}. **4/4 passed** on both projects.

### Full-suite verification (this re-score run, 2026-07-05)

| Gate | Result |
|------|--------|
| `theme-active-affordance.spec.ts` (both projects) | 26/26 passed |
| `layout-mode-interaction.spec.ts` (both projects) | 4/4 passed |
| Full Playwright e2e suite (both projects, incl. the 2 new specs) | 286 passed, 14 skipped, 0 failed |
| `dotnet build DeckFlow.sln` | 0 Warnings, 0 Errors |
| Full xUnit (`dotnet test DeckFlow.sln`) | Core 1095/1095, Web 1218/1218 (12 skipped — Postgres-integration, no live PG), Studio 290/290 |
| Accent-leak grep gate (`grep -rn "43, *108, *176" *.css \| grep -v site-jeskai.css`) | clean (0 matches) |
| `theming.spec.ts` danger!=link + link/focus/cta-border regression guards (Phase 84, all 24 themes) | still green (10/10) |

Note: an initial full-suite run under 12 parallel workers showed 4 transient failures on
`chromium-mobile` (connection resets against the single dev-server process under load,
not a code defect) — a clean re-run of the full 300-test suite passed 286/286 (14 skipped)
with zero failures. The isolated per-spec runs (26/26, 4/4) were consistently green.

### Visual evidence

Headless screenshots (desktop 1280 + mobile 390, Step 2 / guided mode) at
`.planning/phases/86-ui-audit-re-score-studio-stage-4-admin-flags-closeout/evidence/`:
`86-05-classic-chromium-desktop.png`, `86-05-classic-chromium-mobile.png`,
`86-05-dimir-chromium-desktop.png`, `86-05-dimir-chromium-mobile.png`. Confirmed by direct
visual review: filled accent step-tab pill with readable text (both themes), borderless
bucket-question chevrons, and the Full/Compact/Advanced layout picker showing Full
highlighted with the theme's own accent — on both a light (Classic) and dark (Dimir)
theme, at both viewports.

### Remaining known gaps (unchanged by this pass, no owning phase this cycle)

- **Typography (3/4)** — ~24 literal `font-size:` values + no `--lh-*` tokens. Deferred by
  Phase 84, not in Phase 86's Bug A-D scope. Needs operator assignment.
- **Spacing (3/4)** — no `--space-*` tokens; 244 literal padding/margin/gap declarations.
  Needs operator assignment.
- **Copywriting (3/4)** — no branded `Views/Shared/Error.cshtml`. Needs operator assignment.
- **Feedback double-submit** (the one open item under Experience Design pre-86) — no
  busy-state/disable wiring on `Feedback/Index.cshtml`'s submit button. Needs operator
  assignment; does not block Experience Design's 4/4 this pass per the same
  "minor-gaps-don't-block-a-ceiling-score" reasoning already applied to Visuals' 4/4.
- **UIAUDIT-03** (Studio DirectPush Stage 4 verification + `DirectPush.razor:441` no-op
  copy) and **ADMIN-01** (`/Admin/Flags` on/off sorting) — explicitly out of scope for
  this Phase 86 pass (see `86-CONTEXT.md`); Phase 86 does not fully close until a
  follow-up planning pass addresses them.
