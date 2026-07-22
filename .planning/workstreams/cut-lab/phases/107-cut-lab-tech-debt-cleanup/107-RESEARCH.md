# Phase 107: Cut Lab Tech-Debt Cleanup - Research

**Researched:** 2026-07-22
**Domain:** Internal C#/TypeScript/CSS tech-debt cleanup on an existing, shipped feature (Cut Lab). No new libraries, no new endpoints, no new user-facing requirements.
**Confidence:** HIGH — every finding below was verified by reading the actual current source (not training knowledge, not the stale VERIFICATION note text). Two items (4's xmldoc garble, 4's Manabase-copy leak) were found **already fixed** since Phase 101; this is called out explicitly so the planner doesn't schedule redundant work.

## Summary

This is a pure quality/tech-debt phase touching a single existing tool (Cut Lab) across six previously-tracked items. All six are groundable in the current repo state — none require new research into external libraries or unfamiliar patterns. The main risk is **underestimating blast radius**: item 1 (dead DI fields) touches ~43 test-constructor call sites, not "two fields"; item 2 (pool-status chip) is a real commander-inclusive-vs-exclusive counting split across the server and client, not a cosmetic string bug; item 6 (live-patch) is *cheaper* than the ROADMAP framing suggests because the server already computes the data on every decide call — it just isn't serialized to the client yet.

Two items previously listed in 101-VERIFICATION's open items are **already resolved** (confirmed by grep — zero hits): the `CutLabPoolValidator.cs` xmldoc garble and the Manabase-verbatim "per-card castability table" copy in `CutLab.cshtml`. The planner should close these two sub-items of item 4 as "already fixed in a later phase" rather than scheduling tasks for them.

**Primary recommendation:** Treat item 1 and item 6 as their own plans (item 1 because of test blast radius, item 6 because CONTEXT D-04 already asks for isolation); batch items 2, 3, 4 (remaining 3 sub-items), and 5 into one or two mechanical plans. Do not reuse Nyx's exact red hex (`#f87171`) for the other 9 dark themes — it fails AA on 2 of them (see Item 3).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Dead DI field removal (Item 1) | API/Backend (Services/CutLab) | — | Pure C# service + DI container shape; no UI change |
| Pool-status count reconciliation (Item 2) | API/Backend (ViewModel + PageService) | Browser/Client (cut-lab.ts chip patch) | Server computes canonical count; client mirrors it for live-patch parity |
| Dark-theme contrast (Item 3) | CDN/Static (CSS tokens) | — | Pure `:root` token override per theme file; no server/client logic |
| Xmldoc/copy/contrast/truncation fixes (Item 4) | Mixed: Backend (xmldoc — already fixed), Frontend (Razor copy — already fixed), CDN/Static (CSS — remaining 3 sub-items) | — | Each sub-item is independent and single-tier |
| cacheKey/path-base/pluralizer (Item 5) | Browser/Client (cut-lab.ts) | API/Backend (ManabaseWording reuse) | Mechanical dedup, no cross-tier redesign |
| Structural-analysis live-patch (Item 6) | API/Backend (serialize existing server computation) | Browser/Client (new DOM-patch renderer) | Findings are already computed server-side in `PostDecideAsync`; this is a payload + renderer addition, not new business logic |

## Project Constraints (from CLAUDE.md)

- **Theme CSS forks:** layout/structural CSS belongs in `site-common.css`; only token values (`:root` custom properties) go in each guild theme file. Item 3 must ONLY add `--cutlab-delta-up`/`--cutlab-delta-down` declarations inside each theme's own `:root` block — never touch `site-common.css`'s `.cutlab-delta__value--up/--down` rules (they already correctly reference `var(--cutlab-delta-up, var(--success))` with a fallback, which is exactly the seam the theme files hook into).
- **Formatting / changed-lines gate:** `.editorconfig` is authoritative; the pre-commit hook and CI `format-gate` only check **changed lines**. Because item 1 touches ~43 test call sites and item 6 adds a new DTO + renderer, keep edits surgical — don't reflow whole files.
- **Five carve-outs** apply verbatim to any touched C#: never convert `{ get; init; }` → `{ get; }` (breaks System.Text.Json deserialization in .NET 9+, which Cut Lab's `CutLabState`/DTOs depend on directly), never inline `[Attribute]` onto the property line, never re-indent raw-string literals, preserve switch expressions, preserve xmldoc single-space indent.
- **LF line endings** are enforced by `.gitattributes` — do not let any tool/editor convert.
- **Compiled `wwwroot/js/*.js` is gitignored** — item 6 and item 5 touch `wwwroot/ts/cut-lab.ts` (source of truth); never stage the compiled output.
- **No new packages** — none of the six items require one. If the planner is tempted to add a color-contrast npm dev-dependency for item 3, don't; the contrast math is closed-form (WCAG relative luminance) and was already computed by hand for this research (see Item 3 below) — no tool is needed at plan or execution time.
- **VSTest unreliable in WSL** — rely on `dotnet build` clean + full xUnit run via the project's existing harness, plus Playwright e2e via `scripts/run-web-test.sh`. Do not open a browser on the Windows host.
- **UI changes require theme×viewport screenshots** (project convention, reinforced by memory: "UI review after every UI change") — items 3 and 4's three cosmetic sub-items (Nyx badge overlap, Lock-all-lands contrast, mobile label truncation) cannot be fully verified by static code reading alone; the plan must include a screenshot-based re-check.

## Standard Stack

No new libraries. This phase reuses:

| Component | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xUnit | 2.9.3 (existing) | `DeckFlow.Web.Tests` coverage for items 1, 2, 5 | Already the project's server test framework |
| Vitest | existing (`ts-tests/`) | Client-side coverage for items 5, 6 | Already the project's TS test framework |
| Playwright | existing (`e2e/`) | Live-DOM assertions for item 6's live-patch and item 3/4's visual fixes | Already the project's e2e framework |

**Installation:** none — no new packages.

**Version verification:** N/A — no packages added or bumped this phase.

## Package Legitimacy Audit

**N/A — this phase installs no external packages.** All six items are internal C#/TypeScript/CSS edits to existing, already-registered code. No `npm install`, no `dotnet add package`, no slopcheck run needed. If a future planner iteration decides to add a contrast-checking tool, they must stop and follow the Package Legitimacy Gate protocol before doing so — but nothing in the current scope requires it (see Item 3's precomputed contrast table).

## Item-by-Item Findings

### Item 1 — Dead `_spellbook`/`_categoryKnowledge` fields in `CutLabPageService`

**File:** `DeckFlow.Web/Services/CutLab/CutLabPageService.cs`
- Fields: `_categoryKnowledge` (line 105), `_spellbook` (line 106).
- Ctor params: `categoryKnowledge` (line 128), `spellbook` (line 129) — both optional, default `null`.
- **Two live (non-dead) usages inside this class, both must be handled, not just deleted:**
  1. **`HasStructuralAnalysisDependencies`** (lines 159–164): an `internal` test-only DI-shape guard property. Two of its four `&&`-chained conditions reference the fields being removed (`_categoryKnowledge is not null && _spellbook is not null && _manabaseBaseline is not null && _cedhBaseline is not null && !ReferenceEquals(_simulationService, NoOpCutLabSimulationService.Instance)`). This property **must be rewritten** (drop the two removed-field checks) or **retired**, not left referencing deleted fields.
  2. **Ctor fallback construction** (line 147–149): `_analysisContextBuilder = analysisContextBuilder ?? new CutLabAnalysisContextBuilder(cardResolver, sharedResolvedCardCache, spellbook, categoryKnowledge);`. In production this branch is **dead** — `ICutLabAnalysisContextBuilder` is registered in DI (`Extensions/CutLabServiceCollectionExtensions.cs:22`, `AddScoped<ICutLabAnalysisContextBuilder, CutLabAnalysisContextBuilder>()`), so the container always supplies a non-null `analysisContextBuilder` and the `??` never fires at runtime. **But it is NOT dead in tests** — see below.

**DI registration confirmed (Program.cs):** `ICutLabPageService` is `AddScoped` (Program.cs:181); `ICategoryKnowledgeStore` is `AddSingleton` (Program.cs:172); `ICommanderSpellbookService` is registered elsewhere (resolved via `sp.GetRequiredService<ICommanderSpellbookService>()` at Program.cs:195, confirming it's registered). So in production DI resolves non-null `categoryKnowledge`/`spellbook` into `CutLabPageService`'s ctor too — they're just never *used* by `CutLabPageService`'s own logic beyond the two spots above.

**Test blast radius — this is the part CONTEXT's "test-only DI-probe, unused" framing understates:**
- `grep -c "new CutLabPageService("` → **42 call sites in `CutLabPageServiceTests.cs`** + **1 in `CutLabOriginalEntriesTests.cs`** = **43 total**.
- Most use **named arguments** for the later params (`analysisContextBuilder:`, `manabaseBaseline:`, `simulationService:`, `logger:`), which is good — removing the two earlier positional params won't silently misassign those calls.
- **~33 of the 43 call sites** pass nothing (or `null`) in the categoryKnowledge/spellbook slots — purely mechanical, drop-two-args, safe.
- **10 call sites genuinely rely on the fallback-construction behavior** — they pass live `FakeCategoryKnowledgeStore`/`FakeSpellbookService` instances as positional args 4/5 specifically so the ctor's `?? new CutLabAnalysisContextBuilder(cardResolver, sharedResolvedCardCache, spellbook, categoryKnowledge)` fallback wires the fakes into a real analysis builder, without the test having to construct `CutLabAnalysisContextBuilder` itself. These **must be rewritten**, not just have two args deleted, or they will silently stop exercising combo/category-driven structural analysis:
  - `CutLabPageServiceTests.cs` line ~946: `ProcessAsync_BatchedCategoryLookupRunsOnceForWholePool`
  - line ~1050: `ProcessAsync_DfcFrontFaceInput_AssignsRolesBuildsBaselineAndComputesDeltasWithoutWarnings`
  - line ~1119: `ProcessAsync_ResolvesEachUniqueCardExactlyOnceAcrossAnalysisAndDeltas`
  - line ~1359: `ProcessAsync_DecisionRoundTripWithWarmCache_PerformsZeroAdditionalLiveResolves`
  - line ~1409: `ProcessAsync_ColdIntakeWithOneHundredTwentyDistinctCards_UsesTwoCollectionBatches`
  - line ~1472: `ProcessAsync_IntakeWithOneUnresolvableCard_FallsBackOnceAndDoesNotRefetchKnownMissingOnRepeatBuild`
  - line ~1714: `ProcessAsync_SpellbookCancellation_Propagates`
  - line ~1734: `ProcessAsync_CategoryLookupCancellation_Propagates`
  - line ~1826: `ProcessAsync_StructuralAnalysis_WiresRolesFloorsFindingsAndUserFloorPersistence`
  - `CutLabOriginalEntriesTests.cs` line ~250 (a shared private test-service-factory helper, not a `[Fact]` itself, but used by multiple tests in that file)
  - **Fix pattern already exists in the same file** as a model to copy: tests at lines ~1637 and ~1668 (`ProcessAsync_SpellbookFailure_FailsOpenAndLogsWarning`, `ProcessAsync_CategoryLookupFailure_FailsOpenAndLogsWarning`) already construct an explicit `CutLabAnalysisContextBuilder` variable (`analysisBuilder`) wired with the fakes and pass it via the **named** `analysisContextBuilder:` argument — this is exactly the pattern the 10 sites above should be converted to.
- **3 dedicated DI-guard tests** exercise `HasStructuralAnalysisDependencies` directly against a real `ServiceCollection` mirroring `Program.cs`'s shape (`BuildDiGuardProvider` helper, `CutLabPageServiceTests.cs` line ~2136): `CutLabPageService_DiContainerMirrorsProgramRegistrationAndSuppliesOptionalAnalysisDependencies`, `CutLabPageService_DiGuardFailsWhenOptionalAnalysisRegistrationDrops`, `CutLabPageService_DiGuardFailsWhenSimulationRegistrationDrops`. These must be updated to match whatever `HasStructuralAnalysisDependencies` becomes (or repointed at `ICutLabAnalysisContextBuilder`'s own DI shape, which is arguably the more correct place for this guard once `CutLabPageService` no longer holds the two fields itself).
- Also check `ProcessAsync_WithOptionalAnalysisDependenciesOmitted_BehavesAsUnavailable` (line ~1692) — name suggests it also asserts on the omitted-dependencies behavior; verify it during execution.

**Do-not-touch (per CONTEXT, confirmed by code):** `CutLabAnalysisContextBuilder.cs` has its own `_spellbook`/`_categoryKnowledge` fields (lines 81–82) and uses them for real combo/category classification (verified around lines 499–546 per CONTEXT's citation). Scope removal strictly to `CutLabPageService`.

**Recommendation:** Given 10+ test rewrites needed anyway, consider introducing a small private test-factory helper in `CutLabPageServiceTests.cs` (e.g., `BuildServiceWithAnalysis(categoryStore, spellbook, ...)` that internally builds the `CutLabAnalysisContextBuilder` and forwards it) to reduce the chance of future ctor-signature churn causing the same 43-site blast radius again. This is a nice-to-have, not required by CONTEXT — flag as a suggestion, not a mandate.

### Item 2 — Pool-status chip: total vs non-commander count

> **Planning correction (2026-07-22, post cross-AI review):** the previously-tracked dead `CutLabViewModel.PoolStatusText` property (101-VERIFICATION open item 1 / copy triplication) was REMOVED in commit `edb7c21bc` — `rg PoolStatusText` = 0 hits repo-wide, no member, no test asserts it. Item 2's single-source consolidation must therefore CREATE a fresh commander-inclusive chip member (see plan 107-03 Task 1), not repurpose a dead prop. The chip format now lives in only the inline Razor literal + the cut-lab.ts twin; the goal is ONE fresh server source + the one unavoidable TS twin (no 4th copy).

This is a real, systemic **counting-convention split**, not a single typo. There are two different, both internally-consistent conventions in the codebase, and they disagree by exactly 1 (the commander):

**Convention A — non-commander count** (`Model.CardCount`):
- Computed once at intake in `CutLabPageService.cs:234` (`CountNonCommanderCards`, line 594–602) → assigned as `CardCount = nonCommanderCardCount` (line 430).
- Documented on the ViewModel as *"Non-commander pool count returned by the service"* (`CutLabViewModel.cs:33`).
- Rendered in the "Lock your pool" pool-status chip: `CutLab.cshtml:240` — `@Model.CardCount cards in pool · @lockedCount locked`.
- Mirrored client-side in `cut-lab.ts:594-609` (`updateLockedCountChip`) — explicitly filters out the commander row (`row.dataset.cutLabCommander !== 'true'`) before summing quantities, matching the server convention.
- This convention is also what `CutLabPoolValidator.ValidateCardCount`'s 101–150 range checks against (INTAKE-03), and it's load-bearing for existing, passing tests — **do not change this basis**.

**Convention B — commander-inclusive count** (`Model.BaselineCount`, `Model.CurrentCount`, the round engine's "cards remaining to 100"):
- `BaselineCount = pool.Sum(card => card.Quantity)` (`CutLabViewModel.cs:179`) where `pool = result.State?.Pool` — the raw pool INCLUDING the commander entry.
- `CurrentCount = derivedWorkingList.Sum(card => card.Quantity)` (line 180) — also commander-inclusive (the commander is never a decision target, so it always survives `Derive`).
- Rendered in the Compare panel: `CutLab.cshtml:1121` — `@Model.BaselineCount cards → @Model.CurrentCount cards`.
- The 100-card cut target itself is commander-inclusive: `CutLabDecisionApplier.cs:43` — `int remaining = workingList.Sum(card => card.Quantity) - 100;` — this matches the real-world convention that a Commander deck is "100 cards total including the commander," which is **correct and must not change** (it's load-bearing across Phases 103–106's engine + tests).
- `cut-lab.ts`'s `currentCountFromSerializedState` (lines 1682–1735) also does NOT filter out the commander — matches Convention B, used to gate Export-tab enablement and the sticky-bar "N to cut" display.

**The disagreement, concretely:** for a pool with e.g. 149 non-commander cards + 1 commander, the "Lock your pool" chip says "149 cards in pool" while the Compare panel (same pool, same moment) says "150 cards → 100 cards." A user reading both sees numbers that don't reconcile.

**Recommendation (Claude's discretion per CONTEXT):** Do **not** touch Convention B — it's deeply embedded in the round engine, export target logic, and Phase 103–106 tests (`CutLabCutRoundEngine`, `CutLabDecisionApplier`, `CutLabViewModel.CurrentCount`/`BaselineCount` tests in `CutLabViewModelWordingTests.cs`). Instead, make the "Lock your pool" pool-status chip (and its `cut-lab.ts` twin) commander-inclusive to match: reuse the same basis as `BaselineCount` for that chip's display (it's already computed from `pool.Sum(quantity)` including the commander — just don't subtract the commander when building the *displayed* count for this specific chip), while **keeping `Model.CardCount` (non-commander) unchanged** for the internal 101–150 validation logic and its error messages, which correctly and intentionally describe the pool "excluding the commander." This is a display-only fix, zero engine risk.

**Also verify:** `updateLockedCountChip()` in `cut-lab.ts` (called at line 2565, wired to lock-checkbox interactions) is never invoked after `handleAdjustSubmit` (the Phase 106 +/- quantity tuner). The tuner rows (`tr[data-cut-lab-tuner-row]`, `CutLab.cshtml:1039`) are a **separate DOM table** from the lock rows the chip reads (`tr[data-cut-lab-card]`, `CutLab.cshtml:302`) — so after a quantity adjustment, the pool-status chip is doubly stale (both the intake-vs-current gap above, and now a same-basis staleness gap too). Confirm during planning whether this needs its own fix or is accepted as "chip reflects original imported pool, not the live-tuned total" (a legitimate design choice, but should be a documented decision, not an oversight).

### Item 3 — Dark-theme delta contrast

**Token seam** (already exists, do not modify): `site-common.css:4557-4563` —
```css
.cutlab-delta__value--up   { color: var(--cutlab-delta-up, var(--success)); }
.cutlab-delta__value--down { color: var(--cutlab-delta-down, var(--danger)); }
```
Background context: `.cutlab-delta` lives inside `.cutlab-proposal { background: var(--panel); }` (site-common.css:4499-4504) — **`--panel` is the correct background token to contrast-check against.**

**Global fallback values** (site.css, inherited by every theme that doesn't override): `--success: #2f855a` (green), `--danger: #c53030` (red) (site.css:44, 53).

**Dark-theme classification** (by literal `--bg`/`--panel` hex luminance, verified via grep across all 22 `site-*.css` guild/wedge/nyx/planeswalker files):

| Dark theme | `--panel` | Global-success contrast | Global-danger contrast |
|---|---|---|---|
| site-nyx.css | `#2a263e` | 3.20 (FAIL) | 2.66 (FAIL) — **already overridden**, not in scope |
| site-abzan.css | `#1a2014` | 3.67 (FAIL) | 3.05 (FAIL) |
| site-dimir.css | `#1e2840` | 3.23 (FAIL) | 2.68 (FAIL) |
| site-esper.css | `#182030` | 3.59 (FAIL) | 2.98 (FAIL) |
| site-golgari.css | `#1e2a1c` | 3.30 (FAIL) | 2.74 (FAIL) |
| site-grixis.css | `#2e3244` | 2.79 (FAIL) | 2.32 (FAIL) |
| site-jund.css | `#352b24` | 3.04 (FAIL) | 2.52 (FAIL) |
| site-planeswalker-dark.css | `#2e3450` | 2.68 (FAIL) | 2.23 (FAIL) |
| site-rakdos.css | `#301820` | 3.62 (FAIL) | 3.01 (FAIL) |
| site-sultai.css | `#283838` | 2.70 (FAIL) | 2.24 (FAIL) |

All 9 remaining dark themes fail AA normal-text contrast (4.5:1 minimum) against the global success/danger colors on `--panel` — confirms the ROADMAP claim precisely. (WCAG relative-luminance contrast ratio computed directly, not tool-assisted — formula: standard sRGB relative luminance + `(L1+0.05)/(L2+0.05)`.)

**These are the 10 dark theme files (all of `site-*.css` minus the 12 light themes: azorius, bant, boros, gruul, izzet, jeskai, mardu, naya, orzhov, selesnya, simic, temur). Nyx already has overrides; the 9 needing new ones are: abzan, dimir, esper, golgari, grixis, jund, planeswalker-dark, rakdos, sultai.**

**Important — do NOT copy Nyx's exact hex values.** Nyx uses `--cutlab-delta-up: #4ade80` and `--cutlab-delta-down: #f87171` (site-nyx.css:47,57). Verified against all 9 other panels:
- `#4ade80` (green): worst-case contrast across the 9 panels = **6.99:1 — passes comfortably everywhere.** Safe to reuse as-is.
- `#f87171` (red, Nyx's current value): worst-case contrast = **4.41:1 on `site-sultai.css`/`site-planeswalker-dark.css` — FAILS AA** (narrowly). Do not reuse verbatim for the 9 new themes.
- A slightly deeper red passes everywhere with margin: **`#fc8181`** → worst-case 4.99:1 (PASS); **`#fca5a5`** → worst-case 6.42:1 (PASS, larger margin).

**Recommendation:** Add the same shared pair to all 9 remaining dark theme `:root` blocks: `--cutlab-delta-up: #4ade80;` / `--cutlab-delta-down: #fc8181;` (or `#fca5a5` for extra margin). A single shared pair (rather than 9 bespoke per-theme colors) is lower-effort, keeps the "measurable, not judgmental" delta color consistent across all dark themes, and is fully justified by ROADMAP's "quality-only, mechanical, add 2 overrides each" framing. Leaving Nyx unchanged (not required by CONTEXT/D-03, which only asks for the *other* dark themes) is fine — its existing red is only borderline on panels it doesn't use.

### Item 4 — 101-VERIFICATION open items (5 sub-items; 2 already fixed)

1. **Xmldoc garble — ALREADY FIXED.** `CutLabPoolValidator.cs:28` currently reads: *"Loaded non-commander pool card count, excluding the commander — the commander is the plus one."* This is exactly the corrected wording 101-VERIFICATION recommended. Grep confirms no other garbled variant exists in the file. **Close as "already resolved" — no task needed.**

2. **Manabase-verbatim castability copy — ALREADY FIXED.** Grepped `CutLab.cshtml` for "castability table" / "All modes show" — **zero hits.** The misleading copy is gone (removed sometime during Phases 102–106). **Close as "already resolved" — no task needed.**

3. **Nyx-mobile commander badge overlap — STILL NEEDS A LIVE CHECK.** Relevant code: `site-common.css:1254-1257` already has a targeted override — `table[data-prompt-cedh-reference-table] tr[data-cut-lab-commander="true"] td[data-label="Card"] > strong` and `> .cutlab-commander-card-cell__details` are both pinned to `grid-column: 2` inside the mobile responsive-table grid. The commander-locked badge itself is `.cutlab-lock-badge--commander` (`site-common.css:4157`, just a `border-left: 3px solid var(--commander-gold, #d4af37)` — no positioning rules of its own, so any overlap is a symptom of the surrounding grid/flex layout, not the badge's own CSS). Markup: `CutLab.cshtml:319` (`<div class="cutlab-commander-card-cell__details">`). **Cannot confirm visually from static code — requires a fresh Nyx-theme mobile-viewport screenshot before deciding whether the existing grid override (lines 1254-1257) already fixed this or whether it needs more work.**

4. **Lock-all-lands pill contrast — ROOT CAUSE FOUND, more severe than "low contrast."** The "Lock all `<role>`" button (`CutLab.cshtml:434-439`) uses class `manabase-pill @(group.AllLockableMembersLocked ? "is-selected" : null)` and is a plain `<button>` with **no nested `<input type="radio">`**. The only "selected" visual rule in the codebase is `.manabase-pill:has(> input:checked)` (`site-common.css:2544-2550`), which requires a nested radio input to match. **There is zero CSS rule for `.manabase-pill.is-selected` anywhere in the codebase** (`grep` confirms no match). The `is-selected` class IS correctly toggled both server-side (Razor) and client-side (`cut-lab.ts:682`, `setRoleLockButtonState`) and `aria-pressed` is set correctly — but **no visual style ever applies**. This means the "locked" state of this pill never visually distinguishes itself from the unlocked state in ANY theme, which is a stronger bug than "low contrast in one theme." **Recommended fix:** add a `.manabase-pill.is-selected` rule mirroring `:has(> input:checked)` (same `background: var(--accent...); color: var(--on-accent...)` treatment) so button-variant pills get equivalent, themed, AA-passing selected styling.

5. **Mobile pool-row "Package assignment" label truncation — ROOT CAUSE FOUND.** The mobile responsive-table pattern (`site-common.css:1230-1252`) renders each `<td>`'s mobile label via `content: attr(data-label)` with `white-space: nowrap` (line 1251) inside a fixed `grid-template-columns: 6.5rem 1fr` (line 1233 — a ~104px label column). `Package assignment` (19 characters, `CutLab.cshtml:334` `data-label="Package assignment"`) is far longer than the other labels sharing this rule (`Date`, `Card`, etc.) and will overflow/truncate at mobile widths under `nowrap` + a 104px column. **Lowest-risk fix:** shorten only the `data-label` attribute value (e.g. `data-label="Package"`) on this one `<td>` — the visible desktop `<th>` at `CutLab.cshtml:293` ("Package assignment") is a separate string and is unaffected, so this is a one-line, zero-layout-risk change. (Widening the shared 6.5rem column or allowing wrap would touch every row's mobile layout — higher risk, not recommended unless the shortened label still doesn't fit on the narrowest supported viewport, which should be confirmed by the same screenshot pass as sub-item 3.)

### Item 5 — 104-simplify notes

1. **cacheKey → data-attr:** `getForm()` (`cut-lab.ts:423-424`) already selects via `form[data-cache-key="cut-lab"]` — this specific complaint (a hardcoded selector string) is **already resolved** at the primary call site. The one remaining hardcoded literal is the **fallback default** in `loadSavedScenario` (`cut-lab.ts:1059`): `const formCacheKey = form.dataset.cacheKey?.trim() || 'cut-lab';` — a literal `'cut-lab'` string used only if the data-attribute is somehow missing. Confirm during planning whether this fallback is worth removing (the attribute is always rendered server-side at `CutLab.cshtml:116`, so the fallback branch is likely unreachable in practice) or whether it should stay as defensive code with a `// Why:` comment.

2. **Route path-base safety:** ONE remaining hardcoded absolute path: `cut-lab.ts:1145` (`buildDecisionFormBase`) — `form.action = '/cut-lab/decide';`. This is a *new* instance of the same class of bug 101-VERIFICATION originally flagged (the original hardcoded-selector instance at old `cut-lab.ts:103` is already fixed per point 1 above, but a new hardcoded absolute path appeared in the Phase 103 decision-form-builder code). The server-rendered forms already do this correctly via `Url.Content("~/cut-lab/decide")` (`CutLab.cshtml:38`) — the client-built forms (used for restore/re-render after a decide response) should use the same path-base-safe approach, e.g. read the base path once from a `data-*` attribute on the main form (which already knows its own `Url.Content`-resolved action) rather than hardcoding `/cut-lab/decide`.

3. **Shared pluralizer (server):** `DeckFlow.Core/Manabase/ManabaseWording.Pluralize(string singular, int count)` (`ManabaseWording.cs:19` — naive `count == 1 ? singular : singular + "s"`) is an **existing, already-used-elsewhere** helper (10+ call sites across `ManabaseReportTextBuilder`, `ManabaseVerdictSynthesizer`, `ManabaseSwapPromptBuilder`, `ManabaseDisplay`). `CutLabViewModel` independently hand-rolls the same naive-suffix logic via `FormatCountLabel(int count, string singular, string plural)` (`CutLabViewModel.cs:798`), consumed by `FormatCutsMadeCount`/`FormatCutsAcceptedSoFar`. Since every word Cut Lab pluralizes is regular (`card`/`cards`, `cut`/`cuts`), **`ManabaseWording.Pluralize` is a drop-in replacement** — recommend consolidating onto it rather than maintaining a second copy of the same one-liner. Existing tests (`CutLabViewModelWordingTests.cs` — `FormatCutsMadeCount_ReturnsExpectedCardWording`, `FormatCutsAcceptedSoFar_ReturnsExpectedCutWording`) assert on output strings only, so they should stay green through this swap if done correctly.
   - **Note:** `ManabaseWording` lives in the `DeckFlow.Core.Manabase` namespace. Reusing it from Cut Lab is a cross-feature dependency on a namespace named after a different tool. This is a minor naming smell worth flagging to the planner/user, but CONTEXT scopes this as "mechanical" — moving/renaming `ManabaseWording` to a neutral shared location is a bigger, out-of-scope refactor and should NOT be bundled into this phase.
   - **On the client:** no existing TS pluralizer exists to consolidate against — `cut-lab.ts`'s `formatCountLabel` (line 192) is the only implementation in `wwwroot/ts/*.ts`. No JS-side duplication to fix; "server + JS" in the ROADMAP note most likely means "keep the two in sync," not "share code across the language boundary" (not technically possible without a codegen step, which is out of scope).

### Item 6 — Structural-analysis table live-patch

**This item is cheaper than "heaviest item" suggests, because the server already computes the needed data on every decide call — it's currently discarded rather than serialized.**

**Existing live-patch pattern (the thing to mirror):** `handleDecisionSubmit` (`cut-lab.ts:2072-2130`) POSTs to `/api/cut-lab/decide`, receives a `CutLabDecisionResponse`-shaped JSON body, and live-patches multiple independent DOM regions without a page reload: `writeDecisionStateToHiddenInputs`, `rebuildWhatifSelectOptionsFromState`, `patchStickyBar`, `renderRoundBanner`, `renderProposalCard` (which itself calls `renderFloorWarnings`, `appendDeltaLine`), `renderCutsMade`. This is the exact pattern item 6's new renderer should join.

**Server already computes the findings on every decide call and throws them away:** `CutLabApiController.PostDecideAsync` (`CutLabApiController.cs:44-152`) computes `afterFindings` (`CutLabStructuralFindingsResult`, line ~105-110, via `CutLabCutRoundEngine.BuildFindingsAndRoundPlan(afterWorkingList, afterContext, floorByRole, state.Decisions)`). This is used ONLY to build the single next-proposal's `FindingCount`/`FindingChips` (`BuildNextProposal(roundPlan, afterFindings)`) — the **full** findings list (all findings, all groups, the exact shape rendered server-side as `Model.Findings`/`Model.FindingGroups`) is computed but never serialized into `CutLabDecideApiResponse`. **No new engine/business logic work is required for item 6 — this is a plumbing task: serialize an already-computed result and add a client renderer.**

(`PostAdjustAsync`, `CutLabApiController.cs:154-225`, has the identical pattern — it also computes findings via `BuildFindingsAndRoundPlan` and discards them, keeping only `CardsRemaining`. ROADMAP item 6 only asks for "JS decide," but the same staleness will exist after a quantity adjustment too. Flag as an open question for the planner: fix both endpoints for consistency, or intentionally scope to decide-only and document the adjust-path gap.)

**Target DOM section to live-patch:** `CutLab.cshtml:466-518`, the "Structural findings" `<section class="result-panel">`. **Currently has no `data-cut-lab-*` marker** — the plan must add one (e.g. `data-cut-lab-structural-findings`) so the client can locate and rebuild it, following the codebase's existing `data-cut-lab-*` convention. Contains:
- A heading + count badge (`totalStructuralFindings`, `.cutlab-findings-count`, line 475).
- A loop over `Model.FindingGroups` (`IReadOnlyList<CutLabFindingGroupView>`) → each group renders `.cutlab-finding__heading` + one or more `.cutlab-finding__item` blocks with `.cutlab-finding__lead` and optional `.kb-chip-area__chips` evidence chips (lines 482-502).
- Two degradation notes gated on `Model.ComboDataUnavailable`/`Model.CategoryDataUnavailable` (lines 509-517), which map to `CutLabStructuralFindingsResult.ComboDataAvailable`/`CategoryDataAvailable` — also computed by `afterFindings` already, also needs including in the new DTO if the section is to be fully self-consistent after a live-patch.

**View-model shapes to mirror in the new API DTO** (`CutLabViewModel.cs:848-874`):
```csharp
CutLabFindingView      { Kind, Heading, Lead, Evidence: IReadOnlyList<string> }
CutLabFindingGroupView { Kind, Heading, Items: IReadOnlyList<CutLabFindingView> }
```
`BuildFindingGroups` (`CutLabViewModel.cs:386-426`, private static) contains non-trivial grouping logic — it collapses multiple `WeakFloorCase` findings into a single merged group inserted at a specific queue position. **Recommendation: do this grouping server-side and return the already-grouped shape** (mirroring `CutLabDecideProposalDeltasDto`/`CutLabDecideFloorWarningDto`'s existing pattern of pre-shaped DTOs the client renders near-verbatim), rather than porting `BuildFindingGroups`'s merge logic into TypeScript. This avoids a second implementation of the WeakFloorCase-merge rule drifting from the C# original — consistent with the project's "don't hand-roll / single source of truth" instinct even though this isn't a hand-roll-vs-library case.

**Where to add the new DTO:** `DeckFlow.Web/Models/Api/CutLabDecideApiResponse.cs` already contains 6 sibling `CutLabDecide*Dto` records in exactly this file — add `CutLabDecideFindingDto`/`CutLabDecideFindingGroupDto` there, plus a new `StructuralFindings: IReadOnlyList<CutLabDecideFindingGroupDto>` (+ the two `bool` availability flags) property on `CutLabDecideApiResponse`.

**No visibility/tab-hiding concern:** the "Process / Decide / Goals / Export" step tabs (`_WorkflowStepTabs.cshtml`, `CutLab.cshtml:12-23`) render as a single continuous scrolling page for steps 1-3 — only step 4 (Export) has a distinct `id="cut-lab-step-panel-4"` gated panel. The Structural Findings section is always present in the DOM regardless of which step tab was last clicked, so there's no extra show/hide-state handling needed for the live-patch.

**Test surfaces already in place to extend, not create from scratch:**
- e2e: `DeckFlow.Web/e2e/cut-lab-structure.spec.ts` already asserts on `.cutlab-finding__heading`, `.cutlab-findings-count`, and specific finding-group text (e.g. "Weak floor cases") via full-page-load assertions (lines ~128-136). Add a new test in this file asserting these same selectors update their content after a `decide` POST **without a `page.goto()` reload** (e.g., accept a cut that changes a finding, assert the DOM changed via the fetch response, not a navigation).
- Vitest: `DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts` is the natural home for a new unit test on the client-side findings renderer (mirrors how `renderProposalCard` is presumably already tested there).
- Server: `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` already exists — add assertions that `PostDecideAsync`'s response includes the new `StructuralFindings` field with correct content.

**Must preserve (per D-02/D-04):** the server-rendered `Model.Findings`/`Model.FindingGroups` path (full-page reload / no-JS form fallback) must be left completely unchanged — item 6 is additive only.

## Architecture Patterns

### Recommended plan/wave grouping (not a locked decision — Claude's call at planning, but grounded in the blast-radius findings above)
```
Plan A (own wave, per CONTEXT D-04): Item 1 — dead field removal + ~43-site test rewrite, HasStructuralAnalysisDependencies redefinition
Plan B (own wave, per CONTEXT D-04): Item 6 — decide-response DTO + client renderer + e2e/Vitest coverage
Plan C (mechanical batch): Item 3 (9 theme files) + Item 4 remaining 3 sub-items (CSS) + Item 5's cacheKey/path-base/pluralizer
Plan D (if item 2's chip-basis fix and item 4's screenshot verification are large enough to warrant separation from Plan C)
```
Item 2 could ride in Plan C (it's a ViewModel + cshtml + cut-lab.ts display fix, no engine change) but its test blast radius (`CutLabViewModelWordingTests.cs`'s `CurrentCount`/`BaselineCount` tests) should be explicitly checked, not assumed zero-impact.

### Anti-Patterns to Avoid
- **Reusing Nyx's exact red hex for the 9 new dark themes** — fails AA on 2 of them (see Item 3 contrast table). Use `#fc8181` or `#fca5a5` instead.
- **Deleting item 1's fields without auditing the 10 fallback-reliant tests** — will produce silent test regressions (tests still compile and pass but no longer exercise combo/category-driven structural analysis), not a build break, because the fallback default is `null` and the code degrades gracefully rather than throwing.
- **Reimplementing `BuildFindingGroups`'s WeakFloorCase-merge logic in TypeScript for item 6** — creates a second source of truth that will drift. Return the pre-grouped shape from the server instead.
- **Changing the commander-inclusive 100-card target convention to "fix" item 2** — this convention is correct (matches the real 100-card Commander deck size) and deeply embedded across Phases 103-106; fix the chip's display basis instead, not the engine's counting basis.

## Common Pitfalls

### Pitfall 1: Assuming item 1 is a 2-line deletion
**What goes wrong:** Planner schedules a single small task; execution then discovers 43 call sites and 10 tests needing real rework, blowing the estimate.
**Why it happens:** ROADMAP/CONTEXT description ("test-only DI-probe, unused") is accurate about production behavior but doesn't capture the test suite's reliance on the ctor's fallback-construction branch.
**How to avoid:** Size item 1 as its own plan (already CONTEXT's instinct via D-04's "isolate the heavy item" logic — apply the same logic here even though CONTEXT only called out item 6 as heavy).
**Warning signs:** `dotnet build` succeeds after a naive edit, but full test suite shows fewer assertions being meaningfully exercised in structural-analysis-dependent tests (not necessarily failures — some may silently pass with weaker coverage since the fallback returns a builder with `null` spellbook/categoryKnowledge, which fails open rather than throwing).

### Pitfall 2: Treating item 2 as a string-formatting bug
**What goes wrong:** A "fix" that just adjusts wording/labels without touching the actual count basis will look right in isolation but still produce two different numbers when the user compares the two panels.
**Why it happens:** The bug isn't in text — it's in which `int` gets computed which way (commander in vs out) in two different codepaths that don't call each other.
**How to avoid:** Trace both `Model.CardCount` and `Model.BaselineCount`/`CurrentCount` to their computation source (done above) before touching any Razor/TS text.

### Pitfall 3: Verifying items 3/4's cosmetic sub-items purely by code reading
**What goes wrong:** Concluding a fix is complete because the CSS "looks right," without ever rendering the page.
**Why it happens:** Contrast math (item 3) is fully verifiable by formula, but layout overlap (item 4's Nyx badge) and real-device text truncation (item 4's mobile label) depend on actual rendering/font metrics that static analysis can't fully confirm.
**How to avoid:** Follow the existing project convention — theme×viewport screenshots before and after, per prior phases' e2e specs (`cut-lab-smoke.spec.ts`, `cut-lab-structure.spec.ts` already do this pattern).

## Code Examples

### Existing live-patch pattern to extend for Item 6 (cut-lab.ts, decide success path)
```typescript
// Source: DeckFlow.Web/wwwroot/ts/cut-lab.ts:2110-2120 (handleDecisionSubmit, current code)
const data = await response.json() as CutLabDecisionResponse;
clearRestoreConfirmation();
writeDecisionStateToHiddenInputs(data.cutLabStateJson);
rebuildWhatifSelectOptionsFromState(data.cutLabStateJson);
patchStickyBar(data);
renderRoundBanner(data.nextProposal);
renderProposalCard(data, antiForgeryToken);
renderCutsMade(data.cutsMade, data.cutLabStateJson, antiForgeryToken, payload.decision === 'restore');
// Item 6 adds one more line here, e.g.: renderStructuralFindings(data.structuralFindings);
```

### Existing pre-shaped-DTO pattern to mirror for the new findings payload
```csharp
// Source: DeckFlow.Web/Models/Api/CutLabDecideApiResponse.cs (existing sibling DTOs in the same file)
public sealed record CutLabDecideFloorWarningDto
{
    public string Role { get; init; } = string.Empty;
    public int NewCount { get; init; }
    public int Floor { get; init; }
    public string Message { get; init; } = string.Empty; // pre-formatted, client renders verbatim
}
```

### Existing shared pluralizer to reuse for Item 5 (server side)
```csharp
// Source: DeckFlow.Core/Manabase/ManabaseWording.cs:19
public static string Pluralize(string singular, int count) => count == 1 ? singular : singular + "s";
```

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Item 2's chip fix should make the "Lock your pool" chip commander-inclusive (matching Convention B) rather than making Convention B non-commander-inclusive | Item 2 | If the user/planner actually wants the opposite direction, the fix touches the round engine + Phase 103-106 tests instead of just the chip — much larger blast radius. This is flagged as a recommendation, not a locked fact, precisely because CONTEXT left the direction to Claude's discretion. |
| A2 | Nyx's own delta colors should be left unchanged (only the 9 other dark themes get new overrides) | Item 3 | If the user wants full cross-theme uniformity including Nyx, one more file changes — low risk either way, called out explicitly. |
| A3 | The "cacheKey → data-attr" and part of "path-base safety" notes from 104-simplify are already substantially fixed, leaving only the two specific remaining spots identified | Item 5 | If a broader hardcoded-path audit turns up more spots than the one found (`cut-lab.ts:1145`), effort is under-scoped. A `grep -n "'/cut-lab"` re-check at execution time is cheap insurance. |
| A4 | `/api/cut-lab/adjust` does NOT need the same live-patch treatment as `/api/cut-lab/decide` for item 6, since ROADMAP explicitly says "JS decide" only | Item 6 | If the planner/user wants full consistency, `PostAdjustAsync` needs the identical DTO addition — flagged explicitly as an open question, not assumed either way. |

**If this table is empty:** N/A — see above.

## Open Questions (RESOLVED)

> All four resolved at planning time (2026-07-22); resolutions point at the concrete plan decisions.
> 1. **Item 2 chip direction** → RESOLVED in **107-03** (Task 1): chip becomes COMMANDER-INCLUSIVE to match the Compare panel / load-bearing engine convention; `Model.CardCount` (non-commander) unchanged for validation.
> 2. **Item 6 `/api/cut-lab/adjust` scope** → RESOLVED in **107-04**: scoped to the DECIDE endpoint only (per ROADMAP wording); the adjust-path findings staleness is documented as an ACCEPTED gap.
> 3. **Item 4 Nyx badge / mobile label visual confirmation** → RESOLVED in **107-02** checkpoint: decisive fixed-or-closed screenshot pass (apply the site-common.css:1254-1257 fix if overlap remains, else close-with-screenshot).
> 4. **Item 1 `HasStructuralAnalysisDependencies`** → RESOLVED in **107-01**: KEEP the property, re-scoped to the 3 surviving deps (`_manabaseBaseline`, `_cedhBaseline`, `_simulationService`); update (not delete) the 3 DI-guard tests.

Original questions (now answered above):

1. **Item 2 direction:** Does the "Lock your pool" pool-status chip become commander-inclusive to match the Compare panel, or should some other reconciliation be chosen? Recommendation given above; CONTEXT explicitly delegates this choice to Claude at planning time — no user input needed, but the plan should state the chosen direction explicitly in its acceptance criteria.
2. **Item 6 scope on `/api/cut-lab/adjust`:** Should the quantity-tuner endpoint also get the structural-findings payload for consistency, even though ROADMAP only names "decide"? Low incremental cost since the server already computes (and discards) the same data there too.
3. **Item 4 sub-items 3 and 5 (Nyx badge, mobile label):** Final confirmation requires a live screenshot pass — code-level root causes are identified and a recommended fix is given for each, but "still present, still needs fixing" vs. "already good enough" can only be confirmed visually.
4. **Item 1's `HasStructuralAnalysisDependencies` property:** Delete entirely (and its 3 DI-guard tests), or keep it scoped to the remaining 3 real dependencies (`_manabaseBaseline`, `_cedhBaseline`, `_simulationService`)? Either is defensible; the planner should pick one and state it as an acceptance criterion since it affects exactly which tests get deleted vs. rewritten.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`); Vitest (`DeckFlow.Web/ts-tests/`); Playwright (`DeckFlow.Web/e2e/`) |
| Config file | existing project `.csproj`/`vitest.config.*`/`playwright.config.*` — no new config needed |
| Quick run command | `dotnet build DeckFlow.sln` (compile-clean check) then targeted `dotnet test --filter "FullyQualifiedName~CutLab"` |
| Full suite command | `dotnet test DeckFlow.sln` + `npx vitest run` (in `DeckFlow.Web/`) + `scripts/run-web-test.sh` then `npx --no-install playwright test` |

### Phase Requirements → Test Map
This phase has no REQUIREMENTS.md IDs (quality-only, no new requirements). Acceptance is per-item, mapped to existing test files:

| Item | Behavior | Test Type | Automated Command | File Exists? |
|------|----------|-----------|-------------------|-------------|
| 1 | `CutLabPageService` no longer declares removed fields; all structural-analysis behavior still exercised | unit | `dotnet test --filter "FullyQualifiedName~CutLabPageServiceTests"` | ✅ (rewrite in place) |
| 2 | Pool-status chip and Compare panel agree on count basis | unit + e2e | `dotnet test --filter "FullyQualifiedName~CutLabViewModelWordingTests"` + `cut-lab-smoke.spec.ts` | ✅ |
| 3 | 9 dark themes pass AA on `--cutlab-delta-up/down` | manual/screenshot (no automated contrast-check tooling in repo) | theme×viewport screenshot diff | ❌ — no automated contrast test exists; visual-only per project convention |
| 4 | 3 remaining cosmetic sub-items fixed | manual/screenshot | theme×viewport screenshot diff | ❌ — visual-only |
| 5 | No hardcoded `/cut-lab` paths outside `Url.Content`; single pluralizer used server-side | unit + grep | `dotnet test --filter "FullyQualifiedName~CutLabViewModelWordingTests"` + `grep -rn "'/cut-lab" DeckFlow.Web/wwwroot/ts/cut-lab.ts` | ✅ |
| 6 | Structural findings table updates on JS decide without reload | e2e + unit | new test in `cut-lab-structure.spec.ts` + `CutLabApiControllerTests.cs` + `cut-lab-proposal.test.ts` | ✅ (extend existing files) |

### Sampling Rate
- **Per task commit:** targeted `dotnet test --filter "FullyQualifiedName~CutLab"` + `npx vitest run` scoped to cut-lab tests.
- **Per wave merge:** full `dotnet test DeckFlow.sln` + full Vitest + full Playwright.
- **Phase gate:** Full suite green (xUnit 1874+/0 baseline per STATE.md, Vitest baseline, e2e all cut-lab specs) before `/gsd:verify-work`, plus a fresh theme×viewport screenshot pass for items 3/4.

### Wave 0 Gaps
None — existing test infrastructure (xUnit/Vitest/Playwright, all already covering Cut Lab extensively across 6 prior phases) covers every phase item; this phase only extends existing test files, it does not need new frameworks or fixtures.

## Security Domain

`security_enforcement` is not set to `false` in `.planning/config.json`, so this section is included per protocol — but this phase introduces **no new input surface**. Item 6 adds a response field to an existing, already-authenticated-by-same-origin-check endpoint (`SameOriginRequestValidator`, already enforced on `/api/cut-lab/decide` per `CutLabApiController.cs:47-50`); no new request fields, no new endpoints, no new deserialization surface beyond what's already validated. Items 1-5 touch no request/response contracts at all (DI wiring, CSS, display strings, client path constants).

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | No new surface | Existing `[RequestSizeLimit]` + `SameOriginRequestValidator` on the decide endpoint already covers the (unchanged) request shape; the new response field is server-computed output, not user input. |
| V4 Access Control | No change | Existing `[FeatureFlagGate("tool.cut-lab.enabled")]` unaffected. |

No new threat patterns introduced by this phase's scope.

## Sources

### Primary (HIGH confidence — direct repo inspection this session)
- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` (full read, lines 95-280) — Item 1 field/ctor/property analysis
- `DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs` (lines 1-100) — confirmed do-not-touch usage
- `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` — grepped/parsed all 42 `new CutLabPageService(` call sites programmatically to classify blast radius
- `DeckFlow.Web.Tests/CutLabOriginalEntriesTests.cs` — 1 additional call site
- `DeckFlow.Web/Models/CutLabViewModel.cs` (multiple reads) — Item 2 CardCount/BaselineCount/CurrentCount trace, FindingView/FindingGroupView shapes, BuildFindingGroups logic
- `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs`, `CutLabDecisionApplier.cs` — confirmed 100-card-target commander-inclusive convention
- `DeckFlow.Web/Views/Deck/CutLab.cshtml` (multiple range reads) — chip markup, structural findings section, tuner rows, package-assignment label, commander badge markup
- `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (full read, both halves) — live-patch pattern, updateLockedCountChip, currentCountFromSerializedState, hardcoded path, cacheKey handling
- `DeckFlow.Web/wwwroot/css/site-common.css`, all 22 `site-*.css` theme files — Item 3 contrast source values; Item 4 mobile responsive-table CSS, manabase-pill selected-state CSS
- `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` (lines 1-260) — Item 6 decide/adjust endpoint findings-computation trace
- `DeckFlow.Web/Models/Api/CutLabDecideApiResponse.cs` (full read) — existing DTO shape/pattern to mirror
- `DeckFlow.Web/Program.cs`, `DeckFlow.Web/Extensions/CutLabServiceCollectionExtensions.cs` — DI registration confirming production fallback-branch is dead
- `DeckFlow.Core/Manabase/ManabaseWording.cs` and its ~10 call sites — Item 5 existing shared pluralizer
- `.planning/workstreams/cut-lab/phases/107-cut-lab-tech-debt-cleanup/107-CONTEXT.md`, `107-DISCUSSION-LOG.md` — locked decisions and discretion scope
- `.planning/workstreams/cut-lab/phases/101-intake-protection-foundation/101-VERIFICATION.md` — original open-item descriptions (2 of 5 confirmed already resolved)
- `.planning/workstreams/cut-lab/ROADMAP.md`, `.planning/workstreams/cut-lab/STATE.md` — phase scope and project history
- `.planning/config.json` — `nyquist_validation: true` (no `security_enforcement` key, treated as enabled)

### Secondary (MEDIUM confidence)
- None — no WebSearch/external sources were needed for this phase; everything was verifiable directly against the repo.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Item 1 blast radius: HIGH — programmatically parsed every constructor call site in the test file rather than sampling.
- Item 2 counting-convention split: HIGH — traced every count field to its exact computation line across 4 files.
- Item 3 contrast values: HIGH — closed-form WCAG relative-luminance math against verified hex values from the actual CSS files, not estimated.
- Item 4 xmldoc/copy (already fixed): HIGH — direct grep, zero hits.
- Item 4 cosmetic sub-items (badge/contrast/truncation): MEDIUM — root cause identified in code with high confidence, but current visual severity needs a live screenshot to fully confirm.
- Item 5: HIGH — direct grep for hardcoded paths and pluralizer duplication.
- Item 6: HIGH — traced the exact server computation and existing client live-patch pattern; the recommended DTO shape is a design recommendation (reasonable, low-risk, mirrors existing sibling DTOs) rather than a verified external fact.

**Research date:** 2026-07-22
**Valid until:** Should remain valid through this phase's execution (no external dependencies, no fast-moving libraries involved). Re-verify Item 4's 2 cosmetic sub-items and Item 2's chip staleness-after-adjust question with a fresh screenshot/manual pass at execution time, since those are visual/interactive claims rather than pure code facts.
