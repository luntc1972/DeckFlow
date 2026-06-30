---
phase: 78-auto-refreshing-primer
plan: 03
subsystem: ui
tags: [primer, staleness, ui, css, accessibility, checkpoint, feature-flag]

requires:
  - phase: 78-auto-refreshing-primer (plan 02)
    provides: DeckPrimerViewModel staleness props (StaleDetectionEnabled, IsStale, ChangedCardCount, GeneratedPrimerHash), resume-without-rebuild controller path
provides:
  - Stale caution banner in Deck Primer Step 3 (role=status, sr-only "Status:" prefix, ⚠ glyph, exact pluralized microcopy, Regenerate primer submit)
  - Flag-gated hidden GeneratedPrimerHash round-trip field
  - .deck-restored-notice--stale token-only CSS modifier (var(--gold-warning)) in site-common.css
  - README documentation of tool.primer.stale-flag
  - Render tests incl. flag-OFF byte-identity baseline
affects: [deck-primer]

tech-stack:
  added: []
  patterns:
    - "Flag-gated banner + hidden field render NOTHING when StaleDetectionEnabled is false → Step 3 byte-identical OFF (proven by baseline byte-comparison render test)"
    - "Caution tone via one net-new token-only CSS modifier in site-common.css inherited by all guild themes — no per-theme edits"

key-files:
  created:
    - DeckFlow.Web.Tests/DeckPrimerBannerRenderTests.cs
  modified:
    - DeckFlow.Web/Views/Deck/DeckPrimer.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
    - README.md

key-decisions:
  - "Banner gated on StaleDetectionEnabled && IsStale && primer-present (W1) so it never shows without a visible primer to mark stale"
  - "Regenerate is a plain type=submit posting the existing primer form (the existing explicit generate action) — no new endpoint, no JS rebuild (PRIMER-03)"
  - "ChangedCardCount rendered via Razor (HTML-encoded); card names never interpolated (T-78-07)"
  - "Mobile flex-wrap NOT added — operator verify confirmed the button does not crowd the message at ~390px"

patterns-established:
  - "Accessible status banner: role=status (not alert) + sr-only prefix + glyph + literal text so meaning is never color-only"
  - "Flag-OFF byte-identity proven by a baseline byte-comparison render test, not just string-absence asserts"

requirements-completed: [PRIMER-01]

duration: 35min
completed: 2026-06-29
---

# Phase 78-03: Stale Banner UI + Visual Verify Summary

**Accessible caution-gold stale banner in Deck Primer Step 3 ("Deck changed since this primer was generated — N cards differ. Regenerate to refresh the primer.") with a real Regenerate submit, flag-gated and byte-identical when OFF, verified live across Classic/Azorius/Nyx at desktop + mobile.**

## Performance

- **Duration:** ~35 min (incl. /simplify pass + CI fix + live verify)
- **Completed:** 2026-06-29
- **Tasks:** 2 (Task 1 auto + render test; Task 2 README + blocking human-verify checkpoint)
- **Files modified:** 3 + 1 created

## Accomplishments
- Stale banner block in Step 3: `role="status"`, sr-only "Status:" prefix, ⚠ glyph, exact pluralized microcopy (>=2 / ==1 / null), Regenerate primer submit — gated on StaleDetectionEnabled && IsStale && PrimerPromptText present
- Flag-gated hidden `GeneratedPrimerHash` round-trip field (mirrors ScoreJson "only when present")
- `.deck-restored-notice--stale` token-only modifier (var(--gold-warning), var(--ink)) in site-common.css — no per-theme edits
- README documents `tool.primer.stale-flag` (seeded OFF; resume-only activation; never auto-rebuilds/re-fetches)
- `DeckPrimerBannerRenderTests` cover OFF byte-identity baseline + ON fresh/stale + pluralization + W1 primer-present gate + a11y attributes
- Operator visual verify PASSED: caution rail + glyph + correct count, Regenerate clears the banner, clean-resume shows no banner, reorder/printing-swap stays fresh, flag-OFF byte-identical + zip without 02-primer-deck-hash.txt — across Classic/Azorius/Nyx at desktop + mobile

## Task Commits

1. **Task 1: banner + hidden field + CSS modifier + render test** - `64f33dd3` (feat)
2. **Task 2: README** - `f7678c04` (docs)
   - Plus quality + CI follow-ups on the same plan: `daacc1cd` (/simplify), `7565eb24` + `45a9f11b` (CI green)

## Files Created/Modified
- `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml` - banner block + hidden hash field
- `DeckFlow.Web/wwwroot/css/site-common.css` - .deck-restored-notice--stale modifier
- `DeckFlow.Web.Tests/DeckPrimerBannerRenderTests.cs` - render tests (created)
- `README.md` - tool.primer.stale-flag documentation

## Decisions Made
- Codex implemented Task 1 (cross-reviewed APPROVE); Claude ran /simplify (3 cleanups) + drove the CI fixes and the live verify.
- No mobile flex-wrap added — operator confirmed no button crowding at ~390px.

## Deviations from Plan
None functional. Two CI failures surfaced post-push and were fixed: ToolFlagSeedConsistencyTests count (the 16th tool flag, dark-launched) and a DI ValidateOnBuild gap (primer parsers registered only in Program.cs, not the AddDeckFlowExtensions composition — moved into the extension). Both unrelated to the banner UI itself.

## Issues Encountered
- Local full-suite run crashes on the Dapper-PG integration block (known WSL VSTest instability), which masked two failures that only CI surfaced (seed-consistency count + DI composition). Both fixed; CI run 28412906070 is green.

## Next Phase Readiness
- Phase 78 complete (all 3 plans). Cycle 13 (phases 75-78) is now fully implemented.
- `tool.primer.stale-flag` seeded OFF in prod — an operator flips it on to surface the banner once deployed.
- ⚠ Prod deploy is MANUAL (autodeploy OFF). Branch plan/cycle-13-deck-eval green and pushed; merge to main + deploy + flag flip remain.

---
*Phase: 78-auto-refreshing-primer*
*Completed: 2026-06-29*
