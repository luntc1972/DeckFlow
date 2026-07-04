# Phase 79 — UI Review

**Audited:** 2026-07-01
**Baseline:** Abstract 6-pillar standards (no UI-SPEC.md — planner skipped it) + sibling-component consistency (`.chatgpt-score`, `.manabase-lens`, `.bracket-callout`)
**Screenshots:** Not captured — no dev server on :3000/:5173/:8080 AND flag `analysis.interaction-audit` seeded OFF, so the readout does not render without standing up the server with the flag ON. Structural code audit only. Pixel-dependent checks are flagged as **owed: live visual verify at desktop 1280 + mobile 390 across themes**.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Exemplary hedging voice; undercut by a redundant empty-bucket double-negative |
| 2. Visuals | 3/4 | 5-column desktop grid of variable-length card lists is dense/uneven; no intermediate breakpoint |
| 3. Color | 3/4 | Correct token reuse (no new `:root`), but coverage-gaps callout shows `--warning` chrome even on the clean "None flagged" state; cross-theme render unverified |
| 4. Typography | 4/4 | Three token-driven sizes, clear hierarchy, no sprawl, consistent with sibling |
| 5. Spacing | 3/4 | Consistent rem scale + `min-width:0` guard; mixed px/rem (mirrors sibling); rhythm unverified at target widths |
| 6. Experience Design | 4/4 | Per-bucket + gaps empty states handled; deep untrusted-input hardening; flag-gated |

**Overall: 20/24**

No BLOCKERs. Six WARNING-level findings below. The component is a faithful, well-hedged mirror of the proven Phase-77 score block; the deductions are density/copy/color-semantics nits plus the genuine inability to verify rendered pixels while the flag is OFF.

---

## Top 3 Priority Fixes

1. **5-column desktop grid density (`site-common.css:3141-3145`)** — Five buckets side-by-side each hold a variable-length `<ul>` of card names (Targeted removal can be 6-10 cards; Counterspells often 0-2). At 1280 inside the nested `summary-panel`, columns are ~180-200px and heights are wildly uneven, and the grid jumps straight from 5→2 at 860px with nothing between. Card names like "Swords to Plowshares" will wrap mid-column. **Fix:** add an intermediate step (e.g. `repeat(3, 1fr)` between ~1024 and 860px) and/or cap column count so lists stay legible; verify live at 1280 before flag-ON.

2. **Redundant empty-bucket copy (`DeckAnalysis.cshtml:597,609`)** — When a bucket has zero confident cards the readout prints *"Approximately 0 confident; verify against your list."* immediately followed by *"No confident cards found by DeckFlow."* — two negations saying the same thing, and "verify against your list" is nonsensical when there is nothing to verify. **Fix:** suppress the `.interaction-audit-count` line when `bucket.Confident.Count == 0`, letting the `.interaction-audit-empty` message stand alone.

3. **Warning chrome on a clean result (`site-common.css:3181-3188`; `DeckAnalysis.cshtml:618-632`)** — `.interaction-audit-gaps` always renders a `--warning` left border, including the positive branch that outputs *"None flagged by DeckFlow; verify against your list."* A caution-colored callout on a no-problems state is a false alarm. **Fix:** apply the warning border only when `CoverageGaps.Count > 0`; use a neutral `--line` / `--accent-strong` border for the "none flagged" case (mirrors how `.chatgpt-score-crosscheck--agree` swaps to `--success`).

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)
Strengths — the hedge discipline is the best part of this feature and directly serves the project core value ("the AI re-verifies"):
- Eyebrow `DeckAnalysis.cshtml:584`: *"Interaction and answers audit · DeckFlow automated first pass for the AI to re-verify"* — explicit, honest framing.
- Count line `:597`: *"Approximately N confident; verify against your list."* — uses "approximately/verify," never the banned "the deck has N."
- Empty/gaps states all hedged and attributed to DeckFlow (`:609`, `:631`).
- No generic labels (grep for Submit/OK/Cancel/etc. in the block: none). The one action button reads "Render Analysis Summary" (`:530`).

WARNING — the empty-bucket path double-states negativity (`:597`+`:609`, see Fix #2). Minor, but it reads like a bug and dilutes otherwise tight copy. This is the sole reason this pillar is not a 4.

### Pillar 2: Visuals (3/4)
- Sound semantic structure and a11y hooks: `role="region"` + `aria-label` on the container (`:583`), `<section aria-label>` per bucket (`:595`), `role="note"` on the gaps callout (`:618`). Hierarchy comes from accent-colored `<h4>` bucket labels over muted body — consistent with `.chatgpt-score`.
- WARNING — density (Fix #1). Unlike the score block (four fixed single-number cards), each interaction bucket is a variable-length list, so a uniform 5-up grid produces ragged, narrow columns. There is no dominant focal point; the block reads flatter than its siblings. This is inherent to the content but the 5-column choice amplifies it.
- Minor — stale responsive comment at `site-common.css:3209-3212` reads *"4 -> 2 columns … the score block stays compact"*: copied verbatim from the score block and inaccurate for the 5-column interaction grid it now also governs. Documentation-only, but misleading for the next editor.
- **Owed: live visual verify at 1280 + 390** — column balance, wrap behavior, and vertical rhythm cannot be judged from markup.

### Pillar 3: Color (3/4)
- Correct token discipline per the theme constraint: reuses `var(--muted)`, `var(--line)`, `var(--panel-soft-bg)`, `var(--accent-strong)`, `var(--warning)`; every literal hex is a `var(--token, #fallback)` fallback (`#5a6472`, `#c8a040`), not a standalone color. `grep -c ":root"` on `site-common.css` = 1 (unchanged) — **no new tokens, no per-theme fork required.** This is the right call, not a shortcut.
- WARNING — semantic mismatch: the gaps callout hardcodes the `--warning` left border unconditionally (`:3183`), so the clean "None flagged" branch still shows caution chrome (Fix #3).
- **Owed: cross-theme render verify.** Guild themes are standalone forks that `@import site.css`; whether `--accent-strong` / `--warning` / `--panel-soft-bg` resolve legibly on the interaction block across all forks (contrast of accent `<h4>` on soft panel, warning border visibility) genuinely needs pixels per theme. Cannot be a 4 on a pillar that renders differently across ~24 theme forks and has not been seen rendered.

### Pillar 4: Typography (4/4)
- Exactly three sizes in play: eyebrow `0.72rem` (`:3135`, matches `.chatgpt-score__eyebrow`), `--fs-xs` 0.74rem for muted count/empty/review (`:3165`), `--fs-sm` 0.82rem for the card list + `<h4>` (`:3157,3173`). Under the >4-sizes / >2-weights flag threshold.
- Weight differentiation is restrained: `600` on the gaps label only; hierarchy carried by size + color, not weight sprawl.
- Nit — the eyebrow uses a literal `0.72rem` rather than a `--fs-*` token, but this deliberately matches the sibling `.chatgpt-score__eyebrow`, so consistency wins. No deduction.

### Pillar 5: Spacing (3/4)
- Consistent rem ladder (0.4 / 0.45 / 0.55 / 0.65 / 0.85 / 1 / 1.25rem) and a `min-width: 0` on the bucket (`:3152`) — a real, correct guard that lets grid columns shrink instead of overflowing. Good detail.
- Minor — mixed units: `.interaction-audit-gaps` uses `padding: 16px` (`:3186`) while buckets use `padding: 0.85rem`. It faithfully mirrors `.chatgpt-score-crosscheck` / `.bracket-callout` (both `padding: 16px`), so it is an intentional sibling match, but the block is internally inconsistent px-vs-rem.
- WARNING — no arbitrary/off-scale values found, but the actual spacing rhythm (gap between 5 dense columns at desktop, stack spacing on mobile 2-up) is a pixel judgment. **Owed: live rhythm verify at 1280 + 390.**

### Pillar 6: Experience Design (4/4)
- State coverage is complete for a synchronous server-rendered readout: populated bucket (`:600-605`), empty bucket (`:609`), review sub-tier only when present (`:611-614`), gaps present (`:622-627`), and no-gaps (`:631`). No loading/error affordances are needed here — this is part of the already-error-handled Step-3 render, not an async widget.
- Untrusted-input robustness is strong (per plan 79-03): `InteractionAuditJson` round-trips through a size-capped (16 KB), deeply structurally-validated deserialize (`TryDeserializeInteractionAudit` / `IsStructurallyValidInteractionAudit`) that rejects null buckets, null inner lists, blank names, and out-of-range quantities, returning null rather than throwing (threat T-79-03-01). XSS-safe: card names/gaps use Razor `@` auto-encoding, no `Html.Raw` in the block (grep confirmed).
- Flag-gated OFF with proven page + artifact + zip byte-identity (excision-equality render test + zip entry-map test), so there is zero production exposure until an operator flips it on.
- The empty-bucket copy redundancy (Fix #2) is the one experience wrinkle, but it is scored under Copywriting to avoid double-counting.

---

## Live-Render Verification Owed (flag ON, before ship)
The flag is OFF, so the following require standing up the headless server (`scripts/run-web-test.sh`, `DECKFLOW_DISABLE_AUTO_BROWSER=true`) with `analysis.interaction-audit` ON and driving Playwright:
1. **Desktop 1280** — 5-column grid balance, card-name wrapping, uneven column heights (Fix #1 severity confirmation).
2. **Mobile 390** — 2-up grid legibility, no horizontal overflow, gaps callout full-width.
3. **Cross-theme color** — accent `<h4>` contrast on `--panel-soft-bg` and `--warning` border visibility across the standalone guild theme forks (Classic + at least one dark fork e.g. Nyx + one light fork e.g. Azorius).
4. **Populated + empty extremes** — a bucket with 8+ confident cards next to a 0-card bucket in the same row, and the "None flagged" gaps state, to confirm the warning-chrome false-alarm (Fix #3) and empty-copy redundancy (Fix #2) in situ.

*(Note: the existing `e2e/deck-analysis-render.spec.ts` already asserts readout visible@1280/390 ON and absent OFF — a selector-visibility smoke, not a density/contrast judgment. The above still needs eyes-on.)*

Registry audit: skipped — no `components.json` / shadcn (ASP.NET Razor project).

---

## Files Audited
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml:516-635` (conditional hidden field + flag-guarded readout region)
- `DeckFlow.Web/wwwroot/css/site-common.css:3125-3248` (`.interaction-audit*` classes + responsive block)
- Sibling patterns for consistency: `.chatgpt-score*` (`:3013-3123`), `.bracket-callout` (`:1858-1874`), `.manabase-lens*` (`:2608-2794`)
- `.planning/phases/79-interaction-answers-audit/79-03-PLAN.md`, `79-03-SUMMARY.md`, `79-02-SUMMARY.md`
