---
phase: 12-ai-agnostic-url-page-rename
plan: 04
subsystem: artifacts
tags: [chatgpt-artifacts, filename-sanitizer, content-disposition, ai-agnostic, rename]

# Dependency graph
requires:
  - phase: 10-cedh-meta-gap-and-ai-selector
    provides: targetAiPlatform segment + "chatgpt" AI fallback (Phase 10 invariant — both STAY)
provides:
  - AI-agnostic zip filenames from the three Suggest*ZipFileName helpers
  - Updated commander fallback "deck-analysis" (was "deckflow-packet") for SuggestPacketZipFileName
  - Updated mid-segment "-comparison-" (was "-compare2-") for SuggestComparisonZipFileName
  - Updated mid-segment "-cedh-meta-gap-" (was "-cedh-") for SuggestCedhMetaGapZipFileName
  - Closes RENAME-03
affects: [12-05-redirects-and-canonical-urls, 13-class-renames]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Filename helpers stay co-located in ChatGptPacketArtifactStore (class rename deferred to Phase 13 CLASSRENAME-01)"
    - "Content-Disposition header values flow transitively via ASP.NET File(bytes, contentType, fileName) — no controller edits needed when filename helper changes"

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs

key-decisions:
  - "Preserved the 'chatgpt' AI-segment fallback in all three helpers — D-10 explicit invariant carried over from Phase 10 commit 00e5bdd (the AI selector default is independent of artifact branding)."
  - "Did NOT touch LoadFromZip — round-trip load matches by zip CONTENT, not zip filename, so old saved zips continue to load (D-10)."
  - "Did NOT rename the ChatGptPacketArtifactStore class itself — class rename is scoped to Phase 13 CLASSRENAME-01."

patterns-established:
  - "When updating Download artifact filenames, edit ONLY the literal string arguments inside the interpolated CreateSafePathSegment expressions — sanitizer and AI fallback stay untouched."

requirements-completed: [RENAME-03]

# Metrics
duration: ~6min
completed: 2026-05-17
---

# Phase 12 Plan 04: AI-Agnostic Artifact Filenames Summary

**Three Suggest*ZipFileName helpers updated to emit AI-agnostic artifact terminology (deck-analysis / -comparison- / -cedh-meta-gap-) while preserving the Phase 10 'chatgpt' AI-segment fallback invariant.**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-05-17T01:23:00Z (approx)
- **Completed:** 2026-05-17T01:29:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- `SuggestPacketZipFileName`: commander fallback `"deckflow-packet"` → `"deck-analysis"` (D-10).
- `SuggestComparisonZipFileName`: mid-segment `-compare2-` → `-comparison-` (D-10).
- `SuggestCedhMetaGapZipFileName`: mid-segment `-cedh-` → `-cedh-meta-gap-` (D-10).
- `"chatgpt"` AI-segment fallback preserved in all three helpers (D-10 invariant, Phase 10 carryover commit `00e5bdd`).
- Content-Disposition headers on the three Download POST routes auto-update transitively via the ASP.NET `File(bytes, "application/zip", fileName)` helper (D-11) — no controller edits required.
- `DeckFlow.Web` builds clean (0 warnings, 0 errors) under .NET 10.

## Task Commits

1. **Task 1: Update three Suggest*ZipFileName helpers with AI-agnostic artifact segments** — `c87ff5b` (feat)

_Per CLAUDE.md "commit per logical change": all three literal-string edits ship as one cohesive commit because they implement RENAME-03 as a single unit._

## Files Created/Modified

- `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` — Three single-line literal-string edits in the `Suggest*ZipFileName` expression-bodied methods (lines 536-543). No method signatures, no helper bodies, no other code touched.

## Before → After Diffs

```csharp
// SuggestPacketZipFileName
- => $"{CreateSafePathSegment(commanderName, "deckflow-packet")}-analysis-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
+ => $"{CreateSafePathSegment(commanderName, "deck-analysis")}-analysis-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

// SuggestComparisonZipFileName
- => $"{CreateSafePathSegment(commanderName, "deck-comparison")}-compare2-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
+ => $"{CreateSafePathSegment(commanderName, "deck-comparison")}-comparison-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

// SuggestCedhMetaGapZipFileName
- => $"{CreateSafePathSegment(commanderName, "cedh-meta-gap")}-cedh-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
+ => $"{CreateSafePathSegment(commanderName, "cedh-meta-gap")}-cedh-meta-gap-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
```

## Verification

### Automated grep gates (from PLAN acceptance criteria)

| Gate | Expected | Actual | Status |
|------|---------:|-------:|--------|
| `CreateSafePathSegment(commanderName, "deckflow-packet")` count | 0 | 0 | PASS |
| `CreateSafePathSegment(commanderName, "deck-analysis")` count | 1 | 1 | PASS |
| `compare2` count | 0 | 0 | PASS |
| `-cedh-meta-gap-` substring count | ≥1 | 1 | PASS |
| `"chatgpt"` count (AI fallback preserved) | 3 | 3 | PASS |
| `"deck-comparison"` (commander fallback unchanged) count | ≥1 | 1 | PASS |
| `"cedh-meta-gap"` literal count | ≥1 | 1 | PASS |
| Bare `-cedh-(\{|[^m])` mid-segment occurrences | 0 | 0 | PASS |
| `SuggestPacketZipFileName` method signature count | 1 | 1 | PASS |

Note on `-comparison-` raw count: the plan listed an expected count of `1`, but the file contains many unrelated occurrences of the substring `-comparison-` inside other prompt-template / decklist text. The structurally important new occurrence is present once inside `SuggestComparisonZipFileName` (verified manually around line 540), and the old `-compare2-` literal is gone (count 0) — which is the actual invariant the plan was checking.

### Build

```text
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj --nologo --verbosity quiet
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.87
```

(.NET 10 SDK 10.0.300 via Windows `dotnet.exe` — `dotnet` is not on the WSL PATH on this host; the Windows binary is the canonical SDK for this project per CLAUDE.md cross-platform note.)

### Manual round-trip checks (T1, T4, T7)

Per CLAUDE.md user-launched-dev-server policy and the user's persistent preference (`feedback_user_starts_server.md` — "never auto-launch DeckFlow web; ask user to start"), these manual smoke tests must be run by the user after merge:

- **T1 (/deck-analysis):** Generate prompts, click Download. Expect filename pattern `{commander}-analysis-{ai}-{yyyymmdd-hhmmss}.zip`.
- **T4 (/deck-comparison):** Same. Expect `{commander}-comparison-{ai}-{ts}.zip` (was `compare2`).
- **T7 (/cedh-meta-gap):** Same. Expect `{commander}-cedh-meta-gap-{ai}-{ts}.zip` (was bare `cedh`).
- **Edge — empty commander on /deck-analysis:** Expect `deck-analysis-analysis-{ai}-{ts}.zip` (NOT `deckflow-packet-...`).
- **Edge — empty AI selector:** Expect all three filenames to fall back to `chatgpt` segment (UNCHANGED — D-10 invariant).

## Decisions Made

- **Preserve `"chatgpt"` AI-segment fallback:** D-10 explicitly carries forward the Phase 10 invariant (commit `00e5bdd`) — the AI selector default is independent of artifact branding, and ChatGPT remains a valid AI target.
- **Skip LoadFromZip changes:** D-10 — load path matches zip CONTENT, not filename. New filenames are emitted on save; old filenames continue to load. This is the same pattern Phase 10 used when introducing the AI segment.
- **Skip class rename:** `ChatGptPacketArtifactStore` class name STAYS per the plan's `<interfaces>` block — class rename is scoped to Phase 13 (CLASSRENAME-01).

## Deviations from Plan

None - plan executed exactly as written. All three string-literal edits matched the PLAN's TARGET block verbatim.

## Issues Encountered

- **Build-environment workaround (not a code deviation):** The Claude Code worktree does not contain `DeckFlow.Web/package.json` or `DeckFlow.Web/node_modules/` (the former is untracked in git; the latter is gitignored). The MSBuild `CompileTypeScriptAssets` target therefore failed initially with `Cannot find module '...\\node_modules\\typescript\\bin\\tsc'`. Resolved by creating a Windows directory junction (`cmd.exe /c mklink /J node_modules ..\..\..\..\DeckFlow.Web\node_modules`) from the worktree to the main repo's installed `node_modules`. The junction was removed before commit; the build artifact does not need to be tracked. **Repo file content was unaffected** — only the local worktree filesystem was touched, and `git status` showed only the intended .cs file modification at commit time.

## Threat Flags

None — no new security-relevant surface introduced. Per the plan's threat model (T-12-09): all three updated literals are hardcoded server-controlled strings, NOT user input; `commanderName` and `targetAiPlatform` continue to pass through `CreateSafePathSegment` exactly as before. The sanitizer itself is untouched.

## Next Plan Readiness

- RENAME-03 closed. The three Download routes (`/deck-analysis/Download`, `/deck-comparison/Download`, `/cedh-meta-gap/Download`) will now emit AI-agnostic zip filenames the next time the app boots.
- Plan 12-05 (redirects + canonical URLs) is unblocked and remains the next wave's work.

## Self-Check

- [x] File `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` exists and contains the three new literals (verified via grep, build).
- [x] Commit `c87ff5b` exists in `git log` on branch `worktree-agent-a06a50ecea5152795`.
- [x] No STATE.md / ROADMAP.md modifications in this worktree (orchestrator owns those writes).

## Self-Check: PASSED

---
*Phase: 12-ai-agnostic-url-page-rename*
*Completed: 2026-05-17*
