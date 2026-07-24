# 111 — Cut Lab UI Review (CLUP-20)

Representative desktop + mobile review of the Cut Lab **Lock-your-pool** view across the three
CLUP-20 themes, each showing a locked deterministic "Fast mana" package (Sol Ring + Arcane
Signet), the pool table, role-group chips, sticky status, structural findings, and the decide
controls. Captured by `cut-lab-nav-themes.spec.ts` (6 PNGs, `.planning/ui-design/cut-lab/screenshots/`).

Axes: **Usability** (find/operate Lock All, chips, filters, package panel + Lock-package toggle,
decide buttons) · **Understandability** (is the flow legible) · **Aesthetic hierarchy** (primary
action vs secondary evidence vs package panel ranked) · **Readability** (text legible on the theme).

| # | Theme | Viewport | Screenshot | Usability | Understandability | Aesthetic hierarchy | Readability | Verdict |
|---|-------|----------|-----------|-----------|-------------------|---------------------|-------------|---------|
| 1 | Classic | desktop | `cut-lab-review-classic-desktop.png` | PASS — all controls present + operable | PASS | PASS — accept CTA vs evidence vs package panel ranked | PASS — blue-on-light clear | **PASS** |
| 2 | Classic | mobile | `cut-lab-review-classic-mobile.png` | PASS — sections stack, no overflow | PASS | PASS | PASS | **PASS** |
| 3 | Nyx | desktop | `cut-lab-review-nyx-desktop.png` | PASS | PASS | PASS | PASS — purple-on-dark clear | **PASS** |
| 4 | Nyx | mobile | `cut-lab-review-nyx-mobile.png` | PASS | PASS | PASS | PASS | **PASS** |
| 5 | Commander Table | desktop | `cut-lab-review-commander-table-desktop.png` | PASS | PASS | PASS | PASS — green-on-cream clear | **PASS** |
| 6 | Commander Table | mobile | `cut-lab-review-commander-table-mobile.png` | PASS | PASS | PASS | PASS | **PASS** |

## Notes

- The locked **Fast mana** package UI (package panel + Lock-package toggle + "How packages work"
  helper copy) renders correctly in all six shots — the CLUP-20 package evidence requirement is met.
- **Non-blocking observation (not a 111 regression):** the Lock-your-pool view is very long — the
  entire workflow (pool → packages → competes → structural findings → role floors → goals → export
  → scenarios → what-if → cut rounds → tune quantities → cuts made → compare) stacks on one page.
  Density is high but legible; this is a pre-existing design trait, out of scope for this
  regression gate. Captured as a Cycle-20 consideration in the findings ledger.
- The CLUP-19 a11y fixes (focus rings, accent darkening) are focus-state / other-theme changes and
  are validated separately by `cut-lab-theme-readability.spec.ts` (24 themes × 2 viewports green);
  they are not directly visible in these static, unfocused captures.

_Human checkpoint: **APPROVED** (2026-07-24) — reviewer confirmed all six shots PASS on the four axes; no corrections. The page-length observation is recorded as a Cycle-20 consideration in 111-FINDINGS.md._
