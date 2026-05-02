# Phase 02 — UI-SPEC Checker Report

**Run:** 2026-04-30 (post WSL-EIO recovery)
**Spec under review:** `02-UI-SPEC.md` (commit 46d7a73)
**Checker:** gsd-ui-checker

---

## Verdict Matrix

| Dimension | Verdict |
|-----------|---------|
| 1 — Copywriting | BLOCK |
| 2 — Visuals | BLOCK |
| 3 — Color | PASS |
| 4 — Typography | FLAG |
| 5 — Spacing | FLAG |
| 6 — Registry Safety | PASS |

**Overall: BLOCKED** — 2 BLOCKs (same root cause), 3 FLAGs.

---

## BLOCK Findings

### B1.1 / B2.1 — Analyze-group primary contradicts D-02

**Spec line 181:** lists `ChatGPT Analysis` (`~/chatgpt-packets`) as Analyze-group primary card.

**CONTEXT.md D-02 line 28 (locked):** "Per-group primaries: Analyze→**Deck Comparison**".

**Verified target route** in `DeckFlow.Web/Views/Deck/Home.cshtml` line 17: `~/chatgpt-deck-comparison`.

Cascade effect: hero band (ChatGPT Analysis) + Analyze-grid primary border (also ChatGPT Analysis) would stack two accent borders on the same workflow and leave Deck Comparison invisible. Fails ROADMAP SC-1.

### B1.2 — Categories group has no explicit treatment

`Home.cshtml` has **4** hub groups (Analyze, Build, Reference, **Categories** — line 61). Spec covers per-group primaries for the first three. Categories is silent — executor will guess.

---

## FLAG Findings

### F4.1 — Typography table inconsistent with usage

`.hub-hero__eyebrow` rule (line 132) uses `--fs-xs`, but `--fs-xs` is **not** in the typography table (lines 64–70). Spec claims 4 sizes; actual usage is 5.

Weights row (line 72) reads "400 and 600/700" — three weights, not two. Phase doesn't introduce new weights but should phrase it as "consumed, no additions".

### F5.1 — Spacing-scale honesty gap

Spec line 35 claims "All values map to multiples of 4px." Project rem base is **15px** (`html { font-size: 15px }`). None of the declared rem values are 4-multiples (1rem = 15px, 0.25rem = 3.75px, etc). The spec's own px-equiv column at lines 39–44 already shows non-4-multiples — internal contradiction.

Line 46 says "Exceptions: none" but `1.1rem` (line 49) and `0.35rem` (lines 137, 143) are real deviations from the rem-step grid.

### F-Verify — Verification checklist tightening

- Item 4 (no new `:root` tokens): needs explicit Phase 01 baseline citation.
- Item 7 (busy state functional): not greppable; should be marked manual-verify.

---

## Numbered Revision List (apply in order)

1. **Line 181** — Replace Analyze primary entry with `Deck Comparison` card at `~/chatgpt-deck-comparison`; clarify ChatGPT Analysis stays as a regular grid card and the hero band is its dedicated promotion (per D-02 + D-01 "both layers ship").

2. **Line 392** — Verification gate text → "(3 total: Deck Comparison in Analyze, Deck Sync in Build, Card Lookup in Reference)".

3. **Component Inventory §2 (after line 183)** — Add explicit Categories-group exclusion: per-group promotion stops at the three workflow groups; the page-level hero serves as Categories' focal point.

4. **Lines 33–46** — Rewrite Spacing Scale section: drop "multiples of 4px" framing; reframe as quarter-rem-step scale at 15px base; list `1.1rem` (`.hub-card` padding-x) and `0.35rem` (`.hub-hero__eyebrow` / `.hub-hero__title` margin-bottom) as named exceptions instead of "none".

5. **Lines 64–72** — Add `--fs-xs` row (~11px, weight 600) for `.hub-hero__eyebrow` to typography table OR change line 132 from `--fs-xs` to `--fs-sm` to stay inside declared budget. Restate weights row as "Weights consumed (no additions): 400, 600, 700 — all existing in site.css".

6. **Line 304** *(non-blocking)* — Eyebrow copy "Headline workflow" is generic. Consider `Start here` or `New to DeckFlow?` for first-time-visitor anchoring. FLAG only.

7. **Verification Checklist (lines 388–404)** — Item 4: cite `.planning/phases/01-visual-system-tokens/01-03-PLAN.md` as token-baseline source. Item 7: append manual-verify note ("Items 1–6 are machine-verifiable via grep; item 7 requires manual browser verification on a throttled connection").

---

## Sign-Off

- [ ] Revisions 1–5 applied and re-checked → APPROVED
- [ ] Revision 6 applied or explicitly waived
- [ ] Revision 7 applied
