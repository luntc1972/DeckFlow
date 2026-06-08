---
phase: 32-expert-context-selection
plan: 04
subsystem: content-kb-ui
tags: [admin, evergreen, origin-markers, visual-verify, ux-refinement]
requires:
  - "SetEvergreenAsync + ContentSiteIndexRow.IsEvergreen + ClipOrigin (32-01)"
  - ".kb-clip-origin* CSS (32-03)"
provides:
  - "Admin /Admin/ContentKb per-row Evergreen toggle (CSRF + SameOrigin double-guard)"
  - "KbEntryRow.IsEvergreen projection"
  - "What Experts Say panel per-clip origin markers via ClipOriginClass allowlist mapper"
affects:
  - DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml
  - DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs
tech-stack:
  added: []
  patterns:
    - "allowlist CSS-class mapper (defaults to auto) to neutralize attacker-controlled ClipOrigin"
    - "admin toggle mirrors SetVisibility double-guard exactly"
key-files:
  created: []
  modified:
    - DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs
    - DeckFlow.Web/Models/AdminContentKbViewModel.cs
    - DeckFlow.Web/Views/AdminContentKb/Index.cshtml
    - DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml
key-decisions:
  - "AdminContentKbViewModel.cs authorized into scope (plan files_modified omitted it; KbEntryRow lives there)"
  - "MED-7: panel origin CSS suffix produced by ClipOriginClass allowlist mapper (_ => auto) so a crafted ClipOrigin from an uploaded zip cannot inject a class"
  - "Visual-verify checkpoint surfaced 8 follow-on UX/layout items (see below); all fixed + user-approved"
requirements-completed: [SEL-05, SEL-06]
duration: ~2 hours (incl. dogfood UX refinement)
completed: 2026-06-08
---

# Phase 32 Plan 04: Admin Evergreen Toggle + Panel Origin Markers Summary

Closed the admin + result surfaces: a per-row Evergreen toggle on `/Admin/ContentKb` (same `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator` double-guard as SetVisibility, "Evergreen status updated." banner) and per-clip origin markers in the *What Experts Say* panel, with the origin CSS-class suffix produced by a `ClipOriginClass` allowlist mapper (defaults to `auto`) so a crafted `ClipOrigin` from an uploaded zip cannot inject a class (MED-7). The mandatory 2-viewport visual-verify checkpoint passed after a round of dogfood-driven UX refinements.

- **Tasks:** 2 code (admin toggle, panel markers) + 1 human-verify checkpoint
- **Commits (core):** `12b6f0d`, `57cdf6d`
- **Executor:** Codex (gpt-5.4, medium) — Claude review

## Build / Test Results (full phase regression)

- `dotnet build DeckFlow.Web` — succeeded, 0 errors
- `dotnet test DeckFlow.Core.Tests` — **270 passed / 0 failed**
- `dotnet test DeckFlow.Web.Tests` — **608 passed / 5 skipped (PG) / 0 failed** (613 total)
- AdminContentKbControllerTests — 15/15 pass

## Visual Verification (Task 3 — human-verify checkpoint)

**User approved 2026-06-08** after walking the 6-step checklist at desktop + mobile across themes. Dogfooding surfaced 8 follow-on items, all fixed and re-verified:

| Commit | Issue (found in visual verify) | Fix |
|--------|--------------------------------|-----|
| `2159559` | Mobile: pin chips / tray labels ran off-screen | `.kb-chip__label` truncation + `min(16rem,60vw)` |
| `0633f84` | Expert Context floated above the step tabs | moved below the tabs |
| `9160596` | Add-context affordance not discoverable | accent callout + explainer subhead + search icon |
| `dd51eb3` | **Pre-existing:** step-footer nav buttons overflowed on mobile | `.chatgpt-step-footer { flex-wrap: wrap }` |
| `a587cf2` | KB only a nav link, no home presence | flag-gated Knowledge Base home tile (Reference group) |
| `023ccfa` | **Pre-existing:** KB filter input ~160px tall on mobile (column flex-basis) | `flex: 0 0 auto` in the 600px block |
| `c5a5346` / `2466aec` | KB header link redundant; missing from tool dropdown | removed header link; added to Reference dropdown |
| `45127e0` | Expert Context placement unclear in flow | moved into Step 2 as a collapsed accordion |

## Bonus Enhancements (user-requested during review)

- `5a5e428` — **Prompt-size indicator + paste-limit warning**: analysis.txt shows its size; warns past `AiPlatform.PasteWarningBytes` (Gemini = 32 KB; ChatGPT/Claude unlimited). Addresses the real overflow risk (expert context itself is hard-capped at ≤5 clips / 4.5 KB).
- `e6dd201` — **Set-upgrade follow-up JSON prompt**: mirrors the analysis "Optional Follow-up JSON Refresh Prompt" on Step 4 for `set_upgrade_report`.

## Deviations from Plan

**[Rule 2 - Missing critical, authorized] KbEntryRow file** — AdminContentKbViewModel.cs (where KbEntryRow lives) was absent from the plan's files_modified; authorized into scope to add `IsEvergreen`. Commit `12b6f0d`.

**[Rule 2 - Latent regression, found at phase close] Core.Tests fake** — 32-01 grew `IContentSiteIndexStore` but only `DeckFlow.Core` was built then (not `Core.Tests`), so `RunDistillAsyncTests.FakeContentSiteIndexStore` was left missing `SetEvergreenAsync` (CS0535). Caught by the full Core regression at close-out; fixed in `eb125f9`. **Lesson:** per-plan verification should build the matching test project, not just the production project, when an interface changes.

**Total deviations:** 2 (both authorized). **Impact:** none on shipped behavior.

## Reviewer Notes (Claude)

- MED-7 verified: `kb-clip-origin--@ClipOriginClass(clip.ClipOrigin)` only; zero raw `@clip.ClipOrigin` in the class; mapper `_ => "auto"`.
- SetEvergreen double-guard mirrors SetVisibility; banner + KbEntryRow.IsEvergreen present.
- All UX-refinement CSS stayed in `site-common.css` (layout convention); no `site.css`/theme edits; compiled JS not committed (gitignore convention restored in 32-03).

## Issues Encountered

None unresolved. Latent Core.Tests break fixed at close-out (see deviations).

## Phase Completion

SEL-01..SEL-06 all delivered and verified. Visual checkpoint approved. Full regression green (Core 270, Web 608/5-skip). Phase 32 complete.

## Self-Check: PASSED
