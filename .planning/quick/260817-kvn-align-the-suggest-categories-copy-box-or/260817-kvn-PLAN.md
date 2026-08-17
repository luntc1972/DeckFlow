---
phase: quick-260817-kvn
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Web/Controllers/DeckCategoriesController.cs
  - DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs
  - DeckFlow.Web.Tests/DeckCategoriesControllerTests.cs
  - DeckFlow.Web.Tests/SuggestionsApiControllerTests.cs
  - DeckFlow.Core/Reporting/CategorySuggestionReporter.cs
autonomous: true
requirements: [QUICK-260817-KVN]

estimate:
  tokens: 52000
  raw_tokens: 26000
  tasks: 3
  confidence: low

must_haves:
  truths:
    - "On /suggest-categories the copy-to-clipboard textarea lists categories in the SAME order as the weighted evidence table, top to bottom."
    - "The same holds for the JSON API response: MergedCategoriesText line order equals WeightedCategories order."
    - "The copy text FORMAT is unchanged — plain `- Category` lines, no weights, no percentages."
    - "A regression test fails if the copy text is re-sourced from MergeWeighted's raw ranking."
  artifacts:
    - "DeckFlow.Web.Tests/DeckCategoriesControllerTests.cs — new test SuggestCategories_Success_MergedCopyTextFollowsWeightedTableOrder"
    - "DeckFlow.Web.Tests/SuggestionsApiControllerTests.cs — new test PostCardSuggestionAsync_MergedCopyTextFollowsWeightedTableOrder"
  key_links:
    - "DeckCategoriesController: MergedCategoriesText projects the SAME IReadOnlyList<CategoryWeightRow> instance assigned to WeightedCategories."
    - "SuggestionsApiController: same single-instance projection."
    - "CategoryWeightRowFactory.Build stays the single owner of display ranking; DeckFlow.Core stays behaviourally untouched."
---

<objective>
Make the /suggest-categories copy-to-clipboard box list categories in the weighted evidence table's
order instead of `MergeWeighted`'s raw agreement-then-authority order. Today the same eight
categories appear on one screen in two different orders.

Purpose: the tool's core value is paste-into-ChatGPT output. Two contradictory orderings on one
screen make the user distrust which ranking is authoritative.
Output: two controller wiring changes, two regression tests, one corrected comment in Core.

**LOCKED DECISION (user, this session):** the copy box adopts the TABLE's ranking. The copy text
format is otherwise UNCHANGED — plain `- Category` lines, no weights, no percentages. This change
is **ORDER ONLY**.

**Implementation is dispatched to Codex.** Every task below therefore carries an exact
`file:line` edit map and machine-checkable acceptance criteria, not a brief.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
</execution_context>

<context>
@CLAUDE.md
@AGENTS.md

Source files (read only the cited line windows):
- `DeckFlow.Core/Reporting/CategorySuggestionReporter.cs` — lines 51-66 (`Merge`), 86-111 (`MergeWeighted`)
- `DeckFlow.Web/Services/Categories/CategoryWeightRowFactory.cs` — lines 15-28 (`Build`)
- `DeckFlow.Web/Controllers/DeckCategoriesController.cs` — lines 107-143
- `DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs` — lines 80-110
- `DeckFlow.Web/Models/DeckDiffViewModel.cs` — lines 6-33 (`CategoryWeightRow`)
</context>

<investigation_findings>
Verified against HEAD `36deffa1` on branch `fix/category-copy-box-order`. **Do not re-derive any of
this — it is grep-verified and arithmetic-checked.**

## The two comparators

Both sort `SourceCount` DESC first, then diverge. Divergence is therefore only possible among rows
that TIE on `SourceCount`.

| Path | Sort keys | Location |
|------|-----------|----------|
| Copy text (current) | SourceCount DESC → Authority DESC (Tagger/Exact=3, Inferred=2, Edhrec=1) → DisplayLabel ASC (OrdinalIgnoreCase) | `CategorySuggestionReporter.MergeWeighted`, `CategorySuggestionReporter.cs:105-110` |
| Table (target) | SourceCount DESC → (Percent is null ? 1 : 0) ASC → Percent DESC → Category ASC (OrdinalIgnoreCase) | `CategoryWeightRowFactory.Build`, `CategoryWeightRowFactory.cs:22-28` |

## Layering constraint — do not violate

The table's ranking **cannot** move into `DeckFlow.Core`. `Build()` needs `categoryDeckCounts` and
`totalDeckCount`, which are Web-layer lookups Core never sees. The fix is therefore in
`DeckFlow.Web`: project `MergedCategoriesText` from the ranked `IReadOnlyList<CategoryWeightRow>`
that `Build()` already returns. `CategorySuggestionReporter.ToText()` needs no change and
`DeckFlow.Core` needs **no behavioural change at all** (Task 3 is a comment-only edit).

## Consumer census (grep-verified, complete)

**`MergedCategoriesText` readers — none parses or re-sorts the text; all render it verbatim:**
`DeckFlow.Web/Models/DeckDiffViewModel.cs:96`, `DeckFlow.Web/Models/Api/SuggestionResponses.cs:19`,
`DeckFlow.Web/Views/Deck/SuggestCategories.cshtml:9` and `:181` (textarea),
`DeckFlow.Web/wwwroot/ts/category-suggestions.ts` (fills `data-api-field="merged-text"`).
**No TS change and no Razor change is required by this fix.**

**`CategorySuggestionReporter.Merge()` (the plain-label overload, `CategorySuggestionReporter.cs:57-66`)
has ZERO production callers.** Its only callers are
`DeckFlow.Core.Tests/CategorySuggestionReporterMergeTests.cs` lines 60, 72, 84, 96, 108, 120.
Its `//` comment at `:62-63` asserts that sharing one merge pass keeps the copy text and the
weighted table aligned — which becomes **false** the moment Task 1 lands. Task 3 corrects it.
**Decision: `Merge()` is KEPT and its comment reworded, not deleted.** Deleting a public Core API
plus its six Core test cases is a separate call, out of scope for an order-only fix. Recorded as a
follow-up, not an action.

## Existing tests — VERIFIED, none needs editing

The investigation flagged four copy-text assertions as candidates for update. Worked through, **all
four are unaffected**, because in each fixture the two comparators coincide:

| Test | Fixture | Merge order | Table order | Verdict |
|------|---------|-------------|-------------|---------|
| `DeckCategoriesControllerTests.cs:115` (test starts :82) | Draw SourceCount 3, Ramp SourceCount 1 | Draw, Ramp | Draw, Ramp (SourceCount alone decides) | unchanged, must stay green |
| `DeckCategoriesControllerTests.cs:224` (test starts :196) | Draw (Auth 3), Ramp (Auth 2), both SourceCount 1, empty counts dict + totalDeckCount 0 → both Percent null | Draw, Ramp | Draw, Ramp (both Percent null → falls to Category ASC) | unchanged, must stay green |
| `DeckCategoriesControllerTests.cs:237` (test starts :228) | hand-built `DeckDiffViewModel`, never calls the controller | n/a | n/a | untouched by this change |
| `DeckCategoriesControllerTests.cs:122` / `SuggestionsApiControllerTests.cs:88` | pin the TABLE order only | n/a | unchanged | must stay green, **must not be edited** |

`DeckFlow.Core.Tests/CategorySuggestionReporterMergeTests.cs:70` and `:106` pin `MergeWeighted`'s
agreement/authority ranking. Core is behaviourally unchanged, so these **must remain green and must
not be edited**.

**Consequence:** no existing test currently exercises a divergent fixture, so the existing suite
gives this defect zero coverage. The new tests in Tasks 1-2 are the only proof.

## The divergence fixture (shared by both new tests)

A fixture where the two orders coincide **proves nothing** — it would pass identically before and
after the fix. The fixture below makes all three rows tie on `SourceCount`, so authority and percent
are what decide, and they decide differently.

Constructor order is `(cardName, exact, inferred, edhrec, tagger, categoryDeckCounts, cardDeckTotals, usedSources, nothingFound)`.

| Input | Value |
|-------|-------|
| exact | empty |
| inferred | empty |
| edhrec | `["Ramp"]` |
| tagger | `["Protection", "Draw"]` |
| categoryDeckCounts | `Ordinal` dictionary: `["draw"] = 6`, `["ramp"] = 30` — **`protection` deliberately absent** |
| cardDeckTotals | `new CardDeckTotals(60, ...{ ["mainboard"] = 60 })` |
| usedSources | `["EDHREC", "Scryfall Tagger"]` |
| nothingFound | `false` |

Derived state (all three rows: `SourceCount = 1`, `SourceTotal = 2`):

| Category | Source | Authority | DeckCount | Percent |
|----------|--------|-----------|-----------|---------|
| Draw | tagger | 3 | 6 | 10 |
| Protection | tagger | 3 | (absent) | **null** |
| Ramp | edhrec | 1 | 30 | 50 |

- **Merge order (old copy text): `Draw, Protection, Ramp`** — Authority 3, 3, 1; Draw before Protection on label ASC.
- **Table order (new copy text): `Ramp, Draw, Protection`** — Protection's null Percent sinks it; then Percent DESC puts Ramp (50) above Draw (10).

The fixture is deliberately chosen so that plain alphabetical order (`Draw, Protection, Ramp`)
equals the **old** order — an accidental alphabetical sort therefore also fails the new tests.
</investigation_findings>

<tasks>

<task type="tracer" tdd="true">
  <name>Task 1: MVC path — copy text follows the table ranking, proven by a divergent-fixture test</name>
  <files>DeckFlow.Web.Tests/DeckCategoriesControllerTests.cs, DeckFlow.Web/Controllers/DeckCategoriesController.cs</files>

  <read_first>
    - `DeckFlow.Web/Controllers/DeckCategoriesController.cs` lines 107-143
    - `DeckFlow.Web.Tests/DeckCategoriesControllerTests.cs` lines 81-125 (fixture and assertion style to mirror)
    - The `## The divergence fixture` table in `<investigation_findings>` above
  </read_first>

  <behavior>
    RED first. Add exactly one xUnit `[Fact]` named
    `SuggestCategories_Success_MergedCopyTextFollowsWeightedTableOrder`, placed immediately after the
    existing `SuggestCategories_Success_BuildsWeightedCategoriesWithCanonicalLookupSortingAndClamp`
    fact (which currently ends at `DeckCategoriesControllerTests.cs:193`).

    It builds a `DeckCategoriesController` over `StubCategorySuggestionService` with the divergence
    fixture, mirroring the construction at `:125-151` (same `StubCardSearchService`, same
    `NullLogger`, same `ControllerContext`/`DefaultHttpContext`), calls
    `SuggestCategories(new CategorySuggestionRequest { CardName = "Guardian Project" })`, and asserts:

    - Case 1 — pinned literal. `model.MergedCategoriesText` equals
      `"- Ramp" + Environment.NewLine + "- Draw" + Environment.NewLine + "- Protection"`.
    - Case 2 — structural cross-check. The text split on `Environment.NewLine`, with the leading
      `"- "` stripped from each line, equals `model.WeightedCategories.Select(row => row.Category)`
      element-for-element and in order. Compare as sequences (`Assert.Equal` over two
      `IEnumerable<string>`), so the test keeps holding if the fixture is later retuned.
    - Case 3 — the fixture really is divergent. `model.WeightedCategories` is asserted (via
      `Assert.Collection` or an ordered `Select(row => row.Category)` sequence equality) to be
      `Ramp, Draw, Protection` — i.e. NOT the merge order `Draw, Protection, Ramp`. Without this
      case a future fixture edit could silently collapse the two orders and leave the test passing
      while proving nothing.

    Run it BEFORE touching the controller and confirm it fails on Case 1 with actual
    `- Draw / - Protection / - Ramp`. A failure with any other actual value means the fixture was
    mistyped — fix the fixture, do not adjust the expectation.
  </behavior>

  <action>
    Step 1 (RED): add the fact described in `<behavior>`. Run it. Confirm it fails on Case 1 with
    actual `- Draw` / `- Protection` / `- Ramp`. Do not edit the controller yet.

    Step 2 (GREEN): in `DeckFlow.Web/Controllers/DeckCategoriesController.cs`, hoist the display
    ranking into a local before the `DeckDiffViewModel` initializer at `:121`, then project the copy
    text from that local instead of from `weighted`. Exact edit map:
    - `:112` — replace the one-line comment with a `// Why:` comment stating that the ranked display
      rows drive both the weighted table and the plain copy text so the two agree on screen.
    - After the `MergeWeighted(...)` assignment that ends at `:117`, introduce one local of declared
      type `IReadOnlyList<CategoryWeightRow>` (from `DeckFlow.Web.Models`) assigned from
      `CategoryWeightRowFactory.Build(weighted, result.CategoryDeckCounts, result.CardDeckTotals.TotalDeckCount)`
      — the exact expression currently inline at `:127`.
    - `:125-126` — the `MergedCategoriesText` initializer projects `Category` off that local and
      passes it to the unchanged `CategorySuggestionReporter.ToText(..., result.CardName)`.
    - `:127` — `WeightedCategories` is assigned the SAME local instance. `Build` must be called
      exactly once in this method.

    Constraints: `weighted` stays in scope and is still the argument to `Build`. Do NOT introduce a
    new helper type, extension method, or shared service — a two-line projection duplicated across
    two controllers is the correct shape here. Do NOT change `ToText`, `MergeWeighted`, `Build`, or
    the text format. Do NOT add or remove `using` directives beyond what the new local's declared
    type requires. Preserve the file's committed LF line endings and change only the lines whose
    content actually changes.
  </action>

  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --nologo -v:q --filter "FullyQualifiedName~DeckCategoriesControllerTests" > /tmp/df-t1.log 2>&1; grep -nE 'Passed!|Failed!|Failed ' /tmp/df-t1.log | head -20</automated>
    <automated>git show HEAD:DeckFlow.Web/Controllers/DeckCategoriesController.cs | grep -c $'\r'; grep -c $'\r' DeckFlow.Web/Controllers/DeckCategoriesController.cs   # both must print 0</automated>
  </verify>

  <done>
    - The new fact passes; `DeckCategoriesControllerTests.cs:115` and `:224` (the two pre-existing
      copy-text assertions) still pass and were **not edited** —
      `git diff -U0 HEAD -- DeckFlow.Web.Tests/DeckCategoriesControllerTests.cs` shows additions only,
      no deletions inside the pre-existing facts.
    - `CategoryWeightRowFactory.Build` is invoked exactly once in `SuggestCategories`:
      `grep -c 'CategoryWeightRowFactory.Build' DeckFlow.Web/Controllers/DeckCategoriesController.cs`
      returns 1.
    - `grep -n 'weighted.Select' DeckFlow.Web/Controllers/DeckCategoriesController.cs` returns
      nothing (the copy text no longer projects the raw merge).
    - Mutation proof, run once and reverted: change `.ThenByDescending(row => row.Percent)` to
      `.ThenBy(row => row.Percent)` at `CategoryWeightRowFactory.cs:26`. The new fact must go RED
      (table becomes `Draw, Ramp, Protection` while the pinned copy text still expects
      `Ramp, Draw, Protection`), while `DeckCategoriesControllerTests.cs:122` and
      `SuggestionsApiControllerTests.cs:88` stay GREEN — their ties are resolved by the null-Percent
      flag, not by Percent, so only the new guard is sensitive to this mutation. Revert the mutation
      and re-confirm green before committing. If the new fact stays green under the mutation, the
      copy text is still sourced from `MergeWeighted` — the wiring did not land.
    - The copy text format is unchanged: the assertion literals still use `"- "` prefixes with no
      digits, `%`, or `/` in them.
  </done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: API path — same wiring, same divergent fixture</name>
  <files>DeckFlow.Web.Tests/SuggestionsApiControllerTests.cs, DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs</files>

  <read_first>
    - `DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs` lines 80-110
    - `DeckFlow.Web.Tests/SuggestionsApiControllerTests.cs` lines 88-120 (fixture and `CreateController` style to mirror)
  </read_first>

  <behavior>
    RED first. Add exactly one xUnit `[Fact]` named
    `PostCardSuggestionAsync_MergedCopyTextFollowsWeightedTableOrder`, placed immediately after the
    existing `PostCardSuggestionAsync_ReturnsWeightedCategoriesInDisplayOrderWithUnavailableCountsNull`
    fact.

    Same divergence fixture as Task 1. Built through `CreateController(...)` exactly as at `:110-114`
    (same `FakeCommanderCategoryService`, `FakeMechanicLookupService`, `NullLogger`), then
    `PostCardSuggestionAsync(new CategorySuggestionRequest { CardName = "Guardian Project" }, CancellationToken.None)`.
    Unwrap via `Assert.IsType<OkObjectResult>(response.Result)` and
    `Assert.IsType<CategorySuggestionApiResponse>(ok.Value)`, then assert the same three cases as
    Task 1 against `payload.MergedCategoriesText` and `payload.WeightedCategories`.

    Run it BEFORE touching the controller and confirm it fails on the pinned literal with actual
    `- Draw / - Protection / - Ramp`.
  </behavior>

  <action>
    Step 1 (RED): add the fact. Run it. Confirm the stated failure. Do not edit the controller yet.

    Step 2 (GREEN): in `DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs`, apply the same
    shape as Task 1. Exact edit map:
    - After the `MergeWeighted(...)` assignment that ends at `:87`, introduce one local of declared
      type `IReadOnlyList<CategoryWeightRow>` assigned from
      `CategoryWeightRowFactory.Build(weighted, result.CategoryDeckCounts, result.CardDeckTotals.TotalDeckCount)`
      — the expression currently inline at `:92`.
    - `:91` — `MergedCategoriesText` projects `Category` off that local into the unchanged
      `CategorySuggestionReporter.ToText(..., result.CardName)`.
    - `:92` — `WeightedCategories` is assigned the SAME local instance; `Build` is called exactly once.
    - Add a one-line `// Why:` comment above the new local, matching the intent of Task 1's `:112`
      comment (ranked display rows drive both the table and the copy text).

    Same constraints as Task 1: no new helper type, no change to `ToText`/`MergeWeighted`/`Build`, no
    format change, no unrelated `using` churn, preserve the file's committed LF line endings, and
    touch only the lines whose content actually changes. Every other initializer in this object
    initializer (`ExactCategoriesText` through `CardDeckTotals`) is untouched.
  </action>

  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --nologo -v:q --filter "FullyQualifiedName~SuggestionsApiControllerTests" > /tmp/df-t2.log 2>&1; grep -nE 'Passed!|Failed!|Failed ' /tmp/df-t2.log | head -20</automated>
    <automated>git show HEAD:DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs | grep -c $'\r'; grep -c $'\r' DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs   # both must print 0</automated>
  </verify>

  <done>
    - The new fact passes and `SuggestionsApiControllerTests.cs:88` still passes, unedited.
    - `grep -c 'CategoryWeightRowFactory.Build' DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs`
      returns 1, and `grep -n 'weighted.Select' DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs`
      returns nothing.
    - The API response shape is otherwise byte-identical: no property added, removed, or renamed on
      `CategorySuggestionApiResponse`.
  </done>
</task>

<task type="auto">
  <name>Task 3: Correct the now-false Core comment, then prove the whole suite and build are clean</name>
  <files>DeckFlow.Core/Reporting/CategorySuggestionReporter.cs</files>

  <read_first>
    - `DeckFlow.Core/Reporting/CategorySuggestionReporter.cs` lines 51-66
  </read_first>

  <action>
    Replace the two-line `//` comment sitting between the `Merge(...)` parameter list and its
    expression body at `CategorySuggestionReporter.cs:62-63`. The replacement must state, in the
    house `// Why:` style: that `Merge` returns `MergeWeighted`'s ranked output projected to its
    labels; that the Suggest Categories UI does **not** use it, because both the copy box and the
    weighted table now render `CategoryWeightRowFactory.Build`'s display ranking, which needs
    Web-layer deck counts Core cannot see; and that `Merge` is retained as the Core-only
    merge-order accessor.

    This is a **comment-only** edit. Do NOT change the `Merge` signature, its expression body, or
    the xmldoc block at `:51-56` — that xmldoc says the return is "ordered by cross-source
    agreement", which remains true of `Merge` itself. Zero executable lines change in
    `DeckFlow.Core`. Preserve the file's committed LF line endings.

    `Merge()` still has zero production callers. Removing it and its six Core test cases is a
    separate decision, out of scope here — record it as a follow-up in the commit body, do not act
    on it.

    Then run the full solution build and test sweep and confirm the pre-existing suite is
    undisturbed. Redirect all build/test output to a file; do not let it reach the transcript.
  </action>

  <verify>
    <automated>dotnet build DeckFlow.sln -v:q -nologo > /tmp/df-build.log 2>&1; grep -cE ' error ' /tmp/df-build.log</automated>
    <automated>dotnet test --nologo -v:q > /tmp/df-test.log 2>&1; grep -nE 'Passed!|Failed!|Failed ' /tmp/df-test.log | head -20</automated>
    <automated>grep -ci lockstep DeckFlow.Core/Reporting/CategorySuggestionReporter.cs   # must be 0</automated>
    <automated>git diff --stat HEAD -- DeckFlow.Core | tail -3; git diff --ignore-all-space --stat HEAD -- DeckFlow.Core | tail -3   # must match: no EOL churn</automated>
  </verify>

  <done>
    - Build: 0 errors and no NEW warnings versus the pre-change baseline.
    - `dotnet test` reports the two new facts passing and zero regressions. In particular
      `DeckFlow.Core.Tests/CategorySuggestionReporterMergeTests.cs` is green and **unedited**
      (`git diff --stat HEAD -- DeckFlow.Core.Tests` is empty).
    - `git diff HEAD -- DeckFlow.Core` touches comment lines only — every changed line begins with
      `//` after leading whitespace.
    - `scripts/format-check-changed.sh staged` passes on the staged set.
    - No new package, project, or test framework was added: `git diff --stat HEAD` lists no
      `*.csproj`, `packages.lock.json`, or `package.json` change.
    - README needs no update: `grep -in "copy box\|copy-box\|suggest categories\|suggest-categories" README.md`
      returns nothing, so no documented behaviour describes copy-box ordering. Confirm this still
      holds rather than assuming it.
    - **VSTest is unreliable under WSL.** If `dotnet test` will not run to completion, a clean
      `dotnet build` plus a green CI run on the pushed branch is the accepted substitute — say so
      explicitly in the summary rather than reporting untested work as verified.
  </done>
</task>

</tasks>

<verification>
1. Both new facts pass; the six pre-existing order assertions listed in `<investigation_findings>`
   pass unedited.
2. `CategoryWeightRowFactory.Build` remains the single owner of display ranking, invoked once per
   controller action.
3. Mutation proof performed and reverted (Task 1 `<done>`): mutating
   `CategoryWeightRowFactory.cs:26` turns the two new facts RED and leaves the table-order facts
   GREEN — the new guards are load-bearing on exactly the wiring this fix introduces.
4. `DeckFlow.Core` diff is comment lines only.
5. Optional manual confirmation (not required to close the task, per the no-browser rule): start the
   app with `scripts/run-web-test.sh`, POST a card on /suggest-categories, and read the table and the
   textarea top-to-bottom — the sequences must match.
</verification>

<success_criteria>
- The copy box and the weighted evidence table show the same categories in the same order, on both
  the MVC page and the JSON API.
- The copy text format is unchanged: plain `- Category` lines, no weights, no percentages.
- Two regression tests, both on a fixture where the two comparators genuinely diverge, both proven
  load-bearing by the stated mutation.
- Zero behavioural change in `DeckFlow.Core`; zero new packages, projects, or frameworks; every
  touched file keeps its committed LF line endings.
</success_criteria>

<output>
Commit per logical change on `fix/category-copy-box-order`, plain default author, no
`Co-Authored-By` trailer. Suggested split:
1. `test(categories): pin copy-box order to the weighted table ranking` (both RED tests)
2. `fix(categories): source the suggest-categories copy text from the ranked display rows`
3. `docs(categories): correct the stale Merge() lockstep comment`

The commit body of (2) explains WHY the projection moved to the Web layer (Build needs deck counts
Core cannot see). The commit body of (3) records the follow-up: `Merge()` has zero production
callers and is a removal candidate in a later change.
</output>
