---
phase: 98-card-grounding-guard
plan: 01
subsystem: core-knowledge
tags: [card-grounding, scryfall, legality, color-identity, singleton, castability]
requires: []
provides:
  - ICardGroundingGuard Core seam (TryValidateAsync + ValidateAllAsync)
  - CardGroundingVerdict / CardGroundingBatchResult / CardGroundingDeckContext records + CardGroundingRejectReason enum
  - Four pure static decision rules in CardGroundingRules (legality fail-closed, identity subset, normalized singleton, pip castability)
affects: [98-02, 98-03, 98-04, phase-99-artifact-engine]
tech-stack:
  added: []
  patterns: [interface-first Core seam ahead of Web impl (P96 grounder split), pure static rule class per ConflictCalculator house style]
key-files:
  created:
    - DeckFlow.Core/Knowledge/CardGrounding/ICardGroundingGuard.cs
    - DeckFlow.Core/Knowledge/CardGrounding/CardGroundingVerdict.cs
    - DeckFlow.Core/Knowledge/CardGrounding/CardGroundingRejectReason.cs
    - DeckFlow.Core/Knowledge/CardGrounding/CardGroundingDeckContext.cs
    - DeckFlow.Core/Knowledge/CardGrounding/CardGroundingBatchResult.cs
    - DeckFlow.Core/Knowledge/CardGrounding/CardGroundingRules.cs
    - DeckFlow.Core.Tests/Knowledge/CardGrounding/CardGroundingRulesTests.cs
  modified: []
key-decisions:
  - "Singleton rule normalizes the candidate via CardNormalizer.Normalize and documents that DeckCardNames must be populated the same way (Codex HIGH-1 fold) — bare trim+lowercase forbidden."
  - "IsCastable is a conservative WUBRG char scan: hybrid/Phyrexian pips require their color letter (strict-guard-correct false-reject direction); mana-value sanity deferred per D-11."
  - "Basic-land singleton exemption keys off Scryfall type line substring, not a card-name allowlist."
patterns-established:
  - "Fail-closed legality: missing/null legalities or absent commander key => not legal."
requirements-completed: [CS-23]
duration: ~8min (Codex gpt-5.4 dispatch)
completed: 2026-07-18
---

# Phase 98 Plan 01 Summary

Core contracts + pure rules landed. TDD evidence: 26 CardGrounding tests failed CS0103 (rules class absent) then passed 26/26. Full solution build 0 errors; only pre-existing NU1902 AngleSharp advisories. Commit 40675a56.
