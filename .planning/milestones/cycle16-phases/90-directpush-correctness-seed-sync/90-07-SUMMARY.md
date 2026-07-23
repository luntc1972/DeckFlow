---
phase: 90-directpush-correctness-seed-sync
plan: 07
subsystem: content-kb
tags: [aspnet-core-mvc, content-kb, hash-verification, admin-auth, tdd]

# Dependency graph
requires:
  - phase: 90-directpush-correctness-seed-sync
    plan: 01
    provides: "sync.directpush-gitbody flag + ContentKbArtifactPathResolver.TryResolveExistingArtifact flag-gated to git-only (the flag-independent TryResolveGitArtifact added here is additive to that same file)"
  - phase: 89-content-hash-foundation
    provides: "ContentSiteIndexContentSignature.ComputeBodySha256 - the ONE hash surface reused here, not hand-rolled"
provides:
  - "ContentKbArtifactPathResolver.TryResolveGitArtifact(artifactPath, out resolvedFullPath) - resolves strictly against the git ContentBase tree, NEVER the /data overlay, independent of the sync.directpush-gitbody flag state"
  - "GET Admin/api/contentkb/deployed-body-hash?naturalKeyType={t}&naturalKeyValue={v} - authenticated (inherits /Admin BasicAuth), read-only, is_visible-independent deploy-confirm endpoint returning { bodySha256 } or 404"
affects: [90-05, studio-directpush-confirmer]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Flag-independent resolver method sitting alongside a flag-gated one on the same class (TryResolveGitArtifact vs TryResolveExistingArtifact) - reuses the private IsSafeArtifactPath/IsContainedUnderRoot guards verbatim, no second path-validation routine"
    - "Deploy-confirm-by-natural-key: IContentSiteIndexStore.GetByNaturalKeyAsync (unfiltered, not is_visible-gated) sidesteps the local-vs-prod surrogate-id mismatch entirely"
    - "Attribute-routed /Admin/* controller inherits BasicAuth from Program.cs's path-prefix UseWhen branch with zero new middleware wiring"

key-files:
  created:
    - DeckFlow.Web/Controllers/Admin/ContentKbDeployedBodyController.cs
    - DeckFlow.Web.Tests/Controllers/ContentKbDeployedBodyControllerTests.cs
  modified:
    - DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs
    - DeckFlow.Web.Tests/ContentKbArtifactPathResolverTests.cs

key-decisions:
  - "Test file placed at DeckFlow.Web.Tests/Controllers/ContentKbDeployedBodyControllerTests.cs (subfolder) per the plan's explicit files_modified path, but kept in the single project-wide `DeckFlow.Web.Tests` namespace (not `DeckFlow.Web.Tests.Controllers`) to match the repo's established convention that all tests share one namespace per project regardless of subfolder (see AboutControllerTests.cs et al., all flat, same namespace)."
  - "Controller uses [ApiController] + ControllerBase (mirrors ArchidektCacheJobsController's small-JSON-controller shape) rather than Controller (which AdminContentKbController uses for View() rendering) - no views, only JSON."
  - "Both RED phases used a compiling stub (TryResolveGitArtifact always MissingFile; the endpoint action always NotFound) rather than a non-compiling test, mirroring the RED pattern from 90-01's Task 3 (ctor scaffolded, behavior branch unchanged) - each RED commit's new assertions demonstrably failed for the stated reason before the GREEN commit implemented real behavior."

requirements-completed: [SYNC-09]

# Metrics
duration: ~40min
completed: 2026-07-07
---

# Phase 90 Plan 07: Deployed-Body-Hash Deploy-Confirm Endpoint (D-09 REVISED) Summary

**Added a git-`/app`-only artifact resolver method plus an authenticated, `is_visible`-independent `GET Admin/api/contentkb/deployed-body-hash` endpoint that returns a Content-KB row's deployed body hash by natural key (404 when the `/app` artifact is missing) — the race-free deploy-confirm surface DirectPush's hash-gated visibility flip (SYNC-09) needs, replacing the unsound "public detail page returns 200" idea Codex plan-review blocked.**

## Performance

- **Duration:** ~40 min
- **Completed:** 2026-07-07
- **Tasks:** 2 (both TDD: RED + GREEN sub-commits)
- **Files modified:** 4 (2 created, 2 modified)

## Accomplishments
- `ContentKbArtifactPathResolver.TryResolveGitArtifact` resolves a stored artifact path against the git `ContentBase` tree ONLY — reusing `IsSafeArtifactPath`/`IsContainedUnderRoot` verbatim from `TryResolveExistingArtifact` but with zero `DataOverlayBase` branching, and unconditional on the `sync.directpush-gitbody` flag state (grep-confirmed: no `DataOverlayBase` reference inside the method body).
- `ContentKbDeployedBodyController` exposes `GET Admin/api/contentkb/deployed-body-hash?naturalKeyType={t}&naturalKeyValue={v}`: looks the row up via the unfiltered `GetByNaturalKeyAsync` (sidesteps the local-vs-prod surrogate-id mismatch that broke the original design), resolves the artifact through the new git-only method, and returns `{ bodySha256 }` recomputed with the shared `ContentSiteIndexContentSignature.ComputeBodySha256` — never a hand-rolled second hash.
- The endpoint is `is_visible`-independent by construction (no filter on that column anywhere in the query or action), so a not-yet-visible DirectPush'd row still confirms — the entire reason D-09 REVISED replaced the public-detail-page approach.
- Routed under `/Admin` via `[Route("Admin/api/contentkb")]`, so it inherits the existing `BasicAuthMiddleware` gate from Program.cs's path-prefix `UseWhen` branch with **zero** `Program.cs` changes. Deliberately does **not** call `SameOriginRequestValidator` (grep-confirmed absent) since Studio→prod is a server-to-server call with no browser `Origin` header.
- Blank/missing query params → 400; unknown natural key or missing `/app` artifact → 404; present artifact (visible or hidden) → 200 with the correct hash.

## Task Commits

Each task was committed atomically (TDD RED → GREEN):

1. **Task 1: Git-/app-only resolver method** — RED `7392cb01` (test), GREEN `baeb9455` (feat)
2. **Task 2: Authenticated deployed-body-hash endpoint** — RED `8ac6b30b` (test), GREEN `805e3444` (feat)

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP update)

## Files Created/Modified
- `DeckFlow.Web/Controllers/Admin/ContentKbDeployedBodyController.cs` — New controller: `GetDeployedBodyHash` action, natural-key lookup → git-only resolve → hash recompute → `{ bodySha256 }` / 404 / 400.
- `DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs` — Added `TryResolveGitArtifact`, flag-independent, no overlay branch.
- `DeckFlow.Web.Tests/Controllers/ContentKbDeployedBodyControllerTests.cs` — New: present→200+hash, missing→404, hidden-but-present→200+hash, unknown key→404, 6 blank-param theory cases→400.
- `DeckFlow.Web.Tests/ContentKbArtifactPathResolverTests.cs` — Added git-hit→Resolved, git-miss-with-overlay-present→MissingFile (overlay never consulted), and unsafe-path→InvalidPath (2 theory cases) for `TryResolveGitArtifact`.

## Decisions Made
- **Test subfolder placement, single flat namespace.** The new controller test file lives at `DeckFlow.Web.Tests/Controllers/` (per the plan's `files_modified` frontmatter) but declares `namespace DeckFlow.Web.Tests` — matching every other test file in the project (all flat under one namespace regardless of physical subfolder, per the repo's established Naming Patterns convention). This avoids introducing the project's first namespaced test subfolder as an unplanned side effect of following the plan's literal path.
- **ApiController/ControllerBase, not Controller.** `ContentKbDeployedBodyController` returns JSON only (no views), so it mirrors `ArchidektCacheJobsController`'s `[ApiController]` + `ControllerBase` shape rather than `AdminContentKbController`'s `Controller` (which needs `View()`).
- **Compiling RED stubs.** Both tasks' RED phase used a stub that compiles but returns the wrong result (`TryResolveGitArtifact` → always `MissingFile`; the endpoint action → always `NotFound`), consistent with the RED pattern already established in 90-01's Task 3, rather than a non-compiling test file.

## Deviations from Plan

None functionally. All `must_haves` truths, artifacts (`TryResolveGitArtifact`, `ContentKbDeployedBodyController` containing `ComputeBodySha256`), and the `key_links` chain (`GetByNaturalKeyAsync` → git-only resolve → `ComputeBodySha256`) are satisfied exactly as specified. One placement-level deviation, documented above under Decisions Made (test namespace stays flat despite the subfolder path) — Rule 2 territory (consistency correctness) rather than a functional gap; no user decision needed since it only affects internal test organization, not shipped behavior.

## Issues Encountered
None. Both TDD cycles confirmed RED (new assertions failing for the stated reason, all pre-existing assertions in the same files still green) before GREEN, and the full `DeckFlow.Web.Tests` + `DeckFlow.Core.Tests` suites pass after the final commit.

## User Setup Required
None — no external service configuration required. The endpoint requires the same admin BasicAuth credentials (`FEEDBACK_ADMIN_USER`/`FEEDBACK_ADMIN_PASSWORD`) Studio already holds for other `/Admin` reads; no new secret.

## Next Phase Readiness
- SYNC-09's deploy-confirm surface is complete: Plan 90-05 (Studio's confirmer) has a stable, race-free endpoint contract to poll (`200 && bodySha256 == expected` → confirmed; `404` or hash-mismatch → keep polling).
- `TryResolveGitArtifact` is available for any other future git-only-resolve need without touching the flag-gated `TryResolveExistingArtifact` surface (90-01's serving-flip is untouched).
- No blockers. Manual BasicAuth-enforcement verification (the plan's one non-unit-test verification item — authenticated vs. unauthenticated request against a locally running server) is deferred to whichever plan/operator step runs the phase's end-to-end integration pass; not required to close this plan's automated `must_haves`.

## Self-Check: PASSED

All 4 created/modified files confirmed present on disk; all 4 commit hashes
(`7392cb01`, `baeb9455`, `8ac6b30b`, `805e3444`) confirmed present in
`git log --oneline --all`.

---
*Phase: 90-directpush-correctness-seed-sync*
*Completed: 2026-07-07*
