# Phase 76: Codex Review Fix Pass

**Date:** 2026-06-28
**Commit:** bce93c63
**Branch:** plan/cycle-13-deck-eval
**Reviewer:** Codex gpt-5.4 (medium)
**Findings applied:** 3 (HIGH: FIX A, MED: FIX B, MED: FIX C)

---

## FIX A — HIGH: Tutor messaging corrected (keep tutors as Game Changers)

**Finding:** The "tutors not counted" copy was misleading. Demonic Tutor, Vampiric Tutor,
Worldly Tutor etc. ARE on the official Game Changers list and correctly count as Game
Changers. The old copy implied tutors were excluded entirely from the rubric.

**Resolution:** Rewrote all 5 occurrences of "tutors not counted" copy:
- `Bracket.cshtml` hero paragraph (line ~31)
- `Bracket.cshtml` WHY THIS BRACKET footnote (line ~181)
- `Bracket.cshtml` How-brackets-are-determined section (line ~315)
- `ChatGptBracketPromptVariant.cs` AppendClassificationReasons
- `ClaudeBracketPromptVariant.cs` AppendClassificationReasons
- `GeminiBracketPromptVariant.cs` AppendClassificationReasons

New wording: "No separate tutor-count gate: the October 2025 update dropped the old
tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly
Tutor, etc.) remain on the official Game Changers list and still count as Game Changers."

---

## FIX B — MED: Extra-turn cards removed from floor violations

**Finding:** The FLOOR VIOLATIONS section included a `foreach` over `DetectedExtraTurnCards`
tagging them "Extra turns". Extra-turn cards are informational only and do not affect the
bracket number per the current WotC rubric. They must not appear as violations to cut.

**Resolution:**
- Removed the `foreach (var card in cl.DetectedExtraTurnCards)` block from Bracket.cshtml
  floor violations list. Extra turns remain in the WHY THIS BRACKET informational section.
- The 3 prompt variants' `AppendFloorViolations` methods already did not include extra turns;
  `FloorViolationSet` (FIX C) excludes them by design — no extra-turn field exists on the record.

---

## FIX C — MED: Tier-aware floor violations (B5→B4 count advisory; B4 uncapped)

**Finding:** The FLOOR VIOLATIONS list iterated ALL `DetectedGameChangers` whenever
`IsOverTarget` was true, regardless of the target tier's MaxGameChangers cap. This wrongly
listed every Game Changer as a violation when the target was B4 (uncapped, MaxGameChangers=-1).

**Resolution:** Added `FloorViolationSet FloorViolations(BracketTier targetTier)` method on
`BracketClassification` in `DeckFlow.Core.Bracket` (Core-only, no Web reference).

Rules:
- GCs are violations ONLY when target tier caps them (MaxGameChangers >= 0) AND count exceeds cap
- For uncapped targets (B4/B5): no per-GC violations
- Special case B5→B4 via heuristic (deck has >=10 GCs): count advisory fires:
  "Trim Game Changers below 10 (currently N) to drop from B5 to B4"
- Combos/MLD: violations only when target tier number < 4 (they are B4 gates)
- Extra turns: NEVER violations (FIX B enforced at record level)

New type: `FloorViolationSet` record in `DeckFlow.Core/Bracket/FloorViolationSet.cs`.

Updated consumers:
- `Bracket.cshtml` floor violations + starter cuts sections use `violations` from domain method
- `ChatGptBracketPromptVariant` AppendFloorViolations + AppendStarterCuts (hand-edited, ADR-0001)
- `ClaudeBracketPromptVariant` same
- `GeminiBracketPromptVariant` same

---

## Tests added

| File | New tests | Covers |
|------|-----------|--------|
| `BracketClassifierTests.cs` (Core) | 10 | FloorViolations unit tests for all tier/combo/MLD/extra-turn cases |
| `BracketPromptVariantParityTests.cs` (Web) | 9 (3 × 3 platforms) | B4/B5 advisory, B3 GC violations, extra-turn never-a-violation |

**Final counts:**
- Core bracket tests: 26 pass
- Web bracket tests: 55 pass (includes all pre-existing parity tests)
- Full Web.Tests suite: 986 pass, 0 fail, 12 skip
- Full Core.Tests suite: 905 pass, 0 fail
