# Phase 100 — UI Review

**Audited:** 2026-07-19
**Baseline:** `.planning/phases/100-creator-style-tool-surface/100-UI-SPEC.md` (checker-approved 6/6 pre-implementation)
**Screenshots:** captured (empty-store state only — desktop 1280x900, mobile 390x844; committed prod seed is `[]` so the picker/result states cannot be screenshotted and were audited from `CreatorStyle.cshtml` markup + `CreatorStyleViewRenderTests.cs` rendered-HTML assertions)

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 4/4 | Every UI-SPEC copy string (H1, ledes, tile copy, empty-store text, CTA, banners, exemplar line) matches the contract verbatim; no crawl/KB-sourcing leakage. |
| 2. Visuals | 2/4 | Summary-strip verdict chips are wrapped in `.field`, which is `flex-direction: column` — each chip renders on its own vertical row instead of the "small summary strip" the spec/D-100-13 describe. |
| 3. Color | 3/4 | Accent correctly restricted to `.run-button`/`.copy-button`/focus rings, zero hardcoded colors — but verdict chips ship with no `--good`/`--ok`/`--low` modifier, so the help doc's promised "pass/caution/miss" color signal doesn't exist. |
| 4. Typography | 4/4 | Zero new font-size/weight rules; page inherits the existing `--fs-*` token set and default `h1`/`h2` weights exactly as the contract requires. |
| 5. Spacing | 4/4 | Zero new CSS shipped (`site-common.css` untouched, confirmed by diff); all spacing inherited from existing on-grid classes, no arbitrary/inline values introduced. |
| 6. Experience Design | 4/4 | All required states present and distinctly styled: loading (busy-indicator), error, empty-store info, profile-unavailable info (D-100-16), grounding-degraded warning, CSRF + flag-gate on both actions, 10 automated tests covering the state matrix. |

**Overall: 21/24**

---

## Top 3 Priority Fixes

1. **Verdict chips stack vertically instead of forming a strip** — `Views/Deck/CreatorStyle.cshtml:117-127` wraps the rubric-verdict `<span class="manabase-chip">` loop inside `<div class="field">`, and `.field` is defined as `display:flex; flex-direction:column; gap:0.4rem` (`site.css:843-848`) — a class purpose-built for stacking a `<label>` above its `<input>`. Every `<span>` becomes its own flex row, so N verdict chips render as N stacked lines instead of the "small summary strip" the UI-SPEC/D-100-13 call for, and instead of the horizontal chip usage seen everywhere else in the codebase (e.g. `Manabase.cshtml:334` uses a single inline `.manabase-chip` inside a `<p>`, never inside `.field`). **Fix:** wrap the chip loop in a plain `<div class="toolbar">` (already used elsewhere on this same page for the CTA/copy-button rows and is flex-row by convention) or add a single new on-grid rule to `site-common.css` (e.g. `.creator-style-verdicts { display:flex; flex-wrap:wrap; gap:0.5rem; align-items:center; }`) instead of reusing `.field`.

2. **Verdict chips carry no color/status differentiation** — `Views/Deck/CreatorStyle.cshtml:120` renders `<span class="manabase-chip">@verdict</span>` with no modifier class. `.manabase-chip` alone (`site-common.css:2572-2580`) has `border: 1px solid transparent` and no background/text color — it is a bare pill until paired with `--low`/`--ok`/`--good`/`--health-*`, which every other consumer of this class supplies (`Manabase.cshtml:334,575`). The engine's verdict vocabulary is `on-target` / `under` / `over` / `insufficient-measured` (`CreatorStyleRubricScorer.cs:75-83`), so a mapping is straightforward. This matters because `DeckFlow.Web/Help/creator-style.md:29` explicitly promises the reader "fast pass / caution / miss signals" from these chips — as shipped, every verdict looks visually identical, so the promised at-a-glance signal doesn't exist. **Fix:** map verdict string → existing modifier class in the view (e.g. `on-target` → `manabase-chip--good`, `under`/`over` → `manabase-chip--ok`/`--low`, `insufficient-measured` → no fill/neutral) — no new CSS needed, the modifiers already exist.

3. **Exemplar names can fall back to a raw internal `DeckId`** — `Views/Deck/CreatorStyle.cshtml:12-15` selects `exemplar.FolderName` and falls back to `exemplar.DeckId` when `FolderName` is null/blank (`CreatorStyleExemplarDeck.FolderName` is nullable, `CreatorStylePacketService.cs:265-266`). If an exemplar deck in the seed lacks a folder name, the "Exemplars: {name}, {name}" summary strip could show a raw Archidekt/Moxfield deck ID string to the end user instead of a human-readable deck name, which reads as broken/unfinished UI copy. Low severity (depends on seed data quality, not exercisable under the current empty-store screenshots) but worth a guard — either fix at the seed/export layer (Plan 04) so `FolderName` is always populated for exemplars, or fall back to a friendlier "(unnamed exemplar)" string in the view instead of the bare ID.

---

## Detailed Findings

### Pillar 1: Copywriting (4/4)

Verified every UI-SPEC Copywriting Contract row against the actual rendered/source strings:

- H1 "Creator-Style Deck Critique" — `CreatorStyle.cshtml:19`, matches contract and screenshot. ✓
- Lede/secondary lede — `CreatorStyle.cshtml:20-21`, byte-identical to contract. ✓
- Home-tile title/description — `ToolRegistry.cs:17`: `"Creator-Style Critique"` / `"Critique your deck against a creator's measured build style — real exemplars, weighted targets, no vibes."` — matches contract exactly. ✓
- Creator picker label "Creator" — `CreatorStyle.cshtml:49`. ✓
- Picker option format `{Name} — {N} decks · {M} videos` — `CreatorStyleController.cs:121`, verified by `CreatorStyleViewRenderTests.cs:47,56-57` ("Salubrious Snail — 39 decks · 12 videos"). ✓
- Empty-store copy — `CreatorStyle.cshtml:34-35` — matches contract verbatim, confirmed live in `creator-style-desktop.png`/`creator-style-mobile.png`. ✓
- CTA "Build Critique Packet" — `CreatorStyle.cshtml:89`. ✓
- "Your Deck" / "Required" — `CreatorStyle.cshtml:61-62`. ✓
- "Result" heading — `CreatorStyle.cshtml:106`. ✓
- "Exemplars: {name}, {name}, {name}" — `CreatorStyle.cshtml:124`. ✓
- Copy-button "Copy" — `CreatorStyle.cshtml:131`. ✓
- Generic submit-failure copy — `CreatorStyleController.cs:75`: "Couldn't build the packet. Check your deck input and try again." matches contract. ✓
- No mention of "crawl"/"scrape"/"transcript"/"KB" anywhere in the view, controller, or help doc (`grep` clean); README's only use of "videos" is inside the sanctioned evidence-depth label, per the standing craft-first/no-KB-mention rule. ✓
- No generic `Submit`/`Click Here`/`OK`/`Cancel` labels found in the view (`grep` clean).

Minor (informational, not scored down): exemplar-name fallback to raw `DeckId` — see Top Fix #3.

### Pillar 2: Visuals (2/4)

- Focal point: `.run-button` "Build Critique Packet" is the only accent-colored, visually heavier element on the page — correct per contract's "primary visual anchor" instruction. ✓
- No icon-only buttons; `.copy-button`/`.run-button` both carry text labels — no aria-label gap. ✓
- **Defect (Top Fix #1):** the summary-strip verdict chips are nested inside `.field` (`CreatorStyle.cshtml:117-127`), which is `flex-direction: column` (`site.css:843-848`). Each `<span class="manabase-chip">` becomes an independent flex row, so multiple verdict chips stack vertically instead of forming the horizontal "small summary strip" the plan (D-100-13) and UI-SPEC (line 54, citing `.manabase-chip`/`.manabase-lens-pill` as "the visual precedent") describe. This is a real, reproducible rendering outcome (not speculative) — confirmed by reading the CSS rule that applies to `.field` and the chip markup that reuses it verbatim with no override.
- Hierarchy otherwise intact: H1 > lede > nav tabs > form > result — matches sibling tool conventions (`Manabase.cshtml` shape).

### Pillar 3: Color (3/4)

- `grep` for hex/`rgb(`/inline `style=` in `CreatorStyle.cshtml` returns nothing — zero hardcoded colors introduced. ✓
- Accent usage confined to `.run-button` (1 occurrence) and `.copy-button` (1 occurrence) in the view — matches the contract's "reserved for primary CTA, picker selected-state border, copy-button, focus rings — nothing else" rule; no accent leakage onto banners, chips, or body copy. ✓
- `site-common.css` and all 15 theme files are untouched by this phase (`git diff HEAD~1 -- 'DeckFlow.Web/wwwroot/css/*.css'` empty) — no new custom properties, satisfying the "add to `:root` of every theme file" fan-out concern by simply not needing one. ✓
- **Defect (Top Fix #2):** verdict chips ship with the bare `.manabase-chip` class and no `--low`/`--ok`/`--good` modifier (`CreatorStyle.cshtml:120`), unlike every other consumer of this class in the codebase (`Manabase.cshtml:334,575`, which always pair it with `ManabaseDisplay.HealthCss(...)` or `chip.Css`). Result: chips render as a neutral, uncolored pill regardless of whether the verdict is `on-target`, `under`, or `over` — no status color exists at all, directly undercutting the help doc's "pass / caution / miss" promise (`Help/creator-style.md:29`).

### Pillar 4: Typography (4/4)

- No new `font-size`/`font-weight` declarations anywhere in the diff. The page uses plain `<h1>`/`<h2>`/`<label>`/`<p>` tags and pre-existing classes (`.page-lede`, `.lede`, `.sync-column__header`, `.sync-column__status`, `.manabase-chip`), all of which already resolve to the contract's declared `--fs-*` tokens (`--fs-2xl`/`--fs-xl`/`--fs-sm`/`--fs-xs`) and weights (400/600/700) verified in `site-common.css` (e.g. `.hub-hero__title` at `--fs-xl`/700, `.sync-column__status` at `--fs-xs`/600). No 5th font size, no 3rd weight introduced. ✓

### Pillar 5: Spacing (4/4)

- `SUMMARY.md` deviation note confirms `site-common.css` was left untouched ("existing classes sufficed") — verified directly: no diff against `wwwroot/css/*.css` for this phase.
- All spacing in the new markup comes from pre-existing classes (`.field`, `.sync-columns`/`.sync-column`, `.toolbar`, `.result-panel`) reused verbatim, which the UI-SPEC explicitly carves out as legitimate even where the underlying legacy values (e.g. `.sync-column` padding `1rem`, `.field` gap `0.4rem`) sit off the strict 4px grid — these are inherited, not new. No new page-specific spacing rule was authored, so the "restrict new selectors to on-grid multiples-of-4" constraint has nothing to violate. ✓
- No arbitrary inline `style=`/`margin:`/`padding:` values in the view (`grep` clean). ✓

### Pillar 6: Experience Design (4/4)

State coverage, verified against both the controller and the 10 shipped tests (`CreatorStyleControllerTests` ×8, `CreatorStyleViewRenderTests` ×2):

- **Loading:** `data-busy-*` attributes on the `<form>` drive the shared busy-indicator overlay, matching sibling convention. ✓
- **Empty store (D-100-16):** `NoProfilesLoaded` renders `.info-banner` with no form at all — confirmed live in both screenshots (flag ON, committed `[]` seed). ✓
- **Profile-unavailable / InsufficientSample (D-100-16, checker BLOCKER 2):** distinct `.info-banner` outside the `HasResult` block, rendering `Result.Notice`, never dressed as grounding degradation — explicitly tested (`CreatorStyleControllerTests`) and rendered in markup (`CreatorStyle.cshtml:94-99`). ✓
- **Grounding degraded:** `.warning-banner` shown only when `GroundingDegraded && Notice` non-empty (`CreatorStyle.cshtml:110-113`), IN-03-branched wording supplied by the Phase-99 engine and forwarded verbatim (no re-wording in the view, per contract). ✓
- **Generic error:** `.error-banner` bound to `Model.ErrorMessage`, hidden when empty (`CreatorStyle.cshtml:27-29`), four-way guarded ladder (timeout/validation/upstream/generic) in `RunGuardedAsync`. ✓
- **CSRF:** `[ValidateAntiForgeryToken]` + `@Html.AntiForgeryToken()` present and asserted by attribute-presence test. ✓
- **Flag gate:** `[FeatureFlagGate("tool.creator-style.enabled")]` on both GET and POST; route 404s OFF (covered by `ToolRouteGateCoverageTests`, called out in the SUMMARY as the resolved wave-1 red). ✓
- No destructive action exists on this page (matches spec — no confirmation dialog needed). ✓

Registry audit: not applicable — `components.json` absent (confirmed by UI-SPEC's own Design System section: "no shadcn/Tailwind in this stack"). Skipped per audit instructions.

---

## Files Audited

- `DeckFlow.Web/Views/Deck/CreatorStyle.cshtml`
- `DeckFlow.Web/Controllers/CreatorStyleController.cs`
- `DeckFlow.Web/Models/CreatorStyleViewModel.cs`
- `DeckFlow.Web/Help/creator-style.md`
- `README.md` (Creator-Style section + `/creator-style-ledger` reference)
- `DeckFlow.Web/wwwroot/css/site-common.css` (confirmed unmodified by this phase)
- `DeckFlow.Web/wwwroot/css/site.css` (`.field`, `.sync-columns`/`.sync-column`, `.sync-column__*` rules — pre-existing, cross-referenced for the chip-stacking defect)
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` (sibling comparison template)
- `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml`, `DeckComparison.cshtml` (secondary sibling comparison for `.sync-columns`/picker idioms)
- `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs` (`CreatorStyleExemplarDeck`, `CreatorStylePacketResult` shapes)
- `DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs` (verdict vocabulary: `on-target`/`under`/`over`/`insufficient-measured`)
- `DeckFlow.Web/Services/Tools/ToolRegistry.cs` (home-tile title/description entry)
- `DeckFlow.Web.Tests/CreatorStyleViewRenderTests.cs`, `CreatorStyleControllerTests.cs` (rendered-HTML/state assertions used in lieu of live screenshots for the populated-form/result states)
- `.planning/phases/100-creator-style-tool-surface/100-UI-SPEC.md`, `100-CONTEXT.md`, `100-05-PLAN.md`, `100-05-SUMMARY.md`
- Screenshots: `creator-style-desktop.png` (1280x900), `creator-style-mobile.png` (390x844) — empty-store state, flag ON
