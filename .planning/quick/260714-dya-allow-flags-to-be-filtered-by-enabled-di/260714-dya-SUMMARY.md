---
quick_id: 260714-dya
description: allow flags to be filtered by enabled/disabled
status: complete
date: 2026-07-14
branch: quick/flags-status-filter
commit: 998f1ccb
---

# Summary — Filter /Admin/Flags by enabled/disabled

## What shipped

Added a status chip group (**All statuses / Enabled / Disabled**) to
/Admin/Flags that filters rows by their current on/off state and composes
(AND) with the existing prefix search box and namespace chips. Status
selection persists in sessionStorage (`deckflowAdminFlagStatus`) matching the
existing search/prefix persistence. Count line and empty-row behavior remain
correct under combined filters.

## Files changed (commit 998f1ccb)

- `DeckFlow.Web/Views/AdminFlags/Index.cshtml` — status chip group; rows carry `data-flag-enabled`
- `DeckFlow.Web/wwwroot/ts/flag-filter.ts` — pure `statusMatches(enabled, status)` predicate
- `DeckFlow.Web/wwwroot/ts/admin-flags.ts` — status chip wiring, per-group active-chip sync, persistence
- `DeckFlow.Web/ts-tests/flag-filter.test.ts` — 4 new statusMatches unit tests
- `DeckFlow.Web/e2e/admin-flags-filter.spec.ts` — status coverage; also repaired drifted contract (old `tool.` label/chip) and made assertions data-driven (dataset-independent)
- `README.md` — admin flags filter paragraph updated

No C# logic changes (controller already exposed `flag.Enabled`).

## Verification

- `dotnet build` DeckFlow.sln: 0 errors (1 pre-existing CS8602 warning)
- `npx tsc --noEmit`: clean; vitest 34/34
- Playwright `admin-flags-filter.spec.ts`: 4/4 (chromium desktop + mobile)
- Blind verifier (foreman-verifier): PASS 6/6 criteria, incl. live sessionStorage reload check and combined-filter empty-state
- Screenshots at 1280x900 and 390x844: chips render and filter correctly, mobile card layout intact
- EOL churn check: all touched files LF, byte-preserved

## Execution notes

- Codex (gpt-5.4 medium) implemented; 1 fix round: initial e2e asserted a
  non-empty disabled∩service. intersection — dataset-dependent; rewritten
  data-driven.
- First e2e run failed against a STALE Windows dotnet server on 5173 reused
  via `reuseExistingServer` — WSL `ss` cannot see Windows listeners; use
  `cmd.exe netstat` to probe. Kill stale server before local e2e.

## Known limitations (pre-existing)

- Main admin-flags e2e test remains CI-skipped (admin-lock flake,
  `.planning/debug/e2e-admin-beforeeach-timeout.md`) — status assertions run
  locally only.
- sessionStorage persistence has no automated test (verified live).
