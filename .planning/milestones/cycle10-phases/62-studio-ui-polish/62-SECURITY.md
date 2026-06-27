# SECURITY.md — Phase 62: Studio UI Polish

**Audit date:** 2026-06-21
**Auditor model:** claude-sonnet-4-6
**ASVS Level:** 1
**block_on:** high

---

## Threat Verification

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-62-01 | Tampering — StatusBadge | mitigate | CLOSED | See below |
| T-62-02 | Tampering — CreatorNameResolver | mitigate | CLOSED | See below |
| T-62-03 | Integrity — creator filter | mitigate | CLOSED | See below |
| T-62-04 | Information disclosure — Pull Log panel | mitigate | CLOSED | See below |
| T-62-05 | Integrity — progress view | mitigate | CLOSED | See below |
| T-62-06 | DoS — _progressLog | mitigate | CLOSED | See below |
| T-62-07 | Tampering — defaults | mitigate | CLOSED | See below |

---

## Per-Threat Evidence

### T-62-01 — Tampering: StatusBadge render-only over existing VideoStatus

**Declared mitigation:** badge renders an already-resolved VideoStatus enum; no status recomputation.

**Evidence:**

`DeckFlow.Studio/Shared/StatusBadge.razor` lines 7–33: component is a pure `@switch (Status)` over the
`VideoStatus` enum parameter. The only `@code` block (lines 35–39) declares a single
`[Parameter] public VideoStatus Status { get; set; }` property. No store access, no service injection,
no status resolution logic — the badge is purely a mapping from enum value to Bootstrap markup.

`DeckFlow.Core/Content/VideoStatusResolver.cs` lines 55–73: `FromContentRow` is a pure `static`
method; `ResolveStatusAsync` (lines 92–128) routes through it at line 111 for index-row branches.
`Review.razor` calls `VideoStatusResolver.FromContentRow(...)` at line 129 directly — both callers
resolve status independently and then pass the already-resolved enum to `<StatusBadge Status="...">`.

**CLOSED** — mitigation present at cited locations.

---

### T-62-02 — Tampering: CreatorNameResolver traversal-safe

**Declared mitigation:** pure string split with "Unknown" fallback; never used as a path or SQL.

**Evidence:**

`DeckFlow.Studio/Services/CreatorNameResolver.cs`:
- Line 9: `public static class CreatorNameResolver` — static class, no I/O dependencies, no DI.
- Lines 27–30: null/whitespace input → returns `"Unknown"`.
- Lines 33–35: `Path.IsPathRooted(artifactPath)` check → returns `"Unknown"` for rooted paths.
- Line 39: `artifactPath.Replace('\\', '/')` normalizes separators before splitting.
- Lines 44–46: rejects paths with `<3` segments or any `".."` segment → returns `"Unknown"`.
- Line 49: extracts `segments[1].Trim()` (the creator slug) — never passed to `File.*`, `Directory.*`, or
  any SQL context.
- `FromChannelTitle` (lines 59–63): trim + null/empty guard; returns `"Unknown"`.

The result is used only as a display filter value in Harvest.razor and Review.razor (equality
comparison against `_browsCreatorFilter` / `_reviewCreatorFilter` strings).

**CLOSED** — mitigation present at cited locations.

---

### T-62-03 — Integrity: creator filter folded into GetVisibleChannelVideos()

**Declared mitigation:** creator predicate folded into `GetVisibleChannelVideos()` so a
filtered-out row cannot be harvested.

**Evidence:**

`DeckFlow.Studio/Pages/Harvest.razor` lines 1105–1111:
```
private IReadOnlyList<VideoViewModel> GetVisibleChannelVideos()
    => _channelVideos
        .Where(vm => !_skippedVideoIds.Contains(vm.VideoId)
            && (_showAllVideos || vm.Status == VideoStatus.NotHarvested)
            && (string.IsNullOrEmpty(_browsCreatorFilter)
                || CreatorNameResolver.FromChannelTitle(vm.ChannelTitle) == _browsCreatorFilter))
        .ToList();
```

Harvest entry points trace through the same method:
- Line 1119: `ToggleAllChannelSelections` calls `GetVisibleChannelVideos()`.
- Lines 1263–1268: `GetAllSelectedVideos()` calls `GetVisibleChannelVideos().Where(v => v.Selected)`.
- Lines 1356, 1402: `HarvestSelectedAsync` and `HarvestAndAutoDistillAsync` both call
  `GetAllSelectedVideos()` — which roots through `GetVisibleChannelVideos()`.

A row not visible due to the creator filter is therefore excluded from every harvest path.

For Review.razor: `CreatorFilteredRows` (lines 284–289) applies the creator predicate on top of the
tab filter; `ToggleSelectAll` (line 365) iterates `CreatorFilteredRows`, and `RenderBatchBar`
(line 644) sources `checkedRows` from `CreatorFilteredRows` — hidden rows cannot be acted on.

**CLOSED** — mitigation present at cited locations.

---

### T-62-04 — Information disclosure: Pull Log panel sanitization (D-07)

**Declared mitigation:** panel renders only fixed stage strings + `SshDownloadResult` fields
(`RemoteRelativePath`, `Success`, sanitized `FailureReason`); `LocalPath` never rendered;
failures use sanitized copy.

**Evidence:**

`DeckFlow.Studio/Pages/PullFromProd.razor`:

- Lines 478–496: progress callback comment explicitly states "NEVER LocalPath or ex.Message
  (D-07 / T-62-04)"; the callback builds a line from `result.RemoteRelativePath` and
  `result.FailureReason` only — `result.LocalPath` is not referenced anywhere in the callback.
- Lines 551–555: catch block for `Exception ex` — comment "NEVER surface ex.Message in the UI (D-07)";
  `_pullError` is set to a fixed template string containing only the `_pullStage` string (a small set
  of hard-coded stage names); `ex` is passed only to `Logger.LogError` (server-side Serilog).
- Line 560: failure progress log line: `$"Pull failed during: {_pullStage} — see the Studio log for
  details."` — no `ex.Message`, no path, no connection string.
- Lines 668–671 (per-entry apply catch): similar pattern; `Logger.LogError(ex, ...)` on server side,
  UI note string `"Local apply failed for this entry — see logs."`.
- Lines 711–712 (outer apply catch): `"Applying resolutions failed — ..."` fixed copy; `ex` only
  to `Logger.LogError`.

`DeckFlow.Studio/Pages/Review.razor` lines 319–323: `OnInitializedAsync` catch — `_ = ex;`
(consumed, not surfaced); `_loadError = "Could not load review queue — check the Studio data directory
and retry."` (fixed sanitized copy, no `ex.Message`).

**CLOSED** — mitigation present at cited locations.

---

### T-62-05 — Integrity: progress view adds no prod write path

**Declared mitigation:** display-only; reader/downloader calls unchanged except the read-path
progress sink.

**Evidence:**

`DeckFlow.Studio/Pages/PullFromProd.razor`:

- The progress panel (lines 90–113) is a `@if (_pullInFlight || _progressLog.Count > 0)` render block
  with a `<pre>` element only — no form, no button, no action handler.
- The `artifactProgress = new Progress<SshDownloadResult>(...)` callback (lines 480–496) calls only
  `AppendProgressLine` and `StateHasChanged` — no store writes, no SSH sends.
- The `_progressLog` field (line 354) is `List<string>` — display-only state.
- Stage 1 pull path calls `ProdReader.ReadAllAsync` (read) and
  `SshDownloader.DownloadArtifactsAsync` (download to local staging) — both calls are unchanged from
  the pre-Phase-62 implementation. No new write call was introduced.
- Stage 2 `ApplyResolutionsAsync` (lines 569–718) writes exclusively to `IndexStore` (local) and
  `File.Move` within `_stagingRoot`/`_dataRoot` — production is not involved. This method predates
  Phase 62 and was not modified.

**CLOSED** — mitigation present at cited locations.

---

### T-62-06 — DoS: _progressLog bounded to last N lines

**Declared mitigation:** list capped to last N lines (~500).

**Evidence:**

`DeckFlow.Studio/Pages/PullFromProd.razor`:
- Line 353: `private const int ProgressLogMaxLines = 500;`
- Lines 737–743 (`AppendProgressLine` helper):
  ```csharp
  _progressLog.Add(line);
  if (_progressLog.Count > ProgressLogMaxLines)
  {
      _progressLog.RemoveAt(0);
  }
  ```
  Every progress append routes through this helper (lines 426, 443, 454, 463, 491, 509, 524, 543, 560)
  — there is no direct `_progressLog.Add(...)` call outside the helper.

**CLOSED** — mitigation present at cited locations.

---

### T-62-07 — Tampering: conservative defaults preserve harvest/publish projection

**Declared mitigation:** conservative defaults; canonical visible/selected projection still gates
harvest/publish; existing tests guard behavior.

**Evidence:**

`DeckFlow.Studio/Pages/Review.razor`:
- `RenderGoToPublishLink` (lines 697–714) adds navigation only (`<a href="/publish">`); no handler,
  no data mutation. Condition `if (approvedCount == 0) return @<text></text>;` ensures the link is
  absent when no approved rows exist.
- `ToggleSelectAll` (line 361) calls only `CreatorFilteredRows` — an already-filtered view; never
  selects hidden rows.

`DeckFlow.Studio/Pages/Harvest.razor`:
- `_browsCreatorFilter` defaults to `string.Empty` (line 925), which means "All creators" — the
  `GetVisibleChannelVideos()` check at line 1109
  `string.IsNullOrEmpty(_browsCreatorFilter)` short-circuits to show all rows when unset.
- The HSEL-01 default (`_showAllVideos = false`) is unchanged from prior phases (line 921).
- All harvest triggers (`HarvestSelectedAsync`, `HarvestAndAutoDistillAsync`) call
  `GetAllSelectedVideos()` which roots through `GetVisibleChannelVideos()` — the canonical
  visible/selected projection is unmodified.

No new checkbox, toggle, or default widens what the harvest or publish actions operate on.

**CLOSED** — mitigation present at cited locations.

---

## Unregistered Threat Flags

`62-02-SUMMARY.md` Threat Flags section: "No new network endpoints, auth paths, file-access patterns,
or schema changes. CreatorNameResolver reuses the same containment/traversal-rejection logic as
`ReadArtifactSafe` and only performs in-memory string split."

`62-03-SUMMARY.md` Threat Flags section: "None — the progress panel adds no new external surface;
production is read-only (unchanged); all panel content is sanitized."

`62-04-SUMMARY.md` Threat Flags section: "None. This plan is presentation/navigation only."

None of the SUMMARY.md Threat Flags sections identify new attack surface without a threat mapping.

**No unregistered flags.**

---

## Accepted Risks Log

None declared; all threats are mitigated.

---

## Audit Result

**threats_open: 0 / 7**
**Result: SECURED**
