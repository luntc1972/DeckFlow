---
phase: 67-content-kb-value-a-b-validation
milestone: Cycle 11 — Security, Visibility Control & Creator-Lens
type: decision-record
created: 2026-06-25
requirements: [KBVAL-01, KBVAL-02]
gates: [Phase 68 / CREATOR-01, content.kb.enabled flip]
verdict: MARGINAL → NEGATIVE — gate NOT cleared
---

# Phase 67 — Content KB Value A/B Validation — DECISION

## Outcome

**KBVAL-01 (A/B harness):** MET — already built. `DeckFlow.Web.Tests/Spike001KbValueAbHarness.cs`
emits the deck-analysis prompt twice for a real deck (expert-context clips ON vs OFF) via
`DeckAnalysisPacketService.BuildAsync`. Promoted from throwaway spike 001 to a kept re-validation
gate. The two prompts differ only by the `## Expert Context` block.

**KBVAL-02 (judged decision recorded):** MET — verdict is **MARGINAL → leaning NEGATIVE; the gate is
NOT cleared.** Recorded here and in `.planning/spikes/001-kb-value-ab/{README.md,VERDICT.md}`.

**Decision (operator, 2026-06-25): close Phase 67 on the spike verdict.** No new build, no re-wiring
of the retired injection. Consequences below are now in force.

## Consequences (the gates this decision controls)

1. **`content.kb.enabled` stays OFF in production.** No flip this cycle. Expert-context injection
   remains retired (removed in v1.6 Phase 37, RET-01..06); the clips are not in the live prompt path.
2. **Phase 68 (CREATOR-01) DROPS.** CREATOR-01 is explicitly "conditional on KBVAL-02 showing clear
   lift (phase drops if marginal)." The verdict is marginal, so the creator-philosophy research/design
   phase does not run this cycle. Cycle 11 therefore ships phases 64, 65, 66, 69 (68 dropped).
3. **The harness is retained** as the re-validation gate. If KB value is revisited in a future
   milestone, re-run `Spike001KbValueAbHarness` after a retrieval fix and re-judge against the rubric
   in the spike README before reconsidering either gate.

## Evidence (spike 001, 2026-06-10)

Real ~99-card Atraxa "Praetors' Voice" deck (proliferate / +1/+1 counters / superfriends, Bracket 3),
real Scryfall oracle text for all 91 unique cards. `BuildAsync` run with the Expert Context block vs
without. Two runs:

- **Run 1 (hand-picked clips):** with-context answer was substantively the SAME analysis as baseline.
  The one weakness the clips reinforced ("too many directions / unfocused") was already the baseline's
  #1 unprompted finding; clips only added creator attribution. One clip (glass-cannon → build
  redundancy) slightly MISFIT this grindy control pile. Rubric lift: Specificity 1, Creator-voice 3,
  Novel-signal 1, Actionability 2 — marginal, with minor quality-loss risk.
- **Run 2 (GOLD — real `ContentKbRelevanceService` retrieval, flag forced on, prod corpus rebuilt
  locally):** the real scorer was WORSE. It selected 5 clips ALL from a single tangential video
  ("The Problem with Glass Cannon Commanders", score 5.06 each) — 3 about unrelated commanders
  (Kaalia, Animar), 1 mentioning Atraxa only in passing, 1 pushing a misfit glass-cannon frame — and
  IGNORED the genuinely on-point snail videos in the corpus ("You Might Have Too Much Ramp", "5 Most
  Common Deckbuilding Mistakes", "How to Play More Removal").

## Why marginal — and the bounded scope of this verdict

The low lift is driven by **clip genericity + broken retrieval**, not proof that "expert content is
worthless":

- The injected clips are top-of-funnel deckbuilding-101 maxims (focus your deck, protect your
  threats) that a capable model (ChatGPT/Claude/Gemini) already produces unprompted.
- The real `ContentKbRelevanceService` retrieval is defective (selects tangential/off-commander
  passages, ignores on-point ones). The A/B therefore measured "do these generic clips help," not
  "does good retrieval of creator-distinctive passages help."

So this verdict argues:
- **Against** shipping the current generic-clip retrieval as-is (low lift vs. maintenance cost) →
  keep `content.kb.enabled` OFF.
- It does **not** by itself green-light OR permanently kill a future philosophy-profile redesign; it
  says any such redesign must FIRST prove deck-specific, creator-distinctive conditioning and a fixed
  retrieval scorer before it earns a build. That hypothesis is unproven, not validated here — hence
  Phase 68 drops rather than proceeds.

## Known caveats on rigor (carried forward, not blocking the close)

- The spike judge was Claude (Opus 4.8) acting as the target AI, **NOT blind** (saw both prompts);
  no independent real-ChatGPT paste was run. "Blind where feasible" (KBVAL-02) was only partially met.
- Run-1 clips were hardcoded, not retrieved by the live service; Run-2 fixed that but exposed the
  retrieval defect rather than a fair KB.
- These caveats lower confidence in a STRONG positive but do not threaten the NEGATIVE/marginal close:
  both a not-blind self-judge AND the real scorer landed at "no signal the baseline lacked," and the
  real scorer was actively counterproductive. If KB value is revisited, redo the test BLIND on real
  ChatGPT after fixing retrieval.

## Status changes applied with this decision

- REQUIREMENTS: KBVAL-01 ✅ Met (harness), KBVAL-02 ✅ Met (verdict recorded); CREATOR-01 ⊘ Dropped
  (conditional gate not cleared).
- ROADMAP: Phase 67 ✅ Complete (decision); Phase 68 ⊘ Dropped; next active = Phase 69 (Studio UI).
- Production: `content.kb.enabled` unchanged (OFF).
