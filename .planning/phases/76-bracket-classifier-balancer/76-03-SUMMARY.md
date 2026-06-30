---
phase: 76-bracket-classifier-balancer
plan: "03"
subsystem: DeckFlow.Web.Services.PromptBuilders.Bracket + DeckFlow.Web.Extensions
tags: [bracket, prompt-variants, adr-0001, decoupled, parity-test, tdd]
dependency_graph:
  requires:
    - 76-01 (BracketClassification, BracketTier, GameChangerCatalog, TwoCardCombo)
    - 76-02 (IGameChangerCatalogService, tool.bracket.enabled flag)
  provides:
    - IBracketPromptVariant interface + BracketPromptVariantRegistry
    - ChatGptBracketPromptVariant / ClaudeBracketPromptVariant / GeminiBracketPromptVariant (3 decoupled variants)
    - AddDeckFlowPromptVariants() extended with bracket family (DI)
  affects:
    - Phase 76 plan 04 (BracketController calls BracketPromptVariantRegistry.Build)
    - Phase 76 plan 06 (end-to-end result contract tests)
tech_stack:
  added: []
  patterns:
    - Decoupled per-platform prompt strategy (ADR-0001 — no shared helper, no base class)
    - TDD RED/GREEN cycle (parity test commit precedes implementation commit)
    - 3-platform Theory parity test (analog: ResultContractTests.cs)
    - IEnumerable<T> DI dispatch via ToDictionary (analog: PrimerPromptVariantRegistry)
key_files:
  created:
    - DeckFlow.Web/Services/PromptBuilders/Bracket/IBracketPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Bracket/BracketPromptVariantRegistry.cs
    - DeckFlow.Web/Services/PromptBuilders/Bracket/ChatGptBracketPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Bracket/ClaudeBracketPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Bracket/GeminiBracketPromptVariant.cs
    - DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs
  modified:
    - DeckFlow.Web/Extensions/PromptVariantServiceCollectionExtensions.cs (Bracket family added)
decisions:
  - "DI registrations placed in PromptVariantServiceCollectionExtensions.cs (not Program.cs directly) — mirrors all other 6 variant families; single AddDeckFlowPromptVariants() call stays the composition root"
  - "Each variant's private helpers (AppendClassificationReasons, AppendFloorViolations, AppendStarterCuts) are local to the file — ADR-0001 prohibits extraction; the three files share identical helper names but are independently implemented"
  - "Balancer block condition: BracketNumber > target (strict greater-than, never equal) — matches UISpec §6 'At or below target' state: no floor-violations when deck already meets target"
  - "FLOOR VIOLATIONS lists ALL detected GC cards even when count only slightly exceeds cap; AI prompt asks for 'power-equivalent swaps' so the full list gives context"
  - "Claude variant uses <bracket_classification> XML root tag + per-section XML tags to match Claude's prompt contract; ChatGPT and Gemini use markdown ## headers per their respective contracts"
metrics:
  duration_minutes: 28
  completed_date: "2026-06-28"
  tasks_completed: 2
  files_changed: 7
---

# Phase 76 Plan 03: Bracket Prompt Variants + Registry + Parity Test Summary

Three decoupled bracket paste-artifact prompt variants (ChatGpt/Claude/Gemini) authored per ADR-0001, backed by a BracketPromptVariantRegistry with AiPlatform.Default fallback, and a 15-case parity test proving classification + balancer + effective-date + combo-unavailable disclosure across all three platforms.

## What Was Built

### Task 1: IBracketPromptVariant interface + BracketPromptVariantRegistry

**IBracketPromptVariant** (`DeckFlow.Web/Services/PromptBuilders/Bracket/IBracketPromptVariant.cs`):
- `AiPlatform Platform { get; }` — dispatch key
- `string Build(BracketClassification, int? targetBracketNumber, string? deckName, IReadOnlyList<BracketTier>, GameChangerCatalog, CancellationToken)` — full signature; `int? targetBracketNumber` drives the conditional balancer block

**BracketPromptVariantRegistry** (`BracketPromptVariantRegistry.cs`):
- Receives `IEnumerable<IBracketPromptVariant>` from DI; builds `_variants = variants.ToDictionary(v => v.Platform)`
- `Build(AiPlatform, ...)` resolves the matching variant or falls back to `_variants[AiPlatform.Default]`
- No `Enum.Parse` anywhere; `AiPlatform.Default` is a static property

**DI wiring** (`PromptVariantServiceCollectionExtensions.cs`):
- Added to `AddDeckFlowPromptVariants()`: 3× `AddSingleton<IBracketPromptVariant, *>` + `AddSingleton<BracketPromptVariantRegistry>()`
- Registered in Task 2 commit (after concrete classes existed) to keep Task 1 build clean

### Task 2: Three decoupled bracket variants + 3-platform parity test (TDD RED→GREEN)

**RED commit** (`552a1314`): `BracketPromptVariantParityTests.cs` — 15 test cases, failed to compile (CS0246 for the three missing variant classes).

**GREEN commit** (`e326df39`): Three concrete variants + DI wiring added to the extension method.

#### Variant structure (all three files self-contained, ADR-0001)

Each variant's `Build()` method:
1. `ArgumentNullException.ThrowIfNull` on `classification`, `tiers`, `catalog`
2. Looks up `classifiedTier = tiers.FirstOrDefault(t => t.Number == classification.BracketNumber)`
3. **Classification block (always):**
   - Tier verdict line
   - `WHY THIS BRACKET` section: GC count with cap note, combos (or "no combos detected" when available), MLD, extra-turn count, tutor-removed footnote
   - Effective-date stamp: `"Game Changers list effective {classification.EffectiveDate}. Re-confirm Game Changers membership before suggesting swaps."`
   - Combo-unavailable disclosure when `!classification.ComboDetectionAvailable` (T-76-07 mitigated)
4. **Balancer block (conditional — only when `targetBracketNumber is int target && classification.BracketNumber > target`):**
   - `FLOOR VIOLATIONS` heading + list (all detected GC cards, combo halves, MLD cards tagged by kind)
   - `STARTER CUTS` heading + list (cut combo half, cut MLD, trim N GC cards with count rationale)

**Platform framing:**
- **ChatGPT**: Markdown `## heading` throughout
- **Claude**: XML-tag root `<bracket_classification>` with `<why_this_bracket>`, `<effective_date_note>`, `<combo_detection_note>`, `<balancer>` inner tags
- **Gemini**: Persona-scaffold opener ("You are an expert Commander deck analyst. Think carefully...") + markdown `## heading`

#### Parity test (BracketPromptVariantParityTests.cs) — 15 cases

| Theory | InlineData | What it asserts |
|--------|-----------|-----------------|
| `Build_ClassificationBlock_AppearsInAllThreeVariants` | ChatGPT/Claude/Gemini | "WHY THIS BRACKET" + "Game Changers list effective" present |
| `Build_BalancerBlock_AppearsInAllThreeVariants_WhenTargetBelowClassified` | ChatGPT/Claude/Gemini | "FLOOR VIOLATIONS" + "STARTER CUTS" present (B4→B2 target) |
| `Build_BalancerBlock_AbsentWhenAtOrBelowTarget` | ChatGPT/Claude/Gemini | "FLOOR VIOLATIONS" absent (B3→B3 target) |
| `Build_EffectiveDateStamp_AppearsInAllThreeVariants` | ChatGPT/Claude/Gemini | "2026-02-09" present |
| `Build_ComboUnavailable_DisclosedInAllThreeVariants_WhenDetectionUnavailable` | ChatGPT/Claude/Gemini | "combo detection" present; "0 two-card combos"/"no combos found" absent |

## Test Results

```
Passed!  - Failed: 0, Passed: 15, Skipped: 0, Total: 15
```

Full test: `BracketPromptVariantParityTests` — 15 cases, all pass.
Build: `DeckFlow.Web.csproj` — 0 warnings, 0 errors.

## Deviations from Plan

None — plan executed exactly as written.

- Task 1 DI registrations were staged in the extension file after Task 2 concrete classes were created (plan explicitly noted this ordering); all bracket DI lines landed in a single commit (`e326df39`).
- The three variant `AppendClassificationReasons`, `AppendFloorViolations`, `AppendStarterCuts` helpers share identical method names across files — this is intentional per ADR-0001; they are private static methods inside each file and are not shared.

## Known Stubs

None — this plan builds pure prompt-text generators with no UI rendering. No placeholder text, no empty data sources, no conditional content gated on future work.

## Threat Flags

None — all output is public MTG card classification data rendered as paste artifacts. No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries.

Card names from the deck (untrusted deck import, T-76-06) are interpolated into artifact prose. Disposition: accept — output is a paste artifact the user copies into their own AI chat; card names are public strings with no markup execution context. Same surface as existing Primer/Analysis artifacts.

Combo-unavailable disclosure (T-76-07) is implemented in all three variants and asserted by the parity test.

## TDD Gate Compliance

- RED gate: `test(76-03): add failing BracketPromptVariantParityTests (RED)` — commit `552a1314`
- GREEN gate: `feat(76-03): three decoupled bracket prompt variants + DI wiring (GREEN)` — commit `e326df39`
- No REFACTOR pass needed (implementations were clean on first pass)

## Self-Check: PASSED

Files created/exist:
- DeckFlow.Web/Services/PromptBuilders/Bracket/IBracketPromptVariant.cs — FOUND
- DeckFlow.Web/Services/PromptBuilders/Bracket/BracketPromptVariantRegistry.cs — FOUND
- DeckFlow.Web/Services/PromptBuilders/Bracket/ChatGptBracketPromptVariant.cs — FOUND
- DeckFlow.Web/Services/PromptBuilders/Bracket/ClaudeBracketPromptVariant.cs — FOUND
- DeckFlow.Web/Services/PromptBuilders/Bracket/GeminiBracketPromptVariant.cs — FOUND
- DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs — FOUND

Commits confirmed:
- d45f922e — feat(76-03): IBracketPromptVariant interface + BracketPromptVariantRegistry
- 552a1314 — test(76-03): add failing BracketPromptVariantParityTests (RED)
- e326df39 — feat(76-03): three decoupled bracket prompt variants + DI wiring (GREEN)

Parity test: 15/15 PASS — non-vacuous green gate (test was authored RED before implementations existed).
