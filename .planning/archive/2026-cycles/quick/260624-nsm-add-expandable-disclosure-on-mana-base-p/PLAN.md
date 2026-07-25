---
phase: quick-260624-nsm
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Core/Manabase/ManabaseModels.cs
  - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
  - DeckFlow.Core/Manabase/ManabaseClassifier.cs
  - DeckFlow.Web/Views/Deck/Manabase.cshtml
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerRampNamesTests.cs
  - DeckFlow.Web/e2e/manabase-ramp-disclosure.spec.ts
  - README.md
autonomous: true
requirements: [NSM-RAMP-DISCLOSURE]

must_haves:
  truths:
    - "The Mana Base 'Ramp:' line is an expandable <details class=\"manabase-ramp\"> whose summary is the existing count sentence"
    - "Expanding it lists the actual mana rock/dork card names and the ≤2 MV ramp/draw card names"
    - "A card that is both a rock/dork AND ≤2 MV ramp/draw appears under BOTH groups"
    - "RampSourceCount and RampAndDrawUnderThree numbers are unchanged (no math touched)"
    - "When both name lists are empty the plain-text ramp line still renders (current behavior preserved)"
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseModels.cs"
      provides: "RampSourceNames + RampAndDrawNames on ManabaseReport; RampAndDrawNames on ManabaseDeck"
      contains: "RampSourceNames"
    - path: "DeckFlow.Web/Views/Deck/Manabase.cshtml"
      provides: "<details class=\"manabase-ramp\"> disclosure"
      contains: "manabase-ramp"
    - path: "DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerRampNamesTests.cs"
      provides: "xUnit coverage of ramp name projection + de-dup + cross-membership"
  key_links:
    - from: "DeckFlow.Web/Views/Deck/Manabase.cshtml"
      to: "report.RampSourceNames / report.RampAndDrawNames"
      via: "Razor foreach"
      pattern: "report\\.RampSourceNames"
---

<objective>
Make the Mana Base "Ramp: N mana rock(s)/dork(s) · M ramp/draw piece(s)…" line expandable so the
user can see WHICH cards make up those counts, mirroring the existing
`<details class="manabase-unsupported">` disclosure.

Purpose: Counts alone are not actionable; surfacing the card names lets the user verify what the
analyzer credited as ramp.
Output: Two new name lists on the Core report, a `<details class="manabase-ramp">` UI block,
shared CSS, xUnit + Playwright coverage, README touch.

Additive display only. NO feature flag. Do NOT change `RampSourceCount` /
`RampAndDrawUnderThree` math or any castability number.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@CLAUDE.md

<interfaces>
<!-- Verified file:line targets — executor should NOT re-research. -->

ManaSource (DeckFlow.Core/Manabase/ManabaseModels.cs:8-17):
  public required string Name { get; init; }
  public double Weight { get; init; } = 1.0;
  bool IsLand, bool IsConditional  (used in the rock/dork predicate)

ManabaseReport (DeckFlow.Core/Manabase/ManabaseModels.cs:529+):
  Add: public IReadOnlyList<string> RampSourceNames { get; init; } = Array.Empty<string>();
  Add: public IReadOnlyList<string> RampAndDrawNames { get; init; } = Array.Empty<string>();
  Existing: int RampSourceCount; LandTarget.RampAndDrawUnderThree (int)
  USE { get; init; } — get-only breaks System.Text.Json (.editorconfig carve-out).

ManabaseAnalyzer (DeckFlow.Core/Manabase/ManabaseAnalyzer.cs):
  :122  RampSourceCount = deck.Sources.Count(s => !s.IsLand && !s.IsConditional && s.Weight <= 0.75)
        -> project SAME predicate to names for RampSourceNames.
  :239  RampAndDrawUnderThree = deck.RampAndDrawUnderThree
        -> copy deck.RampAndDrawNames to report at the report-construction site near :122/:239.

ManabaseClassifier (DeckFlow.Core/Manabase/ManabaseClassifier.cs):
  :80   int rampUnderThree = 0;  -> declare alongside: var rampNames = new List<string>();
  :171-173  if (card.ManaValue <= 2 && (... IsRepeatableRampOrDraw/IsRampOrDraw)) { rampUnderThree += card.Quantity; }
            -> also rampNames.Add(card.Name); (inside same if)
  :261  RampAndDrawUnderThree = rampUnderThree  -> add RampAndDrawNames = ... alongside.

ManabaseDeck (same file, the record built at :255-266):
  Add: public IReadOnlyList<string> RampAndDrawNames { get; init; } = Array.Empty<string>();

UI render to replace (DeckFlow.Web/Views/Deck/Manabase.cshtml:234-239):
  @if (report.RampSourceCount > 0 || rampDraw > 0) { <p class="manabase-help"><strong>Ramp:</strong> ... </p> }

Disclosure template to mirror (Manabase.cshtml:314-324):
  <details class="manabase-unsupported"><summary>…</summary><ul>@foreach…<li>…</li></ul></details>

Stale help sentence (Manabase.cshtml:330):
  "Counts ramp; mana rocks/dorks aren't listed." — becomes false; update/remove.

e2e conventions (DeckFlow.Web/e2e/manabase.spec.ts): note existing manabase specs
deliberately do NOT submit a real analysis (avoids a live Scryfall call). The new
disclosure spec must drive a real analysis of a known ramp-heavy deck — see Task 3.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Core data — project ramp/dork and ≤2 MV ramp-draw card names onto the report</name>
  <files>DeckFlow.Core/Manabase/ManabaseModels.cs, DeckFlow.Core/Manabase/ManabaseAnalyzer.cs, DeckFlow.Core/Manabase/ManabaseClassifier.cs</files>
  <behavior>
    - RampSourceNames contains the Name of every source matching the EXACT rock/dork predicate
      (!IsLand && !IsConditional && Weight <= 0.75), de-duplicated by name, in deck order.
    - RampAndDrawNames contains the Name of every card credited at the :171-173 ≤2 MV ramp/draw
      site, de-duplicated by name, in deck order.
    - report.RampSourceNames.Count never exceeds RampSourceCount semantics: distinct-name count
      (list length) matches the distinct sources counted; RampAndDrawNames distinct-name list
      length corresponds to the credited cards (quantity still drives the int count).
    - A card that is both a rock/dork and a ≤2 MV ramp/draw piece appears in BOTH lists.
  </behavior>
  <action>
    Add `RampSourceNames` and `RampAndDrawNames` (both `IReadOnlyList&lt;string&gt;`, default
    `Array.Empty&lt;string&gt;()`, `{ get; init; }`) to `ManabaseReport` in ManabaseModels.cs near
    line 529+. Add `RampAndDrawNames` (same shape/default) to the `ManabaseDeck` record.

    In ManabaseClassifier.cs: declare a name accumulator beside `rampUnderThree` at :80
    (e.g. `var rampNames = new List&lt;string&gt;();`). Inside the existing `if` at :171-173, after
    `rampUnderThree += card.Quantity;`, add `rampNames.Add(card.Name);`. At the ManabaseDeck
    construction (:255-266, where `RampAndDrawUnderThree = rampUnderThree`) set
    `RampAndDrawNames = rampNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList()` preserving
    first-seen order (LINQ `Distinct` preserves order). Do NOT change `rampUnderThree += card.Quantity`.

    In ManabaseAnalyzer.cs at the report construction near :122/:239: set
    `RampSourceNames = deck.Sources.Where(s =&gt; !s.IsLand &amp;&amp; !s.IsConditional &amp;&amp; s.Weight &lt;= 0.75).Select(s =&gt; s.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList()`
    (mirror the existing `RampSourceCount` predicate verbatim) and
    `RampAndDrawNames = deck.RampAndDrawNames`. Leave `RampSourceCount` and `RampAndDrawUnderThree`
    expressions untouched.

    Changed-lines-only format gate; preserve LF and the five .editorconfig carve-outs
    (especially: keep `{ get; init; }`, no inline-attribute merge).
  </action>
  <verify>
    <automated>dotnet.exe build DeckFlow.Core/DeckFlow.Core.csproj -c Debug</automated>
  </verify>
  <done>Build clean; both report properties and the ManabaseDeck property exist with init setters and Array.Empty defaults; no math expression changed.</done>
</task>

<task type="auto">
  <name>Task 2: UI + CSS + help — expandable manabase-ramp disclosure</name>
  <files>DeckFlow.Web/Views/Deck/Manabase.cshtml, DeckFlow.Web/wwwroot/css/site-common.css, README.md</files>
  <action>
    In Manabase.cshtml replace the block at :234-239 with a `&lt;details class="manabase-ramp"&gt;`
    mirroring `manabase-unsupported` (:314-324). The existing count sentence
    ("&lt;strong&gt;Ramp:&lt;/strong&gt; @report.RampSourceCount mana rock(s)/dork(s)…") becomes the
    `&lt;summary&gt;`. Body: two labeled groups, each a `&lt;ul&gt;`:
      - "Mana rocks/dorks" iterating `report.RampSourceNames`
      - "Ramp/draw ≤2 MV" iterating `report.RampAndDrawNames`
    Render each only when its list is non-empty. Use default `@name` Razor encoding (HTML-encoded).

    Guard: keep the OUTER `@if (report.RampSourceCount &gt; 0 || rampDraw &gt; 0)`. Inside it, if BOTH
    `report.RampSourceNames` and `report.RampAndDrawNames` are empty, render the CURRENT plain
    `&lt;p class="manabase-help"&gt;` line (unchanged fallback); otherwise render the `&lt;details&gt;`.

    CSS: add `manabase-ramp` rules to wwwroot/css/site-common.css (NOT site.css) so every guild
    theme fork inherits them. Reuse the `manabase-unsupported` selectors as the template; prefer
    sharing existing rules (e.g. group the new selector with the unsupported one) and add new
    rules only if the ramp block needs them (e.g. the two group labels).

    Help copy: update/remove the stale sentence at :330
    ("Counts ramp; mana rocks/dorks aren't listed.") — it is now false. Drop that clause or
    reword to reflect that rocks/dorks ARE listed in the Ramp disclosure above.

    README: if the user-facing manabase feature description / feature list mentions the ramp line,
    update it to note the expandable card-name disclosure. If README has no such mention, leave it.

    Changed-lines-only format gate; preserve LF and the .editorconfig carve-outs.
  </action>
  <verify>
    <automated>dotnet.exe build DeckFlow.Web/DeckFlow.Web.csproj -c Debug</automated>
  </verify>
  <done>Build clean; Manabase.cshtml contains `&lt;details class="manabase-ramp"&gt;` with the count sentence as summary and two name groups; empty-lists fallback preserved; site-common.css has manabase-ramp styling; stale :330 sentence fixed; no layout CSS in site.css.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: Tests — xUnit name projection + Playwright disclosure (desktop + mobile)</name>
  <files>DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerRampNamesTests.cs, DeckFlow.Web/e2e/manabase-ramp-disclosure.spec.ts</files>
  <behavior>
    xUnit (DeckFlow.Core.Tests — Analyze lives in Core):
      - Build a deck input where one card is BOTH a rock and ≤2 MV (Sol Ring or Arcane Signet),
        plus a pure mana dork (Llanowar Elves), plus a pure ≤2 MV draw spell.
      - Assert report.RampSourceNames contains the rock + the dork.
      - Assert report.RampAndDrawNames contains the both-card + the pure draw spell.
      - Assert the both-card appears in BOTH lists.
      - Assert de-dup: each name appears at most once per list (distinct-name count == list length).
      - Assert deck order is preserved (first-seen order).
    Playwright (DeckFlow.Web/e2e/manabase-ramp-disclosure.spec.ts):
      - Analyze a known ramp-heavy deck (real analysis — unlike the existing chrome-only specs;
        submit via PasteText to avoid a public-URL fetch, e.g. paste a small ramp-heavy decklist
        that exercises a rock). reuseExistingServer.
      - Assert `details.manabase-ramp` exists, expands on click, and shows at least one rock name.
      - Run chromium-desktop AND chromium-mobile; assert no horizontal overflow
        (document scrollWidth &lt;= viewport width, matching sibling manabase spec conventions).
  </behavior>
  <action>
    Mirror existing ManabaseAnalyzer*Tests for the xUnit fixture setup (how a ManabaseDeck/deck
    input is constructed and Analyze invoked). Mirror DeckFlow.Web/e2e/manabase.spec.ts conventions
    for the Playwright spec (imports, project tags, no-overflow check). Note: the existing manabase
    chrome specs deliberately do NOT submit a real analysis (live Scryfall) — this new spec DOES,
    so use a paste-text decklist and follow the project UI-testing rule (start server with
    DECKFLOW_DISABLE_AUTO_BROWSER=true + admin creds; reuseExistingServer attaches).

    Do NOT introduce a new test framework or new test project. xUnit only in DeckFlow.Core.Tests.
  </action>
  <verify>
    <automated>dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~RampNames"</automated>
  </verify>
  <done>xUnit RampNames tests green (both lists, cross-membership, de-dup, order). Playwright manabase-ramp-disclosure spec passes on chromium-desktop and chromium-mobile with no horizontal overflow (run via `npx --no-install playwright test manabase-ramp-disclosure`).</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| analyzed deck card names → rendered HTML | Card names (originating from user-supplied decklists / upstream Scryfall) cross into the Razor view |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-nsm-01 | Tampering (XSS) | Manabase.cshtml ramp name rendering | mitigate | Render names via default `@` Razor encoding (HTML-encoded); never `@Html.Raw`. Same as existing manabase-unsupported list. |
| T-nsm-02 | Information disclosure | ramp name lists | accept | Names are already-public card names from the analyzed deck the user submitted; no new data exposed. |
</threat_model>

<verification>
- `dotnet.exe build DeckFlow.sln -c Debug` clean (0 errors, 0 new warnings).
- `dotnet.exe test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` green.
- `npx --no-install playwright test manabase-ramp-disclosure --project=chromium-desktop` and `--project=chromium-mobile` green.
- Changed-lines format gate passes (`scripts/format-check-changed.sh staged`).
- Spot-check: RampSourceCount / RampAndDrawUnderThree numbers identical to before for a sample deck.
</verification>

<success_criteria>
- Ramp line is an expandable `<details class="manabase-ramp">`; expanding lists rock/dork names and ≤2 MV ramp/draw names.
- A card in both categories shows under both groups; lists are de-duped and deck-ordered.
- Empty-lists case still renders the original plain text.
- No castability/count math changed.
- xUnit + Playwright (desktop + mobile) tests added and green; no horizontal overflow.
- Stale :330 help sentence corrected.
- Atomic commits (T1, T2, T3), plain default-author, NO Co-Authored-By trailer. Not pushed.
</success_criteria>

<output>
Create `.planning/quick/260624-nsm-add-expandable-disclosure-on-mana-base-p/SUMMARY.md` when done.
</output>
