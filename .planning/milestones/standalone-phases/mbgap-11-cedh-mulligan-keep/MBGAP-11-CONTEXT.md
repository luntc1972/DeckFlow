# Phase MBGAP-11: cEDH Opening-Hand Keep Heuristic - Context

**Gathered:** 2026-07-14
**Status:** Ready for planning
**Research:** `.planning/manabase-cedh-mulligan-research.md` (full current-code trace with file:line, cEDH theory, redesign §5) — REQUIRED READING for the planner.

<domain>
## Phase Boundary

The Opening Hand panel judges cEDH keeps with a casual-Commander lens. Live defect (user
report, Winota/cEDH deck): a keep-7 was labeled "workable" because a 6-MV payoff
(Auratouched Mage) is castable on curve at **turn 6** — meaningless in cEDH where the
median game ends ~turn 5. Root cause is architectural, not a threshold: one Karsten
mana-functionality keep heuristic (`CastabilitySimulator.LondonMulligan`,
`CastabilitySimulator.cs:2134`) serves two formats whose keep doctrines differ in KIND.
The keep math has **zero cEDH branches today** (verified: `grep -i cedh` on the simulator +
analyzer = no hits). The representative "workable line" is just the cheapest non-commander
demanding spell (`ManabaseAnalyzer.cs:1509-1533`) with no turn cap and no plan-quality
filter; the commander is deliberately excluded from the opener pool (:1516 comment) —
backwards for commander-central cEDH decks.

This phase delivers:

1. **cEDH three-shape keep gate** layered on top of the existing Karsten mana floor
   (floor stays — theory confirms it necessary-but-insufficient). A mana-keepable hand is
   cEDH-keepable iff ≥1 shape holds:
   - **Shape A — Explosive start:** commander OR a Payoff/TutorCombo plan card deployable
     by turn ≤3, counting in-hand acceleration (rocks/dorks/rituals the sim already
     classifies). Commander-central decks: commander deployable ≥1 turn ahead of printed
     curve is the premium signal.
   - **Shape B — Early engine:** an Engine-role card castable turn ≤2.
   - **Shape C — Interaction bridge:** ≥2 Interaction-role cards in hand PLUS continued
     development (land/rock drops).
   Mana-keepable-but-no-shape hands get a distinct decision label ("mana-functional, no
   plan — real table mulls this") and do NOT count toward plan-keepable %.

2. **Two headline numbers in cEDH** (D-01): `mana-keepable %` (today's Karsten metric,
   preserved) AND `plan-keepable %` (new — share passing a keep shape). Casual mode
   headline unchanged.

3. **Representative opener-line rewrite:** never surface a payoff with on-curve turn ≥5 as
   a workable line (turn cap, calibrated — default 4). Commander joins the opener pool in
   cEDH and is preferred as the representative line **when the deck is commander-central**
   (D-02, auto-detected). Shape-labeled copy: "Winota deployable turn 3 — one ahead of
   curve (explosive keep)" / "Mystic Remora turn 1 (engine keep)" / "2 interaction pieces +
   land drops (bridge keep)" / "no plan by turn 4 — mulligan."

4. **Casual-mode curve-coverage metric** (D-03, in-scope this phase): per-hand share of
   turns 1–5 with ≥1 castable play from hand given simulated draws — "plays a spell on ~4
   of first 5 turns." Becomes the casual "workable line" frame. Cheap: sim already walks
   turn-by-turn castability per trial.

5. **Calibration + flag:** shape turn-caps and bridge thresholds as `CedhCalibration`
   constants with pin tests (existing pattern). New feature flag; because the change
   mutates prompt-artifact text (`ManabaseReportTextBuilder.cs:293-334`), the flag MUST
   join `PromptMutatingAnalysisFlags` (packet-cache replay lesson —
   `followup_packet_cache_flag_replay`).

Out of scope: changing casual keep *thresholds* (only the casual framing metric is added);
free-interaction card database beyond existing alt-cost modeling; mid-game/opponent
modeling; verdict/health math changes.

</domain>

<decisions>
## Locked Decisions (from user, 2026-07-14)

- **D-01 — Two headline %s in cEDH.** Show both `mana-keepable %` and `plan-keepable %`.
  Do NOT collapse to one redefined number. Preserves the mana-vs-plan distinction.
- **D-02 — Auto-detect commander centrality.** Commander is surfaced as the keep signal
  only when the deck is commander-central. A centrality heuristic is required — candidate
  inputs: command-zone castability (already simulated, the "casts on curve 88% turn 4"
  callout), commander PlanRole from the classifier, plan-presence data. Planner must
  specify the heuristic concretely and make it testable. Non-central cEDH decks: commander
  eligible in pool but not forced as representative.
- **D-03 — Both cEDH gate AND casual curve-coverage metric this phase.** One phase, both
  deliverables. Accept the larger diff in the analyzer + e2e churn.

## Design Fork to Resolve in Planning (technical — planner/Codex call, document it)

- **F-01 — Role data location.** Roles are classified in the web-layer
  `PlanRoleClassifier` (`DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs`); keep trials
  run in Core `CastabilitySimulator`. Shape gates need role knowledge INSIDE the mulligan
  trials, but role data currently reaches Core only via the separate `SimulatePlanPresence`
  pass (roles already flow into Core there — precedent exists for the boundary crossing).
  Planner must choose ONE: (a) extend `SimulatePlanPresence` to emit per-hand plan-keep
  verdicts and shapes (keeps role logic in its current pass, keep gate reads its output),
  or (b) thread the role lookup into the mulligan trials directly. Prefer (a) if it avoids
  duplicating the trial loop; justify the choice. Respect the Core/Web boundary — no new
  web dependency in Core; pass role data in as the plan-presence pass already does.
</decisions>

<constraints>
## Constraints

- **Delegation:** Codex implements; Claude plans + reviews (CLAUDE.md). Route PLAN.md
  through Codex plan-review (gpt-5.5 medium) before execution. Codex prompts carry the
  per-file line-ending preservation instruction; Claude verifies EOL post-dispatch.
- **Hottest code:** `CastabilitySimulator` + `ManabaseAnalyzer` are the most-tested files
  in the analyzer. Changes need parity/pin tests; existing `CedhCalibration` test pattern
  is the model.
- **Flag gating:** new flag seeded OFF, flipped after UAT. Must be in
  `PromptMutatingAnalysisFlags`. cEDH-interaction-lens flag interplay noted in research
  §2.5.
- **UI + prompt parity:** copy changes hit both `Manabase.cshtml:628-694` AND
  `ManabaseReportTextBuilder.cs:293-334`. Web-page-change rule: xUnit + Playwright, desktop
  + mobile across themes. Reuse the just-landed e2e flag-restore hardening
  (`f8f58586`) — the Opening Hand panel e2e specs (LOW-8/9) will churn.
- **Testing:** VSTest unreliable in WSL — use `dotnet build` clean + push-and-watch CI or
  targeted harness; UI via `scripts/run-web-test.sh` + Playwright headless, never a Windows
  browser window.
</constraints>

<acceptance>
## Acceptance Criteria

1. A cEDH hand whose only payoff is castable turn ≥5 is NOT labeled workable — it reads as
   a mulligan / "no plan by turn 4."
2. cEDH panel shows two headline %s: mana-keepable and plan-keepable, plan-keepable ≤
   mana-keepable by construction.
3. For a commander-central cEDH deck (Winota fixture), the commander appears as a
   representative opener line when deployable ahead of curve; for a non-central deck it does
   not get force-surfaced.
4. Casual mode shows a curve-coverage line ("plays a spell on ~N of first 5 turns").
5. Shape gate correctly credits in-hand acceleration (a hand with rocks that deploys a
   payoff by turn 3 counts as Shape A even if the payoff's printed MV lands it later on a
   land-only curve).
6. `CedhCalibration` constants + pin tests cover turn caps and bridge thresholds.
7. New flag in `PromptMutatingAnalysisFlags`; prompt artifact text reflects the new read
   when flag ON; unchanged when OFF.
8. `dotnet build` clean; Core + Web suites green; Playwright e2e green across 3 themes ×
   2 viewports; EOL clean.
</acceptance>
