# cEDH Opening-Hand Keep Heuristic — Research & Redesign Proposal

Date: 2026-07-14 · Status: research only, no code changes · Trigger: user report on live Winota (cEDH) analysis showing "keep 7 — Auratouched Mage castable on curve (turn 6) — workable line"

## 1. Problem statement

The Opening Hand panel judges cEDH keeps with a casual-Commander lens. Observed defects (user report, screenshot of Winota/cEDH analysis):

1. A keep-7 was labeled "workable" because a slow payoff (Auratouched Mage, 6 MV) is *castable on curve at turn 6*. In cEDH the median game is decided around turn 5 — a turn-6 first payoff is a mulligan, not a workable line.
2. For commander-central decks (Winota is the canonical example), the strongest keep signal is the **commander deployable 1–2 turns ahead of printed curve** via fast mana — the tool currently excludes the commander from opener reasoning entirely.
3. "Castable by turn N on curve" is the wrong frame for cEDH generally. "Can you cast something every / almost every turn" is a **casual** keep criterion; cEDH keep logic is closer to binary: explosive start, early engine, or interaction bridge — otherwise mull.
4. A cEDH hand that does nothing until turn 3–4 is only keepable when stax/interaction density bridges it to that turn.

## 2. Current implementation (verified against main @ 3a684be2)

### 2.1 Keep/mull rule — mode-agnostic

`DeckFlow.Core/Manabase/CastabilitySimulator.cs:2134` `LondonMulligan(...)`:

- Schedule `(Keep, Bottom, Lo, Hi, RampGate)`:
  - keep 7: lands in `[2, hiCap]`; 2-land keeps additionally require ≥1 ramp piece (`RampGate`, :2183).
  - `hiCap = avgMv >= 3.0 ? 5 : 3` (:2140).
  - mull to 6: lands in `[2, hiCap]`, no ramp gate.
  - mull to 5: forced keep, lands `[1, 4]` (:2192, :2205).
- Color gate (MQ-05): distinct opening-land colors ≥ `min(deckColorCount, lands, ColorKeepCap=2)` (:2296–2298, const :43).
- **`grep -i cedh` over `CastabilitySimulator.cs` + `ManabaseAnalyzer.cs`: zero hits.** Keep thresholds are identical for casual and cEDH. The only cEDH-gated behavior anywhere in the pipeline is role classification (counterspells count as Interaction in cEDH — `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs:106,129,196–208`).

### 2.2 "Workable line" example card — cheapest spell wins

`DeckFlow.Core/Manabase/ManabaseAnalyzer.cs:1509–1533`:

- Candidate pool = tracked castability rows, `ManaValue >= 1`, **commander rows excluded** (`nonCommanderRows`, comment :1516–1517: "a commander is rarely the early on-curve play the read is meant to surface").
- Ordered by `ManaValue` ascending, then `OnCurveTurn` ascending; first sample per distinct keep decision surfaces (max 3).
- So the example is just the **lowest-MV demanding spell** — no judgment of whether that spell constitutes a plan. Auratouched Mage surfaced by accident of being the sampled row, and "castable on curve (turn 6)" was presented as a positive signal.
- Per-spell `HasPlan` (:405) = `stashedLands >= 2 && stashedColors >= planColorTarget && onCurveCastable` — "plan" here means only *mana* supports the sampled spell on its printed curve.
- When `analysis.manabase.plan-presence` is on, `planPresence.RepresentativeOpeners` (prefers a plan-card-castable hand) is used instead (:1524–1525) — better, but still curve-relative and still commander-blind.

### 2.3 Payoff-on-curve / role percentages

`SimulatePlanPresence` (`CastabilitySimulator.cs` ~:485–703): per keepable trial, hand roles OR'd from plan-tagged cards (`PlanRoles`, :633), tallied to `rolePercents` (:688–689); `PayoffPercent = rolePercents[Payoff]` (:694); band thresholds :783–789. Role precedence: crowd categories → Commander Spellbook combo piece → oracle heuristic (`PlanRoleClassifier.cs:35–230`); permanent gate strips Payoff/Interaction from one-shot instants/sorceries (:72–92).

### 2.4 Commander

`source.IsCommander` (`CastabilitySimulator.cs:968`) only removes the commander from the drawable library. Command-zone castability is simulated separately (the "casts on curve 88% at turn 4" callout) but **feeds neither the keep decision nor the opener examples**.

### 2.5 Surfaces & flags

- UI: `DeckFlow.Web/Views/Deck/Manabase.cshtml:628–694`, gated by `ShowMulliganEval`; role line gated by `ShowPlanPresence`.
- Flags: `analysis.manabase.mulligan-eval` (:99–103 of `FeatureFlagCatalog.cs`), `analysis.manabase.plan-presence` (:104–111), `analysis.manabase.cedh-interaction-lens` (:112+). `color-aware-mulligan` retired.
- Prompt artifacts include mulligan text (`ManabaseReportTextBuilder.cs:293–334`) — any new flag that alters this text must join `PromptMutatingAnalysisFlags` (packet-cache replay lesson).

## 3. cEDH mulligan theory (web research, 2026-07-14)

### 3.1 Consensus doctrine

- **Mulligan is the default**; a hand needs a positive reason to keep (Sperling, "Can I Keep This? – How to Mulligan in cEDH", topdeck.gg). Ranked keep reasons: (T1) broken early plays — turn 1–2 Remora/Rhystic/Ad Nauseam/commander-engine online; (T2) mana development, explicitly fast mana over card draw ("mana is our most frequent bottleneck"); (T3) interaction as *support*; (T4) card advantage alone — weak; (T5) "avoiding a non-functional hand" — explicitly invalid ("losing big or losing slightly is the same thing").
- GrimDeck power-level split: casual (games end T10+) → keep on functional curve, "can I make a play turns 1–3"; cEDH (games end T3–5) → "mulligan to your combo or interaction — there's no time for value."
- Free interaction (Force of Will/Negation, Fierce Guardianship, Pact, Deflecting Swat) is the format's baseline defensive suite; deckbuilding sources treat **3+ free counterspells as a floor**. Contested: Sperling weights interaction below development as an independent keep reason; deckbuilding guides weight it higher. Both agree interaction-only hands with no path to a plan are weak.

### 3.2 Commander-centric keeps

- Blue Farm (Tymna/Kraum) primer: three keepable shapes — (a) T1–2 draw engine, (b) turbo hand (7+ mana toward early Ad Nauseam/Breach), (c) interaction + ramp (learncedh.com).
- Winota primer (EDHREC/ComedIan): mull to 5 or even 4 for hands with acceleration into Winota + protection (Mother/Giver of Runes) + stax. The commander deployed early and protected IS the keep criterion.
- K'rrik primer: keep = ramp to deploy commander T1–2 **plus** an outlet/tutor. "On curve" for these decks means the ritual-accelerated timeline, not printed MV vs land-per-turn.
- Common thread: **hand judged by whether the deck's specific engine (usually the commander) comes online turns ahead of fair curve, protected — not by generic curve coverage.**

### 3.3 Quantitative anchors

| Metric | cEDH | Casual/Karsten baseline |
|---|---|---|
| Lands | 28–31 | 35–38 |
| Fast mana/ramp | 8–14 | far fewer |
| Interaction | 12–18 total, ≥3 free counterspells | fewer |
| Avg MV | 1.3–2.0 | higher |
| Expected win turn | mean ~5.6, median 5 (cedhstats; moderate confidence) — "decided T3–5" in mulligan writing | T10+ |

- ScrollVault 3.75M-game sim: 30 lands + 12 rocks ≈ consistency of 37-land midrange; rocks substitute land drops (fragility: rock removal). Karsten hypergeometric math is the shared foundation (already the tool's basis).
- Simulators in the wild (eldrazi.gg mulligan trainer, Project Manabase, ScrollVault hand sim, ManaTap) score turn-by-turn castability; none found publishing a cEDH-specific keep rule — DeckFlow implementing one is differentiation, not catch-up.

### 3.4 Stax/interaction bridge

Sperling ("Fighting With or Through Stax"): slow hands are keepable only when they can **accumulate resources while waiting** — land/rock drops continue, tutors set up, and stax/interaction denies faster decks the window. EDHREC stax guide: dense interaction suite is the explosive-start substitute. Implication: late-payoff hands need an explicit interaction/stax-density check before being called workable.

## 4. Gap analysis — user's four points vs code

| # | User point | Current behavior | Verdict |
|---|---|---|---|
| 1 | Never keep on slow payoff castable T6 | Cheapest demanding row surfaced with its on-curve turn, unbounded; T6 presented as "workable line" | Confirmed defect — `ManabaseAnalyzer.cs:1509–1533` has no turn cap, no plan-quality filter, in any mode |
| 2 | Commander early = keep signal for commander-central decks | Commander explicitly excluded from opener pool; keep math ignores command zone | Confirmed — exclusion comment (:1516) encodes the casual assumption; cEDH inverts it |
| 3 | "Castable by T6" meaningless in cEDH; "cast something every turn" suits casual | Single curve-relative frame for both modes | Confirmed — theory (§3.1) says the two modes need different keep *logics*, not different thresholds of one logic |
| 4 | T3–4 do-nothing hand bad unless stax/interaction bridges | Keep rule is lands+ramp+color only; roles computed but never gate the keep | Confirmed — role data exists (`SimulatePlanPresence`) but is display-only |

Root cause: one keep heuristic (Karsten-style mana-functionality) serving two formats whose keep doctrines differ in kind. The mana-functionality floor is right for both; cEDH needs a **plan-quality layer on top** and casual needs a **curve-coverage frame**, and the pipeline already computes most inputs (roles, fast-mana classification, command-zone castability).

## 5. Redesign proposal

### 5.1 cEDH mode: three-shape keep gate (replaces "workable line by cheapest spell")

Layer on top of the existing land/color/ramp floor. A keepable-by-mana hand is **cEDH-keepable** iff at least one shape holds:

- **Shape A — Explosive start:** commander or a Payoff/TutorCombo plan card deployable by turn ≤3, counting in-hand acceleration (rocks/dorks/rituals the sim already classifies). For commander-central decks: commander deployable ≥1 turn ahead of printed curve is the premium signal (user point 2; Winota/K'rrik doctrine §3.2).
- **Shape B — Early engine:** Engine-role card (Remora/Rhystic class) castable turn ≤2.
- **Shape C — Interaction bridge:** ≥2 Interaction-role cards in hand (weighting free interaction ≥1 when detectable) **plus** continued development (land drops / rocks) — legitimizes slow hands per §3.4 (user point 4).

Hands passing the mana floor but no shape → new decision label, e.g. "mana-functional, no plan — real table mulls this," and they stop counting toward the headline keepable %. Two headline numbers possible: mana-keepable % (today's) and plan-keepable % (new, cEDH only) — plan-phase decision.

### 5.2 Representative opener lines, cEDH copy

- Never surface a payoff with on-curve turn ≥5 as a workable line. Cap the representative-line turn at the deck's expected-win horizon (default 4; already have cEDH calibration machinery).
- Line templates by shape: "Winota deployable turn 3 — one ahead of curve (explosive keep)" / "Mystic Remora turn 1 (engine keep)" / "2 interaction pieces + land drops (bridge keep)" / "no plan by turn 4 — mulligan."
- Commander joins the opener pool in cEDH (reverse `nonCommanderRows` exclusion behind mode/flag) and is preferred as the representative when commander-central. Deck "commander-centrality" heuristic needed — candidate inputs: command-zone castability already simulated, commander role from classifier, plan-presence data. Open question for plan phase.

### 5.3 Casual mode: curve-coverage metric (user's "cast something every turn")

- New per-hand metric: share of turns 1–5 (or 2–5) with ≥1 castable spell from hand given the simulated draws — "plays a spell on ~4 of first 5 turns." This becomes the casual "workable line" frame, replacing the single-spell on-curve sample as the headline (single-spell sample can remain as detail).
- Cheap to compute: the sim already walks turn-by-turn castability per trial.

### 5.4 Threshold calibration (from §3.3)

- cEDH explosive window: payoff/commander by T3 (median win turn 5 ⇒ acting later than T3–4 is losing). Make the cap a `CedhCalibration` constant with tests, like existing calibration work.
- Bridge: ≥2 interaction pieces; consider free-interaction detection (MV 0 alternate costs) as a refinement — sim already models alt costs (`alt-cost overrides` feature).
- Keep Karsten mana floor unchanged in both modes — theory confirms it as necessary-but-insufficient.

### 5.5 Touch points & risks

| Area | Change | Risk |
|---|---|---|
| `CastabilitySimulator` | Hand-shape evaluation needs per-hand role + acceleration knowledge at keep time; today roles live in the separate `SimulatePlanPresence` pass. Either extend that pass to emit plan-keep verdicts, or thread role lookup into the mulligan trials. Perf: role data is web-layer (`PlanRoleClassifier`) — Core/Web boundary must be respected (roles already flow into Core for plan presence, so precedent exists). | Medium — hottest, most-tested code in the analyzer |
| `ManabaseAnalyzer:1509–1533` | Opener selection rewrite per §5.2 | Low |
| `Manabase.cshtml` + `ManabaseReportTextBuilder` | New copy, both UI and prompt artifact. **Prompt text changes ⇒ flag must join `PromptMutatingAnalysisFlags`.** | Low, known pattern |
| Flags | New `analysis.manabase.cedh-keep` (or fold into mulligan-eval v2); seed OFF, flip after UAT | Low |
| Tests | `CedhCalibration`-style pin tests for shape gates + turn caps; e2e copy assertions will churn (recent LOW-8/9 lens specs touch this panel) | Medium — e2e flag-restore hardening just landed, reuse it |

### 5.6 Explicitly out of scope for this doc

- Mid-game / post-mulligan play advice, opponent modeling.
- Free-interaction card database beyond what alt-cost modeling already gives.
- Changing casual keep thresholds (only the *framing* metric changes there).

## 6. Sources

- Sperling, "Can I Keep This? – How to Mulligan in cEDH" — https://topdeck.gg/articles/can-i-keep-this-how-to-mulligan-cedh
- Sperling, "Fighting With or Through Stax Pieces in cEDH" — https://topdeck.gg/articles/fighting-with-or-through-stax
- GrimDeck, "How to Mulligan in Commander" — https://grimdeck.com/blog/how-to-mulligan-commander
- Learn cEDH primers: Tymna/Kraum — https://learncedh.com/decklists/tymna-kraum ; K'rrik — https://learncedh.com/decklists/krrik
- ComedIan MTG, "Definitive cEDH Winota Deck Tech and Mulligan Guide" (EDHREC) — https://edhrec.com/articles/comedian-mtg-the-definitive-cedh-winota-deck-tech-and-mulligan-guide-everything-you-need-to-to-know-to-play
- ScrollVault, "Commander Land Count — 3.75M Games of Data" — https://scrollvault.net/guides/commander-land-count-data.html ; Karsten calculator — https://scrollvault.net/tools/manabase/
- EDHREC, "Guide to cEDH Stax" — https://edhrec.com/guides/edhrec-guide-to-cedh-stax ; "Solve the Equation – Maybe You Should Mulligan More" — https://edhrec.com/articles/solve-the-equation-maybe-you-should-mulligan-more
- mtgproxycards.com, "How To Make A cEDH Deck That Wins" — https://mtgproxycards.com/how-to-play-mtg/how-to-make-a-cedh-deck/
- cedhstats.org (win-turn figures: moderate confidence, from search snippet)
- Commander's Herald, "A Beginner's Guide to cEDH" — https://commandersherald.com/a-beginners-guide-to-cedh/

Confidence notes: land/interaction count ranges vary by source (directional, not canonical); cedhstats median-win-turn not re-verified from page content; Sperling vs deckbuilding guides disagree on interaction's weight as an independent keep reason — proposal hedges by making interaction a *bridge* shape requiring development, not a standalone keep.
