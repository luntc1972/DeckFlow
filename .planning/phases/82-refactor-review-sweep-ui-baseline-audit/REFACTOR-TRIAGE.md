# Phase 82 Refactor Triage

**Input:** `82-REVIEW.md` (this phase's code-review sweep findings).
**Consumer:** Plan 82-03 (Wave 2, REVIEW-02) — 82-03 executes every `in-scope` row below and
records every `backlog` row in `REFACTOR-BACKLOG.md` with the deferral reason carried forward.

**8 candidates surfaced — 3 in-scope, 5 backlog** (revised 2026-07-04 after a pre-execution
review of Wave 1's triage: rows 1 & 3 re-triaged conservatively so every remaining in-scope
item is provably behavior-neutral and Phase-85-fenced — see the "post-review re-triage" note in
82-01-SUMMARY.md). Row 1 is NARROWED: only 2 of `deck-sync.ts`'s 6 concerns are in-scope; its
other coupled concerns are deferred as row 1b.

Risk-budget yardstick applied to every row (per 82-01-PLAN.md and CLAUDE.md's byte-identical /
behavior-neutral gate): a target is `in-scope` only when its refactor is behavior-neutral AND
provable with EXISTING automated coverage — C# targets via `dotnet build` clean + xUnit suite
+ `CarveOutGuard` + changed-lines format-gate; TS targets via the Vitest suite
(`npm --prefix DeckFlow.Web test`) + Playwright e2e (functional parity, no pixel-diff claim) —
without standing up new test-harness infrastructure. A target that would need net-new test
scaffolding (a new framework/runner, not merely a new test file on the existing runner) or that
risks an observable behavior change is `backlog`, with the reason spelled out.

Existing coverage was confirmed from source, not assumed — see the "Coverage evidence" column.

---

## Triage Table

| # | File / Cluster | Severity | Decision | Reason | Risk-Budget / Coverage Evidence | If in-scope: refactor approach + files touched |
|---|-----------------|----------|----------|--------|----------------------------------|--------------------------------------------------|
| 1 | `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (2877 LOC, 6-concern SRP violation) — concerns #1 (extension-bridge) + #2 (busy-indicator) ONLY | HIGH | **in-scope (NARROWED — 2 of 6 concerns)** | Named REVIEW-01 flagship; stays in-scope per the 82-03 guardrail ("no TS test harness" is not a valid deferral for this file). **Post-review correction (2026-07-04):** only concern #1 (`moxfield-extension-bridge`) and concern #2 (`busy-indicator`) are cleanly isolated and behavior-neutral to extract — verified both clusters call NONE of `persistFormState`/`restoreFormFields`/card-picker functions. Concerns #3/#4/#6 are behavior-coupled and Phase-85-adjacent → moved to backlog as **row 1b**. | **Coverage evidence:** concern #2 covered by `ts-tests/busy-overlay-pageshow.test.ts` (imports `../wwwroot/ts/deck-sync`, Vitest+jsdom); concern #1 covered by `e2e/deck-sync-bridge-busy.spec.ts` (Playwright, extension-bridge busy-overlay flow). Both clusters verified isolated from the persistence/card-picker tangle (grep of lines 100-431 and 658-841). Provable functional-parity on the EXISTING runner — no new harness. | Extract ONLY `busy-indicator.ts` (concern #2, fully `chatgpt-*`-free, lines 658-841) and `moxfield-extension-bridge.ts` (concern #1, lines 100-431 — MOVE its `chatgpt-packets`/`chatgpt-deck-comparison`/`chatgpt-cedh-meta-gap` cache-key string literals VERBATIM: no rename = Phase-85-safe; does NOT touch chatgpt-packets persistence/reset). Leave concerns #3/#4/#6 in `deck-sync.ts` untouched. Files: `deck-sync.ts` + 2 new TS modules + their new focused test files. |
| 1b | `deck-sync.ts` concerns #3 (form-state persistence, lines 951-1470) + #4 (card-picker) + #6 (chatgpt-packets wizard/reset, lines 1678-2877) | HIGH | **backlog** | Behavior-COUPLED, not independently splittable: `restoreFormFields()` calls `restoreCardPickerFields()` directly (line 1229), and `attachGenericPersistedForms()` branches into `clearChatGptPacketsState()` for the `chatgpt-packets` cache-key (lines 1414-1415) instead of the generic clear. A split risks observable change. ADDITIONALLY the chatgpt-packets persistence/reset is **Phase 85 (`chatgpt-*` rename) territory** — fenced out to avoid colliding with its later owner. | Not behavior-neutral in isolation under the byte-identical gate; would need the `restoreFormFields`↔`restoreCardPickerFields` coupling and the chatgpt-packets clear-branch decoupled behind explicit tests FIRST. **What would unblock it:** a dedicated follow-up that adds decoupling tests, coordinated with Phase 85 for the chatgpt-packets slice. | — |
| 2 | `DeckFlow.Studio/Pages/Harvest.razor.cs` (1225 LOC, 6-concern SRP violation, incl. one ~250-LOC method) | HIGH | **in-scope** | Named REVIEW-01 flagship. The codebase already has a proven precedent for this exact extraction shape (`DirectPushCoordinator.cs` was previously extracted from `DirectPush.razor.cs`'s code-behind) — low novelty risk. | **Coverage evidence:** `DeckFlow.Studio.Tests/HarvestPageTests.cs`, `DeckFlow.Studio.Tests/ViewModels/HarvestPlannerTests.cs`, `DeckFlow.Web.Tests/HarvestRunStoreTests.cs`, `DeckFlow.Web.Tests/HarvestStatsAggregatorTests.cs`, `DeckFlow.Core.Tests/ContentHarvestRunStoreTests.cs` — existing xUnit/bUnit coverage across the harvest surface. Behavior-neutral proof = `dotnet build` clean + these suites green post-extraction; no new test framework required. | Extract-collaborator into Studio ViewModels (mirroring `DirectPushCoordinator`): `HarvestQueueCoordinator` (queue add/remove/toggle), `AutoApproveSettingsCoordinator` (settings + cutoff), `CreatorManagementCoordinator` (creator load/select/filter + block), `SpendCapCoordinator` (cap display/raise). `HarvestAndAutoDistillAsync` (~250 LOC) gets broken into intention-revealing private steps within whichever coordinator owns it. |
| 3 | Cross-file form-state-persistence "duplication" (`deck-sync.ts` lines 951-1390 vs. `category-suggestions.ts` lines 313-408) | LOW (downgraded from HIGH) | **backlog** | **Post-review correction (2026-07-04):** NOT a real duplication. The two share only the `formStateStoragePrefix = 'decksync-form-state-'` string constant; behavior diverges materially — `deck-sync.ts` uses multi-value `Record<string,string[]>` + `:savedAt`/cache-pill + card-picker rows + a `chatgpt-packets` clear branch, while `category-suggestions.ts` uses a flat `Record<string,string>`, a SEPARATE result-envelope store (`formResultStoragePrefix`), and restores ONLY after `.tool-nav__link` tab-nav (gated by `tabNavigationKey`, lines 463-471; otherwise clears). A shared save/restore/clear module WOULD change behavior (category-suggestions would hydrate unconditionally and lose its result-envelope restore). | No behavior-neutral shared extraction exists — only the prefix string is common, and no non-trivial helper is provably identical across both files. **What would unblock it:** nothing worth doing; the two flows are legitimately different features that happen to share a storage-key prefix, and keeping them separate is correct. | — |
| 4 | `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (1096 LOC) — 3 near-duplicate upsert methods (`UpsertRowAsync` / `UpsertRowPreservingVisibilityAsync` / `UpsertContentColumnsOnlyAsync`) | MEDIUM | **in-scope** | Small, contained, single-file extract-method dedup (shared SQL/parameter-binding helper behind the 3 upsert variants) with unusually strong existing regression coverage for its size — low blast radius, meets the risk-budget bar cleanly. | **Coverage evidence:** 5 dedicated Core.Tests files already exercise this exact surface: `ContentSiteIndexStoreTests.cs`, `Content/ContentSiteIndexStoreApprovalTests.cs`, `Content/ContentSiteIndexStoreBatchUpsertTests.cs`, `Content/ContentSiteIndexStoreKeyedVisibilityTests.cs`, `Content/ContentSiteIndexStorePushedToProdTests.cs`, plus `DeckFlow.Web.Tests/ContentSiteIndexStoreTests.cs`. `dotnet build` clean + these suites green is a sufficient behavior-neutral proof; no new coverage required. | Extract a private `BuildUpsertCommand(...)`/shared parameter-binding helper consumed by all three `Upsert*Async` variants; same treatment optionally for the parallel `Set{Visibility,Hidden}Async` / `...BySourceAsync` pairs if the helper generalizes cleanly. File touched: `DeckFlow.Core/Content/ContentSiteIndexStore.cs` only. |
| 5 | `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` (1615 LOC) — mixes harvest-fetch orchestration, distill/LLM-spend orchestration, tagging/filtering, natural-key resolution, and 3 nested outcome-accumulator types | HIGH | **backlog** | Not a named REVIEW-01 flagship. This is the harvest/distill pipeline with real LLM-spend-cap enforcement logic threaded through it (`SkippedOverCap`, `AbortedConfig` outcome states) — a structural split carries materially higher regression risk than the two named UI flagships, and compressing a third large multi-concern C# split into the same single Wave-2 execution plan (82-03) alongside rows 1-2 exceeds this cycle's realistic per-plan risk budget. | Existing coverage is real but partial for a full split (`Orchestration/ContentKbOrchestratorDistillTests.cs`, `ContentKbOrchestratorFactoryTests.cs`, `AddContentKbOrchestratorDiTests.cs`, `DeckFlow.Studio.Tests/Services/ContentKbOrchestratorSmokeServiceTests.cs`) — good for regression-catching an in-place bug fix, not yet proven sufficient to certify a full-file structural split's behavior-neutrality in one pass. **What would unblock it:** a dedicated follow-up pass that first adds boundary-level tests at the proposed Harvest/Distill orchestrator split seam, then executes the split on its own plan (not bundled with rows 1-2's UI extraction work). |
| 6 | `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` (949 LOC) — 4 parallel `Suggest*ZipFileName`/`Load*FromZip` families, one per packet-artifact type | MEDIUM | **backlog** | This store is the persistence layer backing the four fenced PKTSVC god-services (Phase 83). Opening it independently here risks the same file being touched by two overlapping refactors in the same cycle (this phase's dedup vs. Phase 83's SRP split), which is explicitly the scenario 82-REVIEW.md flags for deferral. | N/A this phase — belongs to Phase 83's own scope check. **What would unblock it:** Phase 83 confirms whether its packet-service collaborator extraction naturally folds this store's duplication in, or explicitly punts it to a follow-up. | — |
| 7 | `DeckFlow.Web/wwwroot/ts/df-select.ts` (845 LOC) — single cohesive ARIA-combobox controller, 63 methods | MEDIUM | **backlog** | Not a clear SRP violation — this is one widget's state machine (keyboard nav, search mode, grouping, live-region announcements for a full ARIA 1.2 combobox), not multiple unrelated concerns. Size alone doesn't meet the bar for action given the risk profile below. | **No existing dedicated test file was found** for `df-select.ts` (confirmed: no `ts-tests/*.test.ts` references it). A structural split now would require standing up NEW keyboard/ARIA-interaction test coverage from scratch before a behavior-neutral proof is possible — this is the "would need net-new test scaffolding" exclusion condition, not merely a new test file on rails already laid down (unlike row 3). **What would unblock it:** add `df-select`-specific Vitest coverage first (own future task), then reassess whether a split is warranted. | — |
| 8 | `DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs` (604 LOC) — mixes upstream fetch with card-relevance scoring heuristics (`ScoreSetCard`, `ScoreTextSignals`, oracle-text parsing) | LOW | **backlog** | Low severity — a plausible `CardRelevanceScorer` extract-collaborator exists, but the scoring logic is small and tightly coupled to its one caller. With rows 1-4 already claiming this cycle's refactor budget, a LOW-severity item is the correct one to defer on capacity grounds, not a coverage gap. | `DeckFlow.Web.Tests/ScryfallSetServiceTests.cs` already exists and would support this work if picked up — deferral here is a prioritization call, not a risk-budget block. **What would unblock it:** capacity in a future cleanup pass; no new coverage prerequisite. | — |

---

## Named-candidate confirmation

Both REVIEW-01-named candidates have explicit rows with an explicit decision:

- **`deck-sync.ts`** → row 1, **in-scope**.
- **`Harvest.razor.cs`** → row 2, **in-scope**.

## Summary for 82-03 — FINAL in-scope list Wave 2 will execute

**In-scope this cycle (3):**

1. **Row 1 (NARROWED):** `deck-sync.ts` — extract ONLY `busy-indicator.ts` (concern #2) and
   `moxfield-extension-bridge.ts` (concern #1). Both provably isolated from persistence/card-picker;
   extension-bridge moves its `chatgpt-*` cache-key literals verbatim (no rename, Phase-85-safe)
   and does NOT touch chatgpt-packets persistence/reset.
2. **Row 2:** `Harvest.razor.cs` — coordinator extraction (`HarvestQueueCoordinator` /
   `AutoApproveSettingsCoordinator` / `CreatorManagementCoordinator` / `SpendCapCoordinator`).
3. **Row 4:** `ContentSiteIndexStore.cs` — extract-method dedup behind the 3 `Upsert*Async` variants.

**Backlog (5 candidate rows + row 1b, each with a written reason above):**

- **Row 1b** — `deck-sync.ts` concerns #3/#4/#6 (form-state persistence + card-picker +
  chatgpt-packets wizard/reset): behavior-coupled (`restoreFormFields`→`restoreCardPickerFields`;
  `attachGenericPersistedForms`→`clearChatGptPacketsState`) and Phase-85-fenced.
- **Row 3** — cross-file form-persistence "dedup": not a real duplication; a shared module would
  change behavior. Correct to leave the two flows separate.
- **Row 5** — `ContentKbOrchestrator` split (needs a dedicated follow-up with boundary tests first).
- **Row 6** — `PacketArtifactStore` dedup (defer to Phase 83's own scope check).
- **Row 7** — `df-select.ts` (needs new test coverage before a split is provable).
- **Row 8** — `ScryfallSetService` scoring extraction (LOW; capacity/priority deferral only).

**Phase-85 fence confirmation:** no in-scope slice touches `chatgpt-packets` persistence/reset
logic. The only `chatgpt-*` contact in-scope is the extension-bridge's verbatim MOVE of three
cache-key string literals (`chatgpt-packets`/`chatgpt-deck-comparison`/`chatgpt-cedh-meta-gap`)
in `collectMoxfieldImportTasks` — a relocation with no rename, which Phase 85 will later rename
wherever those literals live. Every remaining in-scope item is provably behavior-neutral.
