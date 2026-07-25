---
slug: reconcile-ramp-draw
created: 2026-07-24
status: in-progress
---

# Reconcile ramp & draw classification + docs sweep

## Goal

Unify the **draw** classification signal across Cut Lab / analysis / Manabase
onto one shared regex, **document** why the **ramp** classifiers intentionally
stay divergent, and refresh user-facing docs for every Cut Lab change shipped
since release 2026.07.9.

Hard constraint: **Manabase results must not change** (land verdict AND
plan-presence lens) — proven by the full test suite, not asserted.

## Part A — Draw reconcile (CODE — Codex)

Today two draw definitions exist:
- `DeckStatClassifier.IsDrawCard(string)` — naive substring: `draw a card` /
  `draw two cards` / `draw X cards` / `investigate` / `connive`. Consumed by
  Cut Lab draw role, `DeckStatAggregator` (analysis `deck_stats`/multi-axis via
  `IsRampOrDrawUnderThreeMv`), and `PlanRoleClassifier.FromHeuristic` (Engine).
- `ManabaseClassifier.YouCardDrawRegex` / `IsYouCardDraw(CardFact)` — robust,
  you-anchored, excludes opponent/trigger/replacement draws, matches any draw
  count. Consumed by Manabase draw budget + `rampCreditV2` land credit.

### Changes

1. **Move** the compiled `YouCardDrawRegex` from `ManabaseClassifier` into
   `DeckStatClassifier` (Core/Analysis), verbatim (same pattern, same options).
   Expose `internal static bool MatchesYouCardDraw(string oracle)` = regex-only.
2. `ManabaseClassifier.IsYouCardDraw(card)` → delegates to
   `DeckStatClassifier.MatchesYouCardDraw(card.OracleText ?? "")`. Regex is
   identical → **Manabase land verdict byte-identical**.
3. `DeckStatClassifier.IsDrawCard(string oracle)` becomes the robust **union**:
   `MatchesYouCardDraw(oracle) || contains "investigate" || contains "connive"`.
   (Robust literal-draw + retain clue/connive card-advantage.)
4. `IsRampOrDrawUnderThreeMv`, `DeckStatAggregator`, `CutLabRoleAssigner`,
   `PlanRoleClassifier` inherit the union automatically — no edits there.

### Manabase insulation (verify-then-pin)

`PlanRoleClassifier:199` (`IsDrawCard`) feeds `ManabaseAnalysisService`
plan-presence AND Cut Lab engines. The union only *adds* accuracy (big-N draw,
opponent-exclusion). Procedure:
- Run full suite. If **any** Manabase plan-presence test/golden drifts, pin
  `PlanRoleClassifier`'s draw test to today's behavior (keep a conservative
  predicate for that one call site) so plan-presence stays byte-identical.
- If nothing drifts, no pin needed. Either way Manabase output is unchanged.

### Tests (Codex)

- `DeckStatClassifierTests`: `draw three cards` → true (was false);
  `target player draws two cards` (opponent) → false (was true);
  `investigate` / `connive` → still true; `you draw a card` → true.
- `ManabaseClassifier` draw-budget tests: confirm byte-identical (regex
  unchanged); add a guard that investigate/connive do NOT enter the Manabase
  draw budget (insulation regression).
- Preserve every touched file's existing line endings (per-file LF/CRLF).

## Part B — Ramp divergence ADR (DOCS — Claude)

`docs/decisions/NNNN-ramp-classifier-divergence.md` (standard ADR): DeckStat's
broad `IsRampCard` (role tally) vs Manabase's tuned, flag-gated ramp predicates
(`IsRampPieceForBudget` / `IsRepeatableRampOrDraw` / `IsRockOrDork`, castability
math) **intentionally differ — do not unify.** Also record the parallel
draw split: investigate/connive is a role-tally concept, NOT Karsten literal
draw (why Manabase excludes it). One-line `// Why:` pointer comment at
`DeckStatClassifier.IsRampCard` and `ManabaseClassifier` ramp sites → the ADR.

## Part C — Docs sweep since 2026.07.9 (DOCS — Claude)

Surfaces: `README.md` (L135 Cut Lab bullet + new CHANGELOG entry),
`DeckFlow.Web/Help/cut-lab.md`, `DeckFlow.Web/Views/Deck/CutLab.cshtml`.
Cover behavior shipped since 2026.07.9:

| Commit | User-facing change | Doc action |
|--------|--------------------|-----------|
| 68988e12 / 9b3f1783 | Card popup (oracle + Lock/Unlock; DFC oracle) | verify/extend (e3335e7f partial) |
| 68988e12 | Portable sessions (export/import) | verify/extend |
| 68988e12 | Lock-state sync fix | verify |
| 153d76a4 | **Group pool by type + search by subtype** (NEW) | ADD to help + view + README |
| fcc0a1bd / 0743df14 | Ramp = mana-symbol producers; engines = repeatable permanents only | help: how roles are classified |
| 1d838dea | Baseline/goals refresh on **Recalculate**, not per cut | help clarification |
| this task | Draw role = robust literal-draw + clue/connive | help: draw role wording |

## Verification (Claude)

- `dotnet build` clean; full suite green (Core/Web/Studio).
- Recapture any drifted Analysis/Comparison goldens from a real run (same
  procedure as 8ef63337). Confirm no Manabase-test drift.
- EOL check on every touched file (diff --stat vs --ignore-all-space).
- Update STATE.md "Quick Tasks Completed"; commit per logical change.

## Out of scope

- No change to Manabase ramp/draw math or castability output.
- No unification of the ramp predicates (documented instead).
