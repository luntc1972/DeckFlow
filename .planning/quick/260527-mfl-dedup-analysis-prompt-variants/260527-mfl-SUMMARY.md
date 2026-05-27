---
quick_id: 260527-mfl
slug: dedup-analysis-prompt-variants
date: 2026-05-27
status: complete
implementer: codex
reviewer: claude
commit: a1fa5ad
---

# Quick Task 260527-mfl Summary

## What changed

Extracted three duplicated guidance blocks from the three deck-analysis prompt
variant classes into a new `AnalysisPromptShared` static helper, and reconciled a
phrasing-drift bug in the MDFC-land guidance.

- NEW `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptShared.cs` —
  3 append-to-StringBuilder helpers:
  - `AppendBracketWeightingGuidance` (4-line bracket/win-turn weighting, byte-identical)
  - `AppendMdfcLandGuidance(linePrefix)` (canonical MDFC sentence; prefix per variant)
  - `AppendDeckProfileFieldDetails(indent)` (11-line field block; indent per variant)
- Repointed `ChatGpt`, `Gemini`, `Claude` AnalysisPromptVariant.cs to the helpers.
- Lock test `BuildAsync_IncludesSharedAnalysisPromptGuidance_ForEveryAiPlatform`
  ([Theory] × ChatGPT/Claude/Gemini) added in DeckAnalysisPacketServiceTests.cs.

## Drift fix

MDFC-land sentence was canonicalized to the ChatGPT/Gemini phrasing
(`"...mana base. Weight them higher than a plain land, since..."`). Claude
previously read `"...mana base, and weight them higher than a plain land since..."`
and now matches. This is the ONLY intended output change.

## Verification

- `dotnet build DeckFlow.sln`: 0 warnings, 0 errors.
- `dotnet test DeckFlow.Web.Tests`: 489 passed, 5 skipped, 0 failed.
- New test red-first on Claude MDFC wording, green after fix (confirms it exercises
  the drift path).
- Byte-identical spot-check: ChatGPT + Gemini prompt output unchanged; Claude diff
  confined to the MDFC sentence.

## Scope held

Did NOT touch evidence rules, bracket-options loop, analysis-questions, or the
OUTPUT FORMAT A/B/C/D body — platform framing there is intentional (70-85% overlap
but genuinely divergent). Dedup was limited to the byte-identical / drift blocks.

## Reviewer note (Claude)

Reviewed diff a1fa5ad — PASS, no findings. Helper text verbatim; indent/prefix
params reconstruct each variant's bytes exactly; defensive null guards added.

## Not done

- Not pushed (operator pushes).
- No golden-file regression test for full byte-identical output (phrase-presence
  lock tests only, per plan).
