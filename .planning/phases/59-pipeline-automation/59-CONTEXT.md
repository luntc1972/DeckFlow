# Phase 59: Pipeline Automation - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning

<domain>
## Phase Boundary

The operator harvests a video in Studio and gets a distilled, review-ready (often
already-approved) entry in **one action** — no separate manual "Distill" click
(AUTO-01) — and high-confidence distills **auto-approve** (skip the review queue)
while low-confidence distills stay in the queue (AUTO-02).

In scope: the harvest→auto-distill→auto-approve pipeline on the Studio Harvest page,
a derived per-distill quality signal, and operator controls for the auto-approve
threshold + on/off. Builds entirely on the existing Cycle 9 Core orchestrator
distill/approve slice — **no distill provider or model swap**.

Out of scope (this phase): the more sophisticated composite quality signal (deferred
to when the KBVAL A/B harness exists), scheduled/cron harvest (AUTO-03), bulk creator
onboarding (AUTO-04), and the creator-source / UI-polish work owned by Phases 60-62.
</domain>

<decisions>
## Implementation Decisions

### Quality signal (AUTO-02 confidence)
- **D-01:** No confidence signal exists in distill output today (the distill produces
  only: a `summary`, a keep/drop `verdict`, 3-8 `clips`, and `tags`). The auto-approve
  signal for THIS phase is **clip count** — the simplest defensible heuristic derived
  from existing output. Do **not** add a model-returned confidence field or otherwise
  change the distill schema/provider/model.
- **D-02:** A richer composite signal (clip-count + tag coverage + summary
  completeness) is intentionally deferred until the KBVAL A/B harness is built (v2,
  out of this cycle). The clip-count approach must be structured so it can be swapped
  for a composite later without reworking the auto-approve plumbing.

### Auto-approve threshold + config (AUTO-02)
- **D-03:** Default cutoff = **≥ 5 clips** (upper-middle of the 3-8 range). A distill
  with 5+ clips auto-approves; 3-4-clip distills remain in the review queue.
- **D-04:** The cutoff value is **operator-adjustable** and auto-approval can be
  **turned off entirely**. With it off, every distill enters the review queue (SC3).
- **D-05:** Both controls (on/off toggle + clip-cutoff number input) live **on the
  Harvest page** as a small "Auto-approve" panel at point-of-use — no new settings
  page, no env-var-only path.
- **D-06:** Auto-approval ships **ON by default** (automation is the point of the phase).
- **D-07:** The on/off + cutoff settings **persist across Studio restarts** (set once
  and forget — unlike the session-only `SessionCapOverride`). Planner/researcher to
  determine the local persistence mechanism (e.g. local DB/config), reusing existing
  Studio config conventions where possible.

### Spend gate interaction (AUTO-01 / SC4)
> **AMENDED 2026-06-20 (Codex plan review HIGH #1, verified against code):**
> `ContentKbOrchestrator.DistillAsync` (ContentKbOrchestrator.cs:244) hard-refuses any
> live (`!dryRun`) distill on a metered provider — it returns `Success=false,
> AbortedReason` and never completes. So metered *live* distill is unsupported by Core
> today; the original D-09 ("auto-approve applies to metered after confirm + distill
> completes") was impossible. One-click + auto-approve are therefore scoped
> **subscription-only** this phase. The operator's live backend is the subscription
> claude-CLI ($0), so this matches reality. SC4 is satisfied trivially: Core itself
> refuses metered spend.
- **D-08:** One-click harvest→auto-distill runs **inline, subscription ($0) providers
  only** (the operator's current claude-CLI backend, gated on
  `StudioDistillConfig.IsSubscriptionProvider`). On a **metered** provider the one-click
  action does **not** distill — it surfaces a clear message that live distill requires a
  subscription provider (mirroring Core's existing refusal at ContentKbOrchestrator.cs:244)
  and directs the operator to the existing manual Distill section (whose dry-run preview
  stays available). Auto-distill never silently spends money; the existing distill
  provider/model is used unchanged. This honors SC4 (no bypass of the spend ceiling).
- **D-09:** Auto-approve (clip-count check) is **independent** of the spend/provider gate
  but applies only to distills that **actually complete** — i.e. subscription distills in
  this phase. Auto-approve runs as a shared post-distill step so that if metered live
  distill is ever made legal in Core (out of scope now), the same step applies without
  rework. Metered live distill auto-approve is **deferred** until that Core gate changes.

### Scope + failure UX
- **D-10:** The one-click action processes the full selected harvested batch and is
  **continue-on-failure** — a single bad transcript/distill does not halt the batch.
- **D-11:** After the action, surface a **per-video outcome summary** in one result
  card: harvested N / distilled M / auto-approved K (≥5 clips) / left-in-review L /
  dropped D (keep-drop verdict = drop) / failed F (with video ids). Nothing is silently
  lost; failed videos stay pending-distill, dropped videos get no site-index entry.
  Reuse existing `DistillResult` fields (`VideosDistilled`, `VideosFiltered`,
  `DistillFailed`, `FailedVideoIds`) where they already carry this.
- **D-12:** The existing **manual Distill section stays** on the Harvest page as a
  fallback — it still owns re-distill (double-confirm overwrite), the metered
  dry-run→confirm gate, and the post-restart "Load harvested (pending distill)" loader.
  Nothing is removed; one-click auto-distill becomes the default path beside it.

### Claude's Discretion
- Exact local persistence mechanism for the auto-approve settings (D-07) — pick the
  lightest option consistent with existing Studio config/DB conventions.
- How the auto-approve check is wired relative to `SetApprovalStatusAsync` (single vs
  batch transactional setter) — choose per existing orchestrator patterns.
- Where the derived clip-count signal is computed (Core orchestrator vs Studio) — keep
  it swappable for a future composite signal (D-02).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase definition & requirements
- `.planning/ROADMAP.md` §"Phase 59: Pipeline Automation" — goal, depends-on, 4 success
  criteria, UI hint, and the explicit open risk ("a per-distill quality/confidence
  signal may not exist yet … No distill provider or model swap is permitted").
- `.planning/REQUIREMENTS.md` — AUTO-01, AUTO-02 (full text), plus the Out-of-Scope
  table row "Distill provider/model swap" and the AUTO-03/04 deferrals.

### Existing pipeline code (Cycle 9 distill/approve slice — build on, don't replace)
- `DeckFlow.Studio/Pages/Harvest.razor` — current harvest + 2-stage (dry-run/confirm)
  distill UI, spend-cap display, re-distill double-confirm, pending-distill loader,
  `RenderBadge` status vocabulary. The one-click flow + auto-approve panel land here.
- `DeckFlow.Core/Orchestration/IDistillOrchestrator.cs` + `ContentKbOrchestrator.cs`
  (`DistillAsync`, `ListPendingDistillAsync`) — the distill entry point; auto-distill
  reuses it (no signature/provider change beyond what auto-approve needs).
- `DeckFlow.Core/Orchestration/DistillResult.cs` — result fields reused for the
  per-video outcome summary (D-11).
- `DeckFlow.Core/Knowledge/DistillationSchemas.cs` — the distill output contract
  (summary / classification keep-drop / clips 3-8 / tags). Confirms the clip-count
  signal source and that no `confidence` field exists (must NOT be added).
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — `SetApprovalStatusAsync`
  (single + batch transactional), and the "new rows insert approval_status='pending'"
  rule that auto-approve flips to 'approved'.
- `DeckFlow.Studio/StudioDistillConfig.cs` — `IsSubscriptionProvider` flag that gates
  inline vs confirm-required auto-distill (D-08).
- `DeckFlow.Studio/SessionCapOverride.cs` — example of a session-only setting; the
  auto-approve settings deliberately differ (persist, D-07).
- `DeckFlow.Core/Content/VideoStatusResolver.cs` + the Cycle 9 `PublishStateDeriver` —
  status vocabulary the result summary/badges should reuse (no duplicate status logic;
  Phase 62 SUI-01 will lean on the same).

### Codebase maps
- `.planning/codebase/ARCHITECTURE.md`, `STRUCTURE.md`, `CONVENTIONS.md` — Studio +
  Core orchestration layout and conventions.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IDistillOrchestrator.DistillAsync(...)` — already supports `dryRun`,
  `isSubscriptionProvider`, `redistill`, targeted `videoIds`, progress sink, cancel.
  Auto-distill calls this directly after harvest; subscription path runs it inline.
- `DistillResult` (`VideosDistilled`, `VideosFiltered`, `DistillFailed`,
  `FailedVideoIds`, `LlmSpendUsd`) — already carries most of the per-video summary data
  for D-11.
- `IContentSiteIndexStore.SetApprovalStatusAsync` (single + batch atomic) — the exact
  hook for auto-approve; only `approval_status` is mutated (is_visible/is_hidden/
  is_evergreen untouched).
- `Harvest.razor` `ActionOrchestratorProgress` + disposal-safe progress sink pattern
  (T-45-18) and the existing spend-cap / cap-raise display — reuse for the one-click run.
- `RenderBadge` / `VideoStatus` vocabulary — reuse for outcome rows.

### Established Patterns
- Studio orchestrator calls run via `Task.Run` off the Blazor sync context (Pitfall 1);
  progress marshalled through `InvokeAsync` with `ObjectDisposedException`/
  `InvalidOperationException` swallowed on circuit drop. Auto-distill must follow this.
- Subscription ($0) vs metered branching is already the spend-gate seam — auto-distill's
  inline-vs-confirm decision (D-08) plugs into the same `IsSubscriptionProvider` check.
- New site-index rows insert `approval_status='pending'`; approval is a separate,
  column-only mutation. Auto-approve = post-distill conditional flip to 'approved'.

### Integration Points
- Harvest button handler (`HarvestSelectedAsync`) → chain into auto-distill for the
  just-harvested ids (subscription) or hand off to the existing confirm gate (metered).
- Post-distill: evaluate clip count per distilled video → `SetApprovalStatusAsync` when
  ≥ cutoff and auto-approve is ON.
- New persisted auto-approve settings store (D-07) read at page init, written on change.
</code_context>

<specifics>
## Specific Ideas

- "Do clip count until A/B is worked on" — the operator explicitly wants the simplest
  signal now (clip count), with the composite quality signal gated behind the future
  KBVAL A/B harness. Don't over-engineer the signal this phase; keep it swappable.
- Operator's live backend is the subscription claude-CLI ($0), so the inline
  auto-distill path (D-08) is the common case; the metered confirm path is the guard
  rail, not the everyday flow.
</specifics>

<deferred>
## Deferred Ideas

- **Composite quality signal** (clip-count + tag coverage + summary completeness, or a
  model-returned confidence) — revisit when KBVAL-01/02 (A/B value harness) is built.
  Out of this cycle.
- **Model-returned confidence field** in the distill schema — rejected for Phase 59
  (borderline vs the "no provider/model swap" constraint); reconsider only alongside the
  composite signal above.
- **New Studio /settings page** for auto-approve and future knobs — not worth a new page
  for two settings this phase; the Harvest-page panel suffices. A settings page could be
  revisited if Studio accrues more global knobs (relates to Phase 62 SUI cleanup).
- Scheduled/cron harvest (AUTO-03) and bulk creator onboarding (AUTO-04) — explicitly v2.
</deferred>

---

*Phase: 59-pipeline-automation*
*Context gathered: 2026-06-20*
