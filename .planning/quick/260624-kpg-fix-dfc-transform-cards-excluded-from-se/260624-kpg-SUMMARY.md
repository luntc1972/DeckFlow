---
phase: quick-260624-kpg
plan: 01
subsystem: set-packet
tags: [scryfall, set-packet, dfc, transform, mdfc, scoring]
requires: []
provides: ["face-aware set-packet scoring + card-line render for transform/MDFC cards"]
affects: ["DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs"]
tech_stack_added: []
key_files_created: []
key_files_modified:
  - DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs
  - DeckFlow.Web.Tests/ScryfallSetServiceTests.cs
decisions:
  - "Single private static ResolveManaCost helper resolves front-face cost only when parent ManaCost is empty; wired into curve-bonus score, tiebreak sort, and card-line render."
  - "NormalizeOracleText falls back to joined face oracle text + front-face P/T only when parent fields are empty — single-face cards are a no-op."
metrics:
  duration_minutes: 12
  completed_date: 2026-06-24
  tasks: 2
  files_modified: 2
---

# Phase quick-260624-kpg Plan 01: Fix DFC/Transform Cards Excluded From Set Packet Summary

Face-aware set-packet scoring + card-line rendering so on-theme transform/MDFC cards (null parent oracle_text, empty parent mana_cost — real data in `card_faces[]`) are no longer scored near-zero and cut from the top-60; single-face cards remain byte-identical.

## What Was Built

**Task 1 — Face-aware NormalizeOracleText + shared mana-cost helper** (`06d9bf06`)
- `NormalizeOracleText(ScryfallCard)`: when parent `OracleText` is null/empty, falls back to joining all non-empty `CardFaces[]` oracle text (back face often holds the payoff). For Power/Toughness, when parent P/T are empty and `CardFaces` is non-empty, uses front-face (`CardFaces[0]`) P/T. The existing parent-oracle path is preserved unchanged when parent `OracleText` is present (faces are NOT appended in that case).
- New `ResolveManaCost(ScryfallCard)` private static helper: returns parent `ManaCost` when non-empty; only when parent `ManaCost` is null/empty AND `CardFaces` is non-empty does it return the front-face (`CardFaces[0]`) cost; otherwise returns the parent value. No special-casing of split/adventure layouts (their parent fields are already representative).
- Wired `ResolveManaCost` into all three call sites:
  - card-line render (`builder.AppendLine($"{card.Name} | {ResolveManaCost(card) ?? string.Empty} | ...")`)
  - `BuildCompactCardPacket` tiebreak `.ThenBy(entry => ParseManaValue(ResolveManaCost(entry.Card)))`
  - `ScoreSetCard` curve bonus `var manaValue = ParseManaValue(ResolveManaCost(card));`
- Effect: empty-cost DFCs stop returning `int.MaxValue` from `ParseManaValue`, so they no longer hit the false `>=7` `-1` curve penalty and now collect the `manaValue<=4` `+1` bonus; the text-signal score is fixed automatically because `ScoreSetCard` already calls `NormalizeOracleText(card)`.

**Task 2 — Tests** (`a01473cc`)
- `BuildSetPacketAsync_TransformCard_IncludedWithFrontFaceCostAndText`: builds a "Monica Rambeau // Photon, Living Light" transform card (parent `OracleText` null, parent `ManaCost` `""`, front face `{2}{W}` with prowess + counter text) plus a plain control card. Asserts the transform card is in the packet, splits on newlines to isolate the `Monica Rambeau` line, and asserts that line contains `{2}{W}` and `prowess` — an empty cost field would fail.
- `BuildSetPacketAsync_SingleFaceCard_LineUnchanged`: a plain single-face card (`CardFaces` null) and asserts the emitted line equals exactly `Sage Scribe | {1}{G} | Creature — Elf | Draw a card. 2/2`, locking the no-op fallback behavior.

## Verification

- `dotnet build DeckFlow.Web` — Build succeeded, 0 errors. Only pre-existing NU1903 SQLite warnings (unrelated).
- `dotnet build DeckFlow.Web.Tests` — Build succeeded, 0 errors. Only pre-existing warnings (NU1903, CS0618 TrustServerCertificate, CS1574 xmldoc cref) unrelated to this change.
- `dotnet.exe test --filter "FullyQualifiedName~ScryfallSetServiceTests" --no-build` — **Passed! Failed: 0, Passed: 12, Skipped: 0**. VSTest ran successfully this time (the known WSL flakiness did not bite); both new tests pass alongside the 10 existing.
- Changed-lines format gate (`scripts/format-check-changed.sh staged`) — EXIT 0 for both staged files.
- Grep confirms no remaining scoring/render reference to `card.ManaCost`/`entry.Card.ManaCost` outside the `ResolveManaCost` helper itself.

Build/test used `dotnet.exe` (Windows SDK 10.0.301) because the Linux `dotnet` SDK is not on PATH in this WSL environment — permitted fallback per the build notes.

## Deviations from Plan

None — plan executed exactly as written. The three Codex-approved changes were applied verbatim; no new scoring buckets or layout special-casing added.

## Known Stubs

None.

## Threat Flags

None — output-only behavior change, no new network endpoints, auth paths, file access, or schema changes.

## Self-Check: PASSED

- Files exist: `DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs`, `DeckFlow.Web.Tests/ScryfallSetServiceTests.cs` — both present and modified.
- Commits exist: `06d9bf06` (Task 1, fix), `a01473cc` (Task 2, test) — both in `git log`.
