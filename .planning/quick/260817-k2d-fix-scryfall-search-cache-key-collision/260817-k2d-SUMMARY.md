---
phase: quick-260817-k2d
plan: 01
status: complete
subsystem: scryfall
tags: [cache, memory-cache, typeahead, correctness, tdd]
dependency-graph:
  requires: [shared IMemoryCache singleton registered at DeckFlow.Web/Program.cs:71]
  provides: [per-query cache-key namespaces for the three Scryfall name-search paths]
  affects:
    - DeckFlow.Web/Services/Scryfall/CardSearchService.cs
    - DeckFlow.Web/Services/Scryfall/ScryfallCommanderSearchService.cs
    - DeckFlow.Web.Tests/ScryfallSearchCacheIsolationTests.cs
tech-stack:
  added: []
  patterns: ["private const string cache-key prefix per class, applied at every read and write site including negative-cache writes"]
key-files:
  created:
    - DeckFlow.Web.Tests/ScryfallSearchCacheIsolationTests.cs
  modified:
    - DeckFlow.Web/Services/Scryfall/CardSearchService.cs
    - DeckFlow.Web/Services/Scryfall/ScryfallCommanderSearchService.cs
---

# Summary — Quick 260817-k2d

## What changed

Three distinct cache-key namespaces replace two colliding bare keys in the shared `IMemoryCache`:

| Class / method | Prefix | Note |
|---|---|---|
| `ScryfallCardSearchService.SearchAsync` | `card-search:` | was bare `atraxa` |
| `ScryfallCardSearchService.SearchCommandersAsync` | `commander:` | value unchanged, now a named const |
| `ScryfallCommanderSearchService.SearchAsync` | `commander-legendary:` | was bare `atraxa` |

Each prefix is a `private const string` on its own class and is applied at every read and write site,
including the 404 negative-cache writes.

## Why three, not two

`SearchCommandersAsync` queries `is:commander name:{q}`; `ScryfallCommanderSearchService.SearchAsync`
queries `is:commander type:legendary (creature|vehicle) name:{q}`. Different result sets. Giving the
latter the existing `commander:` prefix would have relocated the identical collision onto that pair
instead of fixing it — so the third namespace is load-bearing, not cosmetic.

## Verification

- **RED, mutation-proven.** With the two service files reverted and the new tests in place:
  `Failed: 2, Passed: 1`. The two failures are `CardSearchThenCommanderSearch_UsesSeparateCacheEntries`
  and `CommanderSearchThenCardSearch_UsesSeparateCacheEntries`, failing on the upstream-invocation
  counter (the second service served the first's cached list without calling upstream).
- Case C (`CommanderSearchAndLegacyCommanderSearch_UseSeparateCacheEntries`) **passes before the fix**
  and that is correct — pre-fix those two keys were already `commander:atraxa` vs bare `atraxa`. C is a
  guard against the naive `commander:` fix, not a reproduction of the shipped bug. Recorded explicitly
  so nobody later reads it as a void RED case.
- **GREEN:** 3/3 targeted tests pass with the fix restored.
- **Full suite:** `DeckFlow.Web.Tests` — Passed 2327, Failed 0, Skipped 16, Total 2343.
  The 16 skips are the `[PostgresFact]` Testcontainers integration tests (Docker unavailable in WSL);
  unrelated to this change, which touches no persistence.
- **Build:** clean. Only pre-existing `NU1903` warnings (SSH.NET advisory), no new warnings.
- **Line endings:** no churn — `git diff --numstat` and `--ignore-all-space --numstat` both total 19.

## Deviations from plan

Codex (`gpt-5.6-luna`, effort low) produced the service fix and test file but could not run any tests —
`dotnet: command not found` in its sandbox — and correctly declined to claim RED it had not observed.
Two compile defects were then caught in review and fixed in-place as trivial review corrections:

1. missing `using Xunit;` (every `[Fact]` and `Assert` failed to resolve, CS0246 ×6);
2. `Assert.Equal(1, count, "message")` — xUnit 2.9.3 has no message overload on `Assert.Equal`, and no
   other test in the repo used that shape. Replaced with the two-arg assert plus a `// Why:` comment
   carrying the explanation.

The RED proof was performed by the reviewer, not the executor: service files reverted via a saved
patch, suite re-run, patch re-applied, green re-confirmed.

## Environment note

A fresh git worktree has no `DeckFlow.Web/node_modules`, so the MSBuild `tsc` step fails the build
before any C# compiles. Fixed with a junction to the main checkout's copy:
`cmd.exe /c mklink /J node_modules 'C:\...\deckflow\DeckFlow.Web\node_modules'` run from the
worktree's `DeckFlow.Web`. It is gitignored, so it never shows up in `git status`.

## Follow-ups (not done here)

Deliberately out of scope, tracked in the cache-layer research
(`/mnt/d/claude_doc/deckflow/spike/2026-08-17-upstream-cache-layer-research.md`): the shared
`IMemoryCache` still has **no `SizeLimit`**, and the two highest-cardinality consumers on it are
Spellbook (keyed by the whole 99-card decklist) and these autocomplete paths (keyed by every prefix
typed). That is Tier 0 item 4 of the research, not this fix.
