---
phase: 12-ai-agnostic-url-page-rename
plan: 01
subsystem: routing
tags: [aspnet-core, rewrite-middleware, url-rename, 301-redirect, mvc-routing]

# Dependency graph
requires:
  - phase: 09-ai-platform-selector
    provides: per-AI dispatch + AiPlatform selector that motivates dropping "ChatGPT" branding from URLs
  - phase: 11-web-design-guidelines-audit-fixes
    provides: site-common.css cross-cutting rules section (used by later Phase 12 plans, not this one)
provides:
  - "3 new AI-agnostic URL slugs live on DeckController: /deck-analysis, /deck-comparison, /cedh-meta-gap"
  - "9 permanent (301) redirects from legacy chatgpt-* URLs centralized in Program.cs UseRewriter block"
  - "Pipeline-order invariant codified: UseRewriter inserted after UseForwardedHeaders so 301 Location honors X-Forwarded-Proto"
affects: [12-02-view-rename, 12-03-page-labels, 12-04-artifact-sanitizer, 12-05-docs-sweep, 13-class-rename]

# Tech tracking
tech-stack:
  added: [Microsoft.AspNetCore.Rewrite (shared framework, no NuGet add)]
  patterns:
    - "Centralized URL redirect block via UseRewriter + AddRedirect, anchored regex with no backreferences (T-12-01 mitigation)"
    - "Hardcoded literal redirect targets — no user input interpolated into Location header"

key-files:
  created: []
  modified:
    - DeckFlow.Web/Program.cs
    - DeckFlow.Web/Controllers/DeckController.cs

key-decisions:
  - "Used UseRewriter middleware (D-03) instead of per-action [HttpGet] redirect attributes — keeps DeckController from accumulating 12+ thin redirect actions"
  - "Inserted UseRewriter immediately after UseForwardedHeaders so 301 Location honors X-Forwarded-Proto (D-05); HSTS / exception-handler / security-headers / HTTPS-redirect blocks remain downstream"
  - "Preserved cEDH specificity in slug (/cedh-meta-gap, not /meta-gap) per D-02"
  - "Action method names (ChatGptPackets, ChatGptDeckComparison, ChatGptCedhMetaGap) and return View(\"ChatGpt…\") arguments deliberately left unchanged — they ship in Plan 02 alongside the .cshtml view file renames (D-13/D-14)"

patterns-established:
  - "Pattern: URL slug renames in this codebase land as a 2-commit unit — (1) Program.cs UseRewriter add, (2) Controller route-attribute swap — one logical change per commit per CLAUDE.md"
  - "Pattern: 301 redirect regexes use ^…$ anchors and avoid backreferences, so per-request cost stays constant and open-redirect is structurally impossible (T-12-01/T-12-03)"

requirements-completed: [RENAME-01]

# Metrics
duration: ~25min
completed: 2026-05-17
---

# Phase 12 Plan 01: AI-Agnostic URL Routes Summary

**Replaced 12 chatgpt-* HTTP route attributes on DeckController with the new deck-analysis / deck-comparison / cedh-meta-gap slug family, and added a centralized UseRewriter block in Program.cs that 301-redirects all 9 legacy chatgpt-* paths (page-roots + /upload + /download) to the new equivalents.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-17T01:06:00Z
- **Completed:** 2026-05-17T01:31:28Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- All 3 new AI-agnostic page-root slugs registered on DeckController (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`) with their `/upload` and `/download` POST sub-routes — 12 new route attributes total.
- All 9 legacy `chatgpt-*` URL paths now 301-redirect to their new slug equivalents via a single centralized `UseRewriter` block in `Program.cs`, keeping `DeckController` from growing 12+ thin redirect actions per D-03.
- Pipeline-order invariant (D-05) honored: `UseRewriter` sits between `UseForwardedHeaders` and the rest of the response-shaping middleware so the 301 Location response honors `X-Forwarded-Proto: https` and the browser is never downgraded to plaintext.
- Build clean with zero warnings (`dotnet build DeckFlow.Web/DeckFlow.Web.csproj`).

## Task Commits

Each task was committed atomically:

1. **Task 1: Add UseRewriter middleware block to Program.cs with 9 301 redirects** — `5598f9d` (feat)
2. **Task 2: Replace 12 chatgpt-* route attributes in DeckController.cs with AI-agnostic slugs** — `38bb2f8` (feat)

## Files Created/Modified
- `DeckFlow.Web/Program.cs` — Added `using Microsoft.AspNetCore.Rewrite;` and a `UseRewriter(new RewriteOptions()…)` invocation registering 9 `AddRedirect(regex, replacement, 301)` entries (3 page-roots + 3 `/download` + 3 `/upload`). Block sits immediately after `app.UseForwardedHeaders()`.
- `DeckFlow.Web/Controllers/DeckController.cs` — 12 single-line route-attribute swaps. Action method names and `return View("ChatGpt…")` arguments deliberately untouched (deferred to Plan 02).

## Decisions Made
- **Comment-text tweak (cosmetic, in scope):** The Program.cs decision comment originally referenced "UseRewriter MUST run after UseForwardedHeaders." Rewrote to "this middleware MUST run after UseForwardedHeaders" so the literal `UseRewriter` token appears only on the `app.UseRewriter(` invocation line. Driven by the plan's acceptance criterion `grep -c "UseRewriter" DeckFlow.Web/Program.cs == 1`. No semantic change to the comment.
- **Build-environment workaround (out of scope of the commit set):** Worktree lacked `DeckFlow.Web/node_modules`, so the MSBuild TypeScript step failed before reaching C# compilation. Copied `node_modules` from the main repo for the local build verification. Not committed (gitignored), confirmed via `git status` showing only the source files staged. Documented here so a future maintainer running into the same worktree-spawn gap knows the fix.

## Deviations from Plan

None — plan executed exactly as written. Both task acceptance grep checks pass:

- `grep -cE 'Http(Get|Post)\("/chatgpt-' DeckFlow.Web/Controllers/DeckController.cs` → `0`
- `grep -cE 'Http(Get|Post)\("/(deck-analysis|deck-comparison|cedh-meta-gap)' DeckFlow.Web/Controllers/DeckController.cs` → `12`
- `grep -c "AddRedirect.*chatgpt-" DeckFlow.Web/Program.cs` → `9`
- `grep -c "AddRedirect.*301" DeckFlow.Web/Program.cs` → `9`
- `grep -c "UseRewriter" DeckFlow.Web/Program.cs` → `1`
- `grep -c "using Microsoft.AspNetCore.Rewrite;" DeckFlow.Web/Program.cs` → `1`
- Pipeline-order line numbers: `app.UseForwardedHeaders()` (319) < `app.UseRewriter(` (329) < `app.UseDeckFlowSecurityHeaders()` (347)
- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings, 0 errors

## Issues Encountered
- `dotnet` CLI is not on `PATH` inside the WSL worktree shell. Used the Windows-side `/mnt/c/Program Files/dotnet/dotnet.exe` to run the build verification. C# compilation succeeded; the only intermediate failure was the MSBuild TypeScript step, which expected `DeckFlow.Web/node_modules/typescript/bin/tsc` — absent because the worktree was spawned without `npm install`. WSL symlinks didn't resolve from Windows-side `node.exe`, so copied `node_modules` from the main repo into the worktree for the local verification. `node_modules/` is git-ignored, so the copy is invisible to git and was not committed.

## Manual Smoke-Test Status
Deferred to user — per `feedback_user_starts_server.md` the executor does not auto-launch the dev server. The plan's `<verification>` block specifies a curl-based 301 + 200 spot check that the user should run when they next start the dev server (`http://localhost:5173`):
- `curl -i http://localhost:5173/chatgpt-packets` → expect 301 with `Location: /deck-analysis`
- `curl -i http://localhost:5173/deck-analysis` → expect 200 (the view file is still `ChatGptPackets.cshtml` until Plan 02; the action's existing `return View("ChatGptPackets", …)` call resolves it)
- Same pattern for `chatgpt-deck-comparison` → `/deck-comparison` and `chatgpt-cedh-meta-gap` → `/cedh-meta-gap`.

## Defer Notes
- Action method names (`ChatGptPackets`, `ChatGptDeckComparison`, `ChatGptCedhMetaGap`) and `return View("ChatGpt…")` string arguments remain unchanged — they ship in Plan 02 alongside the actual `.cshtml` view file renames (D-13/D-14).
- `@model ChatGpt*ViewModel` directives in Razor views stay until Phase 13 (CLASSRENAME-01) — Phase 12 only touches user-visible/URL surface.

## Next Phase Readiness
- Plan 02 (view file rename) is unblocked: new URL slugs route to existing action methods, which still call `View("ChatGptPackets", …)` etc. Plan 02 will rename the view files and update the corresponding `View(...)` arguments in one atomic commit.
- Browser-extension URL sweep (`browser-extensions/deckflow-bridge/`) is unblocked — old extension URLs continue to work via 301; Plan 05 can update them directly to the new slugs without depending on the redirect chain.

## Self-Check: PASSED
- File `DeckFlow.Web/Program.cs` exists and contains `UseRewriter` invocation + 9 `AddRedirect(...).*chatgpt-` lines.
- File `DeckFlow.Web/Controllers/DeckController.cs` exists and contains 12 new slug route attributes, 0 old `chatgpt-` route attributes.
- Commit `5598f9d` present in `git log --oneline`.
- Commit `38bb2f8` present in `git log --oneline`.
- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` returns 0 warnings + 0 errors.

---
*Phase: 12-ai-agnostic-url-page-rename*
*Plan: 01*
*Completed: 2026-05-17*
