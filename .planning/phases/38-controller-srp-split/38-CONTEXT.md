# Phase 38: Controller SRP Split - Context

**Gathered:** 2026-06-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Decompose two oversized, multi-responsibility units into focused, single-responsibility ones — with **zero** user-visible change:
- `DeckFlow.Web/Controllers/DeckController.cs` (1821 lines, 11 injected services, ~26 routes across ~8 tools) → multiple feature controllers (SRP-01).
- `DeckFlow.CLI/CommandRunners.cs` (2185 lines) → split at the deck-domain ↔ content-KB boundary (SRP-02).
- Behavior unchanged; all existing tests pass against the split (logger-generic refs updated); no new warnings (SRP-03).

This is the v1.6 milestone closer. It is a **pure refactor** — every URL, every CLI command, and every rendered view stays identical. Out of scope: any behavior change, new feature, new route, or view/JS edit beyond what the move mechanically requires.
</domain>

<decisions>
## Implementation Decisions

### Controller decomposition (SRP-01)
- **D-01 (granularity = by tool family, ~5-6 controllers).** Split `DeckController` by tool family, not per-tool and not by nav-group. **Proposed mapping** (planner finalizes exact boundaries; every route below MUST keep its current URL + `DeckPageTab` active-tab):
  - **DeckSyncController** — `GET/POST /sync`, `POST /resolve` (DeckDiff printing-conflict resolution). Owns sync helpers: `BuildViewModel`, `BuildUserFacingErrorMessage`, `IsMoxfieldForbidden`, `HasMoxfieldInput`, `HasArchidektInput`.
  - **DeckConvertController** — `GET/POST /convert`, `GET /convert/commander-search`.
  - **DeckLookupController** — `GET /card-lookup` + `/card-lookup/download` + `/download-json` + `/single`, `GET/POST /mechanic-lookup`. Owns `BuildVerificationFile`.
  - **DeckCategoriesController** — `GET /suggest-categories`, `GET /suggest-categories/card-search`. Owns `HasSuggestionInput`.
  - **DeckPacketController** (ChatGPT-packet family) — `GET/POST /deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/deck-primer`, each with its `/download` + `/upload`. Largest group; planner MAY split further (e.g. Primer separate) only if it stays SRP and keeps URLs/tabs.
  - **JudgeQuestionsController** — `GET /judge-questions`. (Standalone thin controller — NOT a packet. Planner's discretion to fold into Lookup if cleaner, but keep the URL + tab.)
- **D-02 (routes + active-tab preserve automatically).** Routes are already attribute-based (`[HttpGet("/route")]`) and the active workflow tab is a view-model property (`ActiveTab = DeckPageTab.X`), NOT controller-name-derived. So a moved action keeps its URL and its tab with no extra wiring. **Verification gate:** a pre-split route list MUST equal the post-split route list (zero add/remove/change) — this is Success Criterion 1.

### Shared shell + cross-cutting (SRP-01)
- **D-03 (base controller + shell controller).** Introduce an abstract `DeckToolControllerBase` holding the genuinely cross-cutting bits — the cancellation-token-timeout wrapper (`CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted).CancelAfter(...)`) and the timeout constants (`LookupTimeout`, `SuggestionTimeout`) — plus any shared upstream-error funnel. New feature controllers inherit it. A small `ShellController` owns the non-tool routes: `GET /` (Home), the error action (`/Deck` exception view), and `GET /api/set-options`.
- Feature-specific private helpers move WITH their feature controller (most of them are), NOT into the base.

### CommandRunners split (SRP-02)
- **D-04 (two classes, two-commit discipline).** Extract shared content-KB path helpers (`ResolveContentKbDatabasePath`, `ResolveContentKbArtifactRoot`) into a `ContentKbCliPaths` (static) helper **first (commit 1)**, then split (commit 2) into:
  - **DeckCommandRunners** — `RunCompareAsync`, `RunProbeAsync`, `RunExportMoxfieldAsync`, `RunArchidektCategoriesAsync`, `RunArchidektCategoryCardsAsync`, `RunArchidektHarvestRecentAsync`, `RunArchidektCacheAsync`.
  - **ContentKbCommandRunners** — `RunContentSourceAddAsync`, `RunContentSourceSetEnabledAsync`, `RunDistillAsync`, `RunBlockVideoAsync`, `RunUnblockVideoAsync`, `RunListBlockedAsync`, `RunCorpusResetAsync`, `RunContentIndexExportAsync`.
- All commands stay registered + invocable in `DeckFlow.CLI/Program.cs` — only the static call targets change (Success Criterion 3).
- Class names above are the agreed naming; planner may adjust only if a name collides.

### Test layout (SRP-03)
- **D-05 (mirror new controllers).** Split `DeckFlow.Web.Tests/DeckControllerTests.cs` into per-new-controller test files (e.g. `DeckSyncControllerTests`, `DeckLookupControllerTests`, ...), updating `ILogger<DeckController>` generic references to the new controller types. Keeps the project's 1:1 test↔source convention. Build the `.Tests` project as part of verification (interface/ctor drift = CS error).

### Claude's Discretion
- Exact boundary of `DeckPacketController` (whether Primer splits out) and `JudgeQuestionsController` placement — planner decides at plan time within D-01.
- Base-vs-helper for any borderline shared logic beyond the timeout wrapper — planner applies SOLID.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — SRP-01 (DeckController decomposition, routes preserved, tab active-state), SRP-02 (CommandRunners split at content-KB boundary, shared-helpers-first two-commit discipline), SRP-03 (behavior unchanged, tests pass, no new warnings).
- `.planning/ROADMAP.md` §Phase 38 — goal + 3 success criteria (URL list parity, tests pass with only logger-generic updates, CLI split at content-KB boundary).

### Source under refactor
- `DeckFlow.Web/Controllers/DeckController.cs` — the 1821-line controller to split.
- `DeckFlow.CLI/CommandRunners.cs` — the 2185-line runner class to split.
- `DeckFlow.Web/Models/DeckPageTab.cs` — the active-tab enum (preserved per action).
- `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml`, `_WorkflowStepTabs.cshtml` — consume `ActiveTab`; must keep working unchanged.
- `DeckFlow.CLI/Program.cs` — command registration (static call targets update only).
- `DeckFlow.Web.Tests/DeckControllerTests.cs` — tests to mirror-split.

No external ADRs — this is an internal refactor; decisions captured above.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Attribute routing already in place — moving an action carries its URL with it (no route table edits).
- `ActiveTab` view-model property already decouples active-tab from controller identity.
- `CommandRunners` already has an internal-overload test seam (public DI-resolving wrapper → `internal` store-injecting overload) for several runners — preserve this when splitting.

### Established Patterns
- One public type per file; file name == type name (per project conventions). Each new controller = its own file.
- Controllers are thin orchestrators: bind model → call `I*Service` → return `IActionResult`. The split must not move logic INTO controllers.
- `sealed` leaf controllers; constructor `ArgumentNullException.ThrowIfNull` guards; `ILogger<T>` injected.

### Integration Points
- DI: `Program.cs` registers controllers via `AddControllers()` (convention) — new controllers auto-discovered, but verify no manual `DeckController` registration exists.
- Each new controller injects ONLY the services its actions use (subset of the 11) — a key SRP win.
</code_context>

<specifics>
## Specific Ideas

- Two-commit discipline for the CLI split is explicit (SRP-02): helpers-extracted commit, then split commit — keeps each diff reviewable.
- Pre/post route-list parity is the headline acceptance proof (SC1). Planner should specify HOW the route list is captured (e.g. enumerate `[Http*]` attributes or `EndpointDataSource`) so verification is mechanical.
</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. (No behavior changes, no new tooling; pure SRP refactor.)
</deferred>

---

*Phase: 38-controller-srp-split*
*Context gathered: 2026-06-12*
