---
phase: 73-deck-analysis-command-zone-awareness-ad-hoc-trunk-main
plan: 04
subsystem: deck-analysis / UI + controller plumbing
tags: [command-zone, companion, feature-flag, razor, blast-radius, playwright, checkpoint-pending]
requires:
  - "73-01: analysis.command-zone-awareness flag + DeckAnalysisRequest.CompanionName form-bound field"
  - "73-02: service resolves companion (designator-wins) + enriches commander zone behind the flag"
  - "73-03: companion rendered in all 3 prompt variants when the flag is ON"
provides:
  - "Flag-gated single companion designator input on the deck-analysis Step 1 form (parity with the manabase companion input, no hidden mirror)"
  - "Centralized controller flag plumbing: one IsCommandZoneAwarenessEnabled() helper stamps CommandZoneAwarenessEnabled on all 11 DeckAnalysisViewModel render paths (Codex MED-1)"
  - "README + in-app Help documentation of the analysis.command-zone-awareness flag and the companion designator"
  - "Playwright smoke (flag ON renders input[name=CompanionName], flag OFF absent)"
affects:
  - DeckFlow.Web/Models/DeckAnalysisViewModel.cs
  - DeckFlow.Web/Controllers/DeckPacketController.cs
  - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Web/Help/deck-analysis.md
  - README.md
  - DeckFlow.Web.Tests/DeckPacketControllerTests.cs
  - DeckFlow.Web/e2e/deck-analysis-command-zone.spec.ts
tech-stack:
  added: []
  patterns:
    - "Single flag-read helper stamped on every view-model construction site (blast-radius control, Codex MED-1)"
    - "Server-computed init-only view-model flag (not form-bound) — crafted POST cannot enable the feature (T-73-05)"
    - "Exactly one name=CompanionName field in the single deck form — no hidden mirror (DeckAnalysis has ONE form, unlike Manabase's two)"
    - "Layout CSS in site-common.css, never a per-theme fork (theme constraint)"
key-files:
  created:
    - DeckFlow.Web/e2e/deck-analysis-command-zone.spec.ts
    - .planning/phases/73-deck-analysis-command-zone-awareness-ad-hoc-trunk-main/73-04-SUMMARY.md
  modified:
    - DeckFlow.Web/Models/DeckAnalysisViewModel.cs
    - DeckFlow.Web/Controllers/DeckPacketController.cs
    - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/Help/deck-analysis.md
    - README.md
    - DeckFlow.Web.Tests/DeckPacketControllerTests.cs
decisions:
  - "All 11 view-model sites stamp the flag via setting CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled() rather than a wrapping factory — the 11 initializers set very different property sets, so a shared factory would be unwieldy; the single helper is the one source of truth and is referenced by every site (error and upload paths included)."
  - "Controller references DeckAnalysisPacketService.CommandZoneAwarenessFlag (internal const, same assembly) instead of re-typing the string literal — avoids flag-key drift between the service and the controller."
  - "IFeatureFlagCache injected as an OPTIONAL ctor param (= null) mirroring the service, so existing controller tests that construct it with 5 positional args still compile; DI supplies the real cache in production."
  - "New layout uses a dedicated .deck-analysis-overrides class in site-common.css (mirrors .manabase-overrides) rather than reusing the manabase-named class — avoids cross-tool CSS coupling while keeping all layout CSS in the shared file."
metrics:
  duration_minutes: 35
  completed: 2026-06-27
  tasks: 2 of 3 (Task 3 is a blocking human-verify checkpoint — PENDING)
  files_changed: 8
---

# Phase 73 Plan 04: Surface Command-Zone Companion in Deck-Analysis UI Summary

Surfaced the manual companion designator in the `/deck-analysis` Step 1 form so the awareness
feature works for Archidekt and pasted-text decks (which never emit `DetectedCompanionName`). The
input is gated on `analysis.command-zone-awareness`; when the flag is OFF the page is byte-identical
to baseline. The typed value round-trips through the already-form-bound `Request.CompanionName`
(Plan 01) and the service already prefers the designator over auto-detect (Plan 02), so no service
change was needed here — this plan is UI + controller plumbing + docs + tests.

**Status: implementation tasks complete and committed; the blocking operator visual sign-off
(Task 3, `checkpoint:human-verify`) is PENDING. The plan is NOT marked fully complete.**

## What Was Built

### Task 1 — Centralized flag plumbing + single companion input + docs (commit 17eb642b)

- **DeckAnalysisViewModel**: added `bool CommandZoneAwarenessEnabled { get; init; }` (default false),
  server-computed and init-only so it is never form-bound (T-73-05 — a crafted POST cannot enable
  the feature).
- **DeckPacketController**: injected an optional `IFeatureFlagCache? flagCache = null` (mirrors the
  service), added a single private `IsCommandZoneAwarenessEnabled()` helper using the default-OFF
  snapshot pattern (`Snapshot().TryGetValue(DeckAnalysisPacketService.CommandZoneAwarenessFlag, out
  var enabled) && enabled`), and stamped `CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled()`
  on **all 11** `new DeckAnalysisViewModel` sites — the GET, the POST success/validation/upstream
  paths, the download error paths, and every upload path (Codex MED-1: no render path can show the
  wrong UI).
- **DeckAnalysis.cshtml**: inside the single Step 1 deck form, gated on `Model.CommandZoneAwarenessEnabled`,
  rendered a collapsible `<details class="deck-analysis-overrides">` containing exactly ONE labeled
  `<input name="CompanionName">` pre-filled with `@Model.Request.CompanionName`. No hidden mirror —
  `grep -c 'name="CompanionName"'` returns 1 (a second field would duplicate-bind on POST since
  DeckAnalysis has a single form, unlike Manabase's two-form layout).
- **site-common.css**: added `.deck-analysis-overrides` layout rules (mirrors `.manabase-overrides`)
  in the shared common file, never a per-theme fork.
- **README.md**: added a bullet near the `analysis.*` flag docs describing `analysis.command-zone-awareness`
  (default OFF, byte-identical when off, names the full command zone + companion as side metadata)
  and the companion designator.
- **Help/deck-analysis.md**: added a "Companion designator (optional)" subsection under Step 1.
- **DeckPacketControllerTests**: added 4 tests — GET stamps `CommandZoneAwarenessEnabled = true`
  when the flag is ON, `false` when OFF, `false` when the flag cache is missing (null), and the
  validation-error path also stamps `true` when ON (proves error paths carry the flag, MED-1).

### Task 2 — Playwright smoke (commit 532facfa)

- New `DeckFlow.Web/e2e/deck-analysis-command-zone.spec.ts`: toggles `analysis.command-zone-awareness`
  via `/Admin/Flags` (reusing the shared admin lock + per-test forwarded-IP throttle pattern from the
  other `/Admin/*` specs), then asserts case (1) flag ON → `input[name="CompanionName"]` has count 1
  and is visible after opening the `<details>`; case (2) flag OFF → the input and the
  `.deck-analysis-overrides` block have count 0. The `afterEach` restores the flag to its default-OFF
  state. Headless only (server via `scripts/run-web-test.sh`); no Windows-host browser.

## Verification

- **Build:** `dotnet.exe build DeckFlow.Web` and `dotnet.exe build DeckFlow.Web.Tests` — **0 warnings,
  0 errors** each.
- **Targeted controller tests:** `--filter DeckPacketControllerTests` → **14 passed, 0 failed**
  (10 prior + 4 new flag-stamping tests).
- **Full Web suite:** `dotnet.exe test DeckFlow.Web.Tests` → **927 passed, 12 skipped, 0 failed**
  (1m09s). No admin-e2e flake this run.
- **Single-input invariant:** `grep -c 'name="CompanionName"' DeckAnalysis.cshtml` → **1**.
- **Format gate:** `scripts/format-check-changed.sh staged` → exit 0 (changed-lines clean). Initial
  run flagged the 10 nested view-model sites at 12-space indent where 16 was required (the GET site
  at 12 was correct); fixed by indenting only those 10 lines to 16 before committing. No `--no-verify`.
- **Carve-outs:** no C# raw-string literals re-indented; switch expressions / attribute placement
  untouched; LF preserved on all touched files (README, Help, view, css were already LF).
- **Compiled assets:** no `wwwroot/js/*.js` staged; only source files committed.
- **Playwright:** NOT executed live in this run — that is the Task 3 operator checkpoint (the
  orchestrator directed the executor not to drive a live browser). The spec is authored and committed;
  the operator runs it (and the visual sign-off) at the checkpoint.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Corrected indentation of the 10 nested view-model stamp lines**
- **Found during:** Task 1 (format gate).
- **Issue:** the `replace_all` that stamped `CommandZoneAwarenessEnabled = IsCommandZoneAwarenessEnabled()`
  inserted the line at 12-space indent; the 10 sites nested inside try/catch blocks require 16-space
  indent (the GET site at 12 was correct). The changed-lines format gate failed on those 10 lines.
- **Fix:** re-indented only the 10 nested lines to 16 spaces (matched on the preceding 16-space
  `ActiveTab` context so the 12-space GET site stayed untouched); re-ran the gate clean.
- **Files modified:** DeckFlow.Web/Controllers/DeckPacketController.cs.
- **Commit:** 17eb642b.

Otherwise the plan executed as written.

## Threat Flags

None — no new security surface beyond the planned `<threat_model>`. T-73-01 (companion → prompt
injection) stays mitigated upstream by Plan 02's `BoundCompanionName` (single-line collapse + trim +
200-char cap) and Plan 03's Claude XML-escape; the Razor input echoes `Request.CompanionName` in a
`value` attribute, which Razor HTML-encodes by default. T-73-05 (crafted POST setting the flag) is
mitigated by `CommandZoneAwarenessEnabled` being server-computed and init-only (not form-bound),
covered by the new `..._WhenFlagOff` / `..._WhenFlagCacheMissing` controller tests. No
package-manager installs (T-73-SC).

## Known Stubs

None. The companion designator is wired end-to-end: input → `Request.CompanionName` → service
designator-wins resolution → prompt rendering (Plans 01-03). It is only visible when the flag is ON.

## Checkpoint Pending

Task 3 (`type="checkpoint:human-verify"`, `gate="blocking"`) — operator cross-theme + mobile visual
sign-off — is PENDING. The plan is not fully complete until the operator approves. Verification steps
are reproduced in the execution result returned to the orchestrator.

## Commits

- `17eb642b` feat(73-04): surface flag-gated companion designator in deck-analysis UI
- `532facfa` test(73-04): add Playwright smoke for command-zone companion input

## Self-Check: PASSED

All 8 touched files exist; both task commits (17eb642b, 532facfa) are in history; the key tokens are
present: `CommandZoneAwarenessEnabled` in the view model + controller (11 sites) + tests,
`name="CompanionName"` (count 1) gated on `Model.CommandZoneAwarenessEnabled` in the view,
`.deck-analysis-overrides` in site-common.css, `analysis.command-zone-awareness` in README and the
e2e spec, and the companion section in Help/deck-analysis.md.
