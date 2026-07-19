---
phase: 98-card-grounding-guard
plan: 02
subsystem: web-scryfall
tags: [card-grounding, scryfall, fuzzy, cache, fail-closed, di]
requires:
  - phase: 98-card-grounding-guard
    plan: 01
    provides: ICardGroundingGuard seam + CardGroundingRules pure rules
provides:
  - CardGroundingGuard (strict, cached, singleton) implementing ICardGroundingGuard
  - ScryfallCard.Legalities DTO field (appended last optional param — non-breaking)
  - ScryfallErrorResponse 404-body DTO (type/code/details)
  - IScryfallCardResolver.ExecuteNamedFuzzyAsync (throwing default interface member + impl)
  - DI registration AddSingleton<ICardGroundingGuard, CardGroundingGuard>
affects: [98-03, 98-04, phase-99-artifact-engine]
tech-stack:
  added: []
  patterns: [resolution-only cache with distinct key prefix, throwing default interface member for fake compatibility, 75-chunk collection batch per MeasuredStyleProfileBuilder precedent]
key-files:
  created:
    - DeckFlow.Web/Services/Scryfall/ScryfallErrorResponse.cs
    - DeckFlow.Web/Services/Scryfall/CardGroundingGuard.cs
    - DeckFlow.Web.Tests/Services/Scryfall/CardGroundingGuardTests.cs
  modified:
    - DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs
    - DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs
    - DeckFlow.Web/Extensions/ScryfallServiceCollectionExtensions.cs
key-decisions:
  - "Fuzzy 404 discriminator = optional type:\"ambiguous\" field per live-verified research; code field is not_found in both cases."
  - "Cache stores deck-independent CardResolution (canonical name, identity, commander legality status, mana cost, type line) — verdict rules re-run per request, preventing cross-deck contamination (Codex T-98-11 fold)."
  - "UpstreamUnavailable never cached; malformed 404 body lands UpstreamUnavailable (conservative, uncached retry-able)."
  - "Cancellation/timeout inside guard => UpstreamUnavailable verdict per plan (never a crash, never an accept)."
key-links:
  - from: CardGroundingGuard verdict path
    to: CardGroundingRules (98-01)
    via: legality/identity/singleton/castability precedence
patterns-established:
  - "Strict-vs-lenient split: CardGroundingGuard never uses SearchPrintingFallbackCardAsync (the P96 grounder's lenient cascade)."
requirements-completed: [CS-21, CS-24]
duration: ~10min (Codex gpt-5.4 dispatch)
completed: 2026-07-18
---

# Phase 98 Plan 02 Summary

Strict guard landed. EOL: 3 existing files modified with 0 CR churn (2+21+3 line diffs). TDD: 13 guard tests red (CS0246) then 13/13 green; grounder regression 4/4; wave-2 gate full Web suite 1303/1317 green (14 skipped env-gated PG). Commit 0663144b.
