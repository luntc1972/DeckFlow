# Phase 9: Bracket UX + AI Selector Foundation — Research

**Researched:** 2026-05-08
**Domain:** ASP.NET Core MVC Razor / CSS / C# model extension / zip round-trip
**Confidence:** HIGH

---

## Summary

Phase 9 is a focused UI + data-layer change across three existing pages. The codebase is
well-understood and all implementation sites are located. No new libraries, no DI changes,
no DB changes, no TypeScript — this is pure Razor/C#/CSS work.

**BRKT-01** requires wrapping the existing `TargetCommanderBracket` df-select in
`ChatGptPackets.cshtml` Step 2 with a `.bracket-callout` div. Zero logic change — visual
wrapper only. Design contract is fully locked in `09-UI-SPEC.md`.

**AISEL-01** requires a new shared partial `_AiSelector.cshtml` inserted at the top of
Step 2 on all three pages. The partial renders a segmented radio pill group bound to
`TargetAiPlatform`. No JS needed; native radio keyboard nav handles interaction. The partial
accepts a `string selectedValue` parameter.

**AISEL-04** requires `TargetAiPlatform` to round-trip through the packet zip
(`01-request-context.txt`). This requires four coordinated edits: (1) add the property to
`ChatGptDeckRequest`, (2) write it in `ChatGptDeckPacketService.BuildRequestContextText`,
(3) parse it in `ChatGptRequestContextParser` and `ParsedRequestContext`, (4) restore it in
`ChatGptPacketArtifactStore.LoadFromZip`. Comparison and CEDH pages do not have a
`01-request-context.txt` in their zip contracts — their `TargetAiPlatform` is UI-only in
Phase 9 (not persisted to zip).

**Primary recommendation:** Implement as three plans in dependency order — CSS + partials
first, then model field + writer/parser, then Razor view insertions. This minimises the blast
radius of each plan and allows `dotnet build` gating between waves.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Bracket callout visual treatment | Frontend Server (Razor) | — | Pure markup wrap of existing df-select; no logic |
| AI selector radio group | Frontend Server (Razor) | — | Standard form field; server-side `checked` binding; no JS |
| `TargetAiPlatform` model field | API / Backend (model) | — | Form-bound property on `ChatGptDeckRequest` |
| Write `target_ai_platform` to zip | API / Backend (service) | — | `ChatGptDeckPacketService.BuildRequestContextText` owns the writer |
| Parse `target_ai_platform` from zip | API / Backend (service) | — | `ChatGptRequestContextParser` + `ParsedRequestContext` own the parser |
| Restore `TargetAiPlatform` on upload | API / Backend (service) | — | `ChatGptPacketArtifactStore.LoadFromZip` reads parsed result |
| CSS for new components | Frontend Server (CSS) | — | All new selectors go in `site-common.css` only |

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| BRKT-01 | TargetCommanderBracket selector visually prominent with callout treatment | Exact insertion point confirmed: `ChatGptPackets.cshtml` line 200, wrapping the bare `<label class="field">` for `TargetCommanderBracket`. CSS spec locked in UI-SPEC. |
| AISEL-01 | All three analysis pages show AI target selector; ChatGPT default; top of analysis step | Step 2 panel openings confirmed in all three files. Shared partial pattern matches existing `_BusyIndicator`, `_DeckToolTabs`, etc. in `Views/Shared/`. |
| AISEL-04 | Selected AI target stored in zip and restored on resume | Round-trip chain mapped: `ChatGptDeckRequest.TargetAiPlatform` → `BuildRequestContextText` writes `target_ai_platform:` → parser reads it → `LoadFromZip` restores it. Comparison and CEDH zips have no `01-request-context.txt` — only Packets page zip supports full round-trip in Phase 9. |
</phase_requirements>

---

## Standard Stack

### No new libraries required

All work uses existing project primitives. No npm install, no NuGet changes.

| Primitive | Version | Purpose |
|-----------|---------|---------|
| ASP.NET Core MVC Razor partials | .NET 10 | `_AiSelector.cshtml`, `_BracketCallout.cshtml` shared partials |
| C# `sealed class` with null-guard setters | .NET 10 | `TargetAiPlatform` property on `ChatGptDeckRequest` |
| `site-common.css` | project | New `.bracket-callout` and `.ai-selector` selectors |
| `ChatGptRequestContextParser` (existing) | — | Extend for `target_ai_platform` scalar key |
| `ParsedRequestContext` (existing record) | — | Add `TargetAiPlatform` property |
| `ChatGptPacketArtifactStore.LoadFromZip` (existing) | — | Restore `TargetAiPlatform` from parsed result |
| `ChatGptDeckPacketService.BuildRequestContextText` (internal) | — | Emit `target_ai_platform: {value}` line |

---

## Architecture Patterns

### Existing Partial Pattern
All three ChatGPT pages already use `@await Html.PartialAsync("_BusyIndicator")` and
`@await Html.PartialAsync("_DeckToolTabs", Model.ActiveTab)` at the top of the form.
`_AiSelector.cshtml` follows the same call pattern with a model parameter.

Razor partial with typed model parameter — existing project usage:
```csharp
// Caller (view):
@await Html.PartialAsync("_AiSelector", Model.Request.TargetAiPlatform)

// Partial declaration:
@model string
```
[VERIFIED: existing `_MoxfieldBulkEditHint.cshtml` uses `@model string` and is called with
`@await Html.PartialAsync("_MoxfieldBulkEditHint", "Run Analysis")`]

### Round-Trip Write Pattern (01-request-context.txt)
`BuildRequestContextText` emits scalar fields as `key: value` lines. New field follows
exactly this pattern:

```csharp
// In ChatGptDeckPacketService.BuildRequestContextText — after existing scalars:
builder.AppendLine($"target_ai_platform: {NormalizeSingleLine(request.TargetAiPlatform, "ChatGPT")}");
```
[VERIFIED: read `ChatGptDeckPacketService.cs` lines 1651–1683]

### Round-Trip Parse Pattern
`ChatGptRequestContextParser` handles scalar keys in the `switch (key)` block at lines 68–92.
Adding `target_ai_platform` is a one-line case addition:

```csharp
case "target_ai_platform":
    targetAiPlatform = inlineValue.Trim();
    break;
```

Then add `string? targetAiPlatform = null;` to the variable declarations and
`TargetAiPlatform = string.IsNullOrEmpty(targetAiPlatform) ? null : targetAiPlatform` to the
`ParsedRequestContext` constructor.
[VERIFIED: read `ChatGptRequestContextParser.cs` full file]

### Restore Pattern in LoadFromZip
After the existing `if (parsed.DeckSource is not null)` block (line 218):

```csharp
if (parsed.TargetAiPlatform is not null)
{
    request.TargetAiPlatform = parsed.TargetAiPlatform;
}
```
Legacy zips without `target_ai_platform` silently default to `"ChatGPT"` because
`ChatGptDeckRequest.TargetAiPlatform` initialises to `"ChatGPT"`.
[VERIFIED: read `ChatGptPacketArtifactStore.cs` lines 159–221]

### Bracket Callout Wrap Pattern
Current Step 2 HTML at `ChatGptPackets.cshtml` lines 200–210:

```html
<label class="field">
  <span>Target Commander bracket <small>(required)</small></span>
  <select name="TargetCommanderBracket" required aria-required="true" data-df-select>
    ...
  </select>
  <small class="sync-column__hint">...</small>
</label>
```

Replace with:
```html
<div class="bracket-callout">
  <p class="bracket-callout__label">Required before generating</p>
  <label class="field">
    <span>Target Commander bracket <small>(required)</small></span>
    <select name="TargetCommanderBracket" required aria-required="true" data-df-select>
      ...
    </select>
    <small class="sync-column__hint">...</small>
  </label>
</div>
```
[VERIFIED: read `ChatGptPackets.cshtml` lines 200–210]

### AI Selector Insertion Points
All three pages: insert `@await Html.PartialAsync("_AiSelector", Model.Request.TargetAiPlatform)`
as the **first child inside the Step 2 `<section>` body**, after the `<div class="chatgpt-step-heading">` block.

- **Packets** (`ChatGptPackets.cshtml`): after line 187 (closing `</div>` of step heading),
  before the `<div class="chatgpt-instructions">` at line 169.
  Actually: after the step-heading div (closes ~line 167), before `<div class="chatgpt-instructions">`.
- **Comparison** (`ChatGptDeckComparison.cshtml`): Step 2 panel starts line 309. Insert after
  the step-heading div closes (around line 317), before `<div class="chatgpt-instructions">`.
- **CEDH Meta Gap** (`ChatGptCedhMetaGap.cshtml`): Step 2 panel starts line 172. Insert after
  the step-heading div closes (around line 180), before the conditional input summary section.

### Default Value for TargetAiPlatform
`ChatGptDeckRequest.TargetAiPlatform` must initialise to `"ChatGPT"` so the radio group is
pre-selected on first page load and on legacy zip resume (no `target_ai_platform` key present).

Pattern from existing fields with defaults:
```csharp
private string _targetAiPlatform = "ChatGPT";

public string TargetAiPlatform
{
    get => _targetAiPlatform;
    set => _targetAiPlatform = value ?? "ChatGPT";
}
```
[VERIFIED: read `ChatGptDeckRequest.cs` — all string properties follow this null-guard pattern]

### Comparison and CEDH Requests — UI-Only Field
`ChatGptDeckComparisonRequest` and `ChatGptCedhMetaGapRequest` do NOT have a
`01-request-context.txt` in their zip contracts. The AI selector appears on those pages but
`TargetAiPlatform` is NOT persisted to their zips in Phase 9. Two options:

1. Add `TargetAiPlatform` to those request models (UI field only, no round-trip yet)
2. Use a Razor-level local variable for display only

Option 1 is cleaner — the field exists on the model, the Razor partial reads it via
`Model.Request.TargetAiPlatform`. The zip round-trip for those two pages is Phase 10 work.

### `.sr-only` Already Exists
The AI selector HTML uses `class="sr-only ai-selector__option"` on the hidden radio inputs.
`.sr-only` is already defined at `site.css` line 76.
[VERIFIED: grep confirmed `.sr-only` in `site.css:76`]

### No New CSS Tokens
`site-common.css` currently ends at line 1164 with `.chatgpt-sticky-download__button`. New
`.bracket-callout` and `.ai-selector` blocks append to this file. Zero edits to `site.css`
or any `site-*.css` theme file.
[VERIFIED: read `site-common.css` full file — no bracket or ai-selector selectors present]

### Anti-Patterns to Avoid
- **Editing `site.css` or any guild theme file** for new selectors — all Phase 9 CSS goes in
  `site-common.css` only (CLAUDE.md constraint).
- **Using df-select for the AI selector** — UI-SPEC explicitly calls for a segmented radio
  pill group (`<input type="radio">`), not a `<select data-df-select>`.
- **Adding `_AiSelector.cshtml` to a controller-specific folder** — it must be in
  `Views/Shared/` so all three pages can resolve it.
- **Adding `TargetAiPlatform` round-trip to Comparison/CEDH zips in Phase 9** — their
  `LoadComparisonFromZip` and `LoadCedhMetaGapFromZip` methods have no `01-request-context.txt`
  contract. Leave that for Phase 10.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| Visually hidden radio inputs | custom CSS from scratch | existing `.sr-only` in `site.css` |
| Callout left-border visual | custom component | CSS-only `.bracket-callout` per UI-SPEC |
| AI selector keyboard nav | custom JS key handler | native `<input type="radio">` arrow-key behavior |
| Zip round-trip for `TargetAiPlatform` | new serialization mechanism | existing `key: value` scalar pattern in `BuildRequestContextText` / `ChatGptRequestContextParser` |

---

## Common Pitfalls

### Pitfall 1: Bracket Callout in Wrong Step
**What goes wrong:** Wrapping the df-select in Step 1 instead of Step 2.
**Why it happens:** The word "bracket" appears in both step headings' surrounding context.
**How to avoid:** Target the `<label class="field">` whose `<select>` has `name="TargetCommanderBracket"` — this is definitively in the Step 2 section panel (`data-chatgpt-step="2"`), lines 159–457 of `ChatGptPackets.cshtml`.
**Warning signs:** Build succeeds but bracket selector is not visible in Step 2.

### Pitfall 2: Partial Model Parameter Mismatch
**What goes wrong:** `_AiSelector.cshtml` declares `@model string` but caller passes a complex type (or vice versa).
**Why it happens:** Razor partial model binding is implicit; type mismatch causes runtime exception.
**How to avoid:** Declare `@model string` at top of partial; call with `Model.Request.TargetAiPlatform` (a `string`). Confirm all three callers pass the same type.

### Pitfall 3: Radio `checked` Attribute Razor Binding
**What goes wrong:** All three radios render unchecked, or all render checked.
**Why it happens:** Razor `checked` attribute requires a boolean expression, not a string comparison shorthand.
**How to avoid:**
```html
<input type="radio" ... checked="@(Model == "ChatGPT")" />
```
where `@model string` means `Model` is the `string selectedValue` passed from the parent.

### Pitfall 4: `TargetAiPlatform` Not Bound on POST
**What goes wrong:** Form POST doesn't populate `TargetAiPlatform` on the request model.
**Why it happens:** Property missing from `ChatGptDeckRequest`, or form field name mismatch (`name="TargetAiPlatform"` in HTML but property not present).
**How to avoid:** Add property to model before wiring up the view. The radio `name` attribute must exactly match the C# property name (`TargetAiPlatform`).

### Pitfall 5: Writing to Wrong Zip Paths for Comparison/CEDH
**What goes wrong:** Attempting to extend `BuildComparisonZip` or `BuildCedhMetaGapZip` to write `target_ai_platform`.
**Why it happens:** Phase description says "all three pages" for AISEL-04, but only Packets has `01-request-context.txt`.
**How to avoid:** AISEL-04 scope is Packets page only for the zip round-trip. Comparison and CEDH get the UI field but not zip persistence — confirmed by reading `ChatGptPacketArtifactStore`: `BuildComparisonZip` has no `01-request-context.txt` entry, `BuildCedhMetaGapZip` has no `01-request-context.txt` entry.

### Pitfall 6: `ParsedRequestContext` Is a `sealed record` — Use `init` Not a Setter
**What goes wrong:** Trying to add `TargetAiPlatform` as a mutable `set` property.
**Why it happens:** `ParsedRequestContext` uses `init` properties throughout.
**How to avoid:**
```csharp
public string? TargetAiPlatform { get; init; }
```
[VERIFIED: read `ChatGptRequestContextParser.cs` lines 243–272]

---

## Code Examples

### 1. `ChatGptDeckRequest` — New Property
```csharp
// Source: pattern from existing TargetCommanderBracket property (verified)
private string _targetAiPlatform = "ChatGPT";

/// <summary>
/// The AI platform the user intends to paste the generated artifact into.
/// Defaults to "ChatGPT". Accepted values: "ChatGPT", "Claude", "Gemini".
/// </summary>
public string TargetAiPlatform
{
    get => _targetAiPlatform;
    set => _targetAiPlatform = value ?? "ChatGPT";
}
```

### 2. `ParsedRequestContext` — New Property
```csharp
// Source: pattern from existing properties in ParsedRequestContext (verified)
public string? TargetAiPlatform { get; init; }
```

### 3. `ChatGptRequestContextParser.Parse` — New Case
```csharp
// In the switch(key) block alongside "target_commander_bracket":
case "target_ai_platform":
    targetAiPlatform = inlineValue.Trim();
    break;
```

And in the return statement:
```csharp
TargetAiPlatform = string.IsNullOrEmpty(targetAiPlatform) ? null : targetAiPlatform,
```

### 4. `BuildRequestContextText` — New Emit Line
```csharp
// After the target_commander_bracket line (verified location: line 1658)
builder.AppendLine($"target_ai_platform: {NormalizeSingleLine(request.TargetAiPlatform, "ChatGPT")}");
```

### 5. `ChatGptPacketArtifactStore.LoadFromZip` — Restore
```csharp
// After the existing parsed.DeckSource block (verified location: lines 217-221)
if (parsed.TargetAiPlatform is not null)
{
    request.TargetAiPlatform = parsed.TargetAiPlatform;
}
```

### 6. `_AiSelector.cshtml` Partial
```html
@model string
<div class="ai-selector">
  <p class="ai-selector__heading">Analyze with</p>
  <div class="ai-selector__options" role="radiogroup" aria-label="AI analysis target">
    <input type="radio" name="TargetAiPlatform" id="ai-chatgpt" value="ChatGPT"
           class="sr-only ai-selector__option" checked="@(Model == "ChatGPT")" />
    <label for="ai-chatgpt" class="ai-selector__option-label">ChatGPT</label>

    <input type="radio" name="TargetAiPlatform" id="ai-claude" value="Claude"
           class="sr-only ai-selector__option" checked="@(Model == "Claude")" />
    <label for="ai-claude" class="ai-selector__option-label">Claude</label>

    <input type="radio" name="TargetAiPlatform" id="ai-gemini" value="Gemini"
           class="sr-only ai-selector__option" checked="@(Model == "Gemini")" />
    <label for="ai-gemini" class="ai-selector__option-label">Gemini</label>
  </div>
  <p class="ai-selector__hint">ChatGPT format used for all targets in this phase. Claude and Gemini optimizations arrive in Phase 10.</p>
</div>
```

### 7. `_BracketCallout.cshtml` Partial
```html
@* No model — wraps only the bracket df-select; consumed from ChatGptPackets.cshtml only *@
<div class="bracket-callout">
  <p class="bracket-callout__label">Required before generating</p>
  @* Existing <label class="field"> for TargetCommanderBracket goes here *@
</div>
```
Note: the bracket callout wraps the existing Razor inline markup from `ChatGptPackets.cshtml`.
The UI-SPEC lists it as a shared partial but since the bracket field only exists on the Packets
page, the bracket callout can either be a partial or remain inline in the view — both work.
The UI-SPEC names it `_BracketCallout.cshtml`; follow that contract.

---

## File Change Map

| File | Change Type | Description |
|------|-------------|-------------|
| `DeckFlow.Web/Models/ChatGptDeckRequest.cs` | Add property | `TargetAiPlatform` string, default `"ChatGPT"` |
| `DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs` | Add property | `TargetAiPlatform` string, default `"ChatGPT"` (UI-only, no zip) |
| `DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs` | Add property | `TargetAiPlatform` string, default `"ChatGPT"` (UI-only, no zip) |
| `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` | Edit method | `BuildRequestContextText`: emit `target_ai_platform:` line |
| `DeckFlow.Web/Services/ChatGptRequestContextParser.cs` | Edit parser | Add `target_ai_platform` case in switch; add variable + return field |
| `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` | Edit LoadFromZip | Restore `TargetAiPlatform` from parsed context |
| `DeckFlow.Web/Views/Shared/_AiSelector.cshtml` | Create | Radio pill group partial, `@model string` |
| `DeckFlow.Web/Views/Shared/_BracketCallout.cshtml` | Create | Callout wrapper partial (or inline in Packets view) |
| `DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml` | Edit | Wrap bracket field in callout; insert `_AiSelector` at top of Step 2 |
| `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml` | Edit | Insert `_AiSelector` at top of Step 2 |
| `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml` | Edit | Insert `_AiSelector` at top of Step 2 |
| `DeckFlow.Web/wwwroot/css/site-common.css` | Append | `.bracket-callout`, `.bracket-callout__label`, `.ai-selector` family |

**Files NOT touched:**
- `site.css` — no new tokens
- Any `site-*.css` guild theme file — zero edits
- `ChatGptPacketArtifactStore.BuildComparisonZip` — no `01-request-context.txt` in comparison zip
- `ChatGptPacketArtifactStore.BuildCedhMetaGapZip` — no `01-request-context.txt` in CEDH zip
- `ChatGptPacketArtifactStore.LoadComparisonFromZip` — no round-trip in Phase 9
- `ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip` — no round-trip in Phase 9

---

## Existing Test Coverage

One test exists for the Packets zip round-trip:
`DeckFlow.Web.Tests/ChatGptPacketArtifactStoreRoundTripTests.cs`
— `LoadFromZip_AlsoRestoresUserInputs_FromArnaFixture`

This test reads fixture files from `/tmp/arna-test/01-request-context.txt`. After adding
`target_ai_platform` to the writer, the fixture file at `/tmp/arna-test/01-request-context.txt`
will NOT contain the new field (it was generated before this phase). The test will still pass
because `TargetAiPlatform` defaults to `"ChatGPT"` when the key is absent — the parser
returns `null` and `LoadFromZip` leaves the default in place. No test breakage expected.

The test does NOT assert `TargetAiPlatform` — a new assertion can be added to cover the
round-trip, but is not required for Phase 9 correctness (the test was written specifically
for the Arna fixture, not the new field).

**nyquist_validation is explicitly `false` in `.planning/config.json`** — Validation
Architecture section omitted per config.

---

## Environment Availability

Step 2.6: SKIPPED — Phase 9 is code/config changes only. No external CLI tools, databases,
or services beyond what already runs the project.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `_BracketCallout.cshtml` wraps existing Razor inline markup from the parent view (not a fully self-contained partial with its own df-select logic) | File Change Map | If callout must be a fully self-contained partial replicating the df-select, it would need the bracket catalog injected — add `@inject` or pass a model. Low risk: the UI-SPEC HTML shows it wrapping the existing control, not replacing it. |
| A2 | `ChatGptDeckComparisonRequest` and `ChatGptCedhMetaGapRequest` need `TargetAiPlatform` added as model properties (for POST binding), not just as Razor-local variables | File Change Map | If the form POST for those pages doesn't need to persist the value server-side at all in Phase 9, a local variable suffices. But for consistency with Phase 10 (where the value drives artifact format), adding the model property now is cleaner. |

---

## Open Questions

1. **`_BracketCallout.cshtml` — fully self-contained partial or thin wrapper?**
   - What we know: UI-SPEC names it as a shared partial; the bracket df-select exists only on the Packets page; the partial must wrap the existing `<label class="field">` block.
   - What's unclear: whether Codex should move the bracket field Razor code into the partial (cleaner encapsulation) or leave it inline and just wrap with the callout div.
   - Recommendation: Leave the df-select markup inline in `ChatGptPackets.cshtml` and make `_BracketCallout.cshtml` a minimal wrapper that contributes only the outer `.bracket-callout` div and eyebrow `<p>`. Slots the existing markup inside via the calling view. Simplest change, least risk.

2. **`TargetAiPlatform` for Comparison/CEDH — POST binding or view-local?**
   - What we know: The selector appears on all three pages per AISEL-01; the field must POST with the form; Phase 9 doesn't persist to those two zips.
   - Recommendation: Add the property to both request models (no zip wiring). This costs two trivial property additions and leaves the architecture clean for Phase 10.

---

## Project Constraints (from CLAUDE.md)

- All new CSS selectors go in `site-common.css`. Zero edits to `site.css` or any `site-*.css` guild theme file. [ENFORCED]
- No new `:root` token additions in `site.css` or any `site-*.css`. [ENFORCED — all Phase 9 selectors use existing tokens only]
- ASP.NET 10 + Razor — no framework migration. [Not applicable — no new frameworks]
- No `Co-Authored-By` trailer in commits. [Reminder for execution]
- Testing: rely on `dotnet build` clean. VSTest unreliable in WSL. [Validation Architecture omitted — nyquist_validation: false]
- Codex MCP for all code edits. [Reminder for execution]
- Multi-file work spanning ≥2 files → use `gpt-5.4` full model for Codex. [This phase touches 11 files — use full model]

---

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml` — full file read, Step 2 structure verified
- `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml` — Step 2 panel location verified (line 309)
- `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml` — Step 2 panel location verified (line 172)
- `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` — full file read, all three LoadFromZip methods verified
- `DeckFlow.Web/Services/ChatGptRequestContextParser.cs` — full file read, parse pattern verified
- `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` — `BuildRequestContextText` at lines 1651–1683 verified
- `DeckFlow.Web/Models/ChatGptDeckRequest.cs` — full file read, all existing properties confirmed
- `DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs` — full file read
- `DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs` — full file read
- `DeckFlow.Web/wwwroot/css/site-common.css` — full file read, no bracket/ai-selector selectors present
- `DeckFlow.Web/wwwroot/css/site.css` — `.sr-only` confirmed at line 76
- `.planning/phases/09-bracket-ux-ai-selector-foundation/09-UI-SPEC.md` — full design contract read
- `.planning/config.json` — `nyquist_validation: false` confirmed

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new libraries; all work uses existing verified patterns
- Architecture: HIGH — all insertion points located in source, all method signatures read
- Pitfalls: HIGH — all identified from direct code inspection, not inference

**Research date:** 2026-05-08
**Valid until:** 2026-06-08 (stable; no external dependencies)
