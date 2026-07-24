---
phase: 111-cut-lab-upgrade-regression-gate
verified: 2026-07-24T19:26:46Z
status: passed
score: 6/6 success criteria verified (CLUP-09/10/19/20 all PASS)
overrides_applied: 0
re_verification:
  previous_status: none
  previous_score: n/a
---

# Phase 111: Cut Lab Upgrade Regression Gate — Verification Report

**Phase Goal:** Prove the hardening did not regress shipped Cut Lab flows or the newly fixed card-pill locking behavior.
**Verified:** 2026-07-24T19:26:46Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

This is a **regression-gate** phase. The deliverables are tests, a coverage matrix, gate
docs, an all-theme readability spec, reviewed screenshots, a consolidated findings ledger, and
the a11y CSS fixes the gate surfaced. Every one was checked by opening the file/test in the
codebase — not by trusting the SUMMARY narrative. All four CLUP requirements and all six
ROADMAP success criteria are backed by verifiable artifacts.

### Observable Truths (per requirement)

| # | Truth | Status | Evidence |
| --- | --- | --- | --- |
| CLUP-09 | Matched Structural-evidence pill locks/unlocks canonical pool checkbox (server + live JS) | ✓ VERIFIED | `cut-lab-structural-evidence-lock.test.ts:200-238` asserts click flips `input[data-cut-lab-lock-card="Counterspell"]` checked true→false + `aria-pressed`; xUnit `FindLockableEvidenceCard_Matches...` (2 Theory sets, exact-name + mana-value) |
| CLUP-09 | Unmatched Structural evidence renders inert (plain span, no aria-pressed, click no-op) | ✓ VERIFIED | Vitest asserts `span.kb-chip` for "Curve congestion at MV 2", `querySelector('button[data-cut-lab-chip-card="Curve congestion..."]')` is null, inert click leaves checkboxes false; xUnit `FindLockableEvidenceCard_LeavesNonCardAndCommanderEvidenceInert` + `_ReturnsNullForCommanderOnlyAndMissingCardMatches` |
| CLUP-09 | Role-group card pills continue to lock/unlock (regression preserved) | ✓ VERIFIED | Pre-existing `cut-lab-lock-interactions.test.ts` (cited in coverage matrix, combo-badge-nested pill toggle); e2e `cut-lab-pill-interactions.spec.ts` new structural test |
| CLUP-10 | Every changed surface has an explicitly-named passing focused test | ✓ VERIFIED | `111-COVERAGE-MATRIX.md` maps all 6 surfaces to file:test, each opened-and-verified; only gap (package helper direct DOM) closed by `cut-lab-combo-package-copy.test.ts` |
| CLUP-10 | Canonical full-gate command list documented as runnable artifact | ✓ VERIFIED | `111-GATES.md` — tsc → build → xUnit CutLab → vitest cut-lab → focused e2e, with WSL constraints + MED-1 note + observed results |
| CLUP-19 | All named Cut Lab elements visible with adequate contrast, every supported theme | ✓ VERIFIED | `cut-lab-theme-readability.spec.ts` loops 24 theme cookies; `assertContrastFloor` over Lock All pill, role chip, sticky bar, findings panel, select trigger, plan input, accept button, package helper/panel/toggle/member chip |
| CLUP-19 | Focus-visible indicator clears AA contrast on interactive elements | ✓ VERIFIED | `assertFocusIndicatorContrast` (Tab-then-focus modality fix) for select/input/pills; a11y fixes landed `b1bcc34d`, present in `site-common.css:2612,4732,4989` (`--on-accent` inset + `--ink` outline) |
| CLUP-19 | Reusable WCAG contrast helper under e2e/support, used by spec | ✓ VERIFIED | `e2e/support/contrast.ts` exports `parseCssColor`/`relativeLuminance`/`contrastRatio` + `resolveContrast`; imported at spec line 4; math gated by `cut-lab-contrast.test.ts` |
| CLUP-20 | Desktop+mobile screenshots for Classic/Nyx/Commander Table showing locked package UI | ✓ VERIFIED | 6 real PNGs (`file` confirms 1280×10003 desktop, 430×15787 mobile); captured by `cut-lab-nav-themes.spec.ts:314` with deterministic "Fast mana" package |
| CLUP-20 | Each screenshot has explicit pass/fail note on 4 axes | ✓ VERIFIED | `111-UI-REVIEW.md` — 6-row table × usability/understandability/hierarchy/readability, all PASS, human APPROVED 2026-07-24 |
| CLUP-20 | All findings fixed or explicitly deferred with rationale | ✓ VERIFIED | `111-FINDINGS.md` — 7 FIXED (F-01..F-06, R-01), 1 mitigated (R-02), 2 DEFERRED w/ Cycle-20 tracking (D-01 decide-sim parallelism, D-02 page density) |

**Score:** 6/6 ROADMAP success criteria verified; CLUP-09/10/19/20 all PASS.

### Required Artifacts

| Artifact | Expected | Status | Details |
| --- | --- | --- | --- |
| `ts-tests/cut-lab-structural-evidence-lock.test.ts` | Vitest matched-vs-inert lock coverage | ✓ VERIFIED | 9945 bytes; matched button + inert span + click-toggle + no-op assertions present |
| `DeckFlow.Web.Tests/CutLabViewModelWordingTests.cs` | Server FindLockableEvidenceCard coverage | ✓ VERIFIED | 5 Theory/Fact cases: matched, mana-value, inert non-card/commander, unicode-fold null, missing-card null |
| `e2e/cut-lab-pill-interactions.spec.ts` | e2e structural-evidence lock proof | ✓ VERIFIED | Test added line 92; asserts lockable structural buttons, click lock, inert smoke |
| `111-COVERAGE-MATRIX.md` | Surface→test map | ✓ VERIFIED | All 6 surfaces COVERED (gap explicitly shown then closed) |
| `111-GATES.md` | Runnable gate sequence | ✓ VERIFIED | 5-step command sequence + constraints + observed results |
| `ts-tests/cut-lab-combo-package-copy.test.ts` | Combo/package gap-fill smoke | ✓ VERIFIED | Asserts "Combo piece" badge + package helper copy for multi-member package |
| `e2e/support/contrast.ts` | WCAG contrast helper | ✓ VERIFIED | 7606 bytes; pure math + locator resolver, type-only Playwright import |
| `ts-tests/cut-lab-contrast.test.ts` | Contrast-math gate | ✓ VERIFIED | 3 tests (black/white≈21, x/x=1, rgb/rgba parse) |
| `e2e/cut-lab-theme-readability.spec.ts` | All-theme readability spec | ✓ VERIFIED | 24 themes, full named-element set + focus-visible |
| `e2e/cut-lab-nav-themes.spec.ts` | Screenshot capture | ✓ VERIFIED | `screenshotDir` + `cut-lab-review-{theme}-{viewport}` fullPage capture |
| 6× `cut-lab-review-*.png` | Reviewed screenshots | ✓ VERIFIED | Real PNGs, ~1MB each, genuine full-page renders |
| `111-UI-REVIEW.md` | Per-shot 4-axis review | ✓ VERIFIED | Table + human-approved footer |
| `111-FINDINGS.md` | Consolidated ledger | ✓ VERIFIED | Fixed/mitigated/deferred, no open finding |
| `111-RELIABILITY.md` | Flake taxonomy | ✓ VERIFIED | Genuine-flake vs deterministic-wrong analysis, fixed/mitigated/residual |

### Key Link Verification

| From | To | Via | Status |
| --- | --- | --- | --- |
| `createStructuralEvidenceChip` matched | `button[data-cut-lab-chip-card]` → pool checkbox | click toggles `data-cut-lab-lock-card` | ✓ WIRED (Vitest asserts the live toggle) |
| `cut-lab-theme-readability.spec.ts` | `e2e/support/contrast.ts` | `import { contrastRatio }` line 4 | ✓ WIRED |
| spec theme loop | `site-*.css` themes | `deckflow-theme` cookie ×24 | ✓ WIRED |
| a11y CSS fix `b1bcc34d` | `site-common.css` working tree | `--on-accent`/`--ink` focus outlines | ✓ WIRED (lines 2612/4732/4989 present) |
| `cut-lab-nav-themes.spec.ts` | screenshots dir | `page.screenshot fullPage` | ✓ WIRED (6 PNGs on disk) |
| `111-UI-REVIEW.md` | PNG files | each row references exact `.png` | ✓ WIRED |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real Data | Status |
| --- | --- | --- | --- | --- |
| 6 review screenshots | rendered Cut Lab page | live headless server, deterministic package created before capture | Yes — 10003px/15787px heights match documented page density | ✓ FLOWING |
| theme-readability spec | computed element colors | `effectiveBackgroundColor` composites live DOM ancestors | Yes — caught 6 real WCAG defects (F-03..F-06) | ✓ FLOWING |

### Behavioral Spot-Checks

Per orchestrator instruction, the known-verified gate results are trusted and not re-run
(vitest cut-lab 58/58, xUnit CutLab 419/0, e2e readability 24×2 green, pill-interactions 4/4,
nav-themes 6/6, tsc clean). Independent codebase checks performed instead:

| Check | Result | Status |
| --- | --- | --- |
| All 14 claimed artifact files exist | present with expected byte sizes | ✓ PASS |
| Referenced commits resolve | `b1bcc34d`, `38f73725`, `26dbca74` all exist | ✓ PASS |
| a11y CSS fixes still in working tree | `--on-accent`/`--ink` focus outlines present in `site-common.css` | ✓ PASS |
| Screenshots are real PNG captures | `file` → valid PNG, full-page dimensions | ✓ PASS |
| 24-theme spec matches supported themes | 23 non-classic css (excl. mobile/overrides) + classic = 24 | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Status | Evidence |
| --- | --- | --- | --- |
| CLUP-09 | 111-01 | ✓ SATISFIED | Three-layer lock/inert regression (Vitest + xUnit + e2e) |
| CLUP-10 | 111-02 | ✓ SATISFIED | Coverage matrix (6/6 surfaces) + canonical GATES doc + gap-fill smoke |
| CLUP-19 | 111-03 | ✓ SATISFIED | 24-theme readability + focus-contrast spec + contrast helper + a11y CSS fixes |
| CLUP-20 | 111-04 | ✓ SATISFIED | 6 reviewed screenshots + UI-review doc (human-approved) + findings ledger |

No orphaned requirements: REQUIREMENTS.md maps exactly CLUP-09/10/19/20 to Phase 111, all claimed.

### Anti-Patterns Found

| File | Pattern | Severity |
| --- | --- | --- |
| (none) | Debt-marker scan (TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER/not-yet-implemented) across all 8 new/modified phase files | ℹ️ Clean — zero markers |

### Human Verification

The CLUP-20 human UI checkpoint was **already performed and APPROVED** (2026-07-24, recorded in
`111-UI-REVIEW.md` and `111-04-SUMMARY.md`): all six shots PASS on usability, understandability,
aesthetic hierarchy, and readability with no corrections. This is a completed deliverable, not an
outstanding item — no new human verification is required to close the phase.

### Gaps Summary

No gaps. Every deliverable the phase promised exists in the codebase with real substance:
- CLUP-09 lock/inert behavior is asserted deterministically at server (xUnit) and client (Vitest)
  layers, with e2e corroboration — the exact three-layer proof the plan specified.
- CLUP-10's coverage matrix was audited by opening each cited test (not trusting a list); the one
  genuine gap (package-helper DOM assertion) was closed, and GATES.md is a real runnable sequence.
- CLUP-19's readability spec is a genuine defensive gate: it caught 6 real WCAG failures that were
  then fixed in CSS (`b1bcc34d`, still present in the working tree), and covers the full named
  element set across all 24 themes plus focus-visible rings.
- CLUP-20's 6 screenshots are real full-page captures, human-reviewed and approved, and the
  findings ledger dispositions every phase finding (7 fixed, 1 mitigated, 2 deferred with
  Cycle-20 tracking) — satisfying Success Criterion 6.

The goal ("prove the hardening did not regress shipped flows or the pill-locking behavior") is
achieved: the regression evidence exists, runs green in the known-verified state, and the gate
demonstrably did its job by surfacing and forcing fixes for real a11y defects.

---

_Verified: 2026-07-24T19:26:46Z_
_Verifier: Claude (gsd-verifier)_
