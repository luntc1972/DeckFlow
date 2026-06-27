---
phase: 73-deck-analysis-command-zone-awareness-ad-hoc-trunk-main
plan: 02
subsystem: deck-analysis
tags: [feature-flag, command-zone, companion, byte-identity, prompt-injection, partners]
requires:
  - "73-01: analysis.command-zone-awareness flag + companionName Build-chain param + DeckAnalysisRequest.CompanionName"
provides:
  - "Flag-gated command-zone enrichment in DeckAnalysisPacketService.BuildAsync (all partners/Background, oracle-resolved per name, joined ' & ')"
  - "ResolveCompanionName/BoundCompanionName helpers (designator-wins, single-line collapse, trim, 200-char cap)"
  - "companionName forwarded to BuildAnalysisPrompt as side metadata (deck text never mutated)"
  - "3-platform flag-OFF byte-identity gate + partner-pair + single-commander regression tests"
affects:
  - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
  - DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs
tech-stack:
  added: []
  patterns:
    - "Explicit default-OFF flag read via Snapshot().TryGetValue (absent key / null cache / read failure -> off)"
    - "Resolve-then-join: oracle-resolve EACH command-zone name individually before the ' & ' concat (Pitfall 1)"
    - "Injection hardening: single-line collapse (CollapseWhitespace) + trim + 200-char cap before any prompt (Codex HIGH-2)"
    - "Awareness-only: companion is side metadata; deck text / cache key untouched (Codex HIGH-1, Pitfall 3)"
key-files:
  created:
    - .planning/phases/73-deck-analysis-command-zone-awareness-ad-hoc-trunk-main/73-02-SUMMARY.md
  modified:
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs
decisions:
  - "BoundCompanionName reuses the file's existing CollapseWhitespace (already strips CR/LF) for the single-line collapse rather than blind-copying the manabase BoundCompanionName, which only trims+caps (HIGH-2 requires the newline strip)"
  - "Command-zone names ordered by raw name (OrdinalIgnoreCase) BEFORE oracle mapping, so the join order is deterministic and independent of deck-entry order"
  - "Single-commander unchanged proven by full AnalysisPromptText equality (flag-ON == flag-OFF) rather than only a string-absence check, locking out any spurious mutation"
metrics:
  duration_minutes: 22
  completed: 2026-06-27
  tasks: 2
  files_changed: 2
---

# Phase 73 Plan 02: Command-Zone Enrichment + Companion Resolution Summary

Wired the `analysis.command-zone-awareness` flag (registered unwired in 73-01) into
`DeckAnalysisPacketService.BuildAsync`: when ON the singular first-commander string becomes the
FULL command zone (all `Board == "commander"` entries, each oracle-resolved individually then
joined with " & "), and the companion (designator-wins over auto-detected) is resolved to a
single-line, trimmed, 200-char-bounded value and forwarded to `BuildAnalysisPrompt` as side
metadata. When OFF, output is byte-identical to baseline across ChatGPT, Claude, AND Gemini — the
deck text, the cache key, and `ResolvePreScryfallCommanderState` are all left untouched.

## What Was Built

### Task 1 — Flag-gated enrichment in BuildAsync (commit f356840a)

- **Captured the discarded companion:** added `var detectedCompanionName = loaded.DetectedCompanionName;`
  at the `LoadFromSourceAsync` site (previously dropped).
- **Default-OFF flag read:** `commandZoneAwareness` gates on the EXPLICIT snapshot value
  (`_flagCache is not null && Snapshot().TryGetValue(CommandZoneAwarenessFlag, out var commandZoneOn) && commandZoneOn`),
  mirroring the existing `ReferenceDeckStatsFlag` pattern — absent key, null cache, or store-read
  failure all resolve to off, so a flag-system fault never mutates output.
- **Command-zone enrichment (after the existing oracle resolution):** when ON, collect all
  `Board == "commander"` names (distinct OrdinalIgnoreCase), order by name, then oracle-resolve
  EACH name individually via `cardReferenceBundle.OracleNameMap` (resolve-then-join, Pitfall 1).
  If 2+ names result, `commanderName = string.Join(" & ", resolvedNames)`; 1 name leaves it
  as-is; 0 names preserve the existing value.
- **Companion resolution helpers:** `ResolveCompanionName(designator, detected)` returns
  `BoundCompanionName(designator) ?? BoundCompanionName(detected)` (designator wins).
  `BoundCompanionName` returns null for null/whitespace, else runs the value through the file's
  existing `CollapseWhitespace` (which strips CR/LF — the single-line collapse that defeats
  newline prompt-structure injection, Codex HIGH-2), trims, and caps at the new
  `MaxCompanionNameLength = 200`.
- **Forwarded** `companionName` as the new trailing argument to the analysis `BuildAnalysisPrompt(...)`
  call. The deck-text/decklist building, the set-upgrade prompt, `TryComputeCacheKeyAsync`, and
  `ResolvePreScryfallCommanderState` were NOT touched (Pitfall 3 / cache-key invariant).

### Task 2 — Flag-OFF byte-identity gate + enrichment regression tests (commit 3e5d6739)

- `BuildAsync_IsByteIdentical_WhenCommandZoneAwarenessFlagOff` — an xUnit `[Theory]` over
  `[InlineData("ChatGPT")]` / `[InlineData("Claude")]` / `[InlineData("Gemini")]` (MED-2). For each
  platform it builds a companion+Background fixture (`includeBackgroundCommander: true`,
  `detectedCompanionName: "Jegantha, the Wellspring"`) once with the flag explicitly false and once
  with a baseline service (flag unset) and asserts `PacketBytes` equality. Per Codex HIGH-1 it does
  NOT assert companion-absence — the companion may legitimately appear in mainboard deck text;
  per-platform byte-identity alone proves no flag-OFF regression.
- `BuildAsync_CommandZoneAwareness_RendersPartnerPair` — flag ON, two command-zone entries; asserts
  `AnalysisPromptText` contains `"Kraum, Ludevic's Opus & Passionate Archaeologist"` (the existing
  variant already renders `commanderName`, so this passes without Plan 03).
- `BuildAsync_CommandZoneAwareness_SingleCommanderUnchanged` — flag ON solo commander; asserts the
  name is present, there is no spurious `"Kraum, Ludevic's Opus &"`, and the flag-ON
  `AnalysisPromptText` equals the flag-OFF rendering.

## Verification

- **Build:** `dotnet.exe build DeckFlow.Web/DeckFlow.Web.csproj` — **0 warnings, 0 errors**.
- **Targeted tests (VSTest via Windows `dotnet.exe` — ran successfully this session):**
  `--filter "DeckAnalysisPacketServiceTests"` → **60 passed, 0 failed** (includes the 3 new tests =
  3 Theory cases + 2 facts, and the existing `BuildAsync_DoesNotLeakCompanionDeckContent...` +
  `BuildAsync_IsByteIdentical_WhenCommanderCastabilityFlag...` guards stayed green).
- **Full Web suite:** `dotnet.exe test DeckFlow.Web.Tests` → **919 passed, 12 skipped, 0 failed**
  (1m11s). No regression.
- **Format gate:** `scripts/format-check-changed.sh staged` exited 0 for both commits (changed-lines
  clean). Note: the repo's `core.hooksPath` points at the default `.git/hooks` (the format gate is
  opt-in via `.githooks`), so the gate was run manually to satisfy the changed-lines requirement.
- **Line endings:** both touched files are LF; preserved. Carve-outs (switch expressions, raw
  strings, `{ get; init; }`) untouched.
- **Compiled assets:** no `wwwroot/js/*.js` staged; only `.cs` files committed.
- **Cache key / deck text:** no diff in `TryComputeCacheKeyAsync`, `ResolvePreScryfallCommanderState`,
  or the decklist building — verified by the flag-OFF 3-platform byte-identity Theory.

## Deviations from Plan

### Auto-fixed Issues

None — the plan executed as written.

### Notable implementation choice (within plan scope)

- The plan's `<interfaces>` note warned NOT to blind-copy the manabase `BoundCompanionName` (which
  only trims+caps). Rather than re-implement a newline strip, `BoundCompanionName` reuses this file's
  existing `CollapseWhitespace` helper, which already replaces CR/LF and collapses lines to single
  spaces — satisfying the HIGH-2 single-line requirement with the file's own normalization primitive
  (the same one `NormalizeSingleLine` uses for `Format`/`DeckName`).

## Threat Flags

None — no new security surface beyond the planned `<threat_model>`. T-73-01 (companion → prompt
injection) is mitigated by `BoundCompanionName` (single-line collapse + trim + 200-char cap);
T-73-02 (cache poisoning via enriched commanderName) is mitigated by leaving the cache-key path
unmodified. No package-manager installs (T-73-SC).

## Known Stubs

None. The companion is now resolved and forwarded; the three variants render it in Plan 73-03.
This plan's `companionName` reaches `BuildAnalysisPrompt` and is consumed by the registry/variants
(currently a no-op body until 73-03), which is the documented contract handoff, not an accidental stub.

## Commits

- `f356840a` feat(73-02): enrich command zone + resolve companion behind flag
- `3e5d6739` test(73-02): cover command-zone flag-OFF byte-identity + partner pair

## Self-Check: PASSED

Both touched files exist, both task commits (f356840a, 3e5d6739) are in history, and the key
tokens are present: `CommandZoneAwarenessFlag` read via `Snapshot().TryGetValue`,
`detectedCompanionName` captured from `loaded.DetectedCompanionName`, `ResolveCompanionName` +
`BoundCompanionName` helpers, the `companionName` argument on the analysis `BuildAnalysisPrompt`
call, and the `BuildAsync_IsByteIdentical_WhenCommandZoneAwarenessFlagOff` Theory.
