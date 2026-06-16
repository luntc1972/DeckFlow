# Phase 48 — 6-Pillar Visual Audit (UIR-01)

**Date:** 2026-06-16
**Target audited:** Local Debug build, `http://localhost:5173`, **Classic (Jeskai) default theme**
**Method:** gstack headless Chromium, 8 pages × 2 viewports (desktop 1280×720, mobile 375×812). Screenshots in `logs/audit-shots/`.
**Baseline:** v1.0 audit = **16/24** (Color 2, Typography 2, Visuals 3, Copywriting 3, Spacing 3, Experience Design 3).

> **Scope caveats (must be honored at close / UIR-03):**
> 1. Audited the **local build**, per operator choice. CSS assets are static and the Docker publish rebuilds identical files, so local Classic ≈ deployed default. Final remediation verification (UIR-03 / SC3) must still screenshot the **deployed deckflow.gg** site at both viewports before close.
> 2. Audited the **Classic** theme only. The 24 guild themes are full CSS forks sharing layout but with different `:root` token values. Token-level fixes propagate per-theme but each remediated theme needs a spot-check screenshot.

---

## Re-Scored Pillars (current state)

| Pillar | v1.0 | Now | Δ | Note |
|--------|------|-----|---|------|
| Copywriting | 3 | 3 | – | Strong microcopy; minor unexplained jargon |
| Visuals | 3 | **2** | ▼ | Flat, zero iconography, dead space, surfaces barely differ from bg |
| Color | 2 | **3** | ▲ | v1.0 semantic tokens landed; palette still limited, surfaces flat |
| Typography | 2 | **3** | ▲ | v1.0 fixed 18-value sprawl → clean 6-step scale; system font + weight monotony remain |
| Spacing | 3 | 3 | – | Good; Primer dense, short pages over-empty |
| Experience Design | 3 | 3 | – | Good patterns; short pages lack empty-state guidance |
| **TOTAL** | **16** | **17** | | **Target ≥ 20 → need +3** |

### Grounded token evidence (`site.css` :root, Classic)
- `--bg: #eceef3` vs `--panel: #fafafa` — surface/background delta near-invisible → cards read flat (Visuals + Color).
- `--ink: #1a1f2e` on `--bg` ≈ 14:1 (excellent). `--muted: #5c6478` on `--bg` ≈ **5.1:1** (AA-pass, tight on small text).
- Type scale `--fs-xs 0.75rem (~11.25px) … --fs-2xl 1.9rem`, `html{font-size:15px}`. 6 sizes in live DOM: 11.25 / 12.75 / 14.25 / 15 / 22.5 / 28.5px.
- Font: `"Segoe UI", Tahoma, Geneva, Verdana, sans-serif` — system stack, no brand/display face. h1 = 28.5px / 700.

---

## Findings (prioritized)

### HIGH
- **F1 [Visuals]** Flat, plain bordered cards with **zero iconography** on every tool page and the home hub. No tool/section icons, no card elevation or hover affordance. → Add lightweight inline-SVG icons to home hub cards + section headers; add subtle elevation/hover to `.hub-card`/panel containers. *Layout → `site-common.css`; markup → Home + shared partials.* Lifts **Visuals 2→3**.
- **F2 [Color]** Card surfaces (`--panel #fafafa`) barely separate from page bg (`--bg #eceef3`); whole UI reads as one flat gray plane. → Widen surface/bg delta and/or add a subtle shadow + stronger `--line` border so panels lift off the page. *Tokens → each theme `:root`; shadow → `site-common.css`.* Contributes **Color 3→4** + Visuals.

### MEDIUM
- **F3 [Typography]** `--fs-xs 0.75rem (~11.25px)` drives helper/footer text below a comfortable floor. → Raise `--fs-xs` to ~`0.82rem` (≈12.3px) so the smallest text is ≥12.75px. *Token → each theme `:root`.*
- **F4 [Typography]** Weight + face monotony — single system font, near-single weight. → Introduce a heading/label treatment: 700 + slight letter-spacing on section labels, 600 medium for field labels/badges (no new web-font dependency required). *Layout → `site-common.css`.* F3+F4 lift **Typography 3→4**.
- **F5 [Color]** `--muted #5c6478` at 5.1:1 is tight on small text. → Darken to ≥ `#4b5563` (or pair with F3 size bump) for comfortable AA on small helper text. *Token → each theme `:root`.*
- **F6 [Visuals]** Large dead vertical space below short forms (Card Lookup, Ask a Judge). → Cap/center content column or add a supporting example/help panel. *Layout → `site-common.css`.*
- **F7 [Experience Design]** Short pages end abruptly with no empty-state guidance after the form. → Add a brief example/next-step panel. *Markup.*

### LOW
- **F8 [Spacing]** Deck Primer category list is very dense — increase row spacing / clearer group headers.
- **F9 [Copywriting]** Jargon "At target" / "bracket" unexplained inline on Primer/Comparison — add a one-line gloss or tooltip.
- **F10 [Experience Design]** Theme switch has no persistence/scope cue.

---

## Remediation target math
Fix **F1** (Visuals 2→3) + **F2** (Color 3→4) = +2 → 19. Fix **F3 + F4** (Typography 3→4) = +1 → **20/24 ✅**. F5–F7 reinforce Color/Visuals/ED; F8–F10 are optional polish.

**Theme-system constraints (UIR-02):** layout CSS only in `site-common.css`; new/changed design tokens only in the `:root` of each theme file; **no layout rules in `site.css`**.

## Pages audited
Home, Deck Sync, Card Lookup, Deck Comparison, Deck Primer, Ask a Judge, Category Reference, Convert (desktop); Home, Deck Sync, Card Lookup, Deck Comparison, Category Reference (mobile). Mobile responsive behavior is solid (single-column stack, step dots, "Tools" nav collapse) — no responsive defects found.
