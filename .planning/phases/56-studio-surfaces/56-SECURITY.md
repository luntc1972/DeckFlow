# Phase 56 — Studio Surfaces — Security Audit (State B, retroactive)

**Phase:** 56-studio-surfaces
**Branch:** cycle9
**Audited:** 2026-06-18 (T-56-03-03 re-verified after remediation `96a8991`)
**Auditor:** Claude (gsd-secure-phase)
**ASVS Level:** 1 (local single-operator desktop tool, no public auth surface)
**Method:** FORCE stance — every declared mitigation treated as absent until a grep/read match proved it present at a specific `file:line` in committed code. Documentation/intent not accepted as evidence.

## Summary

| Metric | Count |
|--------|-------|
| Total threats (incl. -SC supply-chain entries) | 18 |
| CLOSED (mitigation verified in code) | 17 |
| ACCEPTED (documented accepted risk) | 1 (T-56-03-04) |
| OPEN (declared mitigation absent / not as-described) | 0 |

**Verdict: PASS — no open threats.** The single residual gap (T-56-03-03, information-disclosure hygiene on Blocked.razor) was remediated in commit `96a8991` and re-verified at the file:line level below. All 17 `mitigate` threats are CLOSED with code evidence; the 1 `accept` threat (T-56-03-04) is documented in the accepted-risks log. Disposition gate (`block_on: open`) is satisfied.

Build/test state at audit (from 56-VERIFICATION.md, re-confirmed by git log): full solution builds 0 err / 0 warn; Studio.Tests 47/47 serial; Core VideoStatusResolverTests 8/8. No `.csproj` / `package.json` / lockfile changes across the 11 phase-56 commits (`HEAD~8..HEAD`), so every `-SC` "no package installs" threat is satisfied by construction.

---

## Per-threat verdicts

### Plan 56-01 — VideoStatus / VideoStatusResolver

| Threat ID | Category | Disposition | Verdict | Evidence |
|-----------|----------|-------------|---------|----------|
| T-56-01-01 | Tampering | mitigate | **CLOSED** | Total resolution order present and ordered Blocked > Published > Approved > Distilled > Harvested > NotHarvested in `VideoStatusResolver.cs:60-104` (Blocked first `:61-64`; Published `:77-80`; Approved `:83-86`; Distilled `:88`; Harvested `:93-101`; NotHarvested `:104`). Every arm unit-pinned in `VideoStatusResolverTests.cs` — Blocked `:173`, Distilled `:190`, Harvested `:207`, NotHarvested `:238`, Approved `:255`, Published `:270`, post-unblock NotHarvested `:306`. A dropped state fails CI. |
| T-56-01-02 | Information Disclosure | mitigate | **CLOSED** | Pushed-but-hidden deliberately falls through Published guard (`indexRow.PushedToProdUtc.HasValue && indexRow.IsVisible`, `VideoStatusResolver.cs:77`) to Approved `:83-86`; operator never sees "Published" for not-yet-visible content. Pinned by `ResolveStatusAsync_PushedButHidden_ReturnsApproved` (`VideoStatusResolverTests.cs:288-304`, asserts `VideoStatus.Approved` for pushed + `isVisible:false`). |
| T-56-01-SC | Tampering | mitigate | **CLOSED** | No package installs; no `.csproj`/lockfile delta in phase-56 commits (`git log HEAD~8..HEAD -- **/*.csproj` empty). |

### Plan 56-02 — Publish-state surfaces (Review / Publish)

| Threat ID | Category | Disposition | Verdict | Evidence |
|-----------|----------|-------------|---------|----------|
| T-56-02-01 | Tampering | mitigate | **CLOSED** | Both pages derive publish-state ONLY via `PublishStateDeriver.Derive`: Review per-row `Review.razor:128` (`Deriver.Derive(vm.PushedToProdUtc, vm.IsVisible, vm.IndexedUtc)`); Publish summary `Publish.razor:296` (`GroupBy(r => Deriver.Derive(...))`). Grep for inline four-state logic (`PushedToProdUtc.HasValue` style if/else) in both pages → NONE. `RenderPublishStateBadge` is a pure `switch` over the already-derived `PublishState` (`Review.razor:621-628`, `Publish.razor:329-336`), not a re-derivation. Deriver DI-registered `Program.cs:110`. |
| T-56-02-02 | Information Disclosure | mitigate | **CLOSED** | Every badge arm carries a text label, never color-only: `Never published` / `Pushed-hidden` / `Published` / `Local-newer` (`Review.razor:623-626`, `Publish.razor:331-334`). Published badge additionally carries a checkmark + text (`<span class="oi oi-check me-1" aria-hidden="true"></span>Published`, `Review.razor:625`, `Publish.razor:333`). |
| T-56-02-03 | Denial of Service | mitigate | **CLOSED** | Summary computed inside the SINGLE existing `Task.Run` that already fetches rows — Review `OnInitializedAsync` `Review.razor:287-291`; Publish `OnInitializedAsync` `Publish.razor:290-302` (GroupBy runs inside the same `Task.Run`, returning `summary` in the tuple). No second store call added. UI applied via disposal-safe `InvokeAsync(StateHasChanged)` in `finally` (`Review.razor:303-307`, `Publish.razor:323-326`). |
| T-56-02-SC | Tampering | mitigate | **CLOSED** | No package installs (bUnit 2.7.2 + xUnit 2.9.3 pre-existing). No csproj/lockfile delta. |

### Plan 56-03 — Blocked videos page (/blocked)

| Threat ID | Category | Disposition | Verdict | Evidence |
|-----------|----------|-------------|---------|----------|
| T-56-03-01 | Tampering | mitigate | **CLOSED** | Unblock passes the exact row id: `@onclick="() => UnblockAsync(video.YoutubeVideoId)"` (`Blocked.razor:57`) → `UnblockVideoAsync(videoId, progress: null, _cts.Token)` (`Blocked.razor:119`). Pinned by `BlockedPage_Unblock_RemovesRow` asserting `fake.UnblockCalls` contains the clicked id "abc123" (`BlockedPageTests.cs:83-88`). |
| T-56-03-02 | Denial of Service | mitigate | **CLOSED** | Both orchestrator calls wrapped in `Task.Run` off the Blazor sync context: list `Blocked.razor:88-90`, unblock `Blocked.razor:118-120`. UI updates via disposal-safe `InvokeAsync` with `ObjectDisposedException`/`InvalidOperationException` swallow on the unblock path (`Blocked.razor:134-139`); load finally uses `InvokeAsync(StateHasChanged)` `:104`. |
| **T-56-03-03** | **Information Disclosure** | **mitigate** | **CLOSED** | **Remediated in commit `96a8991`** ("suppress raw exception text in Blocked page"). Re-verified: (1) `OnInitializedAsync` `catch (Exception)` sets a generic operator-safe string `"Could not load blocked videos. Try again."` with NO `ex.Message` (`Blocked.razor:104-108`, guarded by `// Why: do NOT echo exception.Message` `:106`). (2) `UnblockAsync` now captures the `ContentMaintenanceResult` (`:126-128`), removes the row ONLY on `result.Success` (`:129-132`), and on `!Success` surfaces the operator-safe `result.Message` (or generic `"Unblock failed. Try again."` fallback) (`:133-138`); the `catch (Exception)` uses generic copy with NO `ex.Message` (`:143-147`, comment `:145`). (3) Grep for `ex.Message` / `.StackTrace` / `exception.Message` actual usage → NONE (only the two "do NOT echo" guard comments remain). (4) New bUnit test `BlockedPage_UnblockResultFailure_KeepsRowAndShowsSafeError` (`BlockedPageTests.cs:93-131`) pins the row-stays + safe-error behavior: asserts the clicked id is still in markup (`:127`), the safe failure message is shown (`:128`), and the raw DB path `C:\data\deckflow.sqlite` is absent (`:129`). Now matches the Harvest.razor T-56-04-05 pattern. |
| T-56-03-04 | Repudiation | accept | **ACCEPTED** | Disposition is `accept`. Documented accepted risk: unblock is an intentional, reversible recovery action on a local operator-only tool; the `blocked_videos` row removal is itself the audit record. No audit-log mitigation expected. Recorded here in the accepted-risks log per the `accept` verification method. |
| T-56-03-SC | Tampering | mitigate | **CLOSED** | No package installs; no csproj/lockfile delta. |

### Plan 56-04 — Harvest channel-browse Block / paste-add

| Threat ID | Category | Disposition | Verdict | Evidence |
|-----------|----------|-------------|---------|----------|
| T-56-04-01 | Tampering | mitigate | **CLOSED** | Two-step confirm: first click only sets `vm.PendingBlock` via `BeginBlock` (`Harvest.razor:127`, `:1260-1264`); destructive `BlockVideoAsync` runs ONLY on the explicit `Confirm Block` click (`:137` → `ConfirmBlockAsync` → `:1545-1547`). Always-visible warning `This will delete KB artifacts.` (`:147`). Focus moves to Confirm Block button (`@ref="_confirmBlockButton"` `:136`; `OnAfterRenderAsync` `FocusAsync()` `:1266-1274`). |
| T-56-04-02 | Tampering | mitigate | **CLOSED** | Exact `vm.VideoId` passed: `BlockVideoAsync(vm.VideoId, reason: null, ...)` (`Harvest.razor:1546`). Block button disabled when already Blocked: `disabled="@(_operationInFlight || vm.Status == VideoStatus.Blocked)"` (`:128`). Success-path test records `BlockCalls` id match (HarvestPageTests, per 56-VERIFICATION SC4). |
| T-56-04-03 | Tampering | mitigate | **CLOSED** | Routed exclusively through `MaintenanceOrchestrator.BlockVideoAsync` (block-first/delete-second) `Harvest.razor:1545-1547`. No direct store delete in the page — grep for `DeleteVideoByYoutubeIdAsync` / store `.Delete` in Harvest.razor → NONE. |
| T-56-04-04 | Tampering | mitigate | **CLOSED** | Pasted text flows only to the existing `Lister.GetByIdsAsync(idLines.AsReadOnly(), ...)` (`Harvest.razor:944`); no new parsing path. ADD-01 zero-resolved warning is a safe static string ("No videos found for the pasted input...") guarded by `_addToQueueDone && _lastAddCount == 0 && !string.IsNullOrWhiteSpace(_lastAddInput)` (`:198-203`) — never echoes the raw paste body. |
| T-56-04-05 | Information Disclosure | mitigate | **CLOSED** | Result-false path surfaces ONLY `result.Message` (already operator-safe from orchestrator) or a generic fallback "Block failed — the video was not removed." (`Harvest.razor:1557-1559`). Thrown path uses generic copy and explicitly does NOT echo `exception.Message` — comment `// Why: do NOT echo exception.Message (may leak paths)` (`:1564-1567`). This is the Phase 47 HIGH-2 pattern, correctly applied here. (The same pattern is now also applied in Blocked.razor as of commit `96a8991` — see T-56-03-03 CLOSED.) |
| T-56-04-06 | Repudiation | mitigate | **CLOSED** | `ConfirmBlockAsync` branches on `result.Success` (`Harvest.razor:1549`): success → `RefreshBadgesAsync` `:1552`; `!Success` → set `_blockError`, do NOT refresh badge `:1554-1561`; `finally` clears `vm.PendingBlock` on every outcome `:1572-1573`. Operator cannot mistake a silent `Success==false` for success. (Threat was listed in plan register but not in the audit prompt's explicit list — verified for completeness.) |
| T-56-04-SC | Tampering | mitigate | **CLOSED** | No package installs; no csproj/lockfile delta. |

---

## Accepted-risks log

- **T-56-03-04 (Repudiation, Plan 56-03):** Unblock silently re-allows previously-blocked content. ACCEPTED — DeckFlow Studio is a local, single-operator desktop tool with no multi-user accountability requirement; unblock is an intentional reversible recovery action; the removal of the `blocked_videos` row is the de-facto record. No audit trail required at ASVS L1 for this surface.

## OPEN threats

None. The previously-open T-56-03-03 was remediated in commit `96a8991` and is now CLOSED (see per-threat table above).

### Resolved (was OPEN) — T-56-03-03 (Information Disclosure, Plan 56-03, Blocked.razor)

- **Original gap:** Raw `exception.Message` was echoed into the operator-facing alert on both the load and unblock paths, and the unblock path removed the row unconditionally (the orchestrator result was discarded).
- **Remediation (`96a8991`):**
  1. `OnInitializedAsync` `catch (Exception)` now sets a generic operator-safe string with no `ex.Message` (`Blocked.razor:104-108`).
  2. `UnblockAsync` captures the `ContentMaintenanceResult`, removes the row only on `result.Success`, surfaces the operator-safe `result.Message` (or generic fallback) on `!Success`, and the `catch (Exception)` uses generic copy with no `ex.Message` (`Blocked.razor:126-147`).
  3. No `.StackTrace` / raw-path echo remains anywhere in the file (only "do NOT echo" guard comments).
  4. New bUnit test `BlockedPage_UnblockResultFailure_KeepsRowAndShowsSafeError` (`BlockedPageTests.cs:93-131`) pins row-stays + safe-error + no-raw-path behavior.
- **Verification method:** read + grep against the committed file; FORCE stance — confirmed each clause at file:line, not from the commit message.

## Unregistered flags

None. No `## Threat Flags` section was present in the 56-0x SUMMARY files indicating new attack surface beyond the registered threats. No new external entry points, auth surfaces, or dependencies were introduced (Blocked.razor adds a local nav route only; no new package).

---

_Implementation files were treated as READ-ONLY; no code was modified by this audit. The single formerly-OPEN item (T-56-03-03) was remediated by the team in commit `96a8991` and re-verified here at the file:line level. Final state: 17 CLOSED / 1 ACCEPTED / 0 OPEN — verdict PASS._
