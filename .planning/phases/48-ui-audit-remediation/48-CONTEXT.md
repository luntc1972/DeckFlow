# Phase 48: UI Audit + Remediation - Context

**Gathered:** 2026-06-16
**Status:** Ready for planning
**Source:** Operator decisions (audit-first, local target) + completed audit

<domain>
## Phase Boundary
Produce a 6-pillar visual audit (DONE — see `48-AUDIT.md`), then remediate HIGH/MEDIUM findings to lift the score from the current **17/24** to **≥20/24**, verified in-browser at mobile + desktop. UI-only; no backend/feature work.
</domain>

<decisions>
## Implementation Decisions (LOCKED)
- **Audit-first structure**: the audit (`48-AUDIT.md`) is complete and is the source of concrete fix-tasks. Plans target its findings by ID (F1–F10).
- **Target = ≥20/24**: minimum closing path is F1 (Visuals 2→3) + F2 (Color 3→4) + F3+F4 (Typography 3→4) = 20. F5–F7 reinforce; F8–F10 optional polish.
- **Theme-system constraints (hard)**: layout CSS only in `site-common.css`; new/changed design **tokens** only in the `:root` of each theme file; **never** add layout rules to `site.css`. (DeckFlow CLAUDE.md constraint.)
- **No new dependencies**: no web-font download, no CSS framework, no npm packages. Icons = inline SVG. Typography wins use existing system stack + weight/letter-spacing.
- **Per-theme propagation**: token changes must be applied to all 24 guild theme `:root` blocks (they are full forks), with a spot-check screenshot per remediated theme. Layout/markup changes in `site-common.css` + shared partials propagate automatically.
- **Verification (UIR-03)**: every remediated finding verified with browser screenshots at ≥2 viewports (mobile ≤768px, desktop ≥1024px). Grep/static analysis alone is insufficient. Final score re-verify against the **deployed deckflow.gg** site after merge/deploy (audit was local per operator choice).

### Claude's Discretion
- Exact token values, icon set/source (inline SVG), shadow/elevation recipe, max-width for short pages — within the constraints above.
</decisions>

<canonical_refs>
## Canonical References
**Downstream agents MUST read these before planning or implementing.**

### Audit findings (primary input)
- `.planning/phases/48-ui-audit-remediation/48-AUDIT.md` — scored pillars + findings F1–F10 with file targets.

### CSS / theme system
- `DeckFlow.Web/wwwroot/css/site.css` — Classic (Jeskai) theme `:root` token set + base body styles (reference token layout; do NOT add layout rules here).
- `DeckFlow.Web/wwwroot/css/site-common.css` — shared layout CSS (all layout/elevation/spacing changes go here).
- `DeckFlow.Web/wwwroot/css/site-<guild>.css` (24 files) — per-theme forks; token changes replicate into each `:root`.
- `DeckFlow.Web/wwwroot/css/site-mobile.css` — mobile-specific layout.
- DeckFlow project `CLAUDE.md` — theme-system + token constraints, LF line endings, changed-lines format gate.

### Markup
- Home hub + shared partials under `DeckFlow.Web/Views/Shared/` and `Views/Deck/` — for icon/empty-state additions.
</canonical_refs>

<specifics>
## Specific Ideas
- F1: inline-SVG tool icons on home hub cards + section headers; card hover/elevation.
- F2: widen `--panel`/`--bg` delta + subtle shadow + stronger `--line` border.
- F3: `--fs-xs` ~0.75rem → ~0.82rem (smallest text ≥12.75px).
- F4: 700 + letter-spacing section labels; 600 medium field labels/badges.
- F5: `--muted` → ≥ `#4b5563`.
- F6/F7: cap/center short-form pages + add example/empty-state panel.
</specifics>

<deferred>
## Deferred Ideas
- F8–F10 (Primer density, jargon glosses, theme-persistence cue) — LOW; include only if cheap.
- Custom brand web-font — out of scope (no new dependency).
</deferred>

---
*Phase: 48-ui-audit-remediation*
*Context gathered: 2026-06-16 (audit-first; local target)*
