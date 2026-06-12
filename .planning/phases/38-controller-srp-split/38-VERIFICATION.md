---
phase: 38-controller-srp-split
verified: 2026-06-12T17:30:00Z
status: human_needed
score: 3/3 must-haves verified (static); 1 runtime gate PENDING
overrides_applied: 0
re_verification:
human_verification:
  - test: "Page-render smoke: with the app running, GET each split-controller route and confirm HTTP 200 with the expected page markup (NOT a 500 ViewNotFoundException). Routes: GET / (home), /sync, /convert, /card-lookup, /suggest-categories, /deck-analysis, /deck-primer, /judge-questions."
    expected: "Each returns 200 and renders its /Views/Deck/<Name>.cshtml page; no ViewNotFoundException, no blank/500."
    why_human: "Views are runtime-compiled (DeckFlow.Web.csproj has no precompile). `dotnet build` does NOT catch a broken View(\"X\") resolution. The DeckViewLocationExpander fallback is only exercised at render time. Static verification cannot prove it; the app cannot be reliably booted headless in WSL (project constraint: push-and-watch CI / manual harness)."
  - test: "Forced unhandled-exception smoke: trigger an unhandled server exception (or hit a path that throws) in non-Development and confirm the friendly error view renders via UseExceptionHandler(\"/Deck/Error\"), and that its 'Back to home' link resolves to /."
    expected: "Error page renders (ShellController.Error → /Views/Deck/Error.cshtml); 'Back to home' link points to / (Url.Action(\"Home\",\"Shell\"))."
    why_human: "Error-pipeline re-execution + view resolution is runtime-only; the [Route(\"Deck/Error\")] → ShellController.Error mapping and the Home link cannot be proven by build/grep."
---

# Phase 38: Controller SRP Split — Verification Report

**Phase Goal:** `DeckController` and `CommandRunners` are decomposed into focused, single-responsibility units — all existing URLs and CLI commands preserved unchanged, no user-visible behavior altered.
**Verified:** 2026-06-12T17:30:00Z
**Status:** human_needed (PASS-WITH-PENDING-SMOKE)
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 (SC1) | Every pre-split URL exists post-split; zero add/remove/change | ✓ VERIFIED | Independently re-derived: baseline `DeckController.cs`@2e2d5aa had 34 `[Http*]` attrs; conventional `Error()` (no attr) normalizes to `Route("Deck/Error")` → 35. Union of 8 split controllers' attrs = 35. `diff` of normalized PRE vs POST = **EMPTY**. |
| 2 (SC3) | All tests pass against split; only logger-generic refs changed; no new warnings | ⚠ VERIFIED (static) | `dotnet build DeckFlow.sln` = **0 errors, 0 warnings** (re-run by verifier). Both `.Tests` projects compile. 24 `[Fact]`/`[Theory]` preserved (baseline 24 == new 24, split across 3 files). Zero `ILogger<DeckController>`/`new DeckController(` leftovers. *Test EXECUTION not run (VSTest unreliable in WSL — project constraint); build-compile is the gate, runtime behavior covered by smoke below.* |
| 3 (SC2) | `CommandRunners.cs` split at content-KB boundary; separate classes; all commands registered & invocable | ✓ VERIFIED | `CommandRunners.cs` deleted (`git rm`). `DeckCommandRunners` + `ContentKbCommandRunners` + `ContentKbCliPaths` exist (internal static). Program.cs re-points: 13 `DeckCommandRunners.` + 9 `ContentKbCommandRunners.`; **0** bare `CommandRunners.` in any source (only a stale `bin/…/DeckFlow.CLI.xml` artifact). All 19 commands still `new Command(...)`-registered in Program.cs. |
| 4 (SRP) | DeckController god-controller decomposed by tool family | ✓ VERIFIED | `DeckController.cs` **deleted**. 8 controllers exist; 7 inherit `DeckToolControllerBase`, `ShellController : Controller`. Base holds the timeout wrapper (`CreateTimeoutScope` / `CreateLinkedTokenSource` / `CancelAfter`) + `LookupTimeout`/`SuggestionTimeout` constants. Each controller injects ONLY its service subset (e.g. Sync→`IDeckSyncService`; Lookup→`ICardLookupService`+`IMechanicLookupService`) vs the original 11-13 — real SRP win. |
| 5 (D-02) | Active workflow tab preserved per action | ✓ VERIFIED | `ActiveTab = DeckPageTab.X` assignment counts are **byte-identical** between baseline and split for every tab (CardLookup 4, CedhMetaGap 14, DeckAnalysis 11, DeckComparison 14, DeckPrimer 15, JudgeQuestions 1, MechanicLookup 5, SuggestCategories 6, Sync 5). |
| 6 (runtime) | Every split controller's `View("X")` resolves to `/Views/Deck/X.cshtml` at render time | ? UNCERTAIN | Structurally wired: `DeckViewLocationExpander` registered in Program.cs:71 appending `/Views/Deck/{0}.cshtml`; all 12 `View("X")` names map 1:1 to existing `/Views/Deck/*.cshtml`; no per-new-controller view folders created. BUT views are runtime-compiled — build cannot prove resolution. **Page-render smoke NOT performed.** See Human Verification. |

**Score:** 3/3 success criteria verified statically. SC2's "behavior unchanged" claim and the view-resolution invariant (#6) require the deferred runtime smoke.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckController.cs` | DELETED | ✓ | Untracked at HEAD; present at baseline 2e2d5aa |
| `CommandRunners.cs` | DELETED | ✓ | `git rm`'d |
| `ShellController.cs` | Home/Error/set-options | ✓ VERIFIED | plain `Controller`; `[Route("Deck/Error")]` on Error |
| `DeckToolControllerBase.cs` | timeout wrapper + constants | ✓ VERIFIED | abstract; `CreateTimeoutScope` present |
| `DeckSync/Convert/Lookup/Categories/Packet/Primer Controller.cs` + `JudgeQuestionsController.cs` | 8 sealed feature controllers | ✓ VERIFIED | all present, inherit base (except Shell) |
| `DeckViewLocationExpander.cs` | append /Views/Deck fallback | ✓ VERIFIED (static) / ⚠ runtime-unproven | registered Program.cs:71 |
| `DeckCommandRunners.cs` / `ContentKbCommandRunners.cs` / `ContentKbCliPaths.cs` | 21/61/2 split | ✓ VERIFIED | internal static; SUMMARY mechanical inventory reconciled |
| `DeckLookup/Categories/PacketControllerTests.cs` + `DeckControllerTestFakes.cs` | mirror-split, 24 facts, fakes relocated | ✓ VERIFIED | `DeckControllerTests.cs` deleted; 24 facts (11+3+10) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| Program.cs | DeckViewLocationExpander | `ViewLocationExpanders.Add(new ...)` | ✓ WIRED | Program.cs:71, fully-qualified type |
| UseExceptionHandler("/Deck/Error") | ShellController.Error | `[Route("Deck/Error")]` | ✓ WIRED (static) | handler unchanged at Program.cs:390; route attr present |
| Program.cs CLI | DeckCommandRunners / ContentKbCommandRunners | static call targets | ✓ WIRED | 13 + 9 re-points, 0 bare-CommandRunners |
| split controllers | /Views/Deck/*.cshtml | DeckViewLocationExpander fallback | ⚠ RUNTIME-ONLY | cannot be proven by build — smoke required |

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| (none) | TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER | — | Clean across all new controllers, CLI split, and test files |

### Requirements Coverage

| Requirement | Description | Status | Evidence |
|-------------|-------------|--------|----------|
| SRP-01 | DeckController decomposed, routes preserved, active-tab set | ✓ SATISFIED (static) | Truths 1, 4, 5; route diff empty |
| SRP-02 | CommandRunners split at KB boundary, two-commit, all commands registered | ✓ SATISFIED | Truth 3; commits e59dfc7 (helpers) + ba521a0 (split) |
| SRP-03 | Behavior unchanged, tests pass, no new warnings | ⚠ SATISFIED (static) / runtime-pending | Truth 2 (0/0 build, 24 facts); runtime "behavior unchanged" = pending smoke |

### Human Verification Required

See frontmatter `human_verification`. The two items are the **page-render smoke** (8 GET routes return 200 not 500) and the **forced-exception smoke** (`/Deck/Error` renders + Home link). This is the documented deferred gate (38-01-PLAN lines 187, 243) and the only proof of the runtime view-resolution invariant the static build cannot catch.

### Gaps Summary

No static gaps. The decomposition is complete and correct by every check the build/grep/git can perform: both god-files deleted, 8 SRP controllers + base + shell, CLI 3-way split with all commands registered, route parity proven by empty diff (35==35), active-tab parity exact, 24 tests preserved, 0 warnings/0 errors, no debt markers.

**The single unprovable-by-build item is the runtime view resolution.** Because `DeckFlow.Web` compiles views at runtime, a misregistered `DeckViewLocationExpander` or a controller-name/view-name mismatch would NOT surface at build — it throws `ViewNotFoundException` (500) only when the page is rendered. Static evidence is strong (expander registered, all 12 View names map to existing files, no stray view folders), but **the page-render smoke has NOT been run** (latest web log predates the split; no post-split GET of these routes recorded). Per the plans this smoke is the real SC2 "behavior unchanged" proof and a **required gate before declaring the phase user-safe.**

---

**VERDICT: PASS-WITH-PENDING-SMOKE.** All three success criteria verified statically with high confidence; phase goal structurally achieved. Must NOT be declared user-safe until the manual page-render + forced-exception smoke confirms runtime view resolution.

_Verified: 2026-06-12T17:30:00Z_
_Verifier: Claude (gsd-verifier)_
