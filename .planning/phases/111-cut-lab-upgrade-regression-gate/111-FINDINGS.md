# 111 — Consolidated Findings Ledger (Success Criterion 6)

Every finding surfaced across Phase-111 Plans 01–04 (test-revealed defects, contrast breaches,
UI-review notes, reliability issues), each dispositioned FIXED or DEFERRED. No finding left
silently open.

## FIXED

| ID | Source | Description | Disposition |
|----|--------|-------------|-------------|
| F-01 | Plan 02 (CLUP-10 audit) | Package helper copy had no direct DOM assertion (only indirect coverage) | **FIXED** — `cut-lab-combo-package-copy.test.ts` added (commit `38f73725`). |
| F-02 | Plan 03 (CLUP-19 e2e) | Focus-visible probe bug: programmatic `.focus()` never engages `:focus-visible` on a native `<select>`, so the check was invalid | **FIXED** (test) — press `Tab` for keyboard modality before focus (commit `26dbca74`). This corrected probe surfaced F-03…F-06. |
| F-03 | Plan 03 (CLUP-19) | Filled **accept button** focus ring ≈ its own accent fill — contrast 1.55 (all themes, systemic) | **FIXED** — ring uses `var(--on-accent)` inset (commit `b1bcc34d`). |
| F-04 | Plan 03 (CLUP-19) | Checked **package-toggle** focus ring on accent fill, same root cause | **FIXED** — `var(--on-accent)` inset (commit `b1bcc34d`). |
| F-05 | Plan 03 (CLUP-19) | Accept-button **text** contrast < 3.0 (white on pale accent): esper 2.78, abzan 2.84, golgari 2.88 | **FIXED** — `--accent` darkened minimally (hue-preserving) to ≥3.1 in the 3 theme files (commit `b1bcc34d`), per user decision to hold the 3.0 floor and fix only the sub-3.0 themes. |
| F-06 | Plan 03 (CLUP-19) | Focus ring too dim vs dark element backgrounds (WCAG 1.4.11): dimir/grixis/jund/rakdos/sultai on select trigger, plan input, Lock All pill, role chip (2.08–2.86) | **FIXED** — focus outline → `var(--ink)` (contrasts every surface; no regression on the 18 passing themes); `.df-select__trigger` fixed app-wide (commit `b1bcc34d`). |
| R-01 | Reliability hardening (folded in) | `run-web-test.sh` blind `fuser -k 5173` cross-killed sibling test servers; stale Windows listener invisible to WSL `ss` | **FIXED** — curl-probe reuse guard + `FORCE_RESTART=1` escape (commit `26dbca74`). |

## Already mitigated (no action)

| ID | Source | Description | Disposition |
|----|--------|-------------|-------------|
| R-02 | Reliability analysis | Admin-console tool-flag contention under parallel workers | **Already mitigated** — full-test `/tmp/deckflow-admin-e2e.lock` mutual exclusion + synthetic per-test `X-Forwarded-For` IP. Verified across all cut-lab specs. |

## DEFERRED (with rationale + tracking)

| ID | Source | Description | Disposition |
|----|--------|-------------|-------------|
| D-01 | Reliability analysis | Decide-sim starvation under **local** many-worker parallelism (Import-pool render timeout under full-suite load) | **DEFERRED** — CI already pins `workers: 1` + `retries: 1` and is the authoritative gate; specs pass green in isolation. Local guidance documented in `111-GATES.md` (bounded `--workers`) + `111-RELIABILITY.md`. True per-worker tool-flag isolation captured as a **Cycle-20 candidate** (not needed for correctness). |
| D-02 | Plan 04 (CLUP-20 review) | Lock-your-pool view is very long (whole workflow stacked on one page) — high density, legible, pre-existing | **DEFERRED** — out of scope for a regression gate; recorded as a **Cycle-20 UX consideration** (progressive disclosure / step-scoped view). Not a 111 regression. |

## Summary

- **7 findings FIXED** (F-01…F-06, R-01) across commits `38f73725`, `26dbca74`, `b1bcc34d`.
- **1 already mitigated** (R-02).
- **2 deferred with rationale + Cycle-20 tracking** (D-01, D-02).
- CLUP-20 human UI review: **all 6 shots PASS** on all four axes (`111-UI-REVIEW.md`, approved 2026-07-24).
- No finding left silently open. Success Criterion 6 satisfied.
