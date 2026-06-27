# Phase 57: Admin Surface + Distill Quality - Pattern Map

**Mapped:** 2026-06-18
**Files analyzed:** 6 modified files (no new files)
**Analogs found:** 6 / 6

---

## File Classification

| Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Web/Models/AdminContentKbViewModel.cs` | model | request-response | itself (extend `KbEntryRow` record) | exact — same file |
| `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` | controller | request-response | `DeckFlow.Studio/Pages/Review.razor` (deriver injection + call pattern) | role-match |
| `DeckFlow.Web/Program.cs` | config | — | `DeckFlow.Studio/Program.cs:108-110` (singleton registration) | exact copy |
| `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` | view | request-response | itself + `Review.razor:621-626` (badge vocabulary) | exact — same file + badge template |
| `DeckFlow.Web.Tests/AdminContentKbControllerTests.cs` | test | — | itself (extend existing `Row()` helper + `Build()`) | exact — same file |
| `DeckFlow.Core/Knowledge/DistillationSchemas.cs` | utility | — | itself (edit four string properties only) | exact — same file |

---

## Pattern Assignments

### `DeckFlow.Web/Models/AdminContentKbViewModel.cs` (model)

**Analog:** itself — extend the existing `KbEntryRow` sealed record.

**Current `KbEntryRow` record** (lines 64-86 — full record as it exists today):
```csharp
/// <summary>A single index entry row in the per-entry curation grid.</summary>
public sealed record KbEntryRow
{
    /// <summary>Surrogate row id (the SetVisibility key).</summary>
    public required long Id { get; init; }

    /// <summary>Entry title.</summary>
    public required string Title { get; init; }

    /// <summary>Source display name.</summary>
    public required string Source { get; init; }

    /// <summary>Archetype + bracket tag chips for display.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>Whether this entry is currently published to the public surface.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Whether this entry is currently hidden from normal curation views.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Whether this entry is currently marked as evergreen.</summary>
    public bool IsEvergreen { get; init; }
}
```

**Edit point:** Insert three new properties after `IsEvergreen` (line 85), before the closing `}` on line 86. Add `using DeckFlow.Core.Content;` to the file's using block.

**Properties to insert** (pattern: nullable `DateTimeOffset?` for optional timestamp; `required DateTimeOffset` for mandatory; defaulted enum for derivable state):
```csharp
/// <summary>UTC instant the operator last pushed this entry to production, or null if never pushed.</summary>
public DateTimeOffset? PushedToProdUtc { get; init; }

/// <summary>UTC timestamp when the local index row was generated (distill time).</summary>
public required DateTimeOffset IndexedUtc { get; init; }

/// <summary>
/// Derived publish state, computed at mapping time in the controller from PushedToProdUtc +
/// IsVisible + IndexedUtc. Defaults to NeverPublished so test helpers that do not set it
/// explicitly still compile (avoids a required-property ripple through all test Row() callers).
/// </summary>
public PublishState PublishState { get; init; } = PublishState.NeverPublished;
```

**Carve-out:** `{ get; init; }` — NEVER simplify to `{ get; }`. System.Text.Json silently skips get-only properties (.NET 9+); this pattern is a CLAUDE.md protected carve-out.

---

### `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` (controller)

**Analog:** `DeckFlow.Studio/Pages/Review.razor` for the deriver injection + call pattern; `AdminContentKbController.cs` itself for the constructor and mapping pattern to follow.

**DI injection pattern from Studio** (`Review.razor:244`):
```csharp
// Blazor @inject — the MVC equivalent is constructor injection
private PublishStateDeriver Deriver { get; set; } = default!;
```

**Deriver call pattern from Studio** (`Review.razor:128`):
```csharp
// In Razor: Deriver.Derive(vm.PushedToProdUtc, vm.IsVisible, vm.IndexedUtc)
// In MVC controller: _deriver.Derive(r.PushedToProdUtc, r.IsVisible, r.IndexedUtc)
```

**Current constructor** (lines 32-46 — pattern to extend with one new parameter):
```csharp
public AdminContentKbController(
    IContentSiteIndexStore store,
    IContentKbSeedLoader seedLoader,
    IFeatureFlagCache flagCache,
    ILogger<AdminContentKbController> logger)
{
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(seedLoader);
    ArgumentNullException.ThrowIfNull(flagCache);
    ArgumentNullException.ThrowIfNull(logger);
    _store = store;
    _seedLoader = seedLoader;
    _flagCache = flagCache;
    _logger = logger;
}
```

**Edit point — constructor:** Add `PublishStateDeriver deriver` as the 5th parameter; add `ArgumentNullException.ThrowIfNull(deriver);` and `_deriver = deriver;`; add `private readonly PublishStateDeriver _deriver;` field. Also add `using DeckFlow.Core.Content;` if not already present (it already is — line 1).

**Current `.Select()` mapping** (lines 64-74 — the exact block to extend):
```csharp
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
    });
```

**Edit point — mapping:** Add three lines inside the object initializer after `IsEvergreen = r.IsEvergreen,`:
```csharp
        PushedToProdUtc = r.PushedToProdUtc,
        IndexedUtc = r.IndexedUtc,
        PublishState = _deriver.Derive(r.PushedToProdUtc, r.IsVisible, r.IndexedUtc),
```

---

### `DeckFlow.Web/Program.cs` (config — DI registration)

**Analog:** `DeckFlow.Studio/Program.cs:108-110` — exact line to mirror.

**Studio registration** (lines 108-110, with its xmldoc comment):
```csharp
// Why: PublishStateDeriver is a pure stateless class; singleton is safe and avoids allocation
// per-request. Pages inject it via [Inject] to derive publish state from ContentSiteIndexRow fields.
builder.Services.AddSingleton<PublishStateDeriver>();
```

**Edit point:** Insert immediately after line 96 (`builder.Services.AddSingleton<IContentKbSeedLoader, ContentKbSeedLoader>();`). The registration block in context:
```csharp
// line 92-96 (existing)
builder.Services.AddSingleton<DeckFlow.Core.Content.IContentSiteIndexStore>(_ =>
    new DeckFlow.Core.Content.ContentSiteIndexStore(
        DeckFlowDatabaseConnectionFactory.CreateContentSiteIndexConnection(builder.Environment)));
builder.Services.AddSingleton<ContentKbArtifactPathResolver>();
builder.Services.AddSingleton<IContentKbSeedLoader, ContentKbSeedLoader>();
// INSERT HERE — line 97 (new):
builder.Services.AddSingleton<DeckFlow.Core.Content.PublishStateDeriver>();
// line 97 (existing, shifts to 98):
builder.Services.AddSingleton<IAdminBruteForceTrackerStore, AdminBruteForceTrackerStore>();
```

Use the fully-qualified `DeckFlow.Core.Content.PublishStateDeriver` form (matching the existing pattern at lines 92-94 where `DeckFlow.Core.Content.IContentSiteIndexStore` and `DeckFlow.Core.Content.ContentSiteIndexStore` are written fully qualified) — or add a `using DeckFlow.Core.Content;` at the top of the lambda/block if one already exists for this namespace.

---

### `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` (view)

**Analog:** itself for table structure; `DeckFlow.Studio/Pages/Review.razor:621-626` for badge vocabulary (the locked four display strings).

**Studio badge vocabulary** (`Review.razor:621-626`):
```csharp
private static RenderFragment RenderPublishStateBadge(PublishState state) => state switch
{
    PublishState.NeverPublished => @<span class="badge bg-secondary">Never published</span>,
    PublishState.PushedHidden   => @<span class="badge bg-warning text-dark">Pushed-hidden</span>,
    PublishState.Published      => @<span class="badge bg-success text-white">Published</span>,
    PublishState.LocalNewer     => @<span class="badge bg-info text-dark">Local-newer</span>,
    ...
```

The Web admin view uses CSS classes instead of Bootstrap badges; the display strings (the text) are identical and come from `ToDisplayString()`.

**Current `<thead>` columns** (lines 132-138):
```html
<thead>
    <tr>
        <th scope="col">Title</th>
        <th scope="col">Source</th>
        <th scope="col">Tags</th>
        <th scope="col">Status</th>
        <th scope="col">Action</th>
    </tr>
</thead>
```

**Edit point — thead:** Insert `<th scope="col">Publish State</th>` after the `Status` `<th>` (after line 136), before the `Action` `<th>`.

**Current Status `<td>` block** (lines 153-166 — the per-row status cell to insert after):
```html
<td data-label="Status">
    @if (entry.IsVisible)
    {
        <span class="kb-status kb-status--published" aria-label="Published">Published</span>
    }
    else if (entry.IsHidden)
    {
        <span class="kb-status kb-status--hidden" aria-label="Hidden">Hidden</span>
    }
    else
    {
        <span class="kb-status kb-status--unpublished" aria-label="Unpublished">Unpublished</span>
    }
</td>
```

**Edit point — tbody per-row:** Insert a new `<td>` immediately after the closing `</td>` of the Status cell (after line 166), before the Action `<td>`:
```html
<td data-label="Publish State">
    <span class="kb-status @(entry.PublishState switch
    {
        DeckFlow.Core.Content.PublishState.Published    => "kb-status--published",
        DeckFlow.Core.Content.PublishState.PushedHidden => "kb-status--hidden",
        DeckFlow.Core.Content.PublishState.LocalNewer   => "kb-status--local-newer",
        _                                               => "kb-status--unpublished",
    })">@entry.PublishState.ToDisplayString()</span>
</td>
```

**Edit point — empty filter row** (line 241):
```html
<!-- Current (line 241): -->
<td colspan="5">No entries match the current filter.</td>
<!-- Change to: -->
<td colspan="6">No entries match the current filter.</td>
```

**CSS — new class needed in `admin-common.css`** (after line 612, within the existing `.kb-status--unpublished` block's closing `}`). The existing three classes (lines 597-612):
```css
.admin-shell .kb-status--published {
  color: var(--accent);
  border-color: var(--accent);
  background: rgba(59, 130, 246, 0.08);
}

.admin-shell .kb-status--hidden {
  color: var(--muted);
  background: transparent;
}

.admin-shell .kb-status--unpublished {
  color: #b45309;
  border-color: #f59e0b;
  background: rgba(245, 158, 11, 0.10);
}
```

**New class to add** (mirrors Studio's `bg-info text-dark` for Local-newer; use teal info color):
```css
.admin-shell .kb-status--local-newer {
  /* Why: mirrors Studio's bg-info badge for the Local-newer state (local index newer than last push) */
  color: #000;
  border-color: #0dcaf0;
  background: rgba(13, 202, 240, 0.15);
}
```

**Note:** CSS lives in `admin-common.css` (confirmed — all `kb-status` classes are there), NOT `site-common.css` or `site.css`.

---

### `DeckFlow.Web.Tests/AdminContentKbControllerTests.cs` (test)

**Analog:** itself — extend the existing `Build()` helper and `Row()` helper.

**Current `Build()` helper** (lines 282-304):
```csharp
private static AdminContentKbController Build(
    FakeContentSiteIndexStore store,
    out FakeContentKbSeedLoader loaderOut,
    bool crossOrigin)
{
    loaderOut = loader;
    var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool> { ["content.kb.enabled"] = false });
    var controller = new AdminContentKbController(
        store,
        loader,
        flagCache,
        NullLogger<AdminContentKbController>.Instance);
    // ... HttpContext wiring ...
    return controller;
}
```

**Edit point — `Build()`:** The `new AdminContentKbController(...)` call gains a 5th argument. Since `PublishStateDeriver` is a concrete class with a parameterless constructor (pure stateless), pass `new PublishStateDeriver()` directly — no fake needed:
```csharp
var controller = new AdminContentKbController(
    store,
    loader,
    flagCache,
    NullLogger<AdminContentKbController>.Instance,
    new DeckFlow.Core.Content.PublishStateDeriver());
```

**Current `Row()` helper** (lines 306-321 — full method as it exists today):
```csharp
private static ContentSiteIndexRow Row(long id, bool visible, bool hidden = false, DateTimeOffset? indexed = null)
    => new()
    {
        Id = id,
        Source = "EDHRECast",
        Title = "Title " + id,
        VideoUrl = "https://youtu.be/x" + id,
        ArtifactPath = $"content-kb/edhrecast/{id}.md",
        IndexedUtc = indexed ?? new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
        ArchetypeTags = Array.Empty<string>(),
        BracketTags = Array.Empty<string>(),
        CardCategoryTags = Array.Empty<string>(),
        YoutubeVideoId = "x" + id,
        IsVisible = visible,
        IsHidden = hidden,
    };
```

**Edit point — `Row()`:** Add an optional `DateTimeOffset? pushedToProdUtc = null` parameter and wire `PushedToProdUtc = pushedToProdUtc` in the initializer. `IndexedUtc` is already populated from the existing `indexed` parameter — no change needed there.

**New test cases to add** (three facts, one per derived state variant):
```csharp
[Fact]
public async Task Index_MapsPublishState_NeverPublished_WhenNoPushDate()
{
    // row: pushed=null → NeverPublished
}

[Fact]
public async Task Index_MapsPublishState_Published_WhenVisibleAndPushed()
{
    // row: visible=true, pushedToProdUtc=IndexedUtc → Published
}

[Fact]
public async Task Index_MapsPublishState_LocalNewer_WhenIndexedAfterPush()
{
    // row: IndexedUtc > pushedToProdUtc → LocalNewer
}
```

Pattern: assert `((ViewResult)result).Model` cast to `AdminContentKbViewModel`, then check `.Entries[0].PublishState`. Follow the existing `Index_*` test naming style (`Method_Scenario_ExpectedResult`).

---

### `DeckFlow.Core/Knowledge/DistillationSchemas.cs` (utility — prompt strings only)

**Analog:** itself — the only edit is the text content of four string properties at lines 55-87.

**CRITICAL CARVE-OUT:** Lines 55-77 use C# raw-string literals (`= """..."""`). The CLAUDE.md carve-out and `CarveOutGuardTests` forbid re-indenting the `"""` delimiters. The opening `"""` must stay on the same line as `= """` and the closing `"""` must stay at the same indentation column as the current code. Only the text *inside* the literal changes.

**SummarySystemPrompt — current text** (lines 55-59):
```csharp
public static string SummarySystemPrompt { get; } = """
    You extract grounded strategy summaries from Magic: The Gathering video transcripts.
    Output only JSON matching the supplied schema.
    Keep the summary 200 words or fewer, plain prose, and grounded only in the transcript.
    """;
```
Edit: replace only the three prose lines inside the `"""..."""` delimiters. The `public static string SummarySystemPrompt { get; } = """` line and the closing `    """;` line are NOT moved or re-indented.

**ClassificationSystemPrompt — current text** (lines 62-68):
```csharp
public static string ClassificationSystemPrompt { get; } = """
    You classify Magic: The Gathering video transcripts for the Content KB.
    Output only JSON matching the supplied schema.
    KEEP transcripts about deckbuilding decisions: card selection, synergy, slot philosophy, cuts, and deckbuilding philosophy with principles or heuristics applied to a deck context.
    DROP transcripts that are mostly trivia or quiz content, news or set commentary with no deckbuilding application, meta or format philosophy with no actionable deckbuilding advice, intro or announcement or promotional material, or budget-pool reveals without deckbuilding guidance.
    When in doubt, keep.
    """;
```

**ClipsSystemPrompt — current text** (lines 71-77):
```csharp
public static string ClipsSystemPrompt { get; } = """
    You extract 3 to 8 useful key clips from Magic: The Gathering video transcripts.
    Output only JSON matching the supplied schema.
    Every clip must include a non-zero integer timestamp_seconds citing the [mm:ss] marker nearest the advice moment.
    Select substantive mid-video advice moments, not opening housekeeping, and return only clips with a defensible non-zero timestamp grounded in the transcript.
    Excerpts must be grounded only in the transcript.
    """;
```

**TagsSystemPrompt — current text** (lines 80-87, string concatenation — NOT a raw literal, freely editable):
```csharp
public static string TagsSystemPrompt
{ get; } =
    "You infer candidate Content KB tags from Magic: The Gathering video transcripts. "
    + "Output only JSON matching the supplied schema. "
    + "Choose only from these allowlists. "
    + $"Archetype: {FormatAllowlist(ContentTagVocabulary.Archetypes)}. "
    + $"Bracket: {FormatAllowlist(ContentTagVocabulary.Brackets)}. "
    + $"Card category: {FormatAllowlist(ContentTagVocabulary.CardCategories)}.";
```

**What must NOT change in this file:**
- `SummarySchema` (lines 11-15) — JSON schema constant
- `ClassificationSchema` (lines 20-26) — JSON schema constant
- `ClipsSchema` (lines 31-40) — JSON schema constant
- `TagsSchema` (lines 45-52) — JSON schema constant
- `FormatAllowlist` (lines 89-90) — private helper
- The `{ get; }` property accessor pattern on `TagsSystemPrompt` — it uses `{ get; }` not `{ get; init; }` because it is a computed string, not a JSON-deserializable property; this is intentional and correct

---

## Shared Patterns

### Constructor injection + ArgumentNullException.ThrowIfNull guard
**Source:** `AdminContentKbController.cs:32-46`
**Apply to:** the extended constructor for SITE-01
```csharp
// Pattern: one ThrowIfNull per parameter, then assign to readonly field
ArgumentNullException.ThrowIfNull(deriver);
_deriver = deriver;
```

### `sealed record` with `required` + `init` properties
**Source:** `KbEntryRow` in `AdminContentKbViewModel.cs:64-86`
**Apply to:** the three new properties on `KbEntryRow`
- Use `required` for `IndexedUtc` (always present in store rows)
- Use nullable `DateTimeOffset?` (no `required`) for `PushedToProdUtc` (legitimately null)
- Use defaulted `PublishState PublishState { get; init; } = PublishState.NeverPublished` (avoids required-property compile break in test helpers)

### xUnit test pattern — extend existing fact tests
**Source:** `AdminContentKbControllerTests.cs:22-46`
**Apply to:** new publish-state test facts
```csharp
[Fact]
public async Task <Method>_<Scenario>_<ExpectedResult>()
{
    var store = new FakeContentSiteIndexStore();
    store.Rows.Add(Row(1, visible: ..., pushedToProdUtc: ...));
    var controller = Build(store, out _, crossOrigin: false);

    var result = await controller.Index(cancellationToken: default);

    var vm = Assert.IsType<ViewResult>(result).Model as AdminContentKbViewModel;
    Assert.Equal(PublishState.XXX, vm!.Entries[0].PublishState);
}
```

---

## No Analog Found

None — all six files are modifications to existing well-established files with strong analogs.

---

## CLAUDE.md Carve-Outs Flagged for This Phase

| Carve-out | Applies to | Consequence if violated |
|---|---|---|
| Never re-indent `"""` raw-string literals | `DistillationSchemas.cs` lines 55-77 | `CarveOutGuardTests` fail; literal value changes (ships different prompt) |
| Never convert `{ get; init; }` to `{ get; }` | New `KbEntryRow` properties | System.Text.Json silently skips deserialization; data loss |
| Preserve LF line endings | All files | `.gitattributes` enforces LF; CRLF in edits causes `format-gate` noise |
| Changed lines must pass format gate | All `.cs` files | CI `format-gate` fails |
| Layout CSS → `admin-common.css`, not `site.css` | New `.kb-status--local-newer` CSS rule | Confirmed: all `kb-status` classes live in `admin-common.css` (not `site-common.css`) |

---

## Metadata

**Analog search scope:** `DeckFlow.Web/`, `DeckFlow.Web.Tests/`, `DeckFlow.Studio/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Web/wwwroot/css/`
**Files read for excerpts:** 8 source files
**Pattern extraction date:** 2026-06-18
