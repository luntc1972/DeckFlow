# Manabase Page — UX Research Report (2026-07-12)

Scope: usability audit of `/manabase`, split-into-pages question (casual vs cEDH), best practices.
Method: code inventory (Manabase.cshtml 839 lines, 32 DOM sections), live headless audit (7 screenshots, `ux-shots/manabase-audit/`), web research (NN/g, WCAG, competitor scan). Research-only — no code changes.

## Verdict on the split question

**Do NOT split into multiple pages.** Evidence:

- No surveyed MTG tool (Archidekt, Moxfield, deckstats, EDHREC) splits casual vs competitive into separate routes — all use one page with inline density controls.
- NN/g: long-form analysis performs better as one scrolling page than split page-loads; anchor/jump nav is the mechanism for length, not pagination.
- The page already has the right primitive: an explicit Casual/cEDH radio that conditionally renders sections. That is the recommended pattern (single page + conditional sections). NN/g's mode-slip warning applies: the active mode must stay visibly signaled in the *result* area, not only in the form (currently only a small "Mode: cEDH" metadata line ~mid-page).

The real problem is not audience mixing — it's **page length driven by one unbounded table**, fixable in place.

## Measured facts

| State | Desktop height | Mobile height |
|---|---|---|
| Casual result | 5,661 px (~6.3 screens) | 15,674 px (~18.6 screens) |
| cEDH result | 3,241 px (~3.6 screens) | 4,813 px (~5.7 screens) |

- Castability table renders all 65 tracked spells; ~50 rows are 92–100% "good" (no decision value). On mobile each row is a ~150–200px card → the 15.7k px page.
- Auto-scroll-on-load works: verdict chip + biggest-fix land fully in view post-Analyze (good).
- Only 2 semantic headings (`h2 Result`, `h3 Castability`) across the whole result; no anchor nav; only a scroll-to-top FAB.
- 8 collapsibles: 1 open (cost overrides when suggestions detected), 7 closed. Progressive disclosure partially applied already.
- Mobile tables: card-pattern, no horizontal overflow (good) — but long card names hard-clip with no ellipsis/wrap (`Clive, Ifrit's Dominant // Ifrit, Wa`), tooltip not touch-accessible.
- Color cues all pair color + text label ("55% · low") — WCAG 1.4.1 compliant already (good).
- Verdict messaging duplicated: summary card "Biggest fix" repeated ~800px later by "Reading your deck" card with unrelated sections between.
- cEDH bugs: verdict copy says "Full list in the castability table below" but the table doesn't render in cEDH (dangling ref); two-column stat row lopsided (empty right column where Simulated cast rate would be).

## Prioritized recommendations

**HIGH**
1. Cap castability table by default: show worst ~15 rows (or all rows below a risk threshold), "Show all 65" expander. Single biggest length fix, transforms mobile.
2. Mobile long-name clipping: allow wrap or ellipsis + tap-to-reveal; current hard clip loses the identity of exactly the cards the page exists to name.
3. cEDH dangling reference: verdict copy must be mode-aware ("Full list in the castability table below" → cEDH variant without the reference, or link to color-findings table).

**MEDIUM**
4. Merge the two verdict narratives: fold "Reading your deck" issue bullets into (or directly beneath) the verdict summary card — one BLUF block, not two 800px apart.
5. Add semantic headings (`h3`) to every result section (Karsten source check, Simulated cast rate, Untapped sources, Opening hand, Ramp/draw, Color findings…) + an "On this page" anchor nav (sticky on desktop) once results render. Helps AT users and power users; NN/g-recommended alternative to splitting.
6. Fix cEDH lopsided stat row: let Karsten card span full width in cEDH, or put the meta-range panel in the empty slot.
7. Persistent mode indicator in the result header (e.g. chip "cEDH analysis" next to the verdict) — NN/g mode-slip mitigation; currently mode is only discoverable mid-page.

**LOW**
8. Consider tucking Ramp/draw advisory + Command-zone castability behind the same lens-card visual system as the other lenses for consistency.
9. Pair headline sim percentages with shape where cheap (e.g. keep-size process already does this well — extend the pattern to cast-rate: worst-N distribution is effectively that once table is capped).
10. Consider "condensed view" toggle later (Archidekt precedent) only if length remains a complaint after #1.

## Best-practices checklist applied (from research)

- BLUF/verdict-first: ✅ already (auto-scroll + verdict card) — keep.
- Progressive disclosure: partial — collapsibles good, castability table is the gap (#1).
- Anchor nav over pagination for long pages: ❌ missing (#5).
- WCAG 1.4.1 no color-only cues: ✅ already.
- Mobile wide tables → card pattern: ✅ already; name clipping defect (#2).
- Mode signaling: partial (#7).
- Single page for both audiences: ✅ keep — matches all competitor precedent.

## Sources

NN/g Progressive Disclosure; F-Shaped Pattern; Modes in User Interfaces; Anchors OK?; Pagination/View-All. WCAG 1.4.1. Archidekt dev update (pinned/condensed stats); Moxfield features wiki; deckstats; EDHREC FAQ. Competitor UI details directional (help docs/snippets, not pixel-verified).
