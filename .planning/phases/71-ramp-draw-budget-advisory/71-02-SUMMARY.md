---
phase: 71-ramp-draw-budget-advisory
plan: 02
subsystem: core
tags: [manabase, verdict, prompt, text-report, testing]
requires: ["71-01"]
provides:
  - deterministic plain-language manabase verdict synthesis
  - prompt/text-builder verdict and ramp/draw budget append path
affects: [phase-71, manabase, plain-language-verdict]
tech-stack:
  added: []
  patterns:
    - deterministic synthesis from existing ManabaseReport fields only
    - optional trailing builder params with byte-identical null path
key-files:
  created:
    - DeckFlow.Core/Manabase/ManabaseVerdict.cs
    - DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseVerdictSynthesizerTests.cs
  modified:
    - DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs
    - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseSwapPromptBuilderTests.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs
requirements-completed: [PLV-VERDICT, PLV-BUDGET]
completed: 2026-06-26T23:00:00-06:00
---

# Phase 71-02 Summary

**DeckFlow.Core now synthesizes a deterministic "Reading your deck" verdict from the existing manabase report and mirrors that verdict plus the ramp/draw budget block into the single manabase prompt builder and the downloadable text report.**

## What Was Built

- Added `ManabaseVerdict` as the sealed plain-language verdict record for issue lines vs no-issue reasons.
- Added `ManabaseVerdictSynthesizer.Synthesize(report, mode, budget?)` with deterministic templates, issue priority `color -> land -> ramp -> draw`, and a hard cap of 3 surfaced issues.
- Extended `ManabaseSwapPromptBuilder.Build(...)` and `ManabaseReportTextBuilder.Build(...)` with trailing optional nullable `verdict` / `budget` params.
- Appended a "Reading your deck:" block plus the one-line ramp/draw budget summary only when a verdict is supplied.
- Added explicit byte-identical null-path assertions for both builders so the existing output remains unchanged when the new params are omitted or null.

## Task Commits

1. Task 1: manabase plain-language verdict synthesizer - `230510b4`
2. Task 2: append verdict + ramp/draw block to prompt and text builders - `dc5c45f2`

## Key Decisions / Deviations

- Decisions:
  - Kept the synthesizer pure and limited to `ManabaseReport` plus the optional `ManabaseRampDrawBudget`; it does not re-run classification or analysis.
  - Used the exact proxy phrases from the plan for commander-threshold vs curve-proxy budget text.
  - Preserved builder byte identity on the null path by making the new params additive and rendering the new block only when `verdict` is non-null.
- Deviations:
  - None from the plan scope or file fence.

## Test Results

- `cmd.exe /c "dotnet test DeckFlow.Core.Tests\DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~ManabaseVerdictSynthesizer"` -> 4/4 passed
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -clp:ErrorsOnly` -> passed, 0 warnings, 0 errors
- `cmd.exe /c "dotnet test DeckFlow.Core.Tests\DeckFlow.Core.Tests.csproj --filter FullyQualifiedName~Manabase"` -> 180/180 passed
- Byte-identical null-param assertions in `ManabaseSwapPromptBuilderTests` and `ManabaseReportTextBuilderTests` passed within the 180-test manabase run

## Self-Check

PASSED
