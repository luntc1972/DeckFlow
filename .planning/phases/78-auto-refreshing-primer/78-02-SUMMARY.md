---
phase: 78-auto-refreshing-primer
plan: 02
subsystem: api
tags: [primer, staleness, controller, zip-resume, resume-without-rebuild, feature-flag]

requires:
  - phase: 78-auto-refreshing-primer (plan 01)
    provides: StaleFlag const, EvaluateStaleness, TryParseDeckTextLocal, DeckPrimerPacketResult.DeckMultisetHash
provides:
  - DeckPrimerRequest.GeneratedPrimerHash hidden round-trip field (ScoreJson pattern, null-coalescing)
  - DeckPrimerViewModel staleness props (StaleDetectionEnabled, GeneratedPrimerHash, IsStale, ChangedCardCount) — init-only, server-computed
  - PacketArtifactStore PrimerZipRestore record + 02-primer-deck-hash.txt zip persistence
  - DeckPrimerController flag-gated re-arm on generate, hash persist on download, resume-without-rebuild staleness on upload (no BuildAsync, no fetch when flag ON)
affects: [78-03, deck-primer]

tech-stack:
  added: []
  patterns:
    - "Resume-without-rebuild: upload path restores old primer text + generation hash/snapshot from zip and computes staleness against the current Step 1 deck WITHOUT calling BuildAsync or the deck loader"
    - "Flag-OFF byte-identity: VM staleness props default false/null and the zip omits 02-primer-deck-hash.txt (NormalizeSections drops null), so OFF render+zip match pre-phase exactly"

key-files:
  created:
    - DeckFlow.Web.Tests/DeckPrimerStalenessControllerTests.cs
  modified:
    - DeckFlow.Web/Models/DeckPrimerRequest.cs
    - DeckFlow.Web/Models/DeckPrimerViewModel.cs
    - DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs
    - DeckFlow.Web/Controllers/DeckPrimerController.cs

key-decisions:
  - "Resume/upload path is the only reachable activation: generate + today's-upload always rebuild a fresh hash, so the banner can only surface when an OLD primer zip meets a CHANGED current deck in one render"
  - "Flag gated via Snapshot().TryGetValue(StaleFlag) default-OFF (never IsEnabled); both the VM staleness state and the zip hash are set ONLY when ON"
  - "Current deck for staleness parsed network-free from the posted DeckText paste only (never DeckUrl); URL/absent current deck suppresses the banner"

patterns-established:
  - "Controller-level test drives a real generate->download->upload sequence to IsStale=true and asserts the load-seam call-count is UNCHANGED across upload (proves no rebuild/fetch)"
  - "Old zips lacking 02-primer-deck-hash.txt resume gracefully (null hash, banner suppressed, old primer still rendered)"

requirements-completed: [PRIMER-03]  # PRIMER-01 (user-visible banner) completes in 78-03 when the view renders it

duration: 15min
completed: 2026-06-29
---

# Phase 78-02: Primer Staleness Controller Wiring Summary

**Resume-without-rebuild activation path: the Deck Primer upload action restores the old primer + generation hash/snapshot from the zip and flags staleness against the current Step 1 deck with no BuildAsync and no upstream fetch — all behind `tool.primer.stale-flag`, byte-identical when OFF (pages and zips).**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-06-29
- **Tasks:** 3
- **Files modified:** 4 + 1 created

## Accomplishments
- `DeckPrimerRequest.GeneratedPrimerHash` round-trip field (ScoreJson null-coalescing pattern; carve-out preserved)
- `DeckPrimerViewModel` gains init-only `StaleDetectionEnabled`, `GeneratedPrimerHash`, `IsStale`, `ChangedCardCount`
- `PacketArtifactStore`: `BuildPrimerZip` persists `02-primer-deck-hash.txt` (only when a hash is supplied → byte-identical OFF); `LoadPrimerFromZip` now returns a `PrimerZipRestore` record (hash + generation snapshot + old primer texts by platform + input summary), still restoring options into the request
- `DeckPrimerController`: optional `IFeatureFlagCache` ctor + `IsStaleDetectionEnabled()` default-OFF gate; generate re-arms the hash (fresh); download persists the hash (flag ON); upload resume-without-rebuild restores the old primer verbatim and computes staleness from the restored hash vs the current Step 1 deck — no `BuildAsync`, no loader/fetch
- Controller test suite drives a real generate→download→upload sequence to `IsStale=true`, asserting the load-seam call-count is unchanged across upload (no rebuild/fetch), plus flag-OFF byte-identity, clean-resume, and old-zip backward compat

## Task Commits

1. **Task 1: DTO + view-model staleness props** - `6951212f` (feat)
2. **Task 2: zip hash persistence + PrimerZipRestore record** - `d250d271` (feat)
3. **Task 3: controller wiring + resume-without-rebuild + tests** - `38ab9a5c` (feat)

## Files Created/Modified
- `DeckFlow.Web/Models/DeckPrimerRequest.cs` - GeneratedPrimerHash round-trip field
- `DeckFlow.Web/Models/DeckPrimerViewModel.cs` - four init-only staleness props
- `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` - 02-primer-deck-hash.txt persist + PrimerZipRestore
- `DeckFlow.Web/Controllers/DeckPrimerController.cs` - flag gate + re-arm/persist/resume wiring
- `DeckFlow.Web.Tests/DeckPrimerStalenessControllerTests.cs` - 5 controller tests (created)

## Decisions Made
- Codex (gpt-5.5) implemented all 3 tasks per forced cross-AI delegation; Claude cross-reviewed (APPROVE, no blocking findings).
- Followed the plan's locked design: resume/upload is the reachable activation; flag default-OFF gates VM state AND zip hash; current deck parsed network-free from DeckText paste only.

## Deviations from Plan
None — implemented as written. The upload FLAG-OFF branch reproduces today's exact behavior (fresh request + LoadPrimerFromZip options + BuildAsync); the FLAG-ON branch is the new no-rebuild path.

## Issues Encountered
None. Build clean (0/0); focused controller tests 5/5; DeckPrimer+FeatureFlag regression filter 90/90.

## Next Phase Readiness
- 78-03 renders the stale banner + Regenerate action in Step 3 of the view (gated on `StaleDetectionEnabled`), adds the hidden `GeneratedPrimerHash` field + the CSS modifier, updates the README, and runs the theme/mobile human-verify checkpoint.
- Flag stays OFF until 78-03 ships the UI and an operator toggles it in prod.

---
*Phase: 78-auto-refreshing-primer*
*Completed: 2026-06-29*
