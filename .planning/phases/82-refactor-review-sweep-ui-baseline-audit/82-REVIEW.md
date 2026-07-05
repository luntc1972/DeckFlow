# Phase 82 Refactor-Review Sweep — Code Review Findings

**Reviewer:** Claude (Opus 5), depth `standard`, per CLAUDE.md delegation rule (Claude performs
code review; no `gsd-code-reviewer` subagent dispatch tool was available in this executor
context, so the sweep was run directly against the ranked file list below).

**Scope:** Largest/most-duplicated files across `DeckFlow.Web`, `DeckFlow.Studio`,
`DeckFlow.Core`, ranked by LOC via:

```
find DeckFlow.Web DeckFlow.Studio DeckFlow.Core -type f \( -name '*.cs' -o -name '*.ts' \) \
  -not -path '*/obj/*' -not -path '*/bin/*' -not -path '*/node_modules/*' -print0 \
  | xargs -0 wc -l | sort -rn | head -30
```

Both named REVIEW-01 candidates (`deck-sync.ts` 2877 LOC, `Harvest.razor.cs` 1225 LOC) are
covered below, plus the widened ranked set.

**Fence applied:** findings whose PRIMARY action would be one of the four already-owned
families are excluded as NEW candidates (see "Excluded / Already Owned" section). Incidental
mentions of `chatgpt-*` selectors inside an in-scope structural finding are expected and do
not trigger the fence — only a finding whose recommended ACTION is the rename/migration itself
is excluded.

---

## Findings — Named Candidates

### 1. `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (2877 LOC) — **HIGH**

**SRP violation.** Single top-level module (no class boundary — ~110 module-scope `const`
functions) implementing at least five materially distinct concerns in one file:

1. Moxfield browser-extension bridge import (`attachMoxfieldExtensionImport`,
   `postExtensionBridgeRequest`, `getDeckFlowExtensionStatus`, `importMoxfieldDeckTextViaExtension`,
   lines 140–431)
2. Busy-indicator / progress UI (`showBusyIndicator`, `registerBusyIndicator`,
   `scheduleBusyHide`, `abortBridgeBusy`, lines 660–841)
3. Generic sessionStorage form-state persistence (`serializePersistedFormFields`,
   `persistFormState`, `hydrateFormState`, `attachGenericPersistedForms`, lines 951–1455)
4. Card-picker dynamic-row widget (`createCardPickerRow`, `attachCardPickerRow`,
   `restoreCardPickerFields`, lines 1011–1228)
5. Deck-sync API submit + conflict-table rendering (`submitDeckSyncApi`,
   `renderDeckSyncConflicts`, `renderDeckSyncResponse`, lines 1455–1678)
6. ChatGPT-packets multi-step wizard UI (step/mode persistence, validation, bracket-to-version
   sync — `showChatGptStep`, `applyChatGptUiMode`, `validateChatGptPacketsStep`,
   `syncVersioningBracketOptions`, lines 1678–2877+)

**Not all six concerns are independent — corrected 2026-07-04 post-review.** An earlier draft
of this finding claimed "none of these six concerns depends on another." That is FALSE and was
corrected after a pre-execution re-read:

- Concern #3 (form-state persistence) and concern #4 (card-picker) ARE behavior-coupled:
  `restoreFormFields()` (line 1228) directly calls `restoreCardPickerFields()` (line 1229),
  and `restoreFormFields` special-cases `cardPickerFieldName` so the two must restore together.
- Concern #3 and concern #6 (chatgpt-packets wizard) ARE behavior-coupled:
  `attachGenericPersistedForms()` branches into `clearChatGptPacketsState()` for the
  `chatgpt-packets` cache-key (lines 1414-1415) instead of the generic
  `clearPersistedFormState()`.

Therefore only concern #1 (extension-bridge) and concern #2 (busy-indicator) are cleanly
isolated from the persistence/card-picker/chatgpt-packets tangle (verified: neither cluster
calls `persistFormState`/`restoreFormFields`/card-picker functions). Concerns #3, #4, #6 form
one behavior-coupled unit that cannot be split into independent modules without risking an
observable change.

The file legitimately contains ~65 `chatgpt-*` selector references (41 distinct identifiers)
as part of concern #6 — these are incidental to the SRP finding, not itself the finding (the
`chatgpt-*` rename is AICLEAN/Phase 85's job). Note also that the extension-bridge cluster
(concern #1) contains `chatgpt-packets` / `chatgpt-deck-comparison` / `chatgpt-cedh-meta-gap`
cache-key STRING LITERALS in `collectMoxfieldImportTasks` (lines 327-346) — these are read
(string comparison to pick inputs), never the chatgpt-packets persistence/reset logic; a
concern-#1 extraction MOVES them verbatim (no rename), staying Phase-85-safe.

**Suggested refactor shape (for triage):** extract only the two cleanly-isolated modules —
`busy-indicator.ts` (concern #2, fully `chatgpt-*`-free) and `moxfield-extension-bridge.ts`
(concern #1, moving its `chatgpt-*` cache-key literals verbatim). Leave concerns #3/#4/#6
(persistence + card-picker + chatgpt-packets wizard) in place as one coupled unit — a split
there is NOT behavior-neutral (see REFACTOR-TRIAGE row 1, backlogged slices).

### 2. `DeckFlow.Studio/Pages/Harvest.razor.cs` (1225 LOC) — **HIGH**

**SRP violation.** Single `partial class Harvest` code-behind mixing at minimum five
unrelated Studio workflows:

1. Channel browse + selection (`BrowseChannelAsync`, `GetVisibleChannelVideos`,
   `ToggleAllChannelSelections`, lines 167–273)
2. Harvest queue management (`AddToQueueAsync`, `RemoveFromQueue`, `ToggleAllQueueSelections`,
   lines 296–428)
3. Harvest + auto-distill execution (`HarvestSelectedAsync`, `HarvestAndAutoDistillAsync` —
   the latter alone spans lines 531–778, ~250 LOC in one method)
4. Auto-approve settings + cutoff management (`SaveAutoApproveSettings`,
   `OnAutoApproveEnabledChanged`, `OnAutoApproveCutoffChanged`, `ApplyAutoApproveAsync`,
   lines 778–905)
5. Spend-cap display/raise (`RefreshCapDisplayAsync`, `RaiseCapAsync`, lines 906–1121)
6. Creator management + video blocking (`LoadCreatorsAsync`, `OnCreatorSelected`,
   `BeginBlock`, `ConfirmBlockAsync`, `RefreshBadgesAsync`, lines 838–1214)

`HarvestAndAutoDistillAsync` at ~250 LOC is itself a candidate for the ≤30-line
intention-revealing-method guideline (CLAUDE.md Post-implementation review checklist).

**Suggested refactor shape (for triage):** extract-collaborator per concern into
Studio ViewModels (the codebase already has this pattern — see `DirectPushCoordinator.cs`
extracted from `DirectPush.razor.cs` in a prior phase): e.g. `HarvestQueueCoordinator`,
`AutoApproveSettingsCoordinator`, `CreatorManagementCoordinator`, `SpendCapCoordinator`.

---

## Findings — Widened Ranked Set

### 3. `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` (1615 LOC) — **HIGH**

Mixes harvest/transcript-fetch orchestration (`WarnIfFfmpegUnavailableAsync`, `LogFetch`,
`IsTerminalSuccess`, `GetCaptionTrackKind`), distill/LLM-spend orchestration, tagging/filtering
(`FilterTags`), natural-key resolution (`GetContentNaturalKey*`), AND three nested private
outcome-aggregation types (`DistillVideoOutcome`, plus two more `Add(...)` accumulator
classes) in one file. The outcome DTOs are pure data and could be extracted independently of
the harvest/distill orchestration split.

**Suggested refactor shape:** split into a `HarvestOrchestrator` + `DistillOrchestrator` pair
(mirroring the file's two natural halves) with the outcome-accumulator types promoted to their
own file(s).

### 4. `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (1096 LOC) — **MEDIUM (duplication)**

Three near-parallel upsert methods — `UpsertRowAsync`, `UpsertRowPreservingVisibilityAsync`,
`UpsertContentColumnsOnlyAsync` (lines 126–277) — plus parallel `SetVisibilityAsync` /
`SetHiddenAsync` and their `...BySourceAsync` counterparts. Likely duplicated SQL/parameter
binding boilerplate across the three upsert variants. Otherwise a single, cohesive repository
(one entity, `ContentSiteIndexRow`) — the SRP shape itself is fine; the concern is internal
duplication, not multi-responsibility.

### 5. `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` (949 LOC) — **MEDIUM (duplication, PKTSVC-adjacent)**

Four parallel families of near-identical methods — `Suggest{Packet,Comparison,CedhMetaGap,Primer}ZipFileName`
and `Load{...}FromZip` / `LoadComparisonFromZip` / `LoadCedhMetaGapFromZip` / `LoadPrimerFromZip`
— one per artifact family. This is real duplication, but it is the persistence layer backing
the four fenced PKTSVC god-services (packet-analysis ~2372 LOC / comparison ~1033 LOC /
meta-gap ~956 LOC / primer ~904 LOC — see this plan's `<context>` fence list). Recommending
this be opened independently in Phase 82 risks the same file being touched by two overlapping
refactors in the same cycle. **Flagged for Phase 83 to evaluate within its own PKTSVC scope
check**, not surfaced as an independent Phase 82 candidate (see triage row).

### 6. `DeckFlow.Web/wwwroot/ts/df-select.ts` (845 LOC) — **MEDIUM (size only)**

A single `DfSelect`-style controller class with 63 methods implementing a full ARIA 1.2
combobox (keyboard nav, search mode, grouping, live-region announcements). Large, but
single-concern (the combobox's own state machine) — accessibility widgets are inherently
stateful and verbose. Not a clear SRP violation; flagged for completeness given its rank, but
lower priority than findings #1–#5.

### 7. Cross-file SUPERFICIAL similarity: sessionStorage form-state persistence — **LOW (NOT a safe dedup target — corrected 2026-07-04 post-review)**

An earlier draft classified this HIGH and claimed `deck-sync.ts` and `category-suggestions.ts`
"independently re-implement the identical sessionStorage-keyed form-persistence pattern." A
pre-execution re-read proved that claim FALSE. The two implementations share only the
`formStateStoragePrefix = 'decksync-form-state-'` string constant — their behavior diverges
materially:

| Aspect | `deck-sync.ts` (lines 951-1390) | `category-suggestions.ts` (lines 313-408) |
|--------|----------------------------------|--------------------------------------------|
| Serialized shape | multi-value `Record<string, string[]>` (`serializePersistedFormFields`) | flat `Record<string, string>` (`readRequestData`) |
| Card-picker rows | persisted + restored (`restoreCardPickerFields`) | not present |
| `:savedAt` metadata + cache-pill UI | yes (`showCachePill`, `:savedAt` key) | no |
| Result payloads | not stored | separate result-envelope store (`formResultStoragePrefix`, `persistResultState`/`restoreResultState`) |
| Restore trigger | always hydrates on load (`hydrateFormState`) | restores ONLY after `.tool-nav__link` tab-nav, gated by `tabNavigationKey` (lines 463-471); otherwise clears |
| chatgpt-packets special clear | `clearChatGptPacketsState` branch | none |

A shared `save/restore/clear` module would therefore CHANGE behavior (e.g. category-suggestions
would start hydrating unconditionally instead of only after tab-nav, and would lose its
result-envelope restore). The shared-prefix grep
(`grep -rn "formStateStoragePrefix" DeckFlow.Web/wwwroot/ts/*.ts`) only proves the string
constant is shared — NOT that the logic is; the divergence above was confirmed by reading both
files in full.

**Suggested refactor shape:** NONE that is behavior-neutral. This is not a safe dedup target;
see REFACTOR-TRIAGE row 3 (re-triaged to backlog).

### 8. `DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs` (604 LOC) — **LOW**

Mixes upstream fetch (`GetSetsAsync`, `FetchCardsForSetAsync`) with a card-relevance scoring
heuristic (`ScoreSetCard`, `ScoreTextSignals`, `HasHighSignalLandText`,
`IsPlayableInCommanderIdentity`) and oracle-text parsing (`ExtractAbilityWords`,
`NormalizeOracleText`). A `CardRelevanceScorer` extract-collaborator is plausible but the
scoring logic is small and tightly coupled to this one caller — low priority.

### 9. Files reviewed, no material SRP/duplication finding beyond acceptable size

The following ranked files were reviewed and found to be cohesive (single entity/concern)
at their current size, or already follow an established extraction pattern in this codebase
(e.g. `ArchidektCacheJobService.cs`'s nested `HarvestProgressWriter` is a good example of the
collaborator-extraction pattern this sweep recommends elsewhere):

- `DeckFlow.Core/Content/ContentVideoStore.cs` (740) — single-entity repository, cohesive.
- `DeckFlow.Core/Knowledge/CardCategoryRepository.cs` (638) — single-entity repository, cohesive.
- `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` (619) — single-entity repository, cohesive.
- `DeckFlow.Web/Services/ArchidektCacheJobService.cs` (538) — hosted job + already-extracted
  `HarvestProgressWriter` nested collaborator; good pattern, not a finding.
- `DeckFlow.Web/wwwroot/ts/card-lookup.ts` (557) — single concern (card-lookup form UX),
  cohesive despite size.
- `DeckFlow.Web/Services/Scryfall/CardLookupService.cs` (502) — single concern, cohesive.
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` (480) / `DeckFlow.Studio/Pages/DirectPush.razor.cs`
  (468) — already split (coordinator extracted from code-behind in a prior phase); owned by
  UIAUDIT-03 (Phase 86) for its live-verify + copy fix, not a Phase 82 structural target.
- `DeckFlow.Web/Services/MechanicLookupService.cs` (450) — single concern (rules-page
  parsing), cohesive.
- `DeckFlow.Core/Knowledge/DeckQueueRepository.cs` (445) — single-entity repository, cohesive.
- `DeckFlow.Web/wwwroot/ts/category-suggestions.ts` (524) — see finding #7 (duplication with
  deck-sync.ts); no separate SRP finding beyond that.
- `DeckFlow.Core/Integration/CliLlmDistillationService.cs` (421), `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs`
  (401), `DeckFlow.Web/Services/RequestContextParser.cs` (373), `DeckFlow.Web/Services/EdhTop16Client.cs`
  (373), `DeckFlow.Web/wwwroot/ts/primer-selection.ts` (372) — all reviewed at a
  method-count/structure level; no material multi-responsibility or duplication signal beyond
  ordinary size. Not carried into the triage table as candidates.

---

## Excluded / Already Owned (fenced — not surfaced as NEW candidates)

Per this plan's `<context>` fence, the following are excluded from the candidate list even
though several rank in the top-30 LOC scan. This sweep's acceptance gate bans the literal
class-name substrings of the fenced PKTSVC/manabase families anywhere in this document
(outside headers) — so the four fenced files are identified below by LOC + role only, not by
class name, and are deliberately NOT repeated from the Findings sections above:

| Rank LOC | Role | Owned by |
|----------|------|----------|
| 2372 | Deck-analysis packet-building god-service | PKTSVC family → Phase 83 |
| 1539 | Monte-Carlo castability simulation engine | Manabase engine → deferred backlog (needs numeric-parity harness) |
| 1148 | Manabase domain-model records (tightly coupled to the deferred sim/analyzer/classifier trio) | Manabase engine cluster → deferred backlog with the rest of the manabase engine |
| 1077 | Manabase health/curve analysis engine | Manabase engine → deferred backlog |
| 1073 | Manabase deck-type classification engine | Manabase engine → deferred backlog |
| 1033 | Deck-comparison packet-building god-service | PKTSVC family → Phase 83 |
| 962 | `DeckFlow.Web/Controllers/DeckPacketController.cs` — post-split MVC controller calling into the four packet services | Already-DONE DeckController split (verified 2026-07-04); PKTSVC-adjacent orchestration caller — not re-opened |
| 956 | Meta-gap packet-building god-service | PKTSVC family → Phase 83 |
| 904 | Deck-primer packet-building god-service | PKTSVC family → Phase 83 |
| 577 | `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — Web wrapper over the deferred engine | Manabase-adjacent → deferred with manabase cluster |
| 424 | `DeckFlow.Web/Models/MetaGapResponse.cs` — response DTO for the meta-gap packet service | PKTSVC family, Phase 83 |

Verified clean against 82-01-PLAN.md's fenced-class-name acceptance check (this document's
prose never repeats those seven class-name substrings — the fenced files above are identified
by LOC + role instead).

---

## Summary

_(Severities for #1 and #7 revised 2026-07-04 after a pre-execution re-read — see the
"post-review re-triage" note in 82-01-SUMMARY.md.)_

- **HIGH:** deck-sync.ts SRP (#1 — but only concerns #1/#2 (extension-bridge, busy-indicator)
  are cleanly isolable; concerns #3/#4/#6 are behavior-coupled, see finding), Harvest.razor.cs
  SRP (#2), ContentKbOrchestrator SRP (#3)
- **MEDIUM:** ContentSiteIndexStore upsert duplication (#4), PacketArtifactStore duplication —
  PKTSVC-adjacent (#5), df-select.ts size (#6)
- **LOW:** ScryfallSetService scoring-logic mixing (#8); cross-file form-persistence
  SUPERFICIAL similarity (#7 — downgraded from HIGH; NOT a safe dedup target, the two
  implementations diverge behaviorally, only a string constant is shared)
- **No material finding:** 11 files reviewed at structure level (#9)
- **Excluded (fenced, already owned):** 11 files — 4 PKTSVC services + 1 PKTSVC DTO, 4
  manabase-engine files + 1 manabase-adjacent wrapper, 1 already-done DeckController split
  result

This feeds `REFACTOR-TRIAGE.md`.
