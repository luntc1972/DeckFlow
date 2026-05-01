---
phase: 03-tech-debt-cleanup
plan: 03-02
subsystem: services
tags: [public-surface, cleanup, deletion, tech-debt]
requires:
  - phase: 03-tech-debt-cleanup
    provides: TD-02 ctor collapse (03-01) removed all callers of the Null* factories
provides:
  - Production assembly's public surface no longer exposes NullHttpClientFactory or NullScryfallRestClientFactory
  - DeckFlow.Web/Services/Http/ no longer contains test-only types
affects:
  - DeckFlow.Web/Services/Http/NullHttpClientFactory.cs (deleted)
  - DeckFlow.Web/Services/Http/NullScryfallRestClientFactory.cs (deleted)
tech-stack:
  added: []
  patterns:
    - Delete-only cleanup; no migration to TestDoubles per D-01
key-files:
  created:
    - .planning/phases/03-tech-debt-cleanup/03-02-SUMMARY.md
  deleted:
    - DeckFlow.Web/Services/Http/NullHttpClientFactory.cs
    - DeckFlow.Web/Services/Http/NullScryfallRestClientFactory.cs
key-decisions:
  - "Followed D-01: pure deletion, no migration to DeckFlow.Web.Tests/TestDoubles. The existing Fake* doubles + TestServiceFactory (from 03-01) cover all real test needs."
patterns-established:
  - "Wave 2 deletion gate: grep audit → file delete → clean build. No replacement infrastructure needed when 03-01 fully removed callers."
requirements-completed:
  - TD-01
metrics:
  duration: ~5m
  completed: 2026-05-01
---

# Phase 03 Plan 02 Summary

**Both Null* orphan factory files deleted; `dotnet build DeckFlow.sln` clean (0 errors, 0 warnings); production assembly public surface no longer leaks test-only types (ROADMAP Phase 03 SC #1 satisfied).**

## Performance

- **Duration:** ~5m
- **Completed:** 2026-05-01
- **Tasks:** 1
- **Files modified:** 2 deletions

## Pre-delete grep audit

```
$ grep -rn "NullHttpClientFactory\|NullScryfallRestClientFactory" \
    DeckFlow.Web DeckFlow.Web.Tests DeckFlow.Core DeckFlow.CLI \
    --include="*.cs" \
    | grep -v "DeckFlow.Web/Services/Http/Null" \
    | grep -v "/bin/" | grep -v "/obj/"
(empty)
```
Zero non-self references — D-02 sequence respected (03-01 already removed every caller).

## Deletions

```
$ git rm DeckFlow.Web/Services/Http/NullHttpClientFactory.cs
rm 'DeckFlow.Web/Services/Http/NullHttpClientFactory.cs'
$ git rm DeckFlow.Web/Services/Http/NullScryfallRestClientFactory.cs
rm 'DeckFlow.Web/Services/Http/NullScryfallRestClientFactory.cs'
```

## Post-delete state

`DeckFlow.Web/Services/Http/` now contains only `ResiliencePipelineFactory.cs`. The plan's §verification mentioned `IScryfallRestClientFactory` and `ScryfallRestClientFactory` as folder neighbors, but a grep audit found those types actually live in `DeckFlow.Web/Services/` (not under `Http/`). No action needed — plan note was slightly off; no production code is affected.

## Build

`dotnet build DeckFlow.sln` from this orchestrator's WSL2 shell:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Task Commits

- Implementation: `0949b23` (`tech-debt(03-02): delete NullHttpClientFactory and NullScryfallRestClientFactory orphans (TD-01)`)
- Summary commit: separate docs commit (next).

## Follow-Up

- Brownfield invariant: post-deploy `https://www.deckflow.gg` must still respond 200 on `/`, `/feedback`, `/help`, `/about`, `/sync`. This is shared with 03-04's post-deploy human verification — covered by the same Render redeploy.
