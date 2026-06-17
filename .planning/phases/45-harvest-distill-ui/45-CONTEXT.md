# Phase 45: Harvest + Distill UI - Context

**Gathered:** 2026-06-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the **Harvest + Distill UI** page (`DeckFlow.Studio/Pages/Harvest.razor`) in the
standalone local Blazor Server Studio app. The operator can: paste YouTube channel
URL/handle/ID and browse recent videos (HARV-01), paste individual video URLs/IDs to a queue
(HARV-02), see per-video harvested/distilled/blocked/duplicate status badges with dedup against
already-harvested entries (HARV-03), trigger harvest on selected videos with live non-blocking
progress (HARV-04), and run a spend-gated distill with dry-run estimate + actual spend, with
re-distill explicit/opt-in (HARV-05).

**This phase is a UI wrapper, not a rewrite.** All harvest/distill/transcription logic already
exists behind `IContentKbOrchestrator` (extracted in Phase 42). The page wires Blazor UI to the
orchestrator + Core stores. No new NuGet packages.

**The visual + interaction design is fully specified and approved** in `45-UI-SPEC.md`. The
decisions below cover only the data-wiring and operator-behavior choices the UI-SPEC and the
orchestrator contract leave open.

</domain>

<decisions>
## Implementation Decisions

### Provider Mode (isSubscriptionProvider)
- **D-01:** `isSubscriptionProvider` (passed to `IDistillOrchestrator.DistillAsync`) is
  **auto-detected from the wired distill backend in the composition root** — NOT an operator
  toggle and NOT a separate env var. claude-CLI backend = subscription/`$0` = `true`; a metered
  backend (e.g. OpenAI) = `false`. Matches Phase 42 D-05 (provider selection stays host-side).
  Current backend is the claude CLI ($0). The page reads this resolved flag; the operator cannot
  misset it.

### Spend Cap Control (HARV-05)
- **D-02:** The page shows **monthly cap + remaining** (cap minus current month LLM spend). This
  requires a **new read surface** on the ledger — `ILlmSpendLedger` today exposes only
  `WouldExceedCapAsync`. Add a getter for the configured monthly cap and the current-month total
  (or a single "remaining" projection). Cap baseline is read from `DECKFLOW_LLM_MONTHLY_CAP_USD`
  (default `$15.00`) via the existing `SpendLedgerBase` resolver.
- **D-03:** The cap is **editable from the page via a session/runtime override only**. The
  operator can raise the cap for the current Studio session; the override is fed into the cap
  resolver (in-memory), NOT written to env or any persistent store. It **resets to env/default
  ($15) on Studio restart** — cost-safety: a raised cap auto-reverts. No new persistent settings
  store is added. The override must actually affect `WouldExceedCapAsync` (resolver reads the
  override when present, else env/default).

### Channel Browse Scope (HARV-01)
- **D-04:** Channel browse uses an **operator-set count, default 25**, surfaced as a numeric input
  that feeds the orchestrator `limit` parameter. Already-harvested videos appear in the list with
  `table-secondary` tinting per UI-SPEC (shown, not filtered out). Note: the channel lister runs
  serialized (`SemaphoreSlim(1)` — Pitfall 6), so large counts are slow; keep the default modest.

### Session / Queue Persistence
- **D-05:** Paste queue + channel results + checkbox selection are **in-memory component state
  only**. Page refresh or SignalR circuit drop clears the view (operator re-pastes/re-browses).
  No new storage. Consistent with a single-operator local tool.
- **D-06:** Consequence (per UI-SPEC Pitfall 7 / Dispose-cancels): a circuit drop also **cancels
  any in-flight harvest/distill** (CancellationTokenSource is disposed on `IDisposable.Dispose()`).
  Partial harvest state is safe to resume; partial distill spend is already recorded in the ledger.
  This is accepted behavior, not a bug — surface no "resume" affordance in this phase.

### Claude's Discretion
- HARV-01 **channel-video listing path**: the `IContentKbOrchestrator` facade exposes no
  "list channel videos" method. Planner/research decides whether Studio calls
  `IYouTubeChannelVideoLister` directly (storage-agnostic, host-wired like the stores) or whether
  a thin list method is added to the orchestrator. Either is acceptable; keep Core console-free
  and Studio free of any `DeckFlow.CLI` reference.
- **Status-badge / dedup data path** (HARV-03): whether the page queries `IContentVideoStore` /
  `IBlockedVideoStore` / `IContentSiteIndexStore` directly at render time (per UI-SPEC
  "Implementation note") or via a small orchestrator/query helper. UI-SPEC locks the badge
  vocabulary and resolution rules; the wiring is discretion.
- Exact shape of the ledger read surface added for D-02 (separate cap getter + month-total getter
  vs a single "cap + remaining" projection record).
- Progress-sink → UI bridge details (`IOrchestratorProgress.Report` → `InvokeAsync(StateHasChanged)`),
  including any StateHasChanged batching to avoid log-flood re-renders. UI-SPEC fixes the log-box
  markup and `role="log" aria-live="polite"`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Design contract (MUST read — locks all visuals + interaction)
- `.planning/phases/45-harvest-distill-ui/45-UI-SPEC.md` — approved UI design contract: layout,
  spacing/type/color scales, **status badge vocabulary**, per-section interaction contracts
  (channel browse, paste queue, harvest trigger, two-stage distill spend gate), copywriting
  contract, state machine (`_operationInFlight` lock), accessibility notes, and Pitfalls 5/6/7.

### Requirements
- `.planning/REQUIREMENTS.md` §HARV (HARV-01..05) — discovery/harvest/distill requirements for
  this phase; ORCH-01 (orchestrator extraction, already done in Phase 42).

### Orchestrator surface (consume — extracted Phase 42)
- `DeckFlow.Core/Orchestration/IContentKbOrchestrator.cs` — aggregating facade.
- `DeckFlow.Core/Orchestration/IHarvestOrchestrator.cs` — `HarvestAsync(limit, videoIds?, sourceId?, progress?, ct)`.
- `DeckFlow.Core/Orchestration/IDistillOrchestrator.cs` — `DistillAsync(limit, dryRun, isSubscriptionProvider, videoIds?, progress?, ct)`.
- `DeckFlow.Core/Orchestration/OrchestratorProgress.cs` — `IOrchestratorProgress.Report(string)` sink (+ Null impl).
- `DeckFlow.Core/Orchestration/HarvestResult.cs`, `DistillResult.cs` — result records the UI renders
  (`Captions`/`Whisper`/`SkippedNoCaptions`; `WouldRun`/`ProjectedSpendUsd`/`VideosDistilled`/`LlmCalls`/`LlmSpendUsd`/`VideosFiltered`/`DistillFailed`/`FailedVideoIds`/`AbortedReason`/`DryRun`).
- `DeckFlow.Core/Orchestration/ServiceCollectionExtensions.cs` — `AddContentKbOrchestrator()` DI wiring.

### Spend ledger + cap (D-02 / D-03)
- `DeckFlow.Core/Content/ILlmSpendLedger.cs` + `SpendLedgerBase.cs` — `WouldExceedCapAsync`,
  `MonthlyCapConfigurationKey = DECKFLOW_LLM_MONTHLY_CAP_USD`, `DefaultMonthlyCapUsd = 15.00m`,
  `ReadMonthlyCapUsd()` via `configurationValueResolver`. Add the read surface + honor the runtime override here.
- `DeckFlow.Core/Content/LlmSpendLedger.cs` — concrete LLM ledger.

### Stores for badge/dedup + channel listing (HARV-01/03)
- `DeckFlow.Core/Content/IContentVideoStore.cs`, `IContentSiteIndexStore.cs`, `IBlockedVideoStore.cs`.
- `DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs` — channel listing (serialized; Pitfall 6).

### Studio host (integration point)
- `DeckFlow.Studio/Program.cs` (DI), `DeckFlow.Studio/Shared/NavMenu.razor` (add "Harvest" nav,
  `oi oi-cloud-download`), `DeckFlow.Studio/Services/ContentKbOrchestratorSmokeService.cs`
  (Phase 42 smoke wiring — pattern for resolving the orchestrator from Studio).

### Prior context
- `.planning/phases/42-orchestrator-extraction/42-CONTEXT.md` — orchestrator design decisions
  (D-01..D-09): storage-agnostic, host-wired stores, progress sink, console-free Core, no CLI ref.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IContentKbOrchestrator` + sub-interfaces: complete harvest/distill logic with progress sink +
  cancellation already built. Studio resolves and calls; no domain logic in the page.
- `ContentKbOrchestratorSmokeService.cs`: existing Studio→Core resolution pattern to mirror.
- Bootstrap 5 scaffold + Open Iconic already in Studio (`wwwroot/css/bootstrap`, `open-iconic`) —
  UI-SPEC builds entirely on these; no new component library or registry.

### Established Patterns
- Phase 42: provider/store selection in the host composition root, orchestrator is storage-agnostic
  (inject ready store interfaces). Studio must NOT reference `DeckFlow.CLI`.
- `IOrchestratorProgress` is synchronous (`void Report`) by design (no async reordering); the
  Studio sink bridges to `InvokeAsync(StateHasChanged)` from the background `Task.Run`.
- Spend cap is config/env-driven via a resolver delegate — the D-03 runtime override slots into
  that resolver, no signature break.

### Integration Points
- New page `Pages/Harvest.razor` + `NavMenu.razor` entry.
- `Program.cs` DI: `AddContentKbOrchestrator()`, stores, ledger, channel lister, the runtime
  cap-override holder, and the resolved `isSubscriptionProvider` value (D-01).

</code_context>

<specifics>
## Specific Ideas

- Channel browse default count: **25**.
- Cap default baseline: **$15.00** (`DECKFLOW_LLM_MONTHLY_CAP_USD`); runtime override raises it
  for the session only.
- Provider flag derives from the claude-CLI backend currently wired ($0 / subscription).

</specifics>

<deferred>
## Deferred Ideas

- **Persisted writable spend cap** (settings store surviving restart) — rejected for this phase in
  favor of the session/runtime override (D-03). Revisit only if persistent cap config is needed.
- **Persisted draft queue/selection** (survive refresh/circuit drop) — rejected (D-05); revisit if
  operators report losing long-built queues.
- **Block/unblock + hard-delete maintenance actions** — UI-SPEC explicitly scopes these to
  Phase 46+ maintenance; out of scope here.
- **Review queue / approve-reject / publish** — REVQ-* / Phase 46 (`Review Queue + Commit-Publish`).

</deferred>

---

*Phase: 45-harvest-distill-ui*
*Context gathered: 2026-06-15*
