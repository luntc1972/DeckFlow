# Phase 82 Refactor Backlog

**Written by:** Plan 82-03 (Wave 2, REVIEW-02 execution).
**Source:** `REFACTOR-TRIAGE.md`'s `backlog` rows (8 candidates surfaced — 3 in-scope, executed in
this plan's other commits; 5 backlog rows + row 1b, recorded below with a written deferral reason
for each). Nothing was reverted in Task 1 — every in-scope row's gate passed cleanly, so this file
contains only the triage-time `backlog` rows; there are no additional gate-failure deferrals.

**Accounting:** REFACTOR-TRIAGE.md surfaced 8 candidates total (rows 1-8, with row 1 further split
into row 1 in-scope + row 1b backlog). 3 executed (rows 1-narrowed, 2, 4) + 6 recorded here (rows
1b, 3, 5, 6, 7, 8) = 9 rows accounted for, matching the triage table's row count (8 numbered rows,
with row 1 counted once as "in-scope" and its coupled remainder split out as row 1b).

---

## Row 1b — `deck-sync.ts` concerns #3/#4/#6 (form-state persistence + card-picker + chatgpt-packets wizard)

**File:** `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (concerns #3 lines 951-1470ish, #4 card-picker, #6
chatgpt-packets wizard/reset, lines 1678-2877 pre-extraction).

**Reason it exceeds this cycle's risk budget:** Behavior-COUPLED, not independently splittable —
`restoreFormFields()` calls `restoreCardPickerFields()` directly, and
`attachGenericPersistedForms()` branches into `clearChatGptPacketsState()` for the `chatgpt-packets`
cache-key instead of the generic clear. Splitting these into separate files would risk an
observable behavior change (exactly the byte-identical/behavior-neutral gate this cycle enforces).
Additionally, the chatgpt-packets persistence/reset slice is Phase 85 (`chatgpt-*` naming cleanup)
territory — extracting it here risks the same file being touched by two overlapping refactors in
the same cycle, which 82-REVIEW.md explicitly flags as a deferral trigger.

**What would unblock it:** A dedicated follow-up that first adds decoupling tests around the
`restoreFormFields` ↔ `restoreCardPickerFields` coupling and the `chatgpt-packets` clear-branch,
coordinated with Phase 85 so the chatgpt-packets slice isn't touched twice in one cycle.

---

## Row 3 — Cross-file form-state-persistence "duplication" (`deck-sync.ts` vs. `category-suggestions.ts`)

**Files:** `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (lines 951-1390) and
`DeckFlow.Web/wwwroot/ts/category-suggestions.ts` (lines 313-408).

**Reason it exceeds this cycle's risk budget:** Not a real duplication (downgraded from HIGH to LOW
during the pre-execution re-triage). The two files share only the `formStateStoragePrefix =
'decksync-form-state-'` string constant; their behavior diverges materially — `deck-sync.ts` uses a
multi-value `Record<string, string[]>` shape with `:savedAt`/cache-pill UI and card-picker rows,
plus a `chatgpt-packets` special clear branch, while `category-suggestions.ts` uses a flat
`Record<string, string>`, a separate result-envelope store (`formResultStoragePrefix`), and
restores ONLY after `.tool-nav__link` tab-navigation (gated by `tabNavigationKey`) — otherwise it
clears. A shared save/restore/clear module would CHANGE behavior: category-suggestions would start
hydrating unconditionally on load and lose its result-envelope restore. No behavior-neutral shared
extraction exists here.

**What would unblock it:** Nothing worth doing — the two flows are legitimately different features
that happen to share a storage-key prefix. Keeping them separate is the correct design; this row
exists only to record that the duplication claim was investigated and refuted.

---

## Row 5 — `ContentKbOrchestrator.cs` split (harvest/distill/tagging/natural-key/outcome-DTOs)

**File:** `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` (1615 LOC, HIGH per 82-REVIEW.md
finding #3).

**Reason it exceeds this cycle's risk budget:** Not a named REVIEW-01 flagship (unlike deck-sync.ts
and Harvest.razor.cs). This file is the harvest/distill pipeline with real LLM-spend-cap enforcement
logic threaded through it (`SkippedOverCap`, `AbortedConfig` outcome states) — a structural split
here carries materially higher regression risk than the two named UI flagships. Existing coverage
(`ContentKbOrchestratorDistillTests.cs`, `ContentKbOrchestratorFactoryTests.cs`,
`AddContentKbOrchestratorDiTests.cs`, `ContentKbOrchestratorSmokeServiceTests.cs`) is real but only
proven sufficient for regression-catching an in-place bug fix — not yet proven to certify a
full-file structural split's behavior-neutrality in one pass. Bundling a third large multi-concern
C# split into this same Wave-2 execution plan alongside the Harvest.razor.cs and
ContentSiteIndexStore.cs work would exceed this cycle's realistic per-plan risk budget.

**What would unblock it:** A dedicated follow-up pass that first adds boundary-level tests at the
proposed Harvest/Distill orchestrator split seam, then executes the split on its own plan (not
bundled with other structural work).

---

## Row 6 — `PacketArtifactStore.cs` duplication (4 parallel Suggest/Load-ZipFileName families)

**File:** `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` (949 LOC, MEDIUM per
82-REVIEW.md finding #5).

**Reason it exceeds this cycle's risk budget:** This store is the persistence layer backing the
four fenced PKTSVC god-services owned by Phase 83 (packet-analysis / comparison / meta-gap /
primer). Opening it independently in Phase 82 risks the same file being touched by two overlapping
refactors in the same cycle — this phase's dedup vs. Phase 83's SRP split — which 82-REVIEW.md
explicitly flags as the scenario to avoid.

**What would unblock it:** Phase 83 confirms, within its own PKTSVC scope check, whether its
packet-service collaborator extraction naturally folds this store's duplication in, or explicitly
punts it to a follow-up. Not a Phase 82 decision to make.

---

## Row 7 — `df-select.ts` (845 LOC, 63-method ARIA-combobox controller)

**File:** `DeckFlow.Web/wwwroot/ts/df-select.ts` (845 LOC, MEDIUM-size-only per 82-REVIEW.md
finding #6).

**Reason it exceeds this cycle's risk budget:** Not a clear SRP violation — this is one widget's
state machine (keyboard nav, search mode, grouping, live-region announcements for a full ARIA 1.2
combobox), not multiple unrelated concerns; size alone doesn't meet the action bar. More
importantly, no existing dedicated test file was found for `df-select.ts` (confirmed: no
`ts-tests/*.test.ts` references it) — a structural split now would require standing up NEW
keyboard/ARIA-interaction test coverage from scratch before a behavior-neutral proof is possible.
This is the "would need net-new test scaffolding" exclusion condition from this plan's risk-budget
yardstick, not merely a new test file on rails already laid down (unlike, e.g., row 3's now-refuted
duplication claim).

**What would unblock it:** Add `df-select`-specific Vitest coverage first (its own future task),
then reassess whether a split is warranted.

---

## Row 8 — `ScryfallSetService.cs` scoring-logic mixing (upstream fetch + card-relevance heuristics)

**File:** `DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs` (604 LOC, LOW per 82-REVIEW.md
finding #8).

**Reason it exceeds this cycle's risk budget:** Low severity — a plausible `CardRelevanceScorer`
extract-collaborator exists (`ScoreSetCard`, `ScoreTextSignals`, `HasHighSignalLandText`,
`IsPlayableInCommanderIdentity`, oracle-text parsing helpers), but the scoring logic is small and
tightly coupled to its one caller. With rows 1 (narrowed), 2, and 4 already claiming this cycle's
refactor budget, a LOW-severity item is the correct one to defer on capacity grounds — this is a
prioritization call, not a coverage gap: `DeckFlow.Web.Tests/ScryfallSetServiceTests.cs` already
exists and would support this work if picked up.

**What would unblock it:** Capacity in a future cleanup pass; no new coverage prerequisite —
existing test coverage is already sufficient to attempt this extraction whenever priority allows.

---

## Summary

| Row | File | Severity | Unblock trigger |
|-----|------|----------|------------------|
| 1b | deck-sync.ts (concerns #3/#4/#6) | HIGH (coupled) | Decoupling tests + Phase 85 coordination |
| 3 | deck-sync.ts + category-suggestions.ts | LOW (refuted) | None — correct to stay separate |
| 5 | ContentKbOrchestrator.cs | HIGH | Boundary-level tests, own dedicated plan |
| 6 | PacketArtifactStore.cs | MEDIUM | Phase 83's own PKTSVC scope check |
| 7 | df-select.ts | MEDIUM (size) | New Vitest coverage first |
| 8 | ScryfallSetService.cs | LOW | Capacity only — no prerequisite |

Nothing from REFACTOR-TRIAGE.md's 8 surfaced candidates was silently dropped: 3 were executed
(rows 1-narrowed, 2, 4 — see this plan's other commits) and 6 are recorded above with a written
reason and an unblock note.
