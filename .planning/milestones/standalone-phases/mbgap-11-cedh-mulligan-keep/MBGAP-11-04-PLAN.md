---
phase: mbgap-11-cedh-mulligan-keep
plan: 04
type: execute
wave: 4
depends_on: [MBGAP-11-02, MBGAP-11-03]
files_modified:
  - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
  - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
  - DeckFlow.Web.Tests/FeatureFlags/FeatureFlagCatalogTests.cs
  - DeckFlow.Web.Tests/FeatureFlags/FeatureFlagStoreSeedTests.cs
autonomous: true
requirements: [MBGAP-11-AC7]
must_haves:
  truths:
    - "A new feature flag analysis.manabase.keep-shapes exists, is seeded OFF (Postgres FALSE / SQLite 0), and has a catalog description"
    - "keepShapes is gated as (flag && showMulliganEval) so no plan-role classification or shape sim runs when the opening-hand block is hidden (Codex MED-2)"
    - "When the (gated) keepShapes is ON it flows into ManabaseAnalyzer.Analyze; when OFF the analyzer output is byte-identical to today"
    - "Enabling keep-shapes in cEDH also enables plan-role classification so the shape gate has role data"
    - "The flag is registered in PromptMutatingAnalysisFlags as precautionary insurance (satisfies AC7; inert today because manabase text is not PacketSessionCache-served; guards against future cache-routing), with a comment stating so; live byte-identity is guarded by the flag-gated ManabaseReportTextBuilder append"
  artifacts:
    - path: "DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs"
      provides: "KeepShapesFlagKey constant + resolution + threading keepShapes into Analyze + classifyPlanRoles widening + ShowKeepShapes on the result"
      contains: "KeepShapesFlagKey"
    - path: "DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs"
      provides: "Catalog description for analysis.manabase.keep-shapes"
      contains: "analysis.manabase.keep-shapes"
    - path: "DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs"
      provides: "Seed rows (FALSE / 0) for the new flag in both dialects"
      contains: "analysis.manabase.keep-shapes"
  key_links:
    - from: "ManabaseAnalysisService flag resolution"
      to: "ManabaseAnalyzer.Analyze keepShapes param"
      via: "IsFlagOn(KeepShapesFlagKey) passed through"
      pattern: "keepShapes"
    - from: "keepShapes && cEDH"
      to: "classifyPlanRoles"
      via: "widened OR condition so roles are tagged for the shape gate"
      pattern: "classifyPlanRoles"
---

<objective>
Wire the new `analysis.manabase.keep-shapes` feature flag: register it (catalog + seed OFF), resolve
it in `ManabaseAnalysisService`, thread it into `ManabaseAnalyzer.Analyze`, widen plan-role
classification so the cEDH shape gate has role data, and surface `ShowKeepShapes` on the analysis
result for the view/prompt plan. Resolve and DOCUMENT the `PromptMutatingAnalysisFlags` question.

Purpose: Gate the entire MBGAP-11 redesign behind one operator flag seeded OFF (flip after UAT).
One flag drives both the cEDH shape gate and the casual curve-coverage frame (D-03 bundles both).
Off = byte-identical output (Acceptance #7).

Output: flag key + resolution + threading + widened classification + result field; catalog/seed
entries; the documented PromptMutatingAnalysisFlags finding (a code comment + SUMMARY note). No UI
or prompt copy yet (plan 05).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md

CODEX DISPATCH NOTE (line endings): MIXED LF/CRLF repo — preserve each touched file's existing line
endings exactly (per-file detect; never normalize; never assume repo-wide). Change only the lines
whose content changes; the seed SQL blocks are long — append/insert one row each, do not reflow the
surrounding rows.
</execution_context>

<context>
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-CONTEXT.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-PATTERNS.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-02-SUMMARY.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-03-SUMMARY.md

<interfaces>
<!-- Flag-wiring pattern to copy (verified). -->

ManabaseAnalysisService.cs:207 — MulliganEvalFlagKey = "analysis.manabase.mulligan-eval";
ManabaseAnalysisService.cs:214 — CedhInteractionLensFlagKey = "analysis.manabase.cedh-interaction-lens";
ManabaseAnalysisService.cs:221 — PlanPresenceFlagKey = "analysis.manabase.plan-presence";
ManabaseAnalysisService.cs:311 — bool showMulliganEval = IsFlagOn(MulliganEvalFlagKey);
ManabaseAnalysisService.cs:317 — bool showPlanPresence = IsFlagOn(PlanPresenceFlagKey) && showMulliganEval;
ManabaseAnalysisService.cs:319 — bool showCedhInteractionLens = interactionLens && options.Mode == Cedh;
ManabaseAnalysisService.cs:335 — classifyPlanRoles: showPlanPresence || showCedhInteractionLens;
ManabaseAnalysisService.cs:405-415 — ManabaseAnalyzer.Analyze(...) call (add keepShapes: <resolved>).
ManabaseAnalysisService.cs:359-361, 463-465 — ShowMulliganEval/ShowPlanPresence/ShowCedhInteractionLens
  set on both result-construction sites (add ShowKeepShapes alongside).
ManabaseAnalysisService.cs:118-124 — public bool ShowMulliganEval/ShowPlanPresence/ShowCedhInteractionLens
  init props on the result record (add ShowKeepShapes).
ManabaseAnalysisService.cs:501 — IsFlagOn returns false for missing keys (fail-safe OFF).

FeatureFlagCatalog.cs:99-115 — description entries (guard test FeatureFlagCatalogTests.cs:42-43).
FeatureFlagStore.cs:228-230 (Postgres) and :273-275 (SQLite) — seed rows; existing manabase flags
  seed TRUE, but SEED THE NEW ONE FALSE / 0 (CONTEXT: OFF, flip after UAT). Seed test
  FeatureFlagStoreSeedTests.cs:41-42.

DeckAnalysisPacketService.cs:159-166 — PromptMutatingAnalysisFlags registry (see Task 3 finding).

Plan 02: ManabaseAnalyzer.Analyze has a keepShapes param (default false).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add the flag key, resolve it, thread keepShapes + widen classifyPlanRoles</name>
  <files>DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs</files>
  <action>
Add `public const string KeepShapesFlagKey = "analysis.manabase.keep-shapes";` beside the other
manabase flag-key constants (near ManabaseAnalysisService.cs:207-221), with xmldoc: "cEDH three-shape
opening-hand keep gate (explosive / early-engine / interaction-bridge) plus the casual curve-coverage
read. Seeded OFF; flip after UAT. Off = byte-identical output."

Resolve it near the other flag reads (~:311): `bool keepShapesFlag = IsFlagOn(KeepShapesFlagKey);`
(fail-safe OFF via IsFlagOn).

MED-2 (Codex — no hidden work when mulligan-eval is off): the shape read renders ONLY inside the
opening-hand block, which is gated on `showMulliganEval`. So compute a single gated value and use it
EVERYWHERE downstream — do not let the raw flag drive role-tagging / sim when the block is hidden
(mirror the existing `showPlanPresence = IsFlagOn(...) && showMulliganEval` idiom at :317):

  `bool keepShapes = keepShapesFlag && showMulliganEval;`

Use `keepShapes` (the gated value) for ALL of: the classifyPlanRoles widening, the Analyze arg, and
ShowKeepShapes. This prevents invisible plan-role classification + shape simulation when the opening-
hand block will not render.

Widen the plan-role classification gate (:335) so the cEDH shape gate gets role data:
`classifyPlanRoles: showPlanPresence || showCedhInteractionLens || (keepShapes && options.Mode ==
ManabaseMode.Cedh)` — the shape gate needs PlanRoles tagged, and today tagging is off unless
plan-presence or the cEDH lens is on. Add a code comment explaining why (Shape B/C read roles; Shape A
reads Payoff/TutorCombo). Because `keepShapes` already ANDs in `showMulliganEval`, role classification
stays off when the block is hidden.

Pass the gated value into the analyzer at the Analyze call (:405-415): add `keepShapes: keepShapes`
to the argument list. Add `ShowKeepShapes` (bool) to the result record's init props (beside
ShowMulliganEval, ~:118-124) and set `ShowKeepShapes = keepShapes` at BOTH result-construction sites
(the commander-selection-required early return ~:359-361 and the main return ~:463-465). Since
`keepShapes` is already `flag && showMulliganEval`, ShowKeepShapes is correctly false whenever the
block is hidden.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>KeepShapesFlagKey exists; keepShapes resolved, threaded into Analyze, and widens classifyPlanRoles; ShowKeepShapes set on both result sites; Web builds clean.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Catalog description + seed OFF in both dialects</name>
  <files>DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs, DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs, DeckFlow.Web.Tests/FeatureFlags/FeatureFlagCatalogTests.cs, DeckFlow.Web.Tests/FeatureFlags/FeatureFlagStoreSeedTests.cs</files>
  <behavior>
    - FeatureFlagCatalogTests passes: every catalogued key (incl. the new one) has a non-empty description and no orphan.
    - FeatureFlagStoreSeedTests passes: the new key is present in both seed blocks with value FALSE / 0.
  </behavior>
  <action>
Add a catalog entry for `["analysis.manabase.keep-shapes"]` in FeatureFlagCatalog.cs (near :99-115),
modeling the phrasing of the neighboring manabase entries: describe the cEDH three-shape keep gate
(two headline %s: mana-keepable + plan-keepable; shape-labeled representative openers; turn cap so a
turn-6 payoff is never called workable; commander surfaced for commander-central decks) AND the casual
curve-coverage line ("plays a spell on ~N of first 5 turns"). End with "cEDH gate + casual metric;
off = byte-identical output."

Seed the flag in BOTH dialect blocks in FeatureFlagStore.cs: Postgres block (~:228-230) add
`('analysis.manabase.keep-shapes', FALSE),` and SQLite block (~:273-275) add
`('analysis.manabase.keep-shapes', 0),`. FALSE/0 per CONTEXT (seed OFF, flip after UAT). Keep the
`ON CONFLICT (key) DO NOTHING` semantics intact — just insert one row in each list; do not reorder or
reflow existing rows.

Update FeatureFlagCatalogTests / FeatureFlagStoreSeedTests only if they assert an exact key COUNT or
an explicit expected-key set — add the new key there so they stay green (they are the guard tests). If
they iterate dynamically, no test edit is needed; confirm which and note it.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>Catalog + both seed blocks carry the new key (OFF); guard tests updated if they enumerate keys explicitly; Web.Tests builds clean.</done>
</task>

<task type="auto">
  <name>Task 3: Register the flag in PromptMutatingAnalysisFlags (precautionary) + document why</name>
  <files>DeckFlow.Web/Services/DeckAnalysisPacketService.cs</files>
  <action>
FINDING (verified during planning, corroborated by Codex plan-review MED-1 + Claude checker W1):
`PromptMutatingAnalysisFlags` (DeckAnalysisPacketService.cs:159-166) gates ONLY the `/deck-analysis`
packet's `DeckAnalysisPacketResult.AnalysisPromptText` against `PacketSessionCache`. The manabase paste
artifact is a DIFFERENT artifact: built FRESH per request at `ManabaseController.Download`
(ManabaseReportTextBuilder.Build with live `result.ShowMulliganEval` / `ShowPlanPresence` values), and
the manabase swap prompt is rebuilt every `AnalyzeAsync` — neither is served from `PacketSessionCache`.
So TODAY adding the flag here changes no runtime behavior for manabase text (the byte-identity guard
that actually matters is the flag-gated `includeCedhKeepShapes` append plan 05 adds to
`ManabaseReportTextBuilder`, mirroring `includePlanPresence`).

DECISION (reconciles AC7 — CONTEXT §5 / D-01 literally require the flag join this registry; both
reviewers recommend adding it): ADD `analysis.manabase.keep-shapes` to `PromptMutatingAnalysisFlags`
as ZERO-COST INSURANCE. Rationale: (1) satisfies the locked AC7 contract literally; (2) this exact bug
class has bitten the project before — a mutating flag left out of the registry
(`WinConMapCacheBypassTests.cs:330`, `followup_packet_cache_flag_replay`); (3) if a future change ever
routes manabase text (or a merged packet) through `PacketSessionCache`, the flag is already correctly
registered instead of silently replaying stale ON output. The cost is one registry entry; the downside
of omission is a latent correctness bug.

ACTION:
1. Add `analysis.manabase.keep-shapes` (reference `ManabaseAnalysisService.KeepShapesFlagKey` if the
   registry is a set of string keys and the constant is reachable; otherwise the literal, matching how
   neighboring entries are declared) to the `PromptMutatingAnalysisFlags` collection.
2. Add a short comment at the added entry (or extend the registry xmldoc): "Precautionary — the
   manabase paste artifact + swap prompt are rebuilt per request (ManabaseController.Download /
   ManabaseAnalysisService.AnalyzeAsync) and do not currently touch PacketSessionCache, so this entry
   is inert today; registered so any future cache-routing of manabase/merged text cannot replay a
   stale flag-ON prompt (cf. WinConMap / followup_packet_cache_flag_replay). Live byte-identity is
   guarded by the flag-gated append in ManabaseReportTextBuilder."
3. Before editing, VERIFY the no-cache claim: grep DeckAnalysisPacketService.cs for "manabase"/
   "mulligan" (expect only unrelated hits) and confirm ManabaseController.Download builds the text
   inline. Record the result in the SUMMARY. If the grep surprises you and manabase text IS
   cache-routed today, the entry becomes load-bearing (not merely precautionary) — note that in the
   SUMMARY and adjust the comment accordingly.
4. If the guard test (FeatureFlagCatalogTests / any PromptMutatingAnalysisFlags test) asserts an exact
   membership set, update it to include the new key.
  </action>
  <verify>
    <automated>grep -n "keep-shapes\|Precautionary" DeckFlow.Web/Services/DeckAnalysisPacketService.cs | head; "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>Flag added to PromptMutatingAnalysisFlags with the precautionary comment; no-cache claim verified + recorded in SUMMARY; any membership guard test updated; Web builds clean.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| operator (BasicAuth /Admin/Flags) -> feature-flag store | Flag toggle crosses here; already gated by existing admin auth + brute-force throttle. |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap11-06 | Elevation of Privilege | Flag toggle endpoint | accept | Reuses existing BasicAuthMiddleware-gated /Admin/Flags path; no new endpoint or auth surface added. |
| T-mbgap11-07 | Repudiation | Stale flag-ON prompt replay | mitigate | Manabase artifact is rebuilt per request (not cached) so the replay class does not apply today; flag registered in PromptMutatingAnalysisFlags anyway as precautionary insurance (AC7 + future cache-routing), with a comment marking it inert-today; live byte-identity guarded by the flag-gated ManabaseReportTextBuilder append. |
| T-mbgap11-SC | Tampering | package installs | n/a | No package installs this phase. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean.
- Flag resolves OFF by default (seed FALSE/0); with the flag OFF, `ManabaseAnalyzer.Analyze` receives
  keepShapes=false and output is byte-identical to pre-phase.
- EOL: per-file `\r` counts unchanged vs `git show HEAD:<path>`; seed blocks show a single added row.
</verification>

<success_criteria>
- New flag registered, seeded OFF in both dialects, catalogued; guard tests green.
- keepShapes threaded into Analyze; classifyPlanRoles widened for cEDH keep-shapes; ShowKeepShapes on result.
- PromptMutatingAnalysisFlags decision resolved + documented — flag ADDED as precautionary insurance (satisfies AC7), rationale in code + SUMMARY; no-cache claim verified.
</success_criteria>

<output>
Create `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-04-SUMMARY.md` when done.
</output>
