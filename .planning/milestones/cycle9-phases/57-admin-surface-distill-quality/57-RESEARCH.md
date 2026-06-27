# Phase 57: Admin Surface + Distill Quality - Research

**Researched:** 2026-06-18
**Domain:** ASP.NET 10 Razor MVC admin grid + C# raw-string prompt engineering
**Confidence:** HIGH

## Summary

Phase 57 has two independent tasks sharing a phase slot. SITE-01 adds the four-state
`PublishState` column to the existing `/Admin/ContentKb` Razor grid. DIST-01 rewrites the
four system-prompt strings in `DistillationSchemas.cs` to produce better KB artifacts.
Neither task touches the other's code surface.

**SITE-01 is mechanically simple.** `PublishStateDeriver` already lives in
`DeckFlow.Core.Content` (pure, stateless). `GetAllRowsAsync` already returns
`PushedToProdUtc`, `IsVisible`, and `IndexedUtc` on every `ContentSiteIndexRow`. The store
query needs no change. The controller mapping needs `PushedToProdUtc` and `IndexedUtc`
surfaced on `KbEntryRow`, and `PublishStateDeriver` registered in `DeckFlow.Web/Program.cs`
as a singleton (matching the Studio pattern at `Program.cs:110`). The view adds one `<th>`
and one `<td>` using the same badge vocabulary Studio already uses. The existing test helper
`Row()` in `AdminContentKbControllerTests.cs` can be extended with an optional
`pushedToProdUtc` param.

**DIST-01 is a prompt-only edit.** All four system prompts are static properties/constants
in `DeckFlow.Core/Knowledge/DistillationSchemas.cs:55-91`. The CLI service builds its
instruction by concatenating the system prompt with the schema (lines 397-403 of
`CliLlmDistillationService.cs`) — the output contract (`SummaryPayload`, `ClipsPayload`,
`TagsPayload`, `ClassificationPayload`) and the JSON schemas (`SummarySchema`,
`ClipsSchema`, etc.) must NOT change; only the prose system prompt strings change. The
`DistillationValidation` rules (3-8 clips, 200-word summary, allowlisted tags) still
apply and parse identically after the prompt change.

**Primary recommendation:** Treat SITE-01 and DIST-01 as two separate plans in one wave.
No cross-plan dependencies exist. SITE-01 changes: `AdminContentKbViewModel.cs` (`KbEntryRow`
record), `AdminContentKbController.cs` (mapping), `DeckFlow.Web/Program.cs` (singleton
registration), `Views/AdminContentKb/Index.cshtml` (one column). DIST-01 changes:
`DistillationSchemas.cs` (four string properties only).

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SITE-01 | Admin can see the same derived publish-state column on `/Admin/ContentKb` | `PublishStateDeriver` is in Core, `GetAllRowsAsync` returns all needed columns; add `PushedToProdUtc`/`IndexedUtc` to `KbEntryRow`, register deriver, add view column |
| DIST-01 | Reworked distill prompt produces measurably better paste-ready KB entries (no model/provider swap) | All four prompts are string literals in `DistillationSchemas.cs`; output contract/schemas unchanged; quality judged by operator before/after inspection on real content |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Publish-state derivation | Domain (Core) | — | `PublishStateDeriver` is already in `DeckFlow.Core.Content`; pure logic, no I/O |
| Admin grid data load | API/Backend (MVC Controller) | Database/Storage | Controller calls `IContentSiteIndexStore.GetAllRowsAsync`; store owns SQL |
| Admin grid rendering | Razor View | — | `Views/AdminContentKb/Index.cshtml` owns column markup |
| DI registration | Composition root (Program.cs) | — | `DeckFlow.Web/Program.cs` is the single composition root |
| Distill prompt content | Domain (Core) | — | `DeckFlow.Core/Knowledge/DistillationSchemas.cs` owns all system prompts |
| Distill output parsing | Domain (Core) | — | `DistillationValidation` + `CliLlmDistillationService` own the contract; unchanged by this phase |

## Standard Stack

No new packages. All implementation uses existing project dependencies.

### Core (existing, used in this phase)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `DeckFlow.Core.Content.PublishStateDeriver` | in-repo | Derives four-state publish badge from three inputs | Pure Core class; already used by Studio Review and Publish pages |
| `DeckFlow.Core.Content.IContentSiteIndexStore` | in-repo | Loads all KB rows including `PushedToProdUtc` | Existing store; `GetAllRowsAsync` already returns the column |
| ASP.NET Core MVC Razor | 10.0 | Admin grid view | Pinned stack |

### Package Legitimacy Audit

No external packages are added in this phase. Section not applicable.

## Architecture Patterns

### System Architecture Diagram

```
Browser (admin)
     │ GET /Admin/ContentKb
     ▼
AdminContentKbController.Index()
     │ await _store.GetAllRowsAsync()
     ▼
ContentSiteIndexStore (Core)
     │ SELECT id, source, title, ..., pushed_to_prod_utc, indexed_utc, is_visible, ...
     │ FROM content_site_index
     ▼
ContentSiteIndexRow[]  ← PushedToProdUtc already populated
     │
     │ .Select(r => new KbEntryRow { ..., PushedToProdUtc = r.PushedToProdUtc, IndexedUtc = r.IndexedUtc })
     ▼
AdminContentKbViewModel.Entries
     │
     ▼
Index.cshtml
     │ foreach entry: _deriver.Derive(entry.PushedToProdUtc, entry.IsVisible, entry.IndexedUtc)
     │ → PublishState enum → .ToDisplayString() badge
     ▼
<td> Never published | Pushed-hidden | Published | Local-newer </td>
```

For DIST-01:
```
ContentKbOrchestrator.DistillVideoAsync()
     │
     ├─ SummarizeAsync(transcript)
     │       │ BuildInstruction(SummarySystemPrompt ← CHANGED, SummarySchema ← UNCHANGED)
     │       └─ CliLlmDistillationService → claude CLI → JSON → SummaryPayload → DistillationValidation
     │
     ├─ ClassifyAsync(transcript)
     │       │ BuildInstruction(ClassificationSystemPrompt ← CHANGED, ClassificationSchema ← UNCHANGED)
     │       └─ ...
     │
     ├─ ExtractClipsAsync(transcript)
     │       │ BuildInstruction(ClipsSystemPrompt ← CHANGED, ClipsSchema ← UNCHANGED)
     │       └─ ...
     │
     └─ InferTagsAsync(transcript)
            │ BuildInstruction(TagsSystemPrompt ← CHANGED, TagsSchema ← UNCHANGED)
            └─ ...
```

### Recommended Project Structure

No new files required. Changes are to existing files:

```
DeckFlow.Core/
└── Knowledge/
    └── DistillationSchemas.cs      ← DIST-01: four prompt strings only

DeckFlow.Web/
├── Program.cs                      ← SITE-01: AddSingleton<PublishStateDeriver>()
├── Models/
│   └── AdminContentKbViewModel.cs  ← SITE-01: add PushedToProdUtc + IndexedUtc to KbEntryRow
├── Controllers/Admin/
│   └── AdminContentKbController.cs ← SITE-01: map PushedToProdUtc + IndexedUtc; inject deriver
└── Views/AdminContentKb/
    └── Index.cshtml                ← SITE-01: add Publish State <th>/<td> column

DeckFlow.Web.Tests/
└── AdminContentKbControllerTests.cs ← SITE-01: extend Row() helper; add publish-state tests
```

### Pattern 1: PublishStateDeriver DI and Usage (Web Admin)

Studio registers the deriver as a singleton (`DeckFlow.Studio/Program.cs:108-110`):

```csharp
// Source: DeckFlow.Studio/Program.cs:108-110 [VERIFIED: codebase]
// Why: PublishStateDeriver is a pure stateless class; singleton is safe and avoids allocation
// on every request.
builder.Services.AddSingleton<PublishStateDeriver>();
```

The controller must inject and use it analogously to Studio's Review.razor (`Review.razor:244`):

```csharp
// Source: DeckFlow.Studio/Pages/Review.razor:128 [VERIFIED: codebase]
// Invoke in controller or view via Deriver.Derive(entry.PushedToProdUtc, entry.IsVisible, entry.IndexedUtc)
```

Options for where derivation happens:
1. **In the view** — inject `PublishStateDeriver` into `Index.cshtml` via `@inject` and call
   `.Derive()` per row. Studio does this in Blazor via DI injection; Razor MVC supports
   `@inject` in views.
2. **In the controller** — add a `PublishState` property to `KbEntryRow`, derive in `Index()`,
   pass in the view model. Cleaner for testing.
3. **In the view model constructor** — not applicable (KbEntryRow is a record).

**Recommendation:** Option 2 (derive in controller, store result on `KbEntryRow`). This keeps
the view purely presentational, is testable without rendering infrastructure, and matches the
existing "thin view" pattern used by every other admin controller in this codebase.

`KbEntryRow` needs two new `init`-only properties. Add them after the existing `IsEvergreen`
property; no existing property or constructor is changed:

```csharp
// Source: DeckFlow.Web/Models/AdminContentKbViewModel.cs:85-86 — insert after IsEvergreen
// [VERIFIED: codebase]
/// <summary>UTC instant the operator last pushed this entry to production, or null if never pushed.</summary>
public DateTimeOffset? PushedToProdUtc { get; init; }

/// <summary>UTC timestamp when the local index row was generated (distill time).</summary>
public required DateTimeOffset IndexedUtc { get; init; }
```

And one derived property the view can read directly:

```csharp
/// <summary>Derived publish state, computed at mapping time from PushedToProdUtc, IsVisible, IndexedUtc.</summary>
public required PublishState PublishState { get; init; }
```

**Note:** `KbEntryRow` is a `sealed record` with `required` properties. Adding a `required
PublishState PublishState` property requires also populating it in every `new KbEntryRow { ... }`
call — that means the controller mapping and all test `Row()` helper calls. The test `Row()` at
line 306 of `AdminContentKbControllerTests.cs` is the only other site; extend it with a default
value.

### Pattern 2: View Column — Badge Rendering (Razor MVC)

The Studio badge vocabulary in `PublishStateExtensions.ToDisplayString()` produces the
locked four strings. The view renders:

```html
<!-- Source: DeckFlow.Web/Views/AdminContentKb/Index.cshtml — new column pattern [VERIFIED: codebase] -->
<th scope="col">Publish State</th>
...
<td data-label="Publish State">
    @{
        var psClass = entry.PublishState switch
        {
            PublishState.Published      => "kb-status--published",
            PublishState.PushedHidden   => "kb-status--hidden",
            PublishState.LocalNewer     => "kb-status--local-newer",
            _                          => "kb-status--unpublished",
        };
    }
    <span class="kb-status @psClass">@entry.PublishState.ToDisplayString()</span>
</td>
```

Note: `kb-status--local-newer` is a new CSS class; add it to `admin-common.css`
(the file that holds the existing `kb-status--*` rules at ~lines 597-612 — verified by
pattern mapper; NOT `site-common.css` or `site.css`). The existing `kb-status--published`, `kb-status--hidden`, and
`kb-status--unpublished` classes already exist (visible in the current Status column).
Two of the four new states map cleanly to existing classes; only `LocalNewer` needs a new
class (suggest `kb-status--local-newer` using the info/teal color that Studio uses:
`badge bg-info text-dark`).

The column is inserted after the existing **Status** column (the 4th column) and before
**Action** (the 5th). The `colspan="5"` in the empty-filter-row (`<td colspan="5">`) at
line 241 of the view must become `colspan="6"`.

### Pattern 3: Distill Prompt Rework

**Location:** `DeckFlow.Core/Knowledge/DistillationSchemas.cs:55-91` [VERIFIED: codebase]

**Anatomy of each instruction** (`CliLlmDistillationService.cs:397-403` [VERIFIED: codebase]):
```
{SystemPrompt}\n\nOutput ONLY valid JSON matching this exact schema. Do not include markdown fences or explanations:\n{JsonSchema}
```
Transcript is supplied via **stdin** to the claude CLI (`-p <instruction>`). The model sees
the instruction as the `--print` argument and the transcript on stdin.

**Current prompt weaknesses** (observed from artifact samples in `content-kb/`):

1. **Summary (SummarySystemPrompt, line 55-59):** Phrase "grounded only in the transcript"
   is vague. No instruction to produce paste-ready prose a ChatGPT prompt could directly
   consume. No guidance on what "useful" means for a deckbuilding KB entry (actionable
   advice focus, not plot summary).

2. **Classification (ClassificationSystemPrompt, lines 62-68):** The DROP criteria list is
   adequate. The KEEP criteria don't call out timestamps/clips-as-signal. No explicit
   guidance that a video with even ONE actionable deckbuilding segment qualifies as KEEP.

3. **Clips (ClipsSystemPrompt, lines 71-77):** "3 to 8 useful key clips" is understated.
   "Substantive mid-video advice moments" is vague. No instruction to prefer clips with
   concrete card recommendations, heuristics, or decision principles over clips with only
   generic observations. The phrase "not opening housekeeping" is good but not enough.

4. **Tags (TagsSystemPrompt, lines 80-87):** The allowlist is injected at runtime correctly.
   No instruction to choose tags that reflect the video's DOMINANT topic rather than every
   topic mentioned in passing. The artifact sample for `33FoBSdKfU.md` shows 11 card-category
   tags (all of them) and 7 archetype tags — clear over-tagging by a model that sees the full
   list and tags everything mentioned.

**Safe prompt-change envelope:**
- Change: the prose between the leading instruction and the schema line.
- Unchanged: `SummarySchema`, `ClassificationSchema`, `ClipsSchema`, `TagsSchema` (JSON).
- Unchanged: `DistillationValidation` constants (3-8 clips, 200-word max, allowlisted tags).
- Unchanged: `BuildInstruction()` logic in `CliLlmDistillationService`.
- **CARVE-OUT:** These are `static string` properties with `= """..."""` raw-string literals
  or concatenated strings. The CLAUDE.md carve-out forbids re-indenting raw-string literals.
  The existing prompts use `= """..."""` form (lines 55-87). A prompt rewrite can change the
  TEXT content of the literal but must NOT change the indentation of the `"""` delimiters or
  the surrounding code lines. The `TagsSystemPrompt` uses string concatenation (not a raw
  literal); it can be freely edited.

**Recommended prompt improvements:**

*SummarySystemPrompt* — add "paste-ready" framing, "actionable deckbuilding advice" anchor:
- State that the audience is a cEDH/Commander deckbuilder who will paste this into ChatGPT.
- Require the summary to emphasize decisions, principles, and specific card names discussed.
- Keep the 200-word cap instruction explicitly (it matches `DistillationValidation.SummaryMaxWords`).

*ClassificationSystemPrompt* — strengthen the KEEP gate:
- Make explicit that a video with at least one substantial deckbuilding lesson qualifies as KEEP.
- Add examples of KEEP signals: named cards with reasoning, slot philosophy, cut decisions.
- Tighten DROP: "mostly" entertainment/news/quiz with no extractable deckbuilding lesson.

*ClipsSystemPrompt* — sharpen clip selection criteria:
- Prefer clips where a specific card is named AND a reason is given (card X does Y in context Z).
- Prefer clips where a heuristic or principle is stated (e.g., "never cast your commander into a board wipe").
- Prefer clips with timestamps in the 10%-90% range of video runtime.
- Penalize clips that are generic ("you want to run card draw") without a specific application.

*TagsSystemPrompt* — introduce selectivity:
- Instruct to tag only the DOMINANT themes, not every theme mentioned in passing.
- Cap at 3 archetype tags, 2 bracket tags, 5 card-category tags (these caps are advisory in
  the prompt; `DistillationValidation.SanitizeClips/SanitizeTags` strips to `MaxClipCount`
  already, but tags have no hard cap in the validator, so the prompt cap is the only gate).

### Anti-Patterns to Avoid

- **Changing the JSON schemas** (`SummarySchema`, `ClipsSchema`, etc.): these drive the Dapper
  deserialization chain (`SummaryPayload`, `ClipsPayload`). Any property rename/addition breaks
  parsing silently (JSON deserialization to null). Do NOT touch them.
- **Adding a new DI-injected deriver parameter to the controller constructor** without adding a
  corresponding constructor parameter to the existing test `Build()` helper — the test suite will
  fail to compile.
- **Changing the `TagsSchema`** to enforce counts: the schema uses `"type":"array"` with no
  `maxItems`. Adding `maxItems` would be a schema change and break the contract. Use prompt
  instruction only for tag count guidance.
- **Re-indenting raw-string literals** in `DistillationSchemas.cs`: the CLAUDE.md carve-out
  (`CarveOutGuard` test) protects against this. Write replacement strings at the same indentation
  as the current `"""` delimiters.
- **Converting `{ get; init; }` to `{ get; }` on new `KbEntryRow` properties**: the CLAUDE.md
  carve-out explicitly forbids this pattern.
- **Adding the publish-state column to the view's `<tbody>` without updating the `colspan`**
  on the empty-filter row: `<td colspan="5">` at line 241 must become `<td colspan="6">`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Publish-state logic | Custom if/else in view or controller | `PublishStateDeriver.Derive()` | Single truth per PUB-02; already tested in `DeckFlow.Core.Tests/Content/PublishStateDeriverTests.cs` |
| Display strings for publish states | Inline switch in Razor | `PublishState.ToDisplayString()` (in `PublishStateExtensions`) | Locked vocabulary shared with Studio |
| Prompt schema enforcement | New JSON validator | `DistillationValidation.SanitizeClips/SanitizeTags` | Already handles out-of-vocab values |

**Key insight:** Both the state derivation engine and the display vocabulary are already
implemented and tested in Core. SITE-01 is pure wiring.

## Runtime State Inventory

> Not applicable. This is a greenfield column addition (no rename/refactor of existing data)
> and a prompt-only text change. No stored data keys, OS-registered state, or build artifacts
> are affected.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | None — `pushed_to_prod_utc` column already exists (Phase 55 migration), no rename | None |
| Live service config | None | None |
| OS-registered state | None | None |
| Secrets/env vars | None | None |
| Build artifacts | None — prompt change is source-only, no generated output | None |

## Common Pitfalls

### Pitfall 1: PublishStateDeriver Not Registered in DeckFlow.Web
**What goes wrong:** Controller constructor injection throws `InvalidOperationException` at
startup or on first request.
**Why it happens:** `PublishStateDeriver` is registered in `DeckFlow.Studio/Program.cs:110`
but NOT in `DeckFlow.Web/Program.cs`. The Web app has no existing registration.
**How to avoid:** Add `builder.Services.AddSingleton<PublishStateDeriver>();` to
`DeckFlow.Web/Program.cs`, mirroring the Studio registration.
**Warning signs:** `dotnet build` succeeds but app crashes at startup with
`InvalidOperationException: Unable to resolve service for type 'PublishStateDeriver'`.

### Pitfall 2: KbEntryRow `required` Property Without Test Helper Update
**What goes wrong:** `AdminContentKbControllerTests.cs` fails to compile.
**Why it happens:** `KbEntryRow` is a `sealed record`. If `PublishState` is added as
`required`, the `Row()` helper at line 306 must also populate it. If it's not `required`
(i.e., has a default), the tests compile but may produce misleading assertions.
**How to avoid:** Add `PublishState PublishState { get; init; } = PublishState.NeverPublished`
(with default) OR make it required and update `Row()`. The default approach is simpler and
matches how `IsHidden` and `IsEvergreen` are handled (they default to `false`).
**Warning signs:** `CS0200` or `CS8618` compiler errors in the test project.

### Pitfall 3: View colspan Mismatch
**What goes wrong:** The "No entries match the current filter" empty row renders incorrectly
(misaligned cell) after adding the publish-state column.
**Why it happens:** `<td colspan="5">` at line 241 of `Index.cshtml` is hardcoded.
**How to avoid:** Update to `colspan="6"` when adding the 6th column.
**Warning signs:** Visible layout gap or misaligned empty-state row in the admin grid.

### Pitfall 4: Prompt Rework Breaks Clip Validation
**What goes wrong:** `DistillationValidation.ValidateClips` throws because the new prompt
causes the model to return fewer than 3 clips or all clips with `timestamp_seconds: 0`.
**Why it happens:** More aggressive clip-selectivity instructions could cause the model to
return 2 "truly substantive" clips and omit a 3rd.
**How to avoid:** Do NOT raise the minimum clip threshold in the prompt above 3. Keep the
instruction "return 3 to 8 clips" but add quality guidance. Test against a real transcript
in operator UAT (DOGFOOD-01 in Phase 58 is the gate).
**Warning signs:** `DistillResult` with `Status=failed` and message "Clip extraction must
return 3 to 8 clips" during Phase 58 dogfood.

### Pitfall 5: Tag Over-Selectivity Causes Empty Tag Arrays
**What goes wrong:** Tags array returns empty `[]` because the new prompt's selectivity
instructions cause the model to find no dominant theme.
**Why it happens:** Prompts like "only tag themes central to the video" can be interpreted
too strictly.
**How to avoid:** Keep a fallback instruction: "if no dominant theme is clear, still output
at least 1 tag per dimension." Note that `DistillationValidation.SanitizeTags` does NOT
validate a minimum count — empty arrays are allowed through.
**Warning signs:** Artifact files where all three tag arrays are `[]`.

### Pitfall 6: Raw-String Literal Indentation Change Fails CarveOutGuard
**What goes wrong:** CI `format-gate` or `CarveOutGuardTests` fail.
**Why it happens:** The formatter or editor re-indents the `"""` delimiter, changing the
literal content shipped to Claude.
**How to avoid:** When editing `SummarySystemPrompt`, `ClassificationSystemPrompt`,
`ClipsSystemPrompt`, keep the opening `"""` on the same line as `= """` and the closing
`"""` at the same column as the current code.
**Warning signs:** `CarveOutGuardTests` fail with "raw-string literal was re-indented."

## Code Examples

### SITE-01: KbEntryRow with new properties

```csharp
// Source: DeckFlow.Web/Models/AdminContentKbViewModel.cs — proposed additions [VERIFIED: codebase]
// Insert after IsEvergreen (line 85). PublishState defaults to NeverPublished to avoid
// requiring every test helper to set it explicitly.

/// <summary>UTC instant the operator last pushed to prod, or null if never pushed.</summary>
public DateTimeOffset? PushedToProdUtc { get; init; }

/// <summary>UTC timestamp when the local distill/index was generated.</summary>
public required DateTimeOffset IndexedUtc { get; init; }

/// <summary>
/// Derived publish state, computed from PushedToProdUtc + IsVisible + IndexedUtc.
/// Pre-computed in the controller so the view is purely presentational.
/// </summary>
public PublishState PublishState { get; init; } = PublishState.NeverPublished;
```

### SITE-01: Controller mapping (AdminContentKbController.Index)

```csharp
// Source: AdminContentKbController.cs:62-74 — proposed change [VERIFIED: codebase]
// Add _deriver as injected PublishStateDeriver, then:
IEnumerable<KbEntryRow> entries = rows
    .Select(r => new KbEntryRow
    {
        Id = r.Id,
        Title = r.Title,
        Source = r.Source,
        Tags = r.ArchetypeTags.Concat(r.BracketTags).ToArray(),
        IsVisible = r.IsVisible,
        IsHidden = r.IsHidden,
        IsEvergreen = r.IsEvergreen,
        // SITE-01 additions:
        PushedToProdUtc = r.PushedToProdUtc,
        IndexedUtc = r.IndexedUtc,
        PublishState = _deriver.Derive(r.PushedToProdUtc, r.IsVisible, r.IndexedUtc),
    });
```

### SITE-01: DI registration (Program.cs — addition after existing ContentSiteIndexStore line 95)

```csharp
// Source: DeckFlow.Web/Program.cs:95 area — proposed addition [VERIFIED: codebase]
// Mirror of DeckFlow.Studio/Program.cs:110
builder.Services.AddSingleton<PublishStateDeriver>();
```

### SITE-01: View column — Publish State badge

```html
<!-- Source: Index.cshtml — proposed addition, 6th column [VERIFIED: codebase pattern] -->
<!-- In <thead>: after <th scope="col">Status</th> -->
<th scope="col">Publish State</th>

<!-- In <tbody> per entry row, after the Status <td>: -->
<td data-label="Publish State">
    <span class="kb-status @(entry.PublishState switch
    {
        PublishState.Published    => "kb-status--published",
        PublishState.PushedHidden => "kb-status--hidden",
        PublishState.LocalNewer   => "kb-status--local-newer",
        _                        => "kb-status--unpublished",
    })">@entry.PublishState.ToDisplayString()</span>
</td>

<!-- Empty filter row — update colspan to 6: -->
<td colspan="6">No entries match the current filter.</td>
```

### SITE-01: CSS addition (admin-common.css)

```css
/* Source: admin-common.css (~line 612, after existing kb-status--* rules) — proposed addition for LocalNewer badge [VERIFIED: pattern] */
.kb-status--local-newer {
  /* Info/teal — mirrors Studio's bg-info text-dark badge for Local-newer state */
  background-color: var(--color-info, #0dcaf0);
  color: #000;
}
```

### DIST-01: Improved SummarySystemPrompt (proposed)

```csharp
// Source: DeckFlow.Core/Knowledge/DistillationSchemas.cs:55-59 — proposed replacement
// CARVE-OUT: raw-string literal indentation must be preserved exactly as-is
public static string SummarySystemPrompt { get; } = """
    You extract paste-ready strategy summaries from Magic: The Gathering video transcripts for a Commander / cEDH deckbuilding knowledge base.
    Output only JSON matching the supplied schema.
    The audience is a competitive or experienced deckbuilder who will paste this summary into an AI chatbot for deck advice.
    Keep the summary 200 words or fewer, plain prose, grounded only in the transcript.
    Emphasize: specific card names mentioned, deckbuilding decisions made, principles or heuristics stated, and cuts or includes recommended.
    Do not summarize plot, host personality, or sponsor segments.
    """;
```

### DIST-01: Improved ClipsSystemPrompt (proposed)

```csharp
// Source: DeckFlow.Core/Knowledge/DistillationSchemas.cs:71-77 — proposed replacement
// CARVE-OUT: preserve indentation of """ delimiters
public static string ClipsSystemPrompt { get; } = """
    You extract 3 to 8 high-value key clips from Magic: The Gathering video transcripts for a deckbuilding knowledge base.
    Output only JSON matching the supplied schema.
    Every clip must include a non-zero integer timestamp_seconds citing the [mm:ss] marker nearest the advice moment.
    Prefer clips where: a specific card is named with a reason (why it fits this deck), a deckbuilding heuristic or principle is stated, or a cut/include decision is explained.
    Prefer clips from the middle 80% of the video; avoid opening intros, housekeeping, sponsor reads, and closing remarks.
    Avoid clips that state only generic advice without a specific application (e.g., "you want card draw" with no context).
    Excerpts must be grounded only in the transcript.
    """;
```

### DIST-01: Improved TagsSystemPrompt (proposed — string concatenation, no raw-string)

```csharp
// Source: DeckFlow.Core/Knowledge/DistillationSchemas.cs:80-87 — proposed replacement
// TagsSystemPrompt uses string concatenation (not a raw literal) — freely editable
public static string TagsSystemPrompt
{ get; } =
    "You infer Content KB tags from Magic: The Gathering video transcripts. "
    + "Output only JSON matching the supplied schema. "
    + "Choose ONLY from these allowlists — do not invent new values. "
    + "Tag only the DOMINANT topics; if a card category is merely mentioned in passing, do not tag it. "
    + "Aim for at most 3 archetype tags, at most 2 bracket tags, and at most 5 card-category tags. "
    + $"Archetype: {FormatAllowlist(ContentTagVocabulary.Archetypes)}. "
    + $"Bracket: {FormatAllowlist(ContentTagVocabulary.Brackets)}. "
    + $"Card category: {FormatAllowlist(ContentTagVocabulary.CardCategories)}.";
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Status col shows only Published/Unpublished/Hidden | Add Publish State col showing 4-state derived badge | Phase 57 | Admin gets push-time signal without visiting Studio |
| Generic summary/clips/tags prompts | Paste-ready / selectivity-focused prompts | Phase 57 | Better KB artifacts for Phase 58 dogfood |

## Open Questions (RESOLVED)

1. **CSS class `kb-status--local-newer`** — RESOLVED (`57-PATTERNS.md` + `57-01-PLAN.md` Task 2: badge classes live in `admin-common.css`, new state uses `#0dcaf0` info color with the prescribed fallback rule).
   - What we know: `kb-status--published`, `kb-status--hidden`, `kb-status--unpublished` exist.
   - What's unclear: exact hex/token value for the "local-newer" state info color — site-common.css may use a CSS variable.
   - Recommendation: check `site-common.css` for `--color-info` or similar token; if absent, use `#0dcaf0` (Bootstrap info) or define inline. (Pattern mapper confirmed badge classes live in `admin-common.css`, not site-common.css.)

2. **`IndexedUtc` as `required` on `KbEntryRow`** — RESOLVED (`57-01-PLAN.md` Task 1: `IndexedUtc` is `required`, `PublishState` defaulted to `NeverPublished` so existing `Row()` callers do not break; `Row()` gains an optional `pushedToProdUtc` parameter).
   - What we know: all existing rows returned by `GetAllRowsAsync` have a non-null `IndexedUtc`.
   - What's unclear: whether marking it `required` on the record will cause any build issues in other test helpers that construct `KbEntryRow` (currently only `Row()` in `AdminContentKbControllerTests.cs`).
   - Recommendation: make it `required`; update `Row()` to supply a default value (match existing `indexed` parameter already present on the helper).

3. **DIST-01 before/after baseline** — RESOLVED by design (Phase 58 DOGFOOD-01 owns the baseline comparison; Phase 57 ships improved prompts only — see `57-02-PLAN.md` objective/verification NOTEs).
   - What we know: sample artifacts exist in `content-kb/` (e.g., `the-trinket-mage/2KgJNE6vnpg.md` shows 7 clips, 3 archetype tags, 7 card-category tags — current behavior).
   - What's unclear: which transcript to use as the "before" baseline for operator comparison in Phase 58.
   - Recommendation: Phase 58 (dogfood) supplies the baseline comparison; Phase 57 only ships the improved prompts. The planner should note that DIST-01's success criterion is operator inspection of Phase 58 dogfood output vs. an existing `content-kb/` artifact from the same video.

## Environment Availability

> Step 2.6: SKIPPED for SITE-01 and DIST-01 — both are code/config-only changes with no external
> tool or service dependencies. `PublishStateDeriver` is in-repo. The claude CLI for distill is
> already wired (Phase 21.2); Phase 57 changes only the prompt text, not the CLI invocation.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none — test SDK in `.csproj` |
| Quick run command | `dotnet test DeckFlow.Web.Tests/ --no-build -x` |
| Full suite command | `dotnet test --no-build` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SITE-01 | `Index()` maps `PushedToProdUtc` and `IndexedUtc` onto `KbEntryRow` | unit | `dotnet test DeckFlow.Web.Tests/ --no-build --filter "AdminContentKbController"` | ✅ exists — `AdminContentKbControllerTests.cs` |
| SITE-01 | `Index()` derives `PublishState.Published` for visible+pushed rows | unit | same | ✅ extend `AdminContentKbControllerTests.cs` |
| SITE-01 | `Index()` derives `PublishState.NeverPublished` for rows with null `PushedToProdUtc` | unit | same | ✅ extend |
| SITE-01 | `Index()` derives `PublishState.LocalNewer` when `IndexedUtc > PushedToProdUtc` | unit | same | ✅ extend |
| DIST-01 | Prompts produce valid JSON parseable by existing validators | manual (operator UAT in Phase 58) | — | — |

**DIST-01 note:** Prompt content changes have no automated unit test. The observable
contract (`SummarySchema`, `ClipsSchema`, etc.) is unchanged; existing schema parsing tests
continue to cover it. Quality improvement is validated by operator inspection in Phase 58
dogfood.

### Sampling Rate
- **Per task commit:** `dotnet test DeckFlow.Web.Tests/ --no-build -x`
- **Per wave merge:** `dotnet test --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
None — `AdminContentKbControllerTests.cs` already exists and has a working `Build()` helper
and `FakeContentSiteIndexStore`. The test additions are extensions to existing infrastructure.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (admin auth unchanged) | existing `BasicAuthMiddleware` |
| V3 Session Management | no | unchanged |
| V4 Access Control | no | admin route already behind `/Admin` BasicAuth branch |
| V5 Input Validation | no new inputs | no new POST parameters |
| V6 Cryptography | no | unchanged |

**SITE-01 security assessment:** Adding a read-only display column to the admin grid
introduces no new attack surface. `PublishState` is derived entirely from values already
in the database row; no user-controlled input flows into `PublishStateDeriver.Derive()`.
The existing double-CSRF guard on all mutating POSTs is unchanged.

**DIST-01 security assessment:** Prompt change is code-only, not operator-configurable.
No new inputs, no new network calls, no new secrets. The `DistillationValidation`
sanitizers that reject out-of-vocabulary tags and malformed JSON continue to apply.
Transcript injection risk (untrusted content in transcript affecting the prompt) is unchanged
from the existing architecture — this is a known accepted risk (the Codex distill backend
backlog item exists specifically for this reason; it does not apply to the Claude CLI path
used in production).

### Known Threat Patterns for This Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Prompt injection via transcript content | Tampering | Accepted (existing risk, tracked in Codex distill backlog); Claude CLI path already in use |
| New admin column reveals sensitive timestamps | Information Disclosure | `PushedToProdUtc` is operator-created data, not user PII; admin-only surface behind BasicAuth |

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Core/Content/PublishStateDeriver.cs` — confirmed method signature, inputs, return states
- `DeckFlow.Core/Content/PublishState.cs` — confirmed enum members and `ToDisplayString()` extension
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — confirmed `GetAllRowsAsync` returns `PushedToProdUtc`; `EnsureSchemaAsync` already adds the column
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs:107-167` — confirmed `ContentSiteIndexRow.PushedToProdUtc` property exists
- `DeckFlow.Core/Knowledge/DistillationSchemas.cs` — confirmed all four prompt strings and four JSON schemas; exact file:line confirmed
- `DeckFlow.Core/Knowledge/DistillationValidation.cs` — confirmed output validation rules unchanged by prompt change
- `DeckFlow.Core/Integration/CliLlmDistillationService.cs:397-403` — confirmed `BuildInstruction()` concatenation pattern
- `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` — confirmed controller shape, store dependency, no existing `PublishStateDeriver`
- `DeckFlow.Web/Models/AdminContentKbViewModel.cs` — confirmed `KbEntryRow` record shape; missing `PushedToProdUtc`/`IndexedUtc`/`PublishState`
- `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` — confirmed 5-column table; `colspan="5"` empty row; existing badge classes
- `DeckFlow.Web/Program.cs:92-96` — confirmed `IContentSiteIndexStore` registered as singleton; no `PublishStateDeriver` registration
- `DeckFlow.Studio/Program.cs:108-110` — confirmed `PublishStateDeriver` registered as singleton (pattern to mirror)
- `DeckFlow.Studio/Pages/Review.razor:128,621-628` — confirmed `Deriver.Derive()` call pattern and badge rendering
- `DeckFlow.Web.Tests/AdminContentKbControllerTests.cs:306-320` — confirmed `Row()` helper; missing `PushedToProdUtc`/`IndexedUtc`
- `content-kb/the-trinket-mage/2KgJNE6vnpg.md` — real artifact sample for quality baseline (7 clips, 3 archetypes, 7 card-category tags)

### Secondary (MEDIUM confidence)
- `.planning/ROADMAP.md` Phase 57 section — success criteria confirmed
- `.planning/REQUIREMENTS.md` SITE-01, DIST-01 — requirements text confirmed
- `.planning/STATE.md` — Phase 55 marked complete; pushed_to_prod_utc migration landed

### Tertiary (LOW confidence)
None — all claims verified against codebase.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `kb-status--local-newer` CSS class does not yet exist in site-common.css | Code Examples | Low — if it exists, no harm; if absent and not added, badge renders without color |
| A2 | Improved prompts will not push clip count below 3 on typical MTG videos | Pitfalls | Medium — Phase 58 dogfood is the gate; if clips drop below 3 the orchestrator retries up to 3 times |

## Metadata

**Confidence breakdown:**
- SITE-01 (store/controller/view): HIGH — all file:line verified in codebase
- DIST-01 (prompt location and contract): HIGH — all four prompt strings confirmed at file:line
- DIST-01 (prompt improvement quality): MEDIUM — assessed against artifact samples; operator judgment in Phase 58 is the true gate
- Security: HIGH — no new attack surface introduced

**Research date:** 2026-06-18
**Valid until:** 2026-07-18 (stable codebase; store/schema changes would invalidate)
