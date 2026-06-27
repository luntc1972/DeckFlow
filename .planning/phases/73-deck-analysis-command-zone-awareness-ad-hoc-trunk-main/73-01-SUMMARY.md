---
phase: 73-deck-analysis-command-zone-awareness-ad-hoc-trunk-main
plan: 01
subsystem: deck-analysis / feature-flags
tags: [feature-flag, prompt-variants, contract-first, byte-identity, command-zone]
requires: []
provides:
  - "analysis.command-zone-awareness feature flag (seeded OFF, both dialects)"
  - "CommandZoneAwarenessFlag constant on DeckAnalysisPacketService (unwired)"
  - "string? companionName = null threaded through the analysis Build chain"
  - "DeckAnalysisRequest.CompanionName property"
affects:
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
  - DeckFlow.Web/Services/PromptBuilders/Analysis/*
  - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
  - DeckFlow.Web/Models/DeckAnalysisRequest.cs
tech-stack:
  added: []
  patterns:
    - "Decoupled per-AI prompt variants (ADR 0001) — no shared helper for the new parameter"
    - "Operator feature-flag seed/catalog/test lockstep (5 touch points)"
key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs
    - DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptVariantRegistry.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs
    - DeckFlow.Web/Models/DeckAnalysisRequest.cs
    - DeckFlow.Web.Tests/AiPlatformExtensionTests.cs
decisions:
  - "New flag analysis.command-zone-awareness (seeded OFF), NOT a reuse of manabase.commander-castability — keeps the existing byte-identity test valid and lives in the analysis.* namespace"
  - "Added DeckAnalysisRequest.CompanionName so companion awareness can later cover Archidekt/pasted-text imports, not just Moxfield auto-detection"
metrics:
  duration_minutes: 18
  completed: 2026-06-27
  tasks: 2
  files_changed: 12
---

# Phase 73 Plan 01: Command-Zone-Awareness Contract Foundation Summary

Contract-first foundation for command-zone awareness: registered the default-OFF
`analysis.command-zone-awareness` flag, widened the analysis prompt-variant `Build`
chain to carry a `string? companionName = null` (unused, so output is byte-identical),
and added a `CompanionName` designator to `DeckAnalysisRequest`. No rendered output
changed — every edit is additive and the new data is never read yet.

## What Was Built

### Task 1 — Flag registration (commit 551f699f)
Registered `analysis.command-zone-awareness` seeded **FALSE/0** across all five touch points:
- `FeatureFlagStore.PostgresSeedSql` — new `('analysis.command-zone-awareness', FALSE)` row (added trailing comma to the prior `commander-castability` row to keep the VALUES list valid).
- `FeatureFlagStore.SqliteSeedSql` — corresponding `('analysis.command-zone-awareness', 0)` row.
- `FeatureFlagCatalog.Descriptions` — operator-facing description (uses plain hyphens, no em/en dashes).
- `FeatureFlagCatalogTests` — `[InlineData("analysis.command-zone-awareness")]`.
- `FeatureFlagStoreSeedTests` — `[InlineData("analysis.command-zone-awareness", false)]`.

Also added the unwired constant `CommandZoneAwarenessFlag = "analysis.command-zone-awareness"`
to `DeckAnalysisPacketService` in the existing flag-constant block. Not referenced by any logic.

### Task 2 — Build-chain widening + request field (commit 9256bb98)
Added a trailing `string? companionName = null` parameter to:
- `IAnalysisPromptVariant.Build` (with a `<param name="companionName">` xmldoc).
- `AnalysisPromptVariantRegistry.Build` — and forwarded `companionName` to `variant.Build(...)`.
- All three variants (`ChatGpt` / `Claude` / `Gemini`) — parameter is **not referenced** in any body, so each variant renders byte-identically.
- `DeckAnalysisPacketService.BuildAnalysisPrompt` — forwards it to the registry.

Per ADR 0001 (prompt variants intentionally decoupled), no shared helper was introduced —
each signature was hand-edited independently.

Added `DeckAnalysisRequest.CompanionName` — a plain bound `string` with a null-coalescing
setter (`set => _companionName = value ?? string.Empty;`), mirroring `ManabaseRequest.CompanionName`.
No trimming or length bounding here — Plan 73-02 owns the 200-char `BoundCompanionName` bounding.

## Verification

- **Build:** `dotnet build DeckFlow.sln` — succeeded, **0 warnings, 0 errors**.
- **Tests run (VSTest via Windows `dotnet.exe` — succeeded in this session despite the WSL-unreliability caveat):**
  - `FeatureFlagCatalogTests` + `FeatureFlagStoreSeedTests`: 38 passed, 0 failed.
  - `DeckAnalysisPacketServiceTests` + `AiPlatformExtensionTests` + `AnalysisPromptVariantNoExpertContextTests` + `ResultContractTests`: 83 passed, 0 failed — confirming **byte-identity preserved** (the existing `manabase.commander-castability` byte-identity guard and companion-leak guards are green).
- **Format gate:** `scripts/format-check-changed.sh staged` exited 0 for both commits (changed-lines clean).
- **Line endings:** all touched files are LF; preserved.
- **Compiled assets:** no `wwwroot/js/*.js` staged; only `.cs` files committed.
- **Lockfiles:** `npm ci` restored the pinned TypeScript build tooling (documented build prerequisite); `package-lock.json` was NOT modified (verified via `git status`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Restored TypeScript build tooling**
- **Found during:** Task 1 first build.
- **Issue:** `DeckFlow.Web` build failed — `node_modules/typescript/bin/tsc` missing in the fresh worktree, so the MSBuild `CompileTypeScriptAssets` target errored before C# could compile.
- **Fix:** Ran `npm ci` in `DeckFlow.Web/` to restore the already-pinned devDependencies from the committed lockfile (not a new package; the documented build prerequisite). Lockfile unchanged.
- **Files modified:** none committed (node_modules is gitignored).
- **Commit:** n/a (tooling restore).

**2. [Rule 3 - Blocking] Updated test stub for the widened interface**
- **Found during:** Task 2 build.
- **Issue:** `AiPlatformExtensionTests.StubTestAnalysisVariant` implements `IAnalysisPromptVariant` and no longer satisfied the interface after the signature change (CS0535).
- **Fix:** Added the matching `string? companionName = null` parameter to the stub's `Build`.
- **Files modified:** `DeckFlow.Web.Tests/AiPlatformExtensionTests.cs`.
- **Commit:** 9256bb98 (folded into Task 2, the change that caused it).

## Threat Flags

None — no new security surface. `CompanionName` is stored raw and never rendered in this
plan (T-73-01 mitigation `BoundCompanionName` lands in Plan 73-02 before any render path).
The new flag row is operator-only and seeded FALSE (T-73-02, asserted by `FeatureFlagStoreSeedTests`).

## Known Stubs

None. The `companionName` parameter and `CommandZoneAwarenessFlag` constant are intentionally
unwired contract scaffolding for downstream plans (73-02 reads the request field, 73-03 renders
the command zone). This is documented intent, not an accidental stub — output is byte-identical.

## Commits

- `551f699f` feat(73-01): register analysis.command-zone-awareness flag (seeded OFF)
- `9256bb98` feat(73-01): thread companionName through Build chain + add CompanionName request field

## Self-Check: PASSED

All modified files exist, both task commits (551f699f, 9256bb98) are in history, and the
key tokens are present: `analysis.command-zone-awareness` in both seed blocks, `companionName`
in the interface, `CompanionName` on the request model.
