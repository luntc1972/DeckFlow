# Phase 84: Theme Semantic-Token Migration - Research

**Researched:** 2026-07-04
**Domain:** CSS custom-property (design-token) migration across 27 theme stylesheets, ASP.NET Core static-asset pipeline
**Confidence:** HIGH (all claims verified directly against the repository's own CSS files, git history, and existing test suites — no external library research was needed; this is a self-contained CSS refactor)

## Summary

Phase 84 must finish a migration that a prior pass ("UI-VS-01/02/03") started but did not complete: `site.css`'s `:root` already defines four semantic aliases (`--link`, `--danger`, `--focus`, `--cta-border`), and 11 of 22 guild forks already copy that same alias block into their own `:root`. What was never finished is swapping the actual **consumption sites** — 56 declarations in `site-common.css` and 4 in `site.css` still reference `var(--accent-strong, ...)` directly instead of the semantic alias for their real role (link text, focus ring, CTA border, or — for the handful that are genuinely decorative — nothing at all). The specific bug in the roadmap description ("error text reads as a link") is **already fixed** at the one call site that used to cause it (`site-common.css:1445` `.feedback-error` already reads `color: var(--danger)`); Phase 84's real remaining work is (a) the bulk role-reclassification of the 60 leftover call sites, (b) closing one real per-theme gap (`site-commander-table.css` has no `@import` and is missing all four semantic tokens), and (c) proving the reclassification introduces **no** other color drift — which is the hard part, because the four alias tokens today resolve to `var(--accent)` while nearly every leftover call site currently renders in `var(--accent-strong)`, a **different** color in every theme. A naive "replace the variable name" pass will change colors everywhere it touches. The safe path (detailed under Architecture Patterns) is to re-point the alias *definitions* — not just the call sites — so `--link`/`--focus`/`--cta-border` resolve to `var(--accent-strong)` (matching the dominant existing behavior), leaving `--danger` as the one token that must NOT track `--accent-strong` (that divergence is the actual bug fix).

**Primary recommendation:** Re-point `--link` / `--focus` / `--cta-border` in `site.css` (and the 11 forks that duplicate the block) from `var(--accent)` to `var(--accent-strong)`; leave `--danger` as the fixed, non-accent-tracking red it already is; then reclassify the 60 leftover `var(--accent-strong, ...)` call sites onto the correct token by role (see the full map below); add the missing four-token block to `site-commander-table.css`; verify with a computed-style Playwright regression (danger ≠ link, per theme) plus a targeted before/after screenshot diff on the ~8 call sites that already used the old `--accent`-aliased tokens (the one place real, deliberate drift will occur).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Theme color-token resolution (CSS custom properties) | Browser / Client | CDN / Static | Pure CSS cascade resolved by the browser at render time; files are served as static assets (`wwwroot/css/`), no server-side templating touches token values. |
| Theme selection / cookie | Frontend Server (SSR) | — | `deckflow-theme` cookie read server-side to pick which `<link>` stylesheet Razor renders (`_Layout.cshtml`); out of scope for this phase (no selection-logic change). |
| Visual regression verification | Browser / Client (Playwright) | — | `DeckFlow.Web/e2e/theming.spec.ts` already drives computed-style assertions per theme via a real browser; the same mechanism is the natural fit for THEME-02/03 proof. |

No API/backend or database tier is implicated — this is a 100% static-CSS phase.

## Standard Stack

No new libraries, packages, or frameworks are needed for this phase. This is a pure CSS-file edit using the project's existing custom-property system.

### Package Legitimacy Audit

**Not applicable.** This phase installs no external packages (no npm/NuGet additions). Existing tooling (Playwright, already in `DeckFlow.Web/package.json`) is reused as-is.

## Current State (verified by direct grep/read of every file in scope)

### File inventory (27 files = the phase's full "theme forks" scope)

| File | `--accent-strong` refs | Has own `:root` | `@import site.css`? | Has `--link`/`--danger`/`--focus`/`--cta-border`? |
|------|------------------------|------------------|----------------------|----------------------------------------------------|
| `site.css` | 4 | yes (base) | — | yes (base definition; currently aliases to `--accent`, not `--accent-strong`) |
| `site-common.css` | 56 | no (pure layout, correctly per CLAUDE.md) | — | n/a (consumer only) |
| `site-mobile.css` | 5 | no | — | n/a (consumer only) |
| `site-commander-table.css` | 3 | yes (standalone) | **no** | **MISSING all four — real gap** |
| `site-theme-overrides.css` | 3 | no | — | n/a (consumer only) |
| `site-abzan.css` | 7 | yes | no | yes |
| `site-bant.css` | 6 | yes | no | yes |
| `site-esper.css` | 4 | yes | no | yes |
| `site-grixis.css` | 3 | yes | no | yes |
| `site-jeskai.css` | 5 | yes | no | yes |
| `site-jund.css` | 2 | yes | no | yes |
| `site-mardu.css` | 6 | yes | no | yes |
| `site-naya.css` | 6 | yes | no | yes |
| `site-nyx.css` | 4 | yes | no | yes |
| `site-planeswalker-dark.css` | 3 | yes | no | yes |
| `site-sultai.css` | 3 | yes | no | yes |
| `site-rakdos.css` | 1 | yes | **yes** | **partial** — only `--link` overridden (`#ff9ea4`, tagged `UI-VS-02`); `--danger`/`--focus`/`--cta-border` fall through the `@import` to `site.css`'s definitions, resolved against Rakdos's own `--accent`/`--accent-strong` |
| `site-azorius.css` | 6 | yes | yes | no (inherits via cascade — safe, functional) |
| `site-boros.css` | 3 | yes | yes | no (inherits via cascade) |
| `site-dimir.css` | 1 | yes | yes | no (inherits via cascade) |
| `site-golgari.css` | 1 | yes | yes | no (inherits via cascade) |
| `site-gruul.css` | 3 | yes | yes | no (inherits via cascade) |
| `site-izzet.css` | 1 | yes | yes | no (inherits via cascade) |
| `site-orzhov.css` | 2 | yes | yes | no (inherits via cascade) |
| `site-selesnya.css` | 3 | yes | yes | no (inherits via cascade) |
| `site-simic.css` | 2 | yes | yes | no (inherits via cascade) |
| `site-temur.css` | 2 | yes | yes | no (inherits via cascade) |

`[VERIFIED: repository grep]` — every count above is from `grep -c "accent-strong" <file>` and `grep -n "@import"` run directly against this repo's `DeckFlow.Web/wwwroot/css/` on 2026-07-04.

**Key structural fact (HIGH confidence, verified by CSS cascade semantics + direct inspection):** `--danger` is a single fixed hex value (`#c53030`) defined once in `site.css` and **not overridden by any of the 27 files** — it is intentionally guild-independent (every theme shows the same error red). This is already correct and must not change. By contrast, `--accent-strong` (and therefore, once re-pointed, `--link`/`--focus`/`--cta-border`) is guild-tinted — every theme defines its own value. The bug being fixed is that call sites conflated these two categories.

**Two files have no `@import`** and are fully standalone: `site-commander-table.css` and `site-planeswalker-dark.css`. Of these, `planeswalker-dark` already carries the full four-token block (safe). `site-commander-table.css` does not — if any of its 3 `var(--accent-strong)` call sites are swapped to `var(--link)`/`var(--danger)`/`var(--focus)`/`var(--cta-border)` **without** first adding those four custom properties to its own `:root`, the property becomes invalid at that element (unset custom property, no fallback) and the declaration is dropped, likely leaving `color`/`border-color` at its inherited/initial value — a real visual regression specific to this one theme. **This is the one mandatory per-theme token-addition required by THEME-01** (`--danger` can simply reuse the existing hardcoded value `#c53030` for full consistency with every other theme).

The other 10 `@import`-based forks lacking the block (azorius, boros, dimir, golgari, gruul, izzet, orzhov, selesnya, simic, temur) are **functionally safe without changes** — CSS custom properties resolve through the cascade, so their inherited `--link: var(--accent-strong)` (after the re-pointing recommended below) will correctly pick up each fork's own local `--accent-strong` override at `var()`-resolution time, even though the alias's *declaration* physically lives in `site.css`. Adding the block explicitly to these 10 anyway is optional but recommended for consistency with the 11 forks that already do it, and matches the codebase's established pattern of each theme's `:root` being "self-contained and readable without tracing to another file."

### Role classification of the 60 leftover `--accent-strong` call sites

This is the core migration map for THEME-01. Classified by actual visual role, not by file:

**LINK role (anchor-like navigational text) → `--link`:**
| File:Line | Selector |
|---|---|
| site-common.css:1266 | `.about-page a` |
| site-common.css:1300 | `.help-index__link` |
| site-common.css:1318 | `.help-breadcrumb a` |
| site-common.css:1330 | `.kb-back-link` |
| site-common.css:1356 | `.help-prose a` |
| site-common.css:576 | `.kb-chip--followed` (text color on a chip that links to a followed creator) |
| site-common.css:620 | `.kb-clip-origin--followed` (same pattern) |

**FOCUS role (focus-visible rings / outlines / selected-state ring) → `--focus`:**
| File:Line | Selector |
|---|---|
| site-common.css:262 | `.back-to-top-button.is-theme-locked:focus-visible` |
| site-common.css:375 | `.hub-card:focus-visible` |
| site-common.css:694 | `.hub-hero:focus-visible` |
| site-common.css:730 | `.hub-card--primary:focus-visible` |
| site-common.css:940 | `.maintenance-page__action:focus-visible` |
| site-common.css:2035 | `details.info-tooltip > summary:focus-visible` |
| site-common.css:2079 | `details.chatgpt-helper-panel > summary:focus-visible` |
| site-common.css:2481 | `.manabase-pill > input:focus-visible + span` |
| site-mobile.css:228 | `.hub-hero--primary:hover, .hub-hero--primary:focus-visible` |
| site-theme-overrides.css:12-13 | `.hub-card--primary:hover, .hub-card--primary:focus-visible` |

**CTA-BORDER role (button/CTA framing, checked/selected-state border) → `--cta-border`:**
| File:Line | Selector |
|---|---|
| site-common.css:1946 | `.ai-selector__option:checked + .ai-selector__option-label` (border-color) |
| site-common.css:1222/1223 | `table[data-chatgpt-cedh-reference-table] tr:has(...:checked)` (border + box-shadow) |
| site-common.css:1013/1019 | `.page-footer__link--cta` (color + focus background — this is styled as a CTA pill, not a plain link, despite the class also containing "link") |
| site-common.css:1414 | `.feedback-submit` (background — primary submit button) |
| site-theme-overrides.css:27 | `.maintenance-page__action` (border-color — already partially on `--cta-border` with an accent-strong secondary use at line 28, see below) |

**AMBIGUOUS / genuinely decorative — no clean fit in the 4 named tokens (recommend: leave on `--accent-strong`, do not force onto link/danger/focus/cta-border):**
| File:Line | Selector | Why it doesn't fit |
|---|---|---|
| site.css:322 | `.chatgpt-step-tab.is-active` | Active-tab text color — brand emphasis, not a link/button/focus/error |
| site.css:687 | `.back-to-top-button` (base, non-focus) | Floating action button *fill* color — closest to CTA but the 4 tokens don't include a "cta-fill"; only `--cta-border` exists |
| site.css:1083, site-commander-table.css:1034 | `.run-button:hover, .copy-button:hover` | Hover-state background darken — no "cta-hover-bg" token exists |
| site-common.css:78 | `textarea::-webkit-scrollbar-thumb:hover` | Scrollbar chrome, not a semantic UI role at all |
| site-common.css:166 | `.page-brand` | Site wordmark/logo color — pure brand identity |
| site-common.css:797 | `.chatgpt-layout-picker .chatgpt-layout-segment.is-active` | Selected-tab text — same as the site.css step-tab case |
| site-common.css:896/940(bg) | `.maintenance-page__action` (non-focus text/bg) | Decorative CTA framing, background variant not border |
| site-common.css:1236/1283/1348 | `.about-page h1`, `.help-index h1`, `.help-prose h1` | Decorative heading accent color, not a link |
| site-common.css:1514 | `.mechanic-row:hover` | Row hover highlight |
| site-common.css:1579 | `.copy-button.copy-button--icon:hover` | Icon-button hover tint |
| site-common.css:1886/1896 | `.bracket-callout`, `.bracket-callout__label` | Rating/severity callout accent |
| site-common.css:2155 | `.primer-section__help summary` | Decorative summary-toggle color |
| site-common.css:2684/2716/2777/2788/2800 | `.manabase-lens-big`, `.manabase-lens-pill`, `.manabase-rampdraw-line strong`, `.manabase-cmd-castability`, `.manabase-cmd-companion-glyph` | Manabase-analysis emphasis coloring, unrelated to link/danger/focus/CTA |
| site-common.css:2918/2919/2966/2967 | `.bracket-badge`, `.bracket-badge--b3` | Explicitly documented (`site-common.css:2953-54`) as "accent colors mirror the four `.manabase-health--*` baked hex values plus `--accent-strong` for B3 so no new raw colors are introduced" — a deliberate rating-tier color choice, not an error/link/focus/cta role |
| site-common.css:3114/3131/3132/3159/3181 | `.chatgpt-score-value`, `.chatgpt-score-pip--filled`, `.chatgpt-score-crosscheck(__label)` | Score-visualization emphasis coloring |
| site-common.css:3217/3262 | `.interaction-audit-bucket h4`, `.interaction-audit-gaps__label` | Cycle-14 feature decorative headings |
| site-common.css:3376/3402 | `.wincon-map-combo__cards`, `.wincon-map-closers h4` | Cycle-14 feature decorative headings |
| site-mobile.css:88/220 | (mobile responsive overrides mirroring desktop decorative accents) | Same pattern as their desktop counterparts |
| site-commander-table.css:301 | (info-panel decorative accent) | Same pattern |

**Why this ambiguous bucket matters for THEME-01/THEME-03:** THEME-01's literal text says "every ... usage is reclassified onto the correct semantic token ... by role." Read strictly, that would force even the ~30 decorative usages above onto one of the four named tokens. But none of them are actually a link, an error, a focus ring, or a CTA border — they are brand/rating/emphasis coloring that happens to reuse `--accent-strong` because it is the theme's "vivid" color, which is a legitimate, pre-existing pattern (see the `.bracket-badge--b3` code comment explicitly justifying this reuse). Forcing these onto `--link`/`--focus`/`--cta-border` would satisfy THEME-01's literal wording only at the cost of muddying what those tokens mean (a link-colored badge, a CTA-bordered score pip) and does not serve THEME-02's actual goal (fixing the error/link collision). **Recommend treating "correct semantic token" as including "the token stays `--accent-strong` because that IS the correct classification — this usage is decorative brand emphasis, not one of the four named roles."** Surface this interpretation explicitly to the user/planner as a scope decision before execution — it materially shrinks the diff (roughly 20-25 real reclassifications vs. 60 mechanical replacements) and is the only reading consistent with THEME-03's "no unintended visual regression on non-error surfaces."

## Architecture Patterns

### The core problem: the alias tokens don't currently equal what they need to replace

```css
/* site.css :root today */
--accent:        #2b6cb0;   /* example: Classic/Jeskai */
--accent-strong:  #1e4f82;  /* darker/richer variant — DIFFERENT color */
--link:           var(--accent);        /* NOT var(--accent-strong) */
--cta-border:     var(--accent);        /* NOT var(--accent-strong) */
--focus:          var(--accent);        /* NOT var(--accent-strong) */
--danger:         #c53030;              /* fixed, correct, do not touch */
```

Every one of the ~30 real link/focus/cta-border call sites in the classification tables above currently renders in `--accent-strong`'s color. If they are swapped to `var(--link)`/`var(--focus)`/`var(--cta-border)` **without also changing what those tokens resolve to**, every one of those ~30 elements changes color in every theme — a textbook THEME-03 violation.

Conversely, a **small** number of call sites already correctly use the alias tokens today and render in `--accent`'s color:
- `site.css:104,116` — the global `:focus-visible` default outline (`outline: 2px solid var(--focus);`)
- `site.css:1031` — `.cache-pill__reset` link color
- `site.css:1255,1300,1348` — `.judge-howto/.deckflow-bridge-hint/.moxfield-bulkedit-hint > summary` (`color: var(--link);`)
- `site.css:1065` — `.run-button, .copy-button { border: 1px solid var(--cta-border); }`
- `site-common.css:116,1957` — a couple of already-migrated generic focus outlines (`outline: 2px solid var(--focus, var(--accent));`)

### Recommended fix: re-point the alias definitions, not just the call sites

```css
/* site.css :root — RECOMMENDED CHANGE */
--link:        var(--accent-strong);   /* was var(--accent) */
--cta-border:  var(--accent-strong);   /* was var(--accent) */
--focus:       var(--accent-strong);   /* was var(--accent) */
--danger:      #c53030;                /* UNCHANGED — this is the actual fix */
```

Apply the identical 3-line change to the 11 forks that duplicate this block (`site-abzan.css`, `site-bant.css`, `site-esper.css`, `site-grixis.css`, `site-jeskai.css`, `site-jund.css`, `site-mardu.css`, `site-naya.css`, `site-nyx.css`, `site-planeswalker-dark.css`, `site-sultai.css`) plus add the corrected 4-line block (net-new) to `site-commander-table.css`. Do NOT change `site-rakdos.css`'s existing `--link: #ff9ea4;` override — it is already a deliberate, shipped fix (making link visually distinct from both the theme's own reddish accent-strong AND from the fixed danger red) and reverting it to track `--accent-strong` again would be a real regression.

Then swap the ~30 real-role call sites (link/focus/cta-border tables above) from `var(--accent-strong, <fallback>)` to `var(--link, <fallback>)` / `var(--focus, <fallback>)` / `var(--cta-border, <fallback>)`, preserving each existing fallback chain's tail (e.g. `var(--accent-strong, var(--accent))` becomes `var(--focus, var(--accent))` — the `--accent-strong` link in the fallback chain is now redundant since `--focus` itself resolves through `--accent-strong`, so it collapses out naturally). Example:

```css
/* BEFORE (site-common.css:375) */
.hub-card:focus-visible {
  border-color: var(--accent-strong, var(--line));
}

/* AFTER */
.hub-card:focus-visible {
  border-color: var(--focus, var(--line));
}
```

```css
/* BEFORE (site-common.css:1330) */
.kb-back-link {
  color: var(--accent-strong, var(--accent));
}

/* AFTER */
.kb-back-link {
  color: var(--link, var(--accent));
}
```

**Net visual result with this approach:** the ~30 real-role call sites render byte-identically (same resolved color, just via the correctly-named token) in every theme except the ~8 already-migrated call sites, which will now shift from `--accent`'s shade to `--accent-strong`'s shade — a small, deliberate, and far smaller set of changes than the alternative (leaving the alias definitions alone and accepting ~30 changed call sites). **This is a real, if narrow, tradeoff that must be called out to the user/planner rather than silently decided**, since it does technically change ~8 elements' colors as a side effect of harmonizing the tokens. Recommend listing those 8 exact selectors in the plan and screenshotting them before/after (desktop + mobile) as the explicit, accepted, documented delta.

### Recommended project structure for the change

No new files are needed. All edits are in-place to the existing 27 CSS files plus (optionally) a new/extended Playwright spec:

```
DeckFlow.Web/wwwroot/css/
├── site.css                        # re-point 3 alias defs; migrate 4 call sites
├── site-common.css                 # migrate ~52 of the 56 call sites (role-classified above)
├── site-mobile.css                 # migrate the focus-role call sites (2)
├── site-theme-overrides.css        # migrate the focus/cta-border call sites (2-3)
├── site-commander-table.css        # ADD the missing 4-token block; migrate its 2-3 call sites
├── site-{abzan,bant,esper,grixis,jeskai,jund,mardu,naya,nyx,planeswalker-dark,sultai}.css
│                                    # re-point the duplicated 3-line alias block
├── site-rakdos.css                 # NO CHANGE to its --link override; verify --danger still resolves correctly
└── site-{azorius,boros,dimir,golgari,gruul,izzet,orzhov,selesnya,simic,temur}.css
                                     # no required change (cascade-safe); optional consistency add
DeckFlow.Web/e2e/
└── theming.spec.ts                 # extend with a Tier-1/Tier-2 semantic-token regression (see Validation Architecture)
```

### Anti-Patterns to Avoid

- **Mechanically `sed`-replacing every `accent-strong` with one of the four token names.** This is the single biggest risk in this phase — roughly half the call sites are decorative and don't belong on any of the four tokens; forcing them there both muddies the tokens' meaning and, per the analysis above, does not by itself avoid visual drift unless the token *definitions* are also corrected first.
- **Changing `--danger`'s value or making it theme-tinted.** It is intentionally the one token that must NOT vary by guild — that is the entire point of the fix (error color must never coincide with a red guild's brand color).
- **Reverting `site-rakdos.css`'s existing `--link: #ff9ea4` override** to "clean up" the special case — it is a correct, already-shipped, deliberate divergence, not leftover debt.
- **Touching `admin.css`/`admin-common.css`/`admin-mobile.css`.** These have zero `--accent-strong` references and their own independent `--danger: #dc2626` token (guarded by `DeckFlow.Web.Tests/AdminCssPhase1Tests.cs`) — out of scope, do not conflate with the main-site token system.
- **Renaming any `chatgpt-*` class touched in this phase.** Several accent-strong call sites live on `chatgpt-*`-prefixed selectors (`.chatgpt-layout-picker`, `.chatgpt-score-value`, `table[data-chatgpt-cedh-reference-table]`, etc.) — Phase 85 owns renaming those identifiers; Phase 84 must change only the `var(...)` value, never the selector name, to avoid cross-phase file churn (per ROADMAP's own stated sequencing rationale).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Verifying two colors are "different enough" per theme | A custom perceptual-distance/Delta-E color library | Direct string/RGB-tuple inequality check via `getComputedStyle(...).color` in the existing Playwright harness | The requirement (THEME-02) is that danger and link are structurally decoupled (different CSS variables), not that they clear a formal contrast ratio; a simple computed-value inequality check per theme is sufficient and matches the existing `theming.spec.ts` Tier-2 pattern (`expect(new Set(...).size).toBeGreaterThanOrEqual(2)`). |
| Screenshot-diffing 27 themes × N selectors | A new visual-regression tool/service (e.g., Percy, Chromatic) | The project's existing scratch-script + `playwright-core` screenshot method used for the Phase 82 UI baseline audit (`scripts/run-web-test.sh` + a one-off Node script, not a new tracked dependency) | CLAUDE.md forbids adding new packages without asking; the codebase already has a working, non-dependency screenshot method that was used for exactly this kind of before/after visual proof one phase ago. |

**Key insight:** every verification need in this phase already has a working, in-repo precedent (`theming.spec.ts`'s tier pattern; the Phase 82 scratch-script screenshot method) — no new tooling should be introduced.

## Runtime State Inventory

Not applicable — this phase touches only static CSS files, no databases, external services, OS-level registrations, secrets, or build artifacts carry theme-token state. Nothing to migrate.

## Common Pitfalls

### Pitfall 1: Swapping a call site onto a token that isn't defined for that fork
**What goes wrong:** `site-commander-table.css` has no `@import` and doesn't define `--link`/`--danger`/`--focus`/`--cta-border`. Swapping its 3 call sites to reference those tokens without first adding them to its own `:root` makes the custom property unset — the declaration becomes invalid and is dropped, silently falling back to an inherited/initial color.
**Why it happens:** the other 21 guild forks either already have the block or safely inherit it via `@import`; commander-table is the sole exception, easy to miss in a repetitive 27-file sweep.
**How to avoid:** add the corrected 4-line block (`--link/--cta-border/--focus: var(--accent-strong); --danger: #c53030;`) to `site-commander-table.css`'s `:root` as the very first edit in that file, before touching any of its call sites.
**Warning signs:** a themed element on the Commander Table page renders in black/default ink instead of its usual accent color after the migration.

### Pitfall 2: Realiasing the tokens without checking the ~8 already-migrated call sites
**What goes wrong:** re-pointing `--link`/`--focus`/`--cta-border` to `var(--accent-strong)` (recommended above) is necessary to avoid drift on the ~30 unmigrated call sites, but it *will* change the rendered color of the handful of spots that already correctly used the tokens (global focus outline, `.cache-pill__reset`, the three `>summary` help-hint links, `.run-button`/`.copy-button` border).
**Why it happens:** these are the only call sites where the "old" (accent) and "new" (accent-strong) values genuinely diverge, and they're easy to overlook because they look "already done, no action needed."
**How to avoid:** explicitly enumerate and screenshot these ~8 selectors before and after, across at least one light and one dark theme, and call the delta out in the plan/summary as an intended, accepted, minimal side effect — not an accident discovered by the verifier.
**Warning signs:** a plan-checker or code-reviewer flags "why did the global focus ring/border color change?" — have the documented before/after ready.

### Pitfall 3: Treating decorative/brand `--accent-strong` usage as a defect
**What goes wrong:** forcing genuinely decorative usages (badges, score pips, active-tab indicators, hover-state backgrounds) onto one of the four named tokens either breaks their meaning (a "danger-bordered" score pip makes no sense) or, if forced onto `--link`/`--focus`/`--cta-border`, is harmless post-realiasing but adds churn with zero semantic benefit and increases the diff/review surface for no THEME-02 payoff.
**Why it happens:** THEME-01's literal wording ("every ... usage is reclassified") reads as if 100% must move.
**How to avoid:** classify the ~30 decorative usages explicitly as "role: decorative brand emphasis — stays on `--accent-strong`, no change" in the plan, with the `.bracket-badge--b3` code comment (`site-common.css:2953-54`) cited as in-repo precedent that this reuse is intentional, not accidental.
**Warning signs:** a plan that touches all 60 call sites uniformly without any left classified as "no change" is over-scoped relative to the actual bug.

### Pitfall 4: Conflating `--error` with `--danger`/`--accent-strong`
**What goes wrong:** the codebase has a THIRD, separate, per-theme-tinted token, `--error` (used by `.error-banner`, `.error-page__*`), that is unrelated to this migration. Some engineer discovering the accent-strong sweep might "helpfully" also touch `--error` call sites, expanding scope.
**Why it happens:** the names are easy to confuse (`--error`, `--error-strong`, `--danger` all exist; `--error-strong` is even hardcoded to the same `#c53030` as `--danger`, in `site.css:53`).
**How to avoid:** confirm scope is strictly `--accent-strong` call sites (THEME-01's literal subject); leave `--error`/`--error-strong`/`--warning`/`--success` untouched — they are not part of this bug or this requirement.
**Warning signs:** a diff that touches `.error-banner` or `.warning-banner` styling.

### Pitfall 5: Same-file churn colliding with Phase 85 (`chatgpt-*` rename)
**What goes wrong:** several accent-strong call sites in scope live on `chatgpt-*`-named selectors. If Phase 84's diff also renames those selectors "while we're in there," it creates merge/rebase friction with Phase 85, which is explicitly sequenced right after Phase 84 to touch the same files for identifier renaming.
**Why it happens:** natural temptation when a selector like `.chatgpt-score-value` is already open in the diff.
**How to avoid:** change only the `var(...)` value inside `chatgpt-*` selectors in this phase; leave every selector name untouched for Phase 85.
**Warning signs:** a diff hunk where a `chatgpt-*` class name itself changes, not just its declaration body.

## Code Examples

### Token re-pointing (site.css and the 11 duplicating forks)
```css
/* site.css :root — before */
--link:        var(--accent);
--danger:      #c53030;
--cta-border:  var(--accent);
--focus:       var(--accent);

/* site.css :root — after */
--link:        var(--accent-strong);
--danger:      #c53030;   /* unchanged — this is the fix */
--cta-border:  var(--accent-strong);
--focus:       var(--accent-strong);
```

### Adding the missing block to site-commander-table.css
```css
:root {
  /* ...existing tokens... */
  --accent-strong: #1f5c39;

  /* semantic color tokens (UI-VS-02) — added Phase 84, matches site.css pattern */
  --link:        var(--accent-strong);
  --danger:      #c53030;
  --cta-border:  var(--accent-strong);
  --focus:       var(--accent-strong);
}
```

### Call-site migration (role: focus)
```css
/* site-common.css:730 — before */
.hub-card--primary:focus-visible {
  border-color: var(--accent-strong, var(--cta-border, var(--accent, var(--line))));
}

/* after — the token now IS what accent-strong used to provide; drop the now-redundant leg */
.hub-card--primary:focus-visible {
  border-color: var(--focus, var(--accent, var(--line)));
}
```

### Call-site migration (role: link)
```css
/* site-common.css:1356 — before */
.help-prose a {
  color: var(--accent-strong, var(--accent, inherit));
}

/* after */
.help-prose a {
  color: var(--link, var(--accent, inherit));
}
```

### Leaving a decorative usage unchanged (documented, not silently skipped)
```css
/* site-common.css:2966-2967 — NO CHANGE (Phase 84 scope note: decorative rating-tier
   color, not a link/danger/focus/cta role — see 84-RESEARCH.md ambiguous-bucket table) */
.bracket-badge--b3 {
  border-left-color: var(--accent-strong, #7c3aed);
  background: color-mix(in srgb, var(--accent-strong, #7c3aed) 14%, var(--panel-soft-bg, transparent));
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Direct `var(--accent-strong, ...)` at every link/focus/cta call site | Semantic alias tokens (`--link`/`--danger`/`--focus`/`--cta-border`) defined once and consumed by role | Alias tokens added in a prior pass tagged `UI-VS-02` (already in `main` before this cycle); call-site migration is Phase 84's job | Decouples "what color is this guild's vivid accent" from "what role does this specific UI element play," which is what makes it possible to fix the danger/link collision without a per-theme special case for every guild |

**Deprecated/outdated:** none — this is additive token-consumption cleanup, not a framework or library version change.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The "correct" resolution to the alias-realiasing tradeoff is to point `--link`/`--focus`/`--cta-border` at `--accent-strong` (favoring the ~30-call-site majority) rather than leaving them at `--accent` (favoring the ~8-call-site minority) | Architecture Patterns — "Recommended fix" | If the user/planner instead wants the opposite (favor the already-migrated 8 call sites, accept drift on the 30), the whole call-site migration table's "byte-identical" framing inverts — this should be confirmed with the user/discuss-phase before locking the plan, not assumed silently. |
| A2 | Roughly 30 of the 60 leftover call sites are genuinely decorative/brand and should NOT be forced onto one of the four named tokens (the "ambiguous bucket") | Current State — role classification | If the user intends THEME-01 literally (100% of call sites reassigned), the plan under-delivers against a strict reading of the requirement; this is a scope interpretation, not a technical fact, and should be confirmed. |
| A3 | `--error`/`--error-strong`/`--warning`/`--success` are out of scope (separate token family, not part of this bug) | Common Pitfalls — Pitfall 4 | Low risk — these are clearly separate, differently-named tokens with no shared usage sites; unlikely to be contested, but flagged for completeness. |

## Open Questions (RESOLVED — see 84-CONTEXT.md D1–D4)

> All four resolved via inline decisions after research: Q1 → **D1** (re-alias the 3 tokens,
> accept the ~8-site documented shift). Q2 → **D2** (decorative ~30 stay on `--accent-strong`,
> classification satisfies THEME-01). Q3 → **D4** (add token block only where a swapped site
> needs it — mandatory for `site-commander-table.css`; no mass-add for uniformity). Q4
> (typography) → **D3** (deferred to Phase 86). Retained below for audit trail.

1. **Should the ~8 already-`--accent`-aliased call sites be allowed to visually shift to `--accent-strong`'s shade, or should the plan instead special-case them to stay `--accent`-colored (e.g., by NOT re-pointing the base alias and instead only swapping the ~30 real-role sites while accepting THEIR drift)?**
   - What we know: whichever direction is chosen, some real, non-zero set of elements changes color; the two options differ only in which set (8 vs ~30) absorbs the change.
   - What's unclear: whether the phase's "no unintended visual regression" bar treats "smaller set of changes, but a real 8-element diff, clearly documented" as acceptable, or expects literally zero call sites to change color anywhere.
   - Recommendation: default to re-pointing the alias (fewer total pixels change); get explicit user sign-off on the 8-selector list before merging, ideally during `/gsd:discuss-phase` rather than discovered at verify-work time.

2. **Does THEME-01's "every accent-strong usage" require action on the ~30 decorative/brand call sites, or does classifying them as "correctly stays on `--accent-strong`" satisfy the requirement's intent?**
   - What we know: the roadmap's own phrasing ties Phase 84 to fixing "the error-reads-as-link bug ... without any other visual drift" — which argues for the narrower interpretation.
   - What's unclear: whether a stricter reading (every single call site physically reassigned to one of the four token names, even where semantically ill-fitting) is what a plan-checker or the user actually wants scored against.
   - Recommendation: state the ~30/~30 split explicitly in the plan's success criteria and get it confirmed rather than silently picking one reading.

3. **Should the 10 `@import`-based forks lacking the four-token block (azorius, boros, dimir, golgari, gruul, izzet, orzhov, selesnya, simic, temur) get the block added for consistency, even though functionally unnecessary?**
   - What we know: they will render correctly either way (cascade inheritance works); the 11 sibling forks with an explicit block establish a "every theme's `:root` is self-contained" convention.
   - What's unclear: whether closing this consistency gap is in-scope for Phase 84 or an unnecessary expansion of the diff.
   - Recommendation: low-risk, low-cost to include (4 lines × 10 files); recommend doing it in the same pass since it's mechanically identical to what's already being done to the other 12 forks, but flag it as "nice-to-have consistency," not a THEME-01 blocker.

4. **Is the Typography font-size migration (bundled into Phase 84 by the Phase 82 UI-audit handoff in `tasks/UI-REVIEW.md:182-198`, but NOT listed in REQUIREMENTS.md's THEME-01/02/03 text or ROADMAP.md's Phase 84 success criteria) actually in scope for this phase?**
   - What we know: `tasks/UI-REVIEW.md` explicitly assigns "migrate ~24 remaining literal `font-size:` values onto `var(--fs-*)`" and "add `--lh-*` line-height tokens" to "Owner: Phase 84 (THEME)" to close the Typography pillar gap (3/4 → 4/4) — but this work item does not appear anywhere in `REQUIREMENTS.md`'s THEME-01/02/03 or `ROADMAP.md`'s Phase 84 success-criteria/traceability table.
   - What's unclear: whether this is a genuine scope addition the user intends to fold in (since it touches the exact same files) or a UI-audit suggestion that was never formally added to the requirements and should be left to Phase 86 (UIAUDIT-02, which re-scores against "the gap enumerated by the Phase 82 baseline audit") or a future phase.
   - Recommendation: **raise this explicitly in `/gsd:discuss-phase` before planning** — it's a real, low-effort, same-file addition (font-size literal → `var(--fs-*)` swap, same mechanical pattern as the color-token work) but it is not currently a locked requirement for Phase 84, and Phase 86's own success criteria assume the "Typography (3/4→4/4)" gap will already be closed by the time it re-scores. If left out of Phase 84, Phase 86 will find Typography still at 3/4 and have no owning mechanism to fix it (UIAUDIT-02 is a re-score, not a new fix-authoring phase per its own text). This is a real sequencing gap regardless of which way it's resolved.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Playwright (`@playwright/test` ^1.60.0) for browser-rendered CSS verification; xUnit exists for backend but has no relevant coverage here (no C# touched) |
| Config file | `DeckFlow.Web/playwright.config.ts` |
| Quick run command | `cd DeckFlow.Web && npx --no-install playwright test e2e/theming.spec.ts` (after starting the server via `scripts/run-web-test.sh`) |
| Full suite command | `cd DeckFlow.Web && npx --no-install playwright test` (full e2e suite, all specs) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| THEME-01 | Every real link/focus/cta-border call site resolves through the correct alias token (not raw `--accent-strong`) in every theme | computed-style e2e (extend Tier 2 pattern in `theming.spec.ts`) | `npx playwright test e2e/theming.spec.ts` | ❌ Wave 0 — extend existing spec |
| THEME-02 | `--danger`'s computed color is never equal to `--link`'s computed color, in any of the 27 themes (the core structural regression guard) | computed-style e2e (new Tier-1-style assertion) | same spec | ❌ Wave 0 — new test |
| THEME-02 | Live desktop + mobile visual check on the affected red-guild themes (Rakdos/Boros/Jund/Gruul/Izzet/Jeskai/Mardu/Naya/Grixis — every theme whose `--accent-strong` sits in the red family) | manual/screenshot (scratch script, not a tracked test, per Phase-82 precedent) | one-off Node+`playwright-core` script against `scripts/run-web-test.sh` server, screenshots at 1280×900 and 390×844 | n/a — methodology only, matches `tasks/UI-REVIEW.md`'s own evidence basis |
| THEME-03 | The ~8 already-migrated call sites' color change is the ONLY delta versus pre-migration baseline; the ~30 decorative call sites are provably unchanged | before/after computed-style diff (scratch script) OR a small dedicated Playwright assertion pinning the ~8 known-changed selectors' expected new color per representative theme | same scratch/script approach as above, plus optionally a permanent regression test for the 8 known-intentional deltas | ❌ Wave 0 — new script + optional test |

### Sampling Rate
- **Per task commit:** run the extended `theming.spec.ts` (fast — headless, no screenshots) after each file's edit.
- **Per wave merge:** full Playwright e2e suite (`npx playwright test`) plus the manual/scratch screenshot pass across all 27 themes at both viewports.
- **Phase gate:** full suite green + screenshot evidence attached to the phase summary before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] Extend `DeckFlow.Web/e2e/theming.spec.ts` with a new assertion: for every theme in `themeFiles`, `getComputedStyle(document.documentElement).getPropertyValue('--danger')` !== `.getPropertyValue('--link')` (string inequality after trim/lowercase) — this is the direct, permanent regression guard for THEME-02's structural fix.
- [ ] Extend the same spec (or add a new Tier-2-style test) asserting every theme resolves `--link`/`--focus`/`--cta-border` to a non-empty, non-`--accent`-literal value once the migration lands, catching a future regression that re-introduces a raw `--accent-strong` call site.
- [ ] Add a one-off (non-tracked) Node + `playwright-core` screenshot script, following the exact methodology already used for the Phase 82 UI baseline audit (`scripts/run-web-test.sh` server + headless Chromium), to capture desktop+mobile screenshots of: (a) all red-guild themes' error/feedback states, (b) the 8 known-intentional-delta selectors before/after, across at least Classic (light) and one dark fork (e.g. Nyx).
- [ ] No new framework/tooling install needed — Playwright is already present and configured.

## Security Domain

Not applicable — this phase is a pure visual/CSS-token change with no authentication, session, input-validation, or cryptography surface. No ASVS categories apply.

## Sources

### Primary (HIGH confidence — direct repository inspection, 2026-07-04)
- `DeckFlow.Web/wwwroot/css/site.css` — full `:root` block (lines 1-58), all 4 `--accent-strong` call sites, existing `--link`/`--focus` consumers
- `DeckFlow.Web/wwwroot/css/site-common.css` — full grep of all 56 `--accent-strong` occurrences with enclosing selector, `.feedback-error`/`.error-banner`-adjacent selectors, the `.bracket-badge--b3` design-intent comment (lines 2953-54)
- `DeckFlow.Web/wwwroot/css/site-rakdos.css` — the existing `UI-VS-02` `--link` override and its comment
- `DeckFlow.Web/wwwroot/css/site-commander-table.css`, `site-planeswalker-dark.css` — confirmed no `@import`, confirmed one is missing the token block
- All 22 guild-fork CSS files — `--accent-strong` hex values, presence/absence of the 4-token block, `@import` presence
- `DeckFlow.Web/e2e/theming.spec.ts` — existing Tier-1/Tier-2 test pattern used as the template for new THEME-02/03 verification
- `tasks/UI-REVIEW.md` — Phase 82's baseline audit findings and the explicit Phase-84 fix handoff (lines 51-241)
- `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`, `.planning/STATE.md` — requirement text, phase success criteria, prior decisions
- `git log`/`git show` on `b57083ae` (font-size tokenization precedent), `5cfb198e` (theming test tiers), `8d5d6621` (recent print-feature CSS addition to avoid disturbing)
- `scripts/format-check-changed.sh`, `.github/workflows/*.yml` — confirmed the format-gate only covers `*.cs`, not CSS
- `DeckFlow.Web.Tests/AdminCssPhase1Tests.cs` — confirmed admin CSS's separate `--danger: #dc2626` token family is out of scope

### Secondary (MEDIUM confidence)
- None — no external library/framework research was required for this phase.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: N/A — no new stack, HIGH confidence nothing new is needed
- Architecture (role classification + alias-realiasing recommendation): HIGH — every claim is directly verified against the actual CSS files' current contents; the "which option causes less drift" recommendation is a reasoned tradeoff (flagged as Assumption A1), not an external fact
- Pitfalls: HIGH — all five are grounded in direct file inspection (the commander-table gap, the rakdos override, the admin.css separation, the chatgpt-* overlap, the --error confusion risk are all confirmed, not speculative)

**Research date:** 2026-07-04
**Valid until:** 30 days (pure internal CSS state; no external dependency to go stale, but re-verify token counts if any other phase touches these files first)
