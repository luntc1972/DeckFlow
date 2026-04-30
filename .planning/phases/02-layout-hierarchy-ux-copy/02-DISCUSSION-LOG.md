# Phase 02: Layout, Hierarchy & UX Copy - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-30
**Phase:** 02-layout-hierarchy-ux-copy
**Areas discussed:** Hub primary-action treatment, Voice convention, Feedback busy-state mechanism, CSS class strategy

---

## Hub primary-action treatment

### Question 1: Structural promotion mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Single hero CTA above the grid | One large headline action above all 3 groups; other 10 cards stay equal | |
| `.hub-card--primary` on one card per group | Each group has exactly one promoted card; distributed hierarchy | |
| Both — hero + per-group primary | Hero above + each group primary; strongest hierarchy, heaviest design surface | ✓ |
| Hero only, ChatGPT Analysis pulled out of grid | Hero + remove from Analyze grid; no duplication | |

**User's choice:** Both — hero + per-group primary.
**Notes:** Hero = ChatGPT Analysis (full-width band). Per-group primaries = Comparison / Deck Sync / Card Lookup. ChatGPT Analysis stays in the Analyze grid as well (additive, not removal).

### Question 2: Visual treatment for promoted cards

| Option | Description | Selected |
|--------|-------------|----------|
| Accent border (`--cta-border`) | Canonical, subtle, theme-aware; uses Phase 01 token | ✓ |
| Size bump / typography weight | Bolder title or larger card | |
| Subtle background tint | Card uses `--panel` tinted toward `--accent` | |
| Hero gets distinct treatment, group primaries get a lighter one | Two-tier hierarchy | |

**User's choice:** Accent border only.
**Notes:** Single visual signal across hero + 3 group primaries. No size bump, tint, or badge.

---

## Voice convention

### Question 1: Site-wide voice

| Option | Description | Selected |
|--------|-------------|----------|
| Verb-noun ("Send Feedback", "Compare Decks") | Action-forward; matches tool identity; UX-01/UX-03 simplified | ✓ |
| Noun-only ("Feedback", "Deck Comparison") | Section-forward; matches current hub-card titles | |
| Hybrid — noun titles, verb-noun CTAs | Page titles stay short, buttons stay verby | |

**User's choice:** Verb-noun.
**Notes:** Applies to `<title>`, `<h1>`, and CTA buttons. Feedback page becomes "Send Feedback" for both title and h1.

### Question 2: Bulk-edit hint partial

| Option | Description | Selected |
|--------|-------------|----------|
| Parameterize — each call site passes its own verb | Cleanest; partial accepts a verb model param | ✓ |
| Single generic action verb | One non-generic verb across all 5 pages | |
| Move the hint inline into each page | Stop sharing the partial; loses DRY | |

**User's choice:** Parameterize.
**Notes:** Partial used in 5 views — each passes its host page's submit verb to the partial.

---

## Feedback busy-state mechanism

### Question 1: Mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Client-side TS — intercept submit, disable + spinner | Matches existing wwwroot/ts/ pattern; graceful fallback | ✓ |
| Server-side double-submit token + re-render | No JS dependency; doesn't help slow-connection feel | |
| Hybrid — client TS + server token | Strongest; most code | |

**User's choice:** Client-side TS.
**Notes:** Browser handles POST normally. Form remains functional with JS disabled.

### Question 2: Spinner shape

| Option | Description | Selected |
|--------|-------------|----------|
| CSS-only ring + button text swap to "Sending…" | Pure CSS, theme-aware via tokens, ~30 lines | ✓ |
| Disabled state + text swap, no spinner glyph | Simplest; less reassuring on slow connections | |
| CSS spinner + keep original button text | Spinner glyph next to label; harder to scan | |

**User's choice:** CSS ring + "Sending…" text swap.

---

## CSS class strategy

### Question 1: Class naming approach

| Option | Description | Selected |
|--------|-------------|----------|
| Keep ROADMAP names exactly | `.feedback-panel` / `.admin-feedback-detail` / `.admin-action-form`; purpose-named, scoped | ✓ |
| Factor a shared `.panel` base + modifiers | Reusable surface; more work; pattern may not pay off | |
| Hybrid — keep panel names, use generic `.inline-form` | Reusable utility for the 4 inline-display forms | |

**User's choice:** Keep ROADMAP names exactly.
**Notes:** `.feedback-panel` and `.admin-feedback-detail` already exist; only inline `style=` overlays move into the rule body. `.admin-action-form` is new and replaces `style="display:inline"` on 4 admin forms.

---

## Claude's Discretion

- Exact hero copy (label, optional subtitle) beyond "ChatGPT Analysis" / "Run ChatGPT Analysis"
- Whether busy-state TS lives in a new module or `site.ts` extension
- Specific verb passed to `_MoxfieldBulkEditHint` per host page (planner reads each submit button label)
- Specific verb-noun phrasing for new Feedback `<title>`/`<h1>` (e.g., "Send Feedback" vs "Submit Feedback")

## Deferred Ideas

- Server-side anti-double-submit token for `/feedback` (defense-in-depth) — Phase 4 candidate if abuse emerges
- Shared `.panel` / `.inline-form` factoring — revisit if a 3rd panel surface lands
- Voice-convention sweep of remaining pages — not in scope; only Feedback gets the convention applied this phase
- Other inline `style=` instances elsewhere in the app — out of scope (UI-LH-02 bounded to 3 flagged files)
- Size bump / tint / badge for promoted cards — revisit only if border alone proves too subtle in user data
