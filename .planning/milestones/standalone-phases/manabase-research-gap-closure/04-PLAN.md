---
phase: manabase-research-gap-closure
plan: 04
type: execute
wave: 4
depends_on: ["03"]
files_modified:
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
  - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
  - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
  - DeckFlow.Core/Manabase/ManabaseModels.cs
  - DeckFlow.Web/Views/Deck/Manabase.cshtml
  - DeckFlow.Web/e2e/manabase-restricted-lands.spec.ts
  - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs
  - DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs
  - docs/manabase-analysis-rules.md
  - README.md
autonomous: true
requirements: [MBGAP-01]
must_haves:
  truths:
    - "New flag analysis.manabase.restricted-lands is registered in the catalog and seeded FALSE/0 in both Postgres and SQLite branches"
    - "With the flag OFF, ManabaseAnalyzer output is byte-identical to before this phase (parity test proves it)"
    - "The land/source table renders a * marker on the affected restricted-LAND rows (matched by name from the deck-level RestrictedSourceLandNames list, reusing the alt-cost 1* land-row marker pattern)"
    - "A restricted-land entry appears in the existing unsupported-interactions <details> panel, naming the affected lands"
    - "A Playwright spec asserts the disclosure marker renders on a restricted-land deck at desktop and mobile viewports"
  artifacts:
    - path: "DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs"
      provides: "restricted-lands seed FALSE (PG) + 0 (SQLite)"
      contains: "restricted-lands"
    - path: "DeckFlow.Web/Views/Deck/Manabase.cshtml"
      provides: "name-matched land-row * marker + gated footnote + unsupported-interactions entry"
      contains: "RestrictedSourceLandNames"
    - path: "DeckFlow.Web/e2e/manabase-restricted-lands.spec.ts"
      provides: "e2e disclosure-marker visual/functional coverage"
  key_links:
    - from: "ManabaseAnalysisService.cs"
      to: "ManabaseAnalyzer.Analyze restrictedLands param"
      via: "IsFlagOn(RestrictedLandsFlagKey) threaded as trailing optional param"
      pattern: "restrictedLands"
---

<objective>
Ship the flag + disclosure UI half of MBGAP-01 (D-04/D-05): register the new
`analysis.manabase.restricted-lands` flag (seeded OFF), thread it through the analyzer so
plan-03's composition-gated math only activates when on, prove flag-off byte-identical
parity, and surface the approximation to the user via the alt-cost-style land-row disclosure
marker (name-matched from the deck-level RestrictedSourceLandNames list) plus an
unsupported-interactions panel entry.

Purpose: D-04 (flag OFF + golden/parity before flip) and D-05 (land-row marker + panel entry).
Output: catalog+store seed, analyzer/service flag threading, Razor land-row disclosure marker,
Playwright spec, catalog/parity tests, docs + README.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/phases/manabase-research-gap-closure/CONTEXT.md
@.planning/phases/manabase-research-gap-closure/manabase-research-gap-closure-PATTERNS.md
@.planning/phases/manabase-research-gap-closure/03-SUMMARY.md

<interfaces>
<!-- Flag-rollout + disclosure templates (extracted from source). -->

Flag pattern (ritual-burst-mana / cedh-land-target precedent):
- FeatureFlagCatalog.cs:96-103 — add ["analysis.manabase.restricted-lands"] = "<description ... off = byte-identical output.>"
- FeatureFlagStore.cs: PG branch adds `('analysis.manabase.restricted-lands', FALSE),` near line 230-231; SQLite branch adds `('analysis.manabase.restricted-lands', 0),` near line 270-271 (BOTH required)
- ManabaseAnalysisService.cs:224 style — add `public const string RestrictedLandsFlagKey = "analysis.manabase.restricted-lands";`, read `bool restrictedLands = IsFlagOn(RestrictedLandsFlagKey);` (near :282), thread into Analyze(...) call (near :361)
- ManabaseAnalyzer.cs:138-149 — add trailing `bool restrictedLands = false` param to the full Analyze overload; pass down to the classify path guard added in plan 03

Disclosure marker (alt-cost `1*` LAND-row template on the land/source table — mirror the existing alt-cost land-row marker):
- marker span class `manabase-override-mark` (or a new sibling class), title/aria-label describing "restricted-source approximation applied"
- render the `*` only on land/source rows whose land name is in `report.RestrictedSourceLandNames` (deck-level NAME match — NOT a per-castability-row flag)
- gated footnote `<p class="manabase-help">` under `@if (report.HasRestrictedSourceApproximation)`
Unsupported-interactions panel (Manabase.cshtml:655-666) + UnsupportedInteraction record (ManabaseModels.cs:454-461):
- add ONE UnsupportedInteraction entry naming the restricted lands (from RestrictedSourceLandNames) via the existing population call-site pattern

Tests:
- FeatureFlagCatalogTests.cs:15-53 — the [Theory]/[InlineData] "every seeded flag has a description" — add InlineData for the new key
- ManabaseAnalysisServiceTests.cs — existing accuracy/flag parity assertions to mirror for byte-identical-off
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Register restricted-lands flag + thread through analyzer + parity</name>
  <read_first>
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs (lines 90-110)
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs (PG seed ~215-232, SQLite seed ~255-272)
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (flag-key constants ~170-230, flag reads ~265-285, Analyze call ~360-368)
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs (Analyze overloads at :26, :138)
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs, DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs
  </read_first>
  <action>
    (a) Add the catalog description for "analysis.manabase.restricted-lands" (mirror the ritual-burst-mana wording shape;
    end with "Off = byte-identical output.").
    (b) Seed FALSE in the PG branch and 0 in the SQLite branch of FeatureFlagStore.cs — BOTH, together.
    (c) Add RestrictedLandsFlagKey const + IsFlagOn read in ManabaseAnalysisService.cs; thread it into the ManabaseAnalyzer.Analyze
    call as `restrictedLands: restrictedLands`. Add the trailing-optional `bool restrictedLands = false` param to the full
    ManabaseAnalyzer.Analyze overload and pass it to the plan-03 classify guard. Do NOT reorder existing params.
    (d) Add the [InlineData] for the new key to FeatureFlagCatalogTests.cs, and to whatever seed-parity test covers
    FeatureFlagStore (grep FeatureFlagStoreSeedTests; if present, add the key there too).
    (e) Add a parity test to ManabaseAnalysisServiceTests.cs: a deck containing Cavern/Ziggurat/Nykthos produces byte-identical
    ManabaseReport with the flag OFF vs the pre-phase baseline, and DIFFERENT (discounted) weights + a populated
    RestrictedSourceLandNames list with the flag ON.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~FeatureFlagCatalog|FullyQualifiedName~ManabaseAnalysisService" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c "restricted-lands" DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` returns >= 2 (PG + SQLite)
    - catalog + both seed branches contain the new key; FeatureFlagCatalogTests InlineData added
    - parity test proves flag-OFF byte-identical (RestrictedSourceLandNames empty) and flag-ON different (list populated); passes
    - `dotnet build DeckFlow.sln` 0/0
  </acceptance_criteria>
  <done>Flag registered OFF in both dialects, threaded, parity proven.</done>
</task>

<task type="auto">
  <name>Task 2: Land-row disclosure marker + unsupported-interactions entry + docs/README</name>
  <read_first>
    - DeckFlow.Web/Views/Deck/Manabase.cshtml (alt-cost land-row marker :697-712, unsupported panel :655-666)
    - DeckFlow.Core/Manabase/ManabaseModels.cs (UnsupportedInteraction record :454-461; population call-site — grep where UnsupportedInteractions is built; the deck-level RestrictedSourceLandNames from plan 03)
    - docs/manabase-analysis-rules.md, README.md
  </read_first>
  <action>
    (a) In Manabase.cshtml, add the disclosure marker on the LAND/source table rows whose land name appears in
    `report.RestrictedSourceLandNames` (copy the alt-cost `1*` land-row `manabase-override-mark` span shape; distinct
    title/aria-label e.g. "restricted-source approximation applied") plus a gated footnote `<p class="manabase-help">` under
    `@if (report.HasRestrictedSourceApproximation)`. Do NOT add a per-castability-row flag or per-spell-row marker — the signal
    is deck-level and matched by land name. Use a visually distinct marker glyph if the `*` collides with the alt-cost marker
    (planner discretion, e.g. `†`); if a new CSS class is added, put it in site-common.css NOT site.css (theme constraint).
    (b) Populate ONE UnsupportedInteraction entry naming the affected restricted lands (from RestrictedSourceLandNames), in the
    classify/report path that already builds report.UnsupportedInteractions, so the existing <details> panel lists them. Emitted
    only when the flag is on.
    (c) Update docs/manabase-analysis-rules.md flag table with the new flag (default OFF) and the disclosure behavior; update
    README.md where manabase flags/behavior are described. Changed lines only, LF endings.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - `grep -Ec "RestrictedSourceLandNames|HasRestrictedSourceApproximation" DeckFlow.Web/Views/Deck/Manabase.cshtml` returns >= 1 (name-matched land-row marker + footnote gate); no per-castability-row flag used
    - report.UnsupportedInteractions gains a restricted-land entry (naming the lands) when the flag is on
    - Any new CSS class lives in site-common.css, not site.css
    - docs/manabase-analysis-rules.md flag table lists analysis.manabase.restricted-lands (OFF); README updated
    - `dotnet build DeckFlow.sln` 0/0; no EOL churn
  </acceptance_criteria>
  <done>Land-row marker + panel entry render behind the flag; docs+README updated.</done>
</task>

<task type="auto">
  <name>Task 3: Playwright disclosure spec (desktop + mobile)</name>
  <read_first>
    - DeckFlow.Web/e2e/manabase-ramp-disclosure.spec.ts (closest analog — disclosure marker assertions)
    - CLAUDE.md testing constraints (run-web-test.sh; env -u DISPLAY; headless)
  </read_first>
  <action>
    Create DeckFlow.Web/e2e/manabase-restricted-lands.spec.ts mirroring manabase-ramp-disclosure.spec.ts: submit a deck
    containing a restricted land (e.g. Cavern of Souls) with the restricted-lands flag ON (set via the same flag-toggling
    mechanism the existing specs use, or the admin/flag test seam), assert the land-row disclosure marker and footnote are
    visible, and assert the unsupported-interactions panel lists the restricted land. Run the assertions at a desktop viewport
    and a mobile viewport (reuse the project's viewport-parameterization pattern from existing manabase specs). Do NOT open a
    Windows-host browser; the spec must pass under `npx --no-install playwright test` headless with `env -u DISPLAY -u WAYLAND_DISPLAY`.
  </action>
  <verify>
    <automated>scripts/run-web-test.sh &amp; sleep 8; env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test manabase-restricted-lands 2>&1 | tail -20</automated>
  </verify>
  <acceptance_criteria>
    - File DeckFlow.Web/e2e/manabase-restricted-lands.spec.ts exists and exercises 2 viewports
    - The spec passes headless (land-row marker + footnote + panel entry asserted visible)
    - Spec references no absolute Windows browser path and does not launch a host browser
  </acceptance_criteria>
  <done>e2e disclosure spec green at desktop + mobile.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| operator → feature flag store | Flag seeded OFF; only an operator flip activates the new math (D-04) |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap01b-01 | Tampering | flag-off must be byte-identical | mitigate | parity test (Task 1) proves OFF == pre-phase baseline |
| T-mbgap01b-02 | Information disclosure | user misreads approximate weight as exact | mitigate | land-row disclosure marker + unsupported-interactions entry (Task 2, D-05) |
| T-mbgap01b-SC | Tampering | NuGet installs | accept | No new packages this plan |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean; full `dotnet test DeckFlow.sln` green.
- Flag OFF byte-identical; flag ON discounts restricted lands + shows land-row disclosure.
- Playwright restricted-lands spec green at 2 viewports.
- Flag ships OFF — operator flip after calibration is a deferred follow-up, not part of this DoD.
</verification>

<success_criteria>
analysis.manabase.restricted-lands registered OFF in both dialects, threaded with byte-identical-off parity, land-row disclosure marker (name-matched from the deck-level RestrictedSourceLandNames) + panel entry render behind the flag with e2e coverage at 2 viewports, docs + README updated. MBGAP-01 complete (pending operator flip).
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/04-SUMMARY.md` when done.
</output>
