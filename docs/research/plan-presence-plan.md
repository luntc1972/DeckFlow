# Plan: Manabase "plan presence %" opener stat

**Date scoped:** 2026-07-07 · **Status:** SCOPED, decisions locked, NOT built.
**Sequencing:** slot into the manabase-efficacy backlog (M4-M12/L1-L14 line) — NOT a new cycle. Branch + formal GSD plan + Codex plan-review + execute *when picked up*.
**Research basis:** [`plan-presence-research.md`](./plan-presence-research.md).

## What it is

New OPENING HAND box statistic: **share of keepable opening hands holding a win-directed card castable on curve**. Research-backed "role coverage" — the axis Karsten keepable-% and on-curve castability do NOT measure (those are resources/curve). DeckFlow's category-KB + Commander Spellbook knowledge is the moat that generalizes what open-source tools hard-coded per deck.

## Decisions locked (user, 2026-07-07)

- **Ambition** = role-coverage (research-backed). NOT the cheap aggregate of the existing per-opener `HasPlan`, NOT relabel-only.
- **Plan roles** (broad set): Payoff/wincon + Engine (repeatable advantage) + Tutor/combo-piece + Interaction. Ramp / land / filler-draw NEVER qualify.
- **Role source**, first hit wins per spell: (1) CategoryKnowledgeStore category → role, (2) Commander Spellbook combo-piece → TutorCombo, (3) untagged → classifier text-heuristic fallback (payoff / draw-engine / tutor / removal patterns).
- **Noise control:** broad def = high-coverage end → raw % reads high ("94% have a plan"). Antidote = **surface WHICH role** (payoff vs interaction-only reads weaker) + strict calibrated bands + beta flag. Fallback lever if it reads as noise in calibration: tighten to payoff + combo-piece only.

## Architecture (keeps Core I/O-free)

Role decided in **Web**, passed **down** as pure data — same pattern as existing classification (Core receives classified data, never fetches).

```
Web: CategoryKnowledgeStore + CommanderSpellbookService
        │  PlanRoleClassifier maps each spell → PlanRole flags
        ▼
Core: SpellRequirement.PlanRoles field   ← pure data, additive, no I/O
        ▼
CastabilitySimulator.Simulate (mulligan loop): per keepable trial, flag openers
   holding ≥1 PlanRole≠None card ALSO castable by its on-curve turn  ← load-bearing gate
        ▼
ManabaseMulliganEvaluation.PlanPresencePercent + band  (+ OpeningHandSample role enrich)
        ▼
Manabase.cshtml: 2nd line in OPENING HAND box + role breakdown (beta-flagged)
```

On-curve gate is load-bearing: a plan card you cannot cast is not a plan. Reuses existing castability machinery.

## Phases

1. **Core model** — add `PlanRole` flags enum + `PlanRoles` field on `SpellRequirement` (additive `{ get; init; }`, default None — JSON-safe per `.editorconfig` carve-out). No behavior change. Tests: defaults, serialization unaffected.
2. **Web classifier** — new `PlanRoleClassifier` service (3-source precedence above), wired into `ManabaseAnalysisService` before the Core analyzer call. Tests: category→role mapping, combo-piece detection, heuristic fallback, precedence.
3. **Core counting** — in `CastabilitySimulator.Simulate` mulligan loop, count openers with ≥1 castable-on-curve plan-role card; aggregate → `PlanPresencePercent` + band on `ManabaseMulliganEvaluation`; enrich `OpeningHandSample` with role(s) present. KEEP existing `HasPlan` as-is (it means "workable line" = ≥2 lands + colors + on-curve castable — distinct from plan). Tests in `ManabaseMulliganEvaluationTests`: payoff-in-opener → high %, ramp-only → low %, uncastable plan card → does NOT count.
4. **Surface** — NEW flag `analysis.manabase.plan-presence` (OFF default; not reusing mulligan-eval flag), box 2nd line + role breakdown, `.txt` artifact line (paste-ready = Core Value), README. Themes + mobile + Playwright e2e (per web-page-change rule).
5. **Calibrate + ship** — re-baseline calibration decks (broad def reads high → tune band thresholds against known-good vs known-bad decks), Codex review, ship behind flag, operator flips flag.

## Side-effects (cross-cutting)

- **Direct:** `ManabaseModels.cs` (SpellRequirement, ManabaseMulliganEvaluation, OpeningHandSample), `CastabilitySimulator.cs`, `ManabaseAnalyzer.cs`, new `PlanRoleClassifier` (Web), `ManabaseAnalysisService.cs`, `Manabase.cshtml`, `ManabaseDisplay`/`ManabaseViewModel`, `ManabaseReportTextBuilder`, `FeatureFlagStore.cs` (1 idempotent seed row), README.
- **New dependency:** manabase path now reads CategoryKnowledgeStore (Web→Web; memory cache already singleton).
- **Contract:** SpellRequirement + ManabaseMulliganEvaluation gain additive fields — no break, no new packages.
- **Backcompat risk:** calibration numbers shift (like the commander-first-draw change did) — beta flag contains blast radius.

## File anchors (as of 2026-07-07)

- `DeckFlow.Core/Manabase/ManabaseModels.cs` — SpellRequirement; ManabaseMulliganEvaluation:1148; OpeningHandSample:217; **HasPlan:253-258** ("≥2 lands + colors + on-curve castable" = workable-line, NOT plan).
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` :31 class, :253 trial loop, :1319 LondonMulligan.
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` :887-950 ComputeMulliganEvaluation.
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — flag keys ~186-199 (MulliganEvalFlagKey:199).
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` :232 seed rows.
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` :280-319 OPENING HAND box.
- Tests: `DeckFlow.Core.Tests/Manabase/ManabaseMulliganEvaluationTests.cs` (most relevant).

## Open decisions (defaults applied unless changed)

- New flag `analysis.manabase.plan-presence` (not reuse mulligan-eval).
- Keep `HasPlan` name (distinct meaning).
- Report role breakdown, not a bare single %.

## Workflow reminders

DeckFlow rule: Claude codes + plans, Codex reviews. Branch before code (memory: never edit on main / another session's branch). Web-page change → tests + themes + mobile. README updated in same change.
