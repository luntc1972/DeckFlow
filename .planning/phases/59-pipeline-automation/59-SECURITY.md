---
phase: 59-pipeline-automation
slug: pipeline-automation
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-20
---

# Phase 59 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Plans: 59-01 (Core signal seam + clip count), 59-02 (Studio settings + panel), 59-03 (one-click flow + outcome card).

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| distill output → DistillResult | LLM-produced clip list count crosses into the auto-approve decision input | clip count integer (non-sensitive) |
| operator UI → auto-approve-settings.json | operator-entered on/off + cutoff persisted to local disk | two non-sensitive scalars (Enabled bool, Cutoff int) |
| auto-approve-settings.json → app at startup | a file an attacker with local FS access could edit is read back | same two scalars; sanitized on load |
| operator click → harvest+distill+approve chain | one action triggers DB writes + approval_status flips | video natural keys + approval status string |
| auto-approve decision → approval_status | clip-count gate writes 'approved' without per-video manual review | approval_status column only |
| distill provider selection → spend | metered provider must not run live distill from one-click | subscription/metered flag |
| harvest result → distill input | only harvest-ready (transcribed) ids fed to distill | YoutubeVideoId strings |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-59-01 | Tampering | DistillationSchemas.cs | mitigate | No `confidence` field added; signal reads clip count only (D-01) | closed |
| T-59-02 | Elevation of Privilege | ContentKbOrchestrator distill flow | accept | Core records clip count only; does NOT flip approval_status (host's separate step). Publish a separate gate. | closed |
| T-59-03 | Tampering | auto-approve-settings.json | mitigate | Load() validates/falls back to safe defaults on corrupt input; clamps negative→DefaultCutoff, >MaxCutoff(1000)→MaxCutoff; applied on Load AND Save; never throws | closed |
| T-59-04 | Information Disclosure | auto-approve-settings.json | mitigate | Only two scalar settings written (Enabled, Cutoff); file under operator-local studio data dir, not repo; no secrets stored | closed |
| T-59-05 | Elevation of Privilege | cutoff=0 path | accept | cutoff 0 auto-approves all but only sets approval_status; publish a separate operator gate; negative clamped to DefaultCutoff. Single-operator local tool. | closed |
| T-59-06 | Elevation of Privilege | ApplyAutoApproveAsync → SetApprovalStatusAsync | mitigate | Auto-approve sets approval_status='approved' ONLY (is_visible/is_hidden untouched); publish wholly separate operator-confirmed gate | closed |
| T-59-07 | Spoofing/Tampering (spend bypass) | one-click metered path | mitigate | One-click distills ONLY when IsSubscriptionProvider; metered gets requires-subscription message + NO DistillAsync call; mirrors Core refusal at ContentKbOrchestrator.cs:244 | closed |
| T-59-08 | Repudiation | continue-on-failure batch | mitigate | Outcome card lists distilled/auto-approved/left-in-review/dropped/failed with ids, every count canonical-sourced; failed stay pending-distill; nothing silently lost | closed |
| T-59-09 | Tampering | harvest→distill id handoff | mitigate | Distill input = harvest-ready ids (ListPendingDistillAsync ∩ selected), never raw selected ids | closed |
| T-59-10 | Denial of Service | one-click long-running batch | accept | Single-operator local tool; _operationInFlight guard + Cancel button + disposal-safe CTS bound the run | closed |
| T-59-SC | Tampering | npm/pip/cargo installs | mitigate | No new packages; in-solution only; no .csproj changes between main and cycle10; CarveOut + format gates on changed lines | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Threat Verification Detail

### T-59-01 — Tampering | DistillationSchemas.cs | CLOSED

Mitigation: no `confidence` field added to distillation schema.

Verification: `grep -ci confidence DeckFlow.Core/Knowledge/DistillationSchemas.cs` = 0. Schema
file untouched in Phase 59 commits (no diff). Plan 59-01 SUMMARY confirms: "DistillationSchemas.cs
untouched — no `confidence` field (D-01, SC4)." Verification report truth #4: `grep -ci confidence
DistillationSchemas.cs` = 0; no `LlmDistillationProviderFactory` change; existing provider called
unchanged.

Evidence: `DeckFlow.Core/Knowledge/DistillationSchemas.cs` (zero confidence matches confirmed by
grep at audit time).

### T-59-02 — Elevation of Privilege | ContentKbOrchestrator | CLOSED (accepted risk)

Disposition: accept. Compensating control: Core records clip count only; does not flip
`approval_status`. The auto-approve step is authored solely in the Studio host
(`ApplyAutoApproveAsync` in `DeckFlow.Studio/Pages/Harvest.razor`).

Verification: `grep -n "SetApprovalStatusAsync\|approval_status"
DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` returns zero matches. Core does not call
`SetApprovalStatusAsync` or reference the `approval_status` column. The `DistillResult` returned by
Core carries `DistilledVideos` (clip counts) only; the Studio host decides whether to flip status
as a separate, operator-initiated step. Accepted risk documented in Accepted Risks Log below.

Evidence: `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` (zero approval_status/
SetApprovalStatusAsync references); `DeckFlow.Studio/Pages/Harvest.razor:1518-1536`
(`ApplyAutoApproveAsync` is the sole flip site, Studio-only).

### T-59-03 — Tampering | auto-approve-settings.json | CLOSED

Mitigation: `Load()` falls back to safe defaults on corrupt/unparseable input AND applies semantic
clamp on a parsed-but-invalid cutoff.

Verification:
- `DeckFlow.Studio/AutoApproveSettingsStore.cs:42-67`: `Load()` wraps `JsonSerializer.Deserialize`
  in a try/catch on `JsonException | IOException | UnauthorizedAccessException`; returns
  `AutoApproveSettings.Default` on exception or null parse.
- Line 60: `loaded with { Cutoff = Sanitize(loaded.Cutoff) }` — semantic clamp applied on Load.
- `AutoApproveSettingsStore.cs:91-92`: `private static int Sanitize(int cutoff) => cutoff < 0 ?
  ClipCountAutoApproveSignal.DefaultCutoff : Math.Min(cutoff, MaxCutoff)` — negative maps to
  DefaultCutoff (5), above MaxCutoff (1000) clamped down.
- Line 84: `Save()` also applies `Sanitize` before writing, so a bad value cannot reach disk.
- `MaxCutoff = 1000` declared at line 19.

Evidence: `DeckFlow.Studio/AutoApproveSettingsStore.cs:19,42-67,74-87,91-92`.

### T-59-04 — Information Disclosure | auto-approve-settings.json | CLOSED

Mitigation: only two non-sensitive scalars (Enabled bool, Cutoff int) written; no secrets; file
under operator-local studio data dir, not the repo.

Verification:
- `DeckFlow.Studio/AutoApproveSettings.cs`: record contains only `bool Enabled` and `int Cutoff`.
  No connection string, password, token, or key field.
- `AutoApproveSettingsStore.cs:10-11`: XML doc states "NEVER stores secrets — the two persisted
  values are non-sensitive scalars only (T-59-04)."
- `AutoApproveSettingsStore.cs:21`: `SettingsFileName = "auto-approve-settings.json"` — written to
  the studio data directory (an operator-local path, not the repo root).
- `grep -in "secret|password|token|connection.string|apikey"` across both settings files returns
  zero production matches.
- No .csproj changes between `origin/main` and `cycle10`; file not in `.gitignore`-exempted paths
  that would accidentally commit it.

Evidence: `DeckFlow.Studio/AutoApproveSettings.cs:13-21`;
`DeckFlow.Studio/AutoApproveSettingsStore.cs:10-11,21,30-33`.

### T-59-05 — Elevation of Privilege | cutoff=0 path | CLOSED (accepted risk)

Disposition: accept. Compensating control: cutoff=0 approves all distills but only sets
`approval_status`; negative cutoff clamped to DefaultCutoff (5) by `Sanitize`; publish remains a
wholly separate operator-confirmed gate (DirectPush/Publish flow unchanged). Single-operator local
tool with no network-facing surface. Accepted risk documented in Accepted Risks Log below.

Evidence: `DeckFlow.Studio/AutoApproveSettingsStore.cs:91-92` (negative clamped); confirmed that
`SetApprovalStatusAsync` is the ceiling of what auto-approve can change (see T-59-06).

### T-59-06 — Elevation of Privilege | ApplyAutoApproveAsync → SetApprovalStatusAsync | CLOSED

Mitigation: auto-approve sets `approval_status='approved'` ONLY; `is_visible`/`is_hidden` are not
touched; publish remains a wholly separate gate.

Verification:
- Both `SetApprovalStatusAsync` overloads in `DeckFlow.Core/Content/ContentSiteIndexStore.cs`:
  - Single-key overload (lines 544-548): `UPDATE content_site_index SET approval_status = @status
    WHERE natural_key_type = @type AND natural_key_value = @value` — one column set.
  - Batch overload (lines 571-575): identical `SET approval_status = @status` per row in a
    single transaction — no other columns written.
- `DeckFlow.Studio/Pages/Harvest.razor:1525-1535` (`ApplyAutoApproveAsync`): calls
  `IndexStore.SetApprovalStatusAsync(keys, "approved", ...)` — the only approval-related write
  in the one-click path; no direct `is_visible`/`is_hidden` mutation anywhere in the auto-approve
  step.
- Verification report truth #14: "ContentSiteIndexStore.cs:571-576 SQL `UPDATE content_site_index
  SET approval_status = @status` only — no is_visible/publish columns."

Evidence: `DeckFlow.Core/Content/ContentSiteIndexStore.cs:540-589`;
`DeckFlow.Studio/Pages/Harvest.razor:1509-1536`.

### T-59-07 — Spoofing/Tampering (spend bypass) | one-click metered path | CLOSED

Mitigation: one-click distills ONLY when `DistillConfig.IsSubscriptionProvider`; metered path
surfaces a requires-subscription message and returns before any `DistillAsync` call.

Verification:
- `DeckFlow.Studio/Pages/Harvest.razor:1278-1285`: gate checks
  `!DistillConfig.IsSubscriptionProvider` immediately after harvest completes; sets
  `_oneClickMeteredMessage = "Live distill requires a subscription provider..."` and `return`s —
  no `DistillAsync` or `SetApprovalStatusAsync` call follows.
- `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs:244-254`: Core hard-refuses
  `!dryRun && !isSubscriptionProvider` with `Success=false, AbortedReason` — the Studio gate
  mirrors Core's refusal as a defense-in-depth UI layer.
- Verification report truth #13: "bUnit `OneClick_Metered_DoesNotDistill_ShowsRequiresSubscription`
  passes."
- Plan 59-03 acceptance criteria confirmed: "Metered provider → one-click does NOT call
  DistillAsync at all."

Evidence: `DeckFlow.Studio/Pages/Harvest.razor:1278-1285`;
`DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs:244-254`.

### T-59-08 — Repudiation | continue-on-failure batch | CLOSED

Mitigation: outcome card lists all six counts (harvested/distilled/auto-approved/left-in-review/
dropped/failed) with failed video ids; every count traced to a canonical `DistillResult` field;
nothing silently lost.

Verification:
- `DeckFlow.Studio/Pages/Harvest.razor:871-882`: comment block explicitly maps each count to its
  canonical source field (`N=_outcomeHarvestReadyCount`, `M=VideosDistilled`, `K=ApplyAutoApproveAsync
  return`, `L=M-K`, `D=VideosFiltered`, `F/ids=DistillFailed/FailedVideoIds`).
- Lines 330-358: the outcome card renders all six counts from these fields, plus
  `oc.FailedVideoIds` listed when `DistillFailed > 0`.
- `ApplyAutoApproveAsync` (lines 1518-1536) returns the actual count of rows flipped; if
  `SetApprovalStatusAsync` returns 0 for a video (not found), it is counted in `left-in-review`.
- Verification report truth #8 (repudiation/D-11): canonical counts confirmed. Acceptance grep:
  `ApplyAutoApprove` appears 4 times (>=2 for one-click + Stage B), `ListPendingDistillAsync` 5
  times.

Evidence: `DeckFlow.Studio/Pages/Harvest.razor:330-358,871-882,1518-1536`.

### T-59-09 — Tampering | harvest→distill id handoff | CLOSED

Mitigation: distill input = `ListPendingDistillAsync` ∩ selected, never raw selected ids.

Verification:
- `DeckFlow.Studio/Pages/Harvest.razor:1293-1303`: after harvest, calls
  `DistillOrchestrator.ListPendingDistillAsync(_cts.Token)`, intersects with `selectedSet`
  (`HashSet<string>` of the operator's just-selected ids), produces `harvestReadyIds`. Raw
  `selectedIds` are NOT passed to `DistillAsync` directly.
- Line 1328-1337: `DistillAsync(videoIds: ids)` where `ids = harvestReadyIds.AsReadOnly()` —
  only the intersection set.
- Verification report truth #11 (HIGH #2): "bUnit `OneClick_MixedBatch_DistillsOnlyHarvestReadyIds`
  passes (3 selected, 1 skipped → only 2 in distill call)."

Evidence: `DeckFlow.Studio/Pages/Harvest.razor:1293-1337`.

### T-59-10 — Denial of Service | one-click long-running batch | CLOSED (accepted risk)

Disposition: accept. Compensating controls: `_operationInFlight` guard prevents concurrent
operations; `CancellationTokenSource` bound to every long-running Task; Cancel button visible
while `_operationInFlight` is true; disposal-safe progress sink handles `ObjectDisposedException`/
`InvalidOperationException`. Single-operator local tool with no network-facing surface.
Accepted risk documented in Accepted Risks Log below.

Verification:
- `DeckFlow.Studio/Pages/Harvest.razor:862`: `private CancellationTokenSource? _cts;`
- Lines 1258,1268: `_operationInFlight = true` and `_cts = new CancellationTokenSource()` set at
  start of `HarvestAndAutoDistillAsync`.
- Lines 1363: `_operationInFlight = false` in the `finally` block.
- Lines 1538-1541: `CancelOperation()` calls `_cts?.Cancel()`.
- Lines 22-48: every action button disabled when `_operationInFlight` is true; Cancel button
  shown while true.

Evidence: `DeckFlow.Studio/Pages/Harvest.razor:22-48,862,1258,1268,1316-1326,1363,1538-1541`.

### T-59-SC — Tampering | npm/pip/cargo installs | CLOSED

Mitigation: no new packages; in-solution only; format/CarveOut gates on changed lines.

Verification:
- `git diff origin/main..cycle10 -- "*.csproj"` returns empty (no .csproj changes in Phase 59).
- Plan summaries for all three plans confirm `tech-stack.added: []`.
- `AutoApproveSettingsStore.cs` uses `System.Text.Json` — already a framework-included package.
- The pre-commit changed-lines format gate reports clean on all Phase 59 changed lines (confirmed
  in each plan SUMMARY verification section).

Evidence: `git diff origin/main..cycle10 -- "*.csproj"` (empty); 59-01/02/03 SUMMARY
`tech-stack.added: []`.

---

## Unregistered Flags

None. No SUMMARY `## Threat Flags` section was present in any of the three plan SUMMARYs —
no new attack surface was flagged by the executor during implementation. All threats are
accounted for in the register above.

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-59-01 | T-59-02 | Core records clip count only; does NOT flip approval_status. Auto-approve cannot occur from Core alone — it requires the Studio host (a separate, operator-initiated step). Publish to production remains a wholly separate operator-confirmed gate. The compensating control (no SetApprovalStatusAsync in Core) is verified: zero matches in ContentKbOrchestrator. | Operator (phase plan author) | 2026-06-20 |
| AR-59-02 | T-59-05 | Operator may set cutoff=0, which auto-approves every distill. This only flips approval_status to 'approved' — it does not publish content (publish is a separate DirectPush/Publish flow requiring operator confirmation). Negative cutoffs are clamped to DefaultCutoff (5) by Sanitize, so they cannot reach the signal. Risk is acceptable on a single-operator local tool with no network-facing surface. | Operator (phase plan author) | 2026-06-20 |
| AR-59-03 | T-59-10 | One-click batches run synchronously within the Blazor render loop (via Task.Run off the sync context). A very large batch of videos could run for several minutes on a local machine. Mitigated by the _operationInFlight guard (prevents concurrent operations), the Cancel button (operator can abort), and the disposal-safe CTS pattern. Acceptable on a single-operator local tool. | Operator (phase plan author) | 2026-06-20 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-20 | 11 | 11 | 0 | Claude (gsd-security-auditor, claude-sonnet-4-6) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log (AR-59-01 / AR-59-02 / AR-59-03)
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-20
