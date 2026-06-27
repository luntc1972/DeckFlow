---
phase: 54-feature-debt
plan: 02
requirement: FEAT-01
status: verified-with-finding
measured: 2026-06-17
---

# Phase 54 — FEAT-01 Gemini Paste-Limit Verification

**Requirement:** FEAT-01 — Verify the Gemini paste-limit path is genuinely usable across all four
workflows that emit a Gemini variant (deck analysis, deck comparison, cEDH meta-gap, Deck Primer),
keep `DECKFLOW_GEMINI_ENABLED` default `false`, and record the evidence. Per CONTEXT.md (LOCKED):
VERIFY-ONLY — do not change the default, do not implement packet trimming; any artifact over the
limit is a recorded FINDING, not a silently-shipped truncating path.

---

## How the sizes were measured

- **Test:** `DeckFlow.Web.Tests/GeminiVariantSizeTests.cs` (commit `2017b77`).
- **Method:** Each Gemini variant's `Build(...)` is called synchronously (pure CPU, no HTTP/DB) for a
  single representative ~100-card cEDH fixture (Kraum / Tymna combo shell padded to 100 cards), and
  the prompt's `string.Length` (char count — NOT UTF-8 byte count, per RESEARCH Pitfall 3) is emitted
  with a WITHIN/OVER label against the conservative 30,000-char Gemini paste ceiling.
- **Ceiling:** 30,000 chars. The Gemini web UI "message too long" warning triggers at approximately
  30,000–32,768 chars (community-sourced; RESEARCH §"Gemini paste limit"). 30,000 is the conservative
  pass threshold. The codebase's own caps (Primer 32,000; Analysis 50,000) bracket this figure.
- **Routing evidence:** Analysis / comparison / meta-gap route to the Gemini variant under
  `TargetAiPlatform=Gemini`; this is already proven by `AiPlatformPhase10RoundTripTests` (zip
  round-trip + `RequestContextParser` restore `target_ai_platform: Gemini`). The size test measures the
  routed/built Gemini variant directly rather than duplicating that routing coverage (must-have #18
  satisfied for all four workflows). The Primer fact additionally measures the prompt produced through
  the flag-on `DeckPrimerPacketService` fan-out (`geminiEnabled: true`) and asserts the enabled-platform
  set includes `Gemini` — the flag-flipped path (`GetEnabledPlatforms`, DeckPrimerPacketService.cs:512-518).
- **Run:** `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~GeminiVariantSizeTests"` →
  4/4 PASS (WSL VSTest ran successfully this session). `dotnet build DeckFlow.sln` → 0 errors,
  0 new warnings.

---

## Per-workflow size table

| Workflow      | Gemini variant file                                                            | Internal cap | Measured chars | Ceiling | Verdict |
|---------------|--------------------------------------------------------------------------------|--------------|----------------|---------|---------|
| Deck analysis | `Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs` (cap 50,000, no drop)   | 50,000       | 24,994         | 30,000  | PASS    |
| Deck comparison | `Services/PromptBuilders/Comparison/GeminiComparisonPromptVariant.cs` (no cap, no drop) | none         | 23,830         | 30,000  | PASS    |
| cEDH meta-gap | `Services/PromptBuilders/MetaGap/GeminiMetaGapPromptVariant.cs` (no cap, no drop)        | none         | 18,026         | 30,000  | PASS    |
| Deck Primer   | `Services/PromptBuilders/Primer/GeminiPrimerPromptVariant.cs` (cap 32,000, AppendIfFits) | 32,000       | 5,553          | 30,000  | PASS    |

All four workflows measured WITHIN the 30,000-char ceiling for the representative fixture.

---

## Findings

**Finding F-54-FEAT01-01 (informational — analysis is the highest-risk workflow, but PASSED here).**
RESEARCH Open Question 1 / Pitfall 5 flagged `GeminiAnalysisPromptVariant` as the likely over-limit case:
it carries a 50,000-char defensive cap and has **no section-dropping**, so a sufficiently large deck +
reference document + schema can produce a prompt that exceeds Gemini's ~30,000-char paste warning and is
silently truncated on paste. For the representative ~100-card cEDH fixture measured here it came in at
**24,994 chars — WITHIN** the ceiling, with the least headroom of the four workflows (~5,000 chars / ~17%).

- **Risk:** A real deck with a larger card-reference document (more unique cards, longer oracle/mechanic
  text), more selected analysis questions, full-decklist-output questions, or accented/long card names can
  push the analysis prompt over 30,000 chars. Because the analysis variant has no `AppendIfFits`
  section-drop, the over-limit prompt is generated and stored in full; the truncation happens silently in
  the Gemini UI at paste time.
- **Disposition:** Trimming / section-dropping for the analysis variant is **DEFERRED this cycle** per
  CONTEXT.md ("surface oversize as a finding, don't implement reduction"). No trimming was added in this
  plan. The recorded mitigation is VISIBILITY: the repeatable `GeminiVariantSizeTests` emits a
  WITHIN/OVER label per workflow so a regression that pushes the analysis prompt over the ceiling is
  observable in test output. If a future fixture or real deck measures OVER, that is the recorded finding —
  not a code change in this plan.
- **No silent-ship:** The path is not shipped enabled — the flag stays default-off (below). The operator
  flips it knowing the analysis workflow has the least headroom.

---

## Default-off confirmation

`DECKFLOW_GEMINI_ENABLED` default remains **`false`** and was **NOT changed** by this plan.

- Source: `DeckFlow.Web/Program.cs:78-82` —
  `options.GeminiEnabled = bool.TryParse(raw, out var enabled) && enabled;` → resolves to `false` when the
  env var is unset or unparsable. No production source file was modified in Plan 54-02 (only the new test
  file `DeckFlow.Web.Tests/GeminiVariantSizeTests.cs` + this verification doc).
- UI gating: `_AiSelector.cshtml:13,25` hides the Gemini radio when the flag is false. (Server still accepts
  a directly-POSTed `TargetAiPlatform=Gemini` — pre-existing, documented at `AiPlatformOptions.cs:16`, UI-hide
  only; out of FEAT-01 scope.)

---

## Manual paste verification

**Status: (b) WAIVED-WITH-REASON.**

The live operator paste step (set `DECKFLOW_GEMINI_ENABLED=true` locally, generate each of the four
workflow packets, paste each Gemini artifact into gemini.google.com, confirm no truncation) was **not run
this cycle** and is explicitly waived, mirroring the HARD-02 waive-with-reason precedent.

**Reason for waiver:**
1. The flag stays default-off; nothing is shipped enabled, so prod users are not exposed to a truncating
   path before an operator deliberately flips the flag.
2. The automated char-count measurement (`GeminiVariantSizeTests`, all four workflows WITHIN 30,000 for the
   representative deck) plus the existing `AiPlatformPhase10RoundTripTests` routing coverage stand in as the
   repeatable evidence that the four workflows generate Gemini prompts within the limit for a representative
   deck.
3. Real Gemini paste behavior is external (gemini.google.com) and not automatable in CI.

**Outstanding operator action (when the operator decides to flip the flag in prod):** run the four-workflow
live paste with a real max-input deck and confirm no truncation — paying particular attention to the
**analysis** workflow (least headroom, no section-drop, Finding F-54-FEAT01-01). If the live analysis
artifact for a real deck exceeds 30,000 chars, that is the trigger to schedule the deferred analysis-variant
trimming work.

---

## Summary

FEAT-01 is **verified with one informational finding**. All four Gemini workflows generate prompts WITHIN
the 30,000-char paste ceiling for a representative ~100-card cEDH deck; the analysis workflow has the least
headroom and remains the highest-risk case (no section-drop) — surfaced as Finding F-54-FEAT01-01 with
trimming deferred per CONTEXT.md. The `DECKFLOW_GEMINI_ENABLED` default stays `false`, unchanged. No packet
trimming was implemented. The live operator paste is waived-with-reason; the repeatable size test is the
standing regression guard.
