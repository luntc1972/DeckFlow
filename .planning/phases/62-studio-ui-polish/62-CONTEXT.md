# Phase 62: Studio UI Polish - Context

Cycle 10 — Studio Automation, Sync & Polish. **Final phase** (after 59 + 60 + 61 + 63 complete).
Authored manually 2026-06-21 in the cycle10 worktree (operator chose manual planning; Codex
peer-reviews before execute). Presentation pass over the now-settled Studio surfaces — no new
data/behavior lanes (those were 59/60/61).

## Phase Boundary

**In:** Make pipeline state obvious at a glance and the overall harvest → review → publish *flow*
(navigation/affordances only) fast/clear: consistent status badges (Harvest + Review),
creator filtering (Harvest + Review), better feedback states (incl. a live Pull-from-Prod progress
view), fewer clicks, denser layout/nav, and the one-line MainLayout About-link fix. NOTE: the Publish
page itself gets only the navigation/feedback touches (Review→Publish link, sanitized load errors) —
it has NO per-row list, so SUI-01 badges and SUI-05 creator filter do NOT apply to it.

**Out:** New stores, new workflows, new prod paths. No theme system / Playwright (Studio is a local
single-theme Blazor app, bUnit-tested — the web-page themes/mobile rule targets DeckFlow.Web, not
Studio). No DeckFlow.Web changes.

**Requirements:** SUI-01, SUI-02, SUI-03, SUI-04, SUI-05, SUI-06.

## Implementation Decisions (LOCKED)

### SUI-01 — one shared status-badge component, reuse existing status engine
`RenderBadge(VideoStatus)` currently lives inline in `Harvest.razor` (RenderFragment switch, used 3×).
Extract it to a single reusable component `Shared/StatusBadge.razor` (parameter: `VideoStatus`) and
use it on the pages that have a per-item status LIST — **Harvest + Review only**. (Codex review:
Publish.razor has NO per-row list today, only an approved-count + publish-state SUMMARY; adding a
per-row Publish list is net-new behavior, out of this polish phase — Publish is excluded from SUI-01.)
NO duplicate status logic — Review derives its row VideoStatus via a SHARED pure mapper
`VideoStatus FromContentRow(approvalStatus, pushedToProdUtc, isVisible)` that `VideoStatusResolver`
also uses, so the rule lives in one place. Markup/classes stay byte-identical so nothing regresses.

### SUI-05 — creator filter derived from the existing row data
Creator is already implicit: browse rows carry `ChannelTitle`; stored rows carry `ArtifactPath`
(`content-kb/<creator-slug>/<id>.md`). Add a creator filter control (dropdown) to the
video/entry lists on **Harvest browse + Review** (Publish excluded — no per-row list, Codex review)
that filters the rendered rows by creator parsed from that existing data — NO new store, NO new
column. Default = all creators. On Harvest the predicate folds into the Phase-61
`GetVisibleChannelVideos()` projection so Select-All/harvest already respect it.

### SUI-03 — live Pull-from-Prod progress view + consistent feedback (operator request)
`PullFromProd.razor` already tracks `_pullStage` and the downloader exposes an
`IProgress<SshDownloadResult>` (currently passed `progress: null`). Wire a progress sink that appends
each stage transition + per-artifact result into a scrolling UI panel that streams live as the pull
runs (not just spinners + final table). Apply the same loading/error/success-feedback consistency
(spend warnings already exist on Harvest; ensure clear failure messages) — all UI copy stays
sanitized (D-07: never surface ex.Message / paths). The panel must not change the read-only-toward-prod
invariant.

### SUI-02 + SUI-04 — flow tightening + density, no behavior change
Fewer clicks (sensible defaults, multi-select ergonomics, less page back-and-forth) and a denser,
clearer layout/nav. These are presentation-only — must not alter what any action does, only how
quickly/clearly the operator reaches it. Reuse existing handlers.

### SUI-06 — one-line About-link fix
`Shared/MainLayout.razor` "About" link points at `https://docs.microsoft.com/aspnet/` (a leftover
Blazor scaffold, with a TODO). Repoint it to a real, relevant target (the deckflow.gg site) and
remove the TODO. Trivial.

## Canonical References (read before executing)

### Surfaces
- `DeckFlow.Studio/Pages/Harvest.razor` — `RenderBadge` (line ~2053), 3 badge call sites, browse list, multi-select. (SUI-01 badge + SUI-05 creator filter apply here.)
- `DeckFlow.Studio/Pages/Review.razor` — per-entry list (rows carry `ArtifactPath`). (SUI-01 badge + SUI-05 creator filter apply here.)
- `DeckFlow.Studio/Pages/Publish.razor` — READ-ONLY reference: confirm it has only an approved-count/publish-state SUMMARY (no per-row list), so SUI-01/SUI-05 do NOT apply; it gets only the Review→Publish nav target + sanitized load errors.
- `DeckFlow.Studio/Pages/PullFromProd.razor` — `_pullStage`, the `progress: null` download call (~line 409). (SUI-03 live progress.)
- `DeckFlow.Studio/Pages/{Blocked,Skipped,CreatorSources}.razor` — small list pages (density/nav only if relevant).
- `DeckFlow.Studio/Shared/{MainLayout,NavMenu}.razor` — About link + nav.
- `DeckFlow.Core/Content/VideoStatus.cs`, `VideoStatusResolver` — status source. SUI-01 adds the shared `FromContentRow` mapper here and routes `VideoStatusResolver` through it (reuse, do not duplicate). `PublishStateDeriver` referenced only as the existing publish-state engine.

### Project rules
- Claude codes / Codex reviews (until 2026-06-24). One public type per file; LF endings; changed-lines
  format gate; bUnit for Studio tests; README updated when behavior changes; no new packages.
- Build with Windows `dotnet.exe` in the worktree (Linux dotnet absent; Web .sln fails on tsc — build
  Studio/Core individually). Known pre-existing parallel-isolation flake (BlockedPage/ReviewPage) —
  passes in isolation; do not let it mask real failures.

## Threat / safety notes (per-plan STRIDE)
- D-07 holds everywhere: no ex.Message / DB path / connection string / SSH target in any UI copy or
  log surfaced to the operator — especially the new live progress panel (it streams stage names +
  per-artifact *sanitized* results, never raw exceptions).
- SUI-03 must preserve PullFromProd's read-only-toward-prod invariant — the progress panel is display
  only; it adds no write path.
- Creator filter parses untrusted `ArtifactPath` / `ChannelTitle` — treat as display strings; no SQL,
  no path traversal (filtering is in-memory over already-loaded rows).

## Plan map
- 62-01 (wave 1): SUI-01 shared `StatusBadge.razor` (extract from Harvest, apply to Harvest + Review;
  Publish excluded — no per-row list) + shared `FromContentRow` status mapper + SUI-06 MainLayout
  About-link fix. bUnit.
- 62-02 (wave 2, depends 62-01): SUI-05 creator filter on the Harvest + Review lists (Publish excluded). bUnit.
- 62-03 (wave 2): SUI-03 live Pull-from-Prod progress view + feedback-state consistency. bUnit.
- 62-04 (wave 3, depends 62-01/02): SUI-02 flow tightening + SUI-04 layout/nav density. bUnit.
