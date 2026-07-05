# Phase 84: Theme Semantic-Token Migration - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning
**Source:** Inline decisions after research (user opted to examine UI changes + research instead of full discuss-phase)

<domain>
## Phase Boundary

Finish migrating leftover `--accent-strong` consumption in shared CSS onto the correct
semantic tokens (`--link` / `--danger` / `--focus` / `--cta-border`) across all 27 theme
forks, WITHOUT other visual drift. Requirements THEME-01, THEME-02, THEME-03.

Research finding: the originally-named bug (`.feedback-error` reading as a link) is ALREADY
fixed — `site-common.css` `.feedback-error` already uses `var(--danger)`. Remaining work is
the ~60 leftover `--accent-strong` call sites (56 in `site-common.css`, 4 in `site.css`).
</domain>

<decisions>
## Implementation Decisions (LOCKED)

### D1 — Drift-absorption direction
Re-alias the three semantic tokens `--link` / `--focus` / `--cta-border` to point at
`var(--accent-strong)` (they currently alias `var(--accent)`), THEN swap the genuine
link/focus/cta call sites from `var(--accent-strong)` → `var(--link|--focus|--cta-border)`.
- Net effect: the ~30 real link/focus/cta call sites stay byte-identical; only the ~8
  already-migrated call sites shift color. Smallest possible visible delta.
- Do NOT re-alias `--danger` — danger must remain a distinct danger color per theme.

### D2 — Decorative sites stay on --accent-strong
Only migrate call sites with a genuine link / danger / focus / cta-border role. The ~30
decorative brand-emphasis sites (badges, score pips, active-tab indicators, hover
backgrounds — e.g. `.bracket-badge--b3`) STAY on `--accent-strong` and are documented as
intentional decorative use. This honors THEME-03 (no visual drift) over a literal reading
of THEME-01. THEME-01 is satisfied by "every usage correctly classified by role" — where
the correct classification is decorative, `--accent-strong` is the correct token.

### D3 — Typography/font-size cleanup deferred to Phase 86
`tasks/UI-REVIEW.md` assigns a Typography/font-size migration to "Phase 84", but it is NOT
in THEME-01/02/03 or the ROADMAP Phase 84 success criteria. It is OUT of scope for Phase 84.
Note the handoff so the Phase 86 UI re-score owns it; do not let Phase 84 silently drop it.

### D4 — Missing token blocks (from research)
`site-commander-table.css` has NO `@import` and is missing all four semantic tokens — it
MUST get the needed token(s) added to its `:root` before any call site there is swapped
(otherwise an unresolved custom property with no fallback breaks rendering). For the other
`@import`-based forks, add a semantic token to a fork's `:root` only when a swapped call
site actually needs it; do not mass-add tokens to forks purely for uniformity (minimize
churn / drift). Prefer defensive fallback chains (`var(--link, var(--accent-strong))`) at
swap sites to survive any fork that lacks the token.

### Claude's Discretion
- Exact task/wave breakdown, fallback-chain style, and per-file swap ordering.
- Whether to split the migration into (a) alias re-point + shared-file swaps and (b) per-fork
  workaround unwind, or keep as one plan.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Research + requirements
- `.planning/phases/84-theme-semantic-token-migration/84-RESEARCH.md` — full call-site
  role-classification map, red-guild list, per-fork token inventory, verification approach
- `.planning/REQUIREMENTS.md` — THEME-01/02/03 exact wording + UIAUDIT handoff context
- `.planning/ROADMAP.md` — Phase 84 success criteria

### Project CSS constraints (from CLAUDE.md)
- Layout CSS goes in `site-common.css`, NOT `site.css`; token additions go in each theme's
  own `:root`. Guild themes are standalone forks (most `@import site.css`).
- LF line endings enforced (`.gitattributes`); changed-lines format gate; byte-identical
  carve-outs (never re-indent raw strings, preserve switch expressions, etc.).

### Existing test precedent to EXTEND (not reinvent)
- `DeckFlow.Web/e2e/theming.spec.ts` — Tier-1/Tier-2 computed-style tests across all 27
  themes; extend for THEME-02 (danger ≠ link per theme, desktop + mobile).
</canonical_refs>

<specifics>
## Specific Ideas

- Red-guild workaround to unwind: `site-rakdos.css` `/* UI-VS-02 */` override of `--link`
  so error-on-red stays distinct — with `--danger` distinct, re-check whether the workaround
  is still needed or should be normalized. Find all UI-VS-* workarounds.
- Precedent for status colors: commit `15d34cc4` (manabase health chips, fixed status colors).
- Recent CSS additions the migration must NOT disturb: `8d5d6621` print-to-paper.
</specifics>

<deferred>
## Deferred Ideas

- Typography/font-size tokenization → Phase 86 (see D3).
- Mass-adding semantic token blocks to all @import forks for uniformity → out (see D4).
</deferred>

<verification>
## Verification Expectations (feed must_haves)

- THEME-01: every `--accent-strong` call site is either swapped to the correct semantic
  token OR documented as intentional decorative; token additions live in the fork `:root`;
  no layout CSS moved into `site.css`.
- THEME-02: error/danger text resolves to `--danger`, visually distinct from `--link`, in
  red guild themes; verified live desktop + mobile (Playwright via WSL headless server,
  DECKFLOW_DISABLE_AUTO_BROWSER=true — never open a Windows browser).
- THEME-03: theme render diff limited to intended semantic corrections (the ~8 documented
  shifts from D1); a spot-check across all 27 themes × light/dark shows no other surface
  changed color.
- `dotnet build` clean; existing theming e2e tests green; format gate passes on changed lines.
</verification>

---

*Phase: 84-theme-semantic-token-migration*
*Context gathered: 2026-07-04 via inline decisions + research*
