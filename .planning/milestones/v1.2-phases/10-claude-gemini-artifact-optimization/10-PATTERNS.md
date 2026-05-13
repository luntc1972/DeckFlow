# Phase 10: Claude + Gemini Artifact Optimization - Pattern Map

**Mapped:** 2026-05-09
**Files analyzed:** 8 modified, 0 new (all dispatch + extension work fits in existing files)
**Analogs found:** 8 / 8 (100% — every modification site has an in-repo analog)

> Phase 10 is dominated by **modifications to existing files**. There are zero
> required new C# classes — every per-AI prompt variant lives as private static
> helpers next to the existing `Build*Prompt` method, every zip-extension lives
> next to the existing `Build*Zip` method, and the response shim lives at the
> top of the existing `ExtractJsonPayload` body. The strongest analog material
> for the planner is therefore in-place: the Phase 9 Packets round-trip
> implementation, the existing prompt-builder shapes, and the existing
> `ExtractJsonPayload` flow.

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` (M) | service / prompt builder | request → string transform | self — `BuildAnalysisPrompt:839`, `BuildSetUpgradePrompt:1051`, `BuildRequestContextText:1655` | exact (in-place edit + new private helpers) |
| `DeckFlow.Web/Services/ChatGptDeckComparisonService.cs` (M) | service / prompt builder | request → string transform | `ChatGptDeckPacketService.BuildAnalysisPrompt` (sibling builder, same shape) | exact role + flow |
| `DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs` (M) | service / prompt builder | request → string transform | `ChatGptDeckPacketService.BuildAnalysisPrompt` (sibling builder, same shape) | exact role + flow |
| `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` (M) | service / artifact store | zip read/write (file I/O over MemoryStream) | self — `BuildZip:52` + `LoadFromZip:142` are the canonical Packets pattern; Comparison/CedhMetaGap mirror it | exact (extend in-place) |
| `DeckFlow.Web/Services/ChatGptRequestContextParser.cs` (M, optional) | service / parser | text → record | self — existing `Parse:29` + `ParsedRequestContext:248` | exact (extend `ParsedRequestContext` with new fields if planner picks full-form-state symmetry per Open Question 1) |
| `DeckFlow.Web/Services/ChatGptJsonTextFormatterService.cs` (M) | service / utility | text → text shim | self — existing `ExtractJsonPayload:7` | exact (insert `<result>` regex pre-step at top of existing method) |
| `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (M) | utility / DOM event handler | event-driven (click, change) | self — `registerChatGptDownloadDebounce:754`, `wireChatGptZipUpload:2386` | exact (D-14/D-15 in-place fixes) |
| `DeckFlow.Web.Tests/...` (additive only, optional) | test | xUnit fixture | `ChatGptPacketArtifactStoreRoundTripTests`, `ChatGptResponseParsersTests` | role-match (additive `<result>` fixture + Comparison/CedhMetaGap zip-round-trip case) |

> **No new classes.** Per RESEARCH.md `Don't Hand-Roll` and CONTEXT.md D-13
> "planner discretion within the constraint", the recommended decomposition is
> **switch expression at the top of each `Build*Prompt`** dispatching to private
> static `*Claude` / `*Gemini` / `*ChatGpt` helpers in the same file. No
> `IPromptBuilder` interface, no separate strategy classes.

---

## Pattern Assignments

### `ChatGptDeckPacketService.cs` — `BuildAnalysisPrompt` per-AI fork (modified)

**Role:** service / prompt builder
**Data flow:** `ChatGptDeckRequest` + reference data → `string` (the prompt artifact)
**Analog:** Self — existing `BuildAnalysisPrompt` body (`ChatGptDeckPacketService.cs:839-1049`)

**Existing method signature** (line 839):

```csharp
private static string BuildAnalysisPrompt(
    ChatGptDeckRequest request,
    string decklistText,
    string referenceText,
    string deckProfileSchemaJson,
    string? commanderName,
    IReadOnlyList<string> selectedQuestionIds,
    IReadOnlyList<string> bannedCards,
    CommanderSpellbookResult? comboResult = null,
    bool includeCardVersions = false)
{
    // ... ~210 lines of StringBuilder composition, current ChatGPT-shaped output
}
```

**Pattern to copy: top-of-method dispatch** (insert at line 850, just after the
existing local-variable block, before any `builder.AppendLine` calls):

```csharp
// Phase 10: per-AI prompt dispatch. ChatGPT path is the existing body
// (renamed). Claude/Gemini are new private helpers. request.TargetAiPlatform
// is normalized by the model setter to "ChatGPT" for null/unrecognized values
// (see ChatGptDeckRequest.TargetAiPlatform setter), so the default arm is safe.
return request.TargetAiPlatform switch
{
    "Claude" => BuildAnalysisPromptClaude(request, decklistText, referenceText, deckProfileSchemaJson, commanderName, selectedQuestionIds, bannedCards, comboResult, includeCardVersions),
    "Gemini" => BuildAnalysisPromptGemini(request, decklistText, referenceText, deckProfileSchemaJson, commanderName, selectedQuestionIds, bannedCards, comboResult, includeCardVersions),
    _        => BuildAnalysisPromptChatGpt(request, decklistText, referenceText, deckProfileSchemaJson, commanderName, selectedQuestionIds, bannedCards, comboResult, includeCardVersions),
};
```

**Pattern to copy: rename existing body → ChatGPT helper** (lines 850-1049
become a new private static method, identical content, plus one appended
`<result>` instruction line at the end of the `## OUTPUT FORMAT` section).

**Existing `## OUTPUT FORMAT` insertion site** (line ~930, current code):

```csharp
builder.AppendLine("## OUTPUT FORMAT");
builder.AppendLine("Structure your response as follows:");
builder.AppendLine();
// ... existing A. Requested Question Answers, B. recommendation sections ...
```

**Per D-07 / D-08:** the only ChatGPT-path content change is one new line at
the end of `## OUTPUT FORMAT`:

```csharp
builder.AppendLine();
builder.AppendLine("After the fenced ```json code block, also wrap the JSON response in <result>...</result> tags so DeckFlow's parser can extract it uniformly across ChatGPT/Claude/Gemini. The existing fenced ```json block remains as a fallback — do not remove it.");
```

**Pattern to copy: Claude variant skeleton** (new private static helper,
inserted next to the renamed ChatGPT helper, ~150-200 lines):

```csharp
// Source: pattern from Anthropic prompt-engineering docs (CONTEXT.md D-01..D-04, RESEARCH.md Pattern 2)
// Reuses every upstream string input unchanged — only the structural skeleton differs.
private static string BuildAnalysisPromptClaude(
    ChatGptDeckRequest request,
    string decklistText,
    string referenceText,
    string deckProfileSchemaJson,
    string? commanderName,
    IReadOnlyList<string> selectedQuestionIds,
    IReadOnlyList<string> bannedCards,
    CommanderSpellbookResult? comboResult,
    bool includeCardVersions)
{
    var bracket = CommanderBracketCatalog.Find(request.TargetCommanderBracket);
    var selectedQuestions = AnalysisQuestionCatalog.ResolveTexts(
        selectedQuestionIds,
        request.CardSpecificQuestionCardNames,
        request.BudgetUpgradeAmount);
    var allRequestedQuestions = selectedQuestions.ToList();
    if (!string.IsNullOrWhiteSpace(request.FreeformQuestion))
    {
        allRequestedQuestions.Add(request.FreeformQuestion.Trim());
    }

    var builder = new StringBuilder();

    // <role>
    builder.AppendLine("<role>");
    builder.AppendLine("You are an expert Magic: The Gathering deck analyst specializing in Commander.");
    builder.AppendLine("</role>");
    builder.AppendLine();

    // <commander> / <bracket>
    if (!string.IsNullOrWhiteSpace(commanderName))
    {
        builder.AppendLine($"<commander>{commanderName}</commander>");
        builder.AppendLine();
    }
    if (bracket is not null)
    {
        builder.AppendLine("<bracket>");
        builder.AppendLine($"target_bracket: {bracket.Label}");
        builder.AppendLine($"summary: {bracket.Summary}");
        builder.AppendLine($"turns_expectation: {bracket.TurnsExpectation}");
        builder.AppendLine("</bracket>");
        builder.AppendLine();
    }

    // <deck>
    builder.AppendLine("<deck>");
    builder.AppendLine(decklistText);
    builder.AppendLine("</deck>");
    builder.AppendLine();

    // <reference><cards/><combos/><banlist/></reference>
    builder.AppendLine("<reference>");
    builder.AppendLine("  <cards>");
    builder.AppendLine(referenceText);
    builder.AppendLine("  </cards>");
    if (comboResult is not null)
    {
        builder.AppendLine("  <combos>");
        // reuse existing combo text composition helper
        builder.AppendLine("  </combos>");
    }
    builder.AppendLine("  <banlist>");
    builder.AppendLine(FormatBannedCardsLine(bannedCards));
    builder.AppendLine("  </banlist>");
    builder.AppendLine("</reference>");
    builder.AppendLine();

    // <questions>
    builder.AppendLine("<questions>");
    for (var i = 0; i < allRequestedQuestions.Count; i++)
    {
        builder.AppendLine($"{i + 1}. {allRequestedQuestions[i]}");
    }
    builder.AppendLine("</questions>");
    builder.AppendLine();

    // <output_schema>
    builder.AppendLine("<output_schema>");
    builder.AppendLine(deckProfileSchemaJson);
    builder.AppendLine("</output_schema>");
    builder.AppendLine();

    // <task> — instructions LAST per D-03
    builder.AppendLine("<task>");
    builder.AppendLine("- Read every section above before responding.");
    builder.AppendLine("- Cite specific cards from <reference> / <deck> when making claims.");
    builder.AppendLine("- Answer every numbered question in <questions> with 6-12 sentences.");
    builder.AppendLine("- Wrap your final structured output in <result>...</result> tags.");
    builder.AppendLine("  Inside <result>, return a single JSON object matching <output_schema>.");
    builder.AppendLine("  No prose inside <result>; no JSON outside <result>.");
    builder.AppendLine("</task>");

    return builder.ToString().TrimEnd();
}
```

**Pattern to copy: Gemini variant skeleton** (markdown skeleton + persona +
step-by-step + schema-strictness; structurally derived from the renamed
`BuildAnalysisPromptChatGpt` body, NOT from scratch — per D-05, the
differentiation is in the instruction layer, not the structure):

```csharp
// Source: derivation of BuildAnalysisPromptChatGpt with persona at top,
// step-by-step scaffold, firmer schema-strictness, <result> wrapper.
// Pattern from Google AI prompting strategies (CONTEXT.md D-05, RESEARCH.md Pattern 3).
private static string BuildAnalysisPromptGemini( /* same parameters */ )
{
    var builder = new StringBuilder();
    // 1. persona at very top
    builder.AppendLine("You are an expert Magic: The Gathering analyst with deep cEDH metagame knowledge.");
    builder.AppendLine("You analyze Commander decks rigorously and base every conclusion on observable card text and deck composition.");
    builder.AppendLine();

    // 2. step-by-step scaffold (Gemini benefits from this more than ChatGPT)
    builder.AppendLine("Think carefully through the problem before responding. Read every supplied section in full before forming any conclusion. When in doubt, prefer evidence-based caveats over confident speculation.");
    builder.AppendLine();

    // 3. existing markdown skeleton (reuse helpers; share with ChatGPT path
    //    if the planner extracts AppendDeckContextSection / AppendBracketGuidanceSection / etc.)

    // 4. firmer schema-strictness language in OUTPUT FORMAT section
    builder.AppendLine("## OUTPUT FORMAT");
    builder.AppendLine("Return ONLY a single JSON object inside the <result>...</result> wrapper.");
    builder.AppendLine("Do not include any prose, markdown headings, or commentary outside the <result> tags.");
    builder.AppendLine("The JSON must conform exactly to the schema below — no extra fields, no missing fields, no narrative wrappers.");

    // 5. <result> wrapper instruction (same as Claude and ChatGPT)

    return builder.ToString().TrimEnd();
}
```

**Test seam:** all three variants are `private static` and accept the same
parameters. The dispatch switch is the only new public-ish surface — testable
by pre-existing `dotnet build` clean + manual round-trip per CONTEXT.md and
CLAUDE.md (VSTest unreliable in WSL).

---

### `ChatGptDeckPacketService.cs` — `BuildSetUpgradePrompt` per-AI fork (modified)

**Role:** service / prompt builder (same role as `BuildAnalysisPrompt`)
**Data flow:** request → string
**Analog:** the `BuildAnalysisPrompt` dispatch pattern above. Apply the same
switch-expression dispatch at the top of `BuildSetUpgradePrompt:1051`. Same
three-helper rename (`*ChatGpt` / `*Claude` / `*Gemini`).

**Tag taxonomy for set-upgrade Claude variant** (per D-02 "adapt for the
comparison prompt"; same idea applies here):

`<role>`, `<commander>`, `<deck_profile>` (replaces `<deck>`),
`<set_packet>`, `<reference>`, `<output_schema>`, `<task>`.

---

### `ChatGptDeckComparisonService.cs` — `BuildComparisonPrompt` per-AI fork (modified)

**Role:** service / prompt builder
**Data flow:** comparison summary + decks → string
**Analog:** existing `BuildComparisonPrompt:609`

**Existing method signature** (line 609):

```csharp
private static string BuildComparisonPrompt(
    DeckComparisonDeckSummary deckA,
    DeckComparisonDeckSummary deckB,
    string deckAListText,
    string deckBListText,
    string deckAComboText,
    string deckBComboText,
    string comparisonContextText,
    string comparisonSchemaJson)
```

**Critical caller-site change:** the current call at line 151 in the same file
does NOT receive a `ChatGptDeckComparisonRequest` argument. To branch on
`request.TargetAiPlatform` per D-13, the caller (the `BuildAsync` orchestration
around line 151) needs to pass the request through, OR the planner can pass
`request.TargetAiPlatform` as a `string targetAiPlatform` parameter on the
builder. Either is fine; preferred is `string targetAiPlatform` to keep the
builder dependent only on what it needs (consistent with the existing
parameters that are already explicit data, not the request DTO).

**Tag taxonomy for Claude comparison variant** (per D-02): `<role>`,
`<deck_a>` (containing `<commander>`, `<bracket>`, `<list>`, `<combos>`),
`<deck_b>` (same), `<comparison_context>`, `<output_schema>`, `<task>`.

**Same dispatch and rename pattern as `BuildAnalysisPrompt`.**

---

### `ChatGptDeckComparisonService.cs` — `BuildFollowUpPrompt` per-AI fork (modified)

**Role:** service / prompt builder
**Data flow:** schema → string
**Analog:** existing `BuildFollowUpPrompt:710`

**Per Open Question 3 in RESEARCH.md, planner verifies and forks.**
Same switch dispatch at top, three rename helpers, three skeleton variants
(Claude XML, Gemini markdown+tweaks, ChatGPT unchanged + `<result>` line).

The follow-up prompt is shorter than the main comparison prompt but uses the
same JSON schema and same `<result>` requirement, so the per-AI structural
shape should match the main prompt for visual consistency.

---

### `ChatGptCedhMetaGapService.cs` — `BuildPrompt` per-AI fork (modified)

**Role:** service / prompt builder
**Data flow:** my-deck + reference decks + schema → string
**Analog:** existing `BuildPrompt:301` (note: named `BuildPrompt`, NOT
`BuildMetaGapPrompt`, per memory observation 6065).

**Existing method signature** (line 301):

```csharp
private static string BuildPrompt(
    string commanderName,
    IReadOnlyList<DeckEntry> myDeckEntries,
    CommanderSpellbookResult? myDeckCombos,
    IReadOnlyList<EdhTop16Entry> selectedEntries,
    IReadOnlyList<CommanderSpellbookResult?> referenceDeckCombos,
    IReadOnlyDictionary<string, string> oracleNameMap,
    string schemaJson)
```

**Critical caller-site change:** like the comparison builder, this method
takes no request DTO. Planner adds a `string targetAiPlatform` parameter and
threads `request.TargetAiPlatform` through from the call site at line 160.

**Tag taxonomy for Claude meta-gap variant** (per D-02 "adapt for meta-gap");
recommendation: `<role>`, `<my_deck>` (`<commander>`, `<list>`, `<combos>`),
`<reference_decks>` (containing repeated `<reference>` blocks for R1..R4),
`<output_schema>`, `<task>`.

---

### `ChatGptPacketArtifactStore.cs` — Comparison + CedhMetaGap zip round-trip (modified)

**Role:** service / artifact store
**Data flow:** request → zip bytes (build); zip stream → request (load)
**Analog:** the Packets `BuildZip:52` + `LoadFromZip:142` pattern in the same
file — this is the canonical analog the planner is mirroring.

#### Allow-list extension

**Pattern to copy** — current Comparison allow-list (lines 30-42):

```csharp
private static readonly HashSet<string> ComparisonAllowedNames = new(StringComparer.OrdinalIgnoreCase)
{
    "00-comparison-input-summary.txt",
    "10-deck-a-list.txt",
    "11-deck-b-list.txt",
    // ... existing entries ...
};
```

**Add** the new entry (matching Packets convention `01-request-context.txt`):

```csharp
private static readonly HashSet<string> ComparisonAllowedNames = new(StringComparer.OrdinalIgnoreCase)
{
    "00-comparison-input-summary.txt",
    "01-request-context.txt", // NEW Phase 10
    "10-deck-a-list.txt",
    // ... rest unchanged ...
};
```

**Same change** to `CedhAllowedNames:44`:

```csharp
private static readonly HashSet<string> CedhAllowedNames = new(StringComparer.OrdinalIgnoreCase)
{
    "00-input-summary.txt",
    "01-request-context.txt", // NEW Phase 10
    "30-meta-gap-prompt.txt",
    // ... rest unchanged ...
};
```

#### `BuildComparisonZip` extension

**Existing signature** (line 83) — extend with a new `requestContextText`
parameter:

```csharp
public static byte[] BuildComparisonZip(
    ChatGptDeckComparisonRequest request,
    string inputSummary,
    string deckAListText,
    string deckBListText,
    string deckAComboText,
    string deckBComboText,
    string comparisonContextText,
    string comparisonPromptText,
    string followUpPromptText,
    string comparisonSchemaJson,
    string? requestContextText)   // <-- NEW Phase 10
{
    // existing ArgumentNullException.ThrowIfNull(request) stays
    var sections = NormalizeSections(
    [
        ("00-comparison-input-summary.txt", "COMPARISON INPUT SUMMARY", inputSummary),
        ("01-request-context.txt", "REQUEST CONTEXT", requestContextText),    // <-- NEW
        ("10-deck-a-list.txt", "DECK A LIST", deckAListText),
        // ... rest unchanged ...
    ]);
    // ...
}
```

**Same shape** to `BuildCedhMetaGapZip:114`.

#### `BuildRequestContextText` per-page variants

**Existing analog** (`ChatGptDeckPacketService.cs:1655-1688`):

```csharp
internal static string BuildRequestContextText(ChatGptDeckRequest request, string? commanderName)
{
    var builder = new StringBuilder();
    builder.AppendLine($"workflow_step: {request.WorkflowStep}");
    builder.AppendLine($"format: {NormalizeSingleLine(request.Format, "Commander")}");
    builder.AppendLine($"deck_name: {NormalizeSingleLine(request.DeckName, string.Empty)}");
    builder.AppendLine($"commander: {NormalizeSingleLine(commanderName, string.Empty)}");
    builder.AppendLine($"target_commander_bracket: {NormalizeSingleLine(request.TargetCommanderBracket, string.Empty)}");
    builder.AppendLine($"target_ai_platform: {NormalizeSingleLine(request.TargetAiPlatform, "ChatGPT")}");
    // ... more fields ...
    return builder.ToString().TrimEnd() + Environment.NewLine;
}
```

**Comparison variant** (new method, planner picks location — recommended:
`internal static string BuildRequestContextText(...)` on
`ChatGptDeckComparisonService` to match Packets convention):

```csharp
internal static string BuildRequestContextText(ChatGptDeckComparisonRequest request)
{
    var builder = new StringBuilder();
    builder.AppendLine($"workflow_step: {request.WorkflowStep}");
    builder.AppendLine($"deck_a_name: {NormalizeSingleLine(request.DeckAName, string.Empty)}");
    builder.AppendLine($"deck_b_name: {NormalizeSingleLine(request.DeckBName, string.Empty)}");
    builder.AppendLine($"deck_a_bracket: {NormalizeSingleLine(request.DeckABracket, string.Empty)}");
    builder.AppendLine($"deck_b_bracket: {NormalizeSingleLine(request.DeckBBracket, string.Empty)}");
    builder.AppendLine($"target_ai_platform: {NormalizeSingleLine(request.TargetAiPlatform, "ChatGPT")}");
    return builder.ToString().TrimEnd() + Environment.NewLine;
}
```

> **Field carryover decision (Open Question 1):** RESEARCH.md recommends full
> form-state symmetry (deck names + brackets + AI platform for Comparison;
> commander + time_period + sort_by + min_event_size + max_standing for
> CedhMetaGap). Planner makes the final call; minimum-acceptable scope is
> just `target_ai_platform`.

#### `LoadComparisonFromZip` extension

**Existing analog** — Packets restoration block (`ChatGptPacketArtifactStore.cs:167-234`):

```csharp
if (!string.IsNullOrWhiteSpace(requestContextText))
{
    var parsed = ChatGptRequestContextParser.Parse(requestContextText);
    if (!string.IsNullOrEmpty(parsed.Format))      { request.Format = parsed.Format; }
    if (parsed.DeckName is not null)               { request.DeckName = parsed.DeckName; }
    // ... 13+ more "if-not-null-then-assign" branches ...
    if (parsed.TargetAiPlatform is not null)       { request.TargetAiPlatform = parsed.TargetAiPlatform; }
}
```

**Pattern to copy** — extend `LoadComparisonFromZip:244` to read and apply
`01-request-context.txt`:

```csharp
public static void LoadComparisonFromZip(Stream zipStream, ChatGptDeckComparisonRequest request)
{
    ArgumentNullException.ThrowIfNull(zipStream);
    ArgumentNullException.ThrowIfNull(request);

    var entries = ReadEntries(zipStream, ComparisonAllowedNames);
    if (!entries.TryGetValue("40-deck-comparison-response.json", out var responseJson)
        || string.IsNullOrWhiteSpace(responseJson))
    {
        throw new InvalidOperationException("Imported zip did not contain 40-deck-comparison-response.json.");
    }
    // ... existing response/deckA/deckB restoration ...

    // NEW Phase 10: apply request-context if present
    if (entries.TryGetValue("01-request-context.txt", out var requestContextText)
        && !string.IsNullOrWhiteSpace(requestContextText))
    {
        var parsed = ChatGptRequestContextParser.Parse(requestContextText);
        if (parsed.TargetAiPlatform is not null)
        {
            request.TargetAiPlatform = parsed.TargetAiPlatform;
        }
        // ... other fields if planner picks full form-state symmetry ...
    }
}
```

> **Per Pitfall 3 (RESEARCH.md):** Comparison and CedhMetaGap LOAD methods
> currently throw if the response file is absent. Phase 10 keeps that "response
> required" semantics — the AI selector restoration is a sub-feature of an
> existing zip that already has a response. Do NOT relax this to partial-zip
> upload like Packets did in commit f26e63d.

**Same shape** to `LoadCedhMetaGapFromZip:276`.

---

### `ChatGptRequestContextParser.cs` — optional new fields (modified, optional)

**Role:** service / parser
**Data flow:** text → record
**Analog:** self — existing parser at `ChatGptRequestContextParser.cs`

**Existing parser handles:** `format`, `deck_name`, `commander`,
`target_commander_bracket`, `target_ai_platform` (already wired,
line 82-84), `include_sideboard_in_analysis`, `include_maybeboard_in_analysis`,
`card_specific_question_card_names`, `budget_upgrade_amount`,
`selected_analysis_questions`, `selected_set_codes`, `strategy_notes`,
`meta_notes`, `deck_source`. Unknown keys are silently ignored.

**Decision point:** if the planner picks full form-state symmetry for
Comparison/CedhMetaGap zips, NEW keys (`deck_a_name`, `deck_a_bracket`,
`deck_b_name`, `deck_b_bracket` for Comparison; `time_period`, `sort_by`,
`min_event_size`, `max_standing` for CedhMetaGap) need switch cases added to
`Parse:67-94` and corresponding properties on
`ParsedRequestContext:248-283`.

**Pattern to copy** — switch case + record property addition (line 82-84
pattern):

```csharp
// in Parse():
case "deck_a_name":
    deckAName = inlineValue.Trim();
    break;
case "deck_a_bracket":
    deckABracket = inlineValue.Trim();
    break;
// ...

// in ParsedRequestContext record:
public string? DeckAName { get; init; }
public string? DeckABracket { get; init; }
// ...
```

If the planner picks minimum scope (`target_ai_platform` only), this file does
not need to change at all — existing code already handles
`target_ai_platform`.

---

### `ChatGptJsonTextFormatterService.cs` — `<result>` extraction shim (modified)

**Role:** service / utility
**Data flow:** text → text
**Analog:** self — existing `ExtractJsonPayload:7`

**Existing method body** (entire file at this point):

```csharp
internal static string ExtractJsonPayload(string input)
{
    var trimmed = input.Trim();
    if (string.IsNullOrWhiteSpace(trimmed))
    {
        return trimmed;
    }

    var payloadStart = FindPayloadStart(trimmed);
    if (payloadStart < 0)
    {
        return trimmed;
    }

    var payloadEnd = FindPayloadEnd(trimmed, payloadStart);
    if (payloadEnd < 0)
    {
        return trimmed[payloadStart..].Trim();
    }

    return trimmed[payloadStart..(payloadEnd + 1)].Trim();
}
```

**Pattern to insert** — `<result>` regex shim at top, fall-through to
existing `FindPayloadStart/End` logic on miss:

```csharp
using System.Text.RegularExpressions;   // <-- NEW using

namespace DeckFlow.Web.Services;

public static class ChatGptJsonTextFormatterService
{
    // Phase 10: unified <result>...</result> wrapper across ChatGPT/Claude/Gemini.
    // Lazy quantifier (.*?) ensures FIRST matching pair wins if user pasted text
    // contains stray <result> tokens. Singleline so dot matches newlines.
    private static readonly Regex ResultTagRegex = new(
        @"<result>\s*(.*?)\s*</result>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    internal static string ExtractJsonPayload(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        // NEW: try <result>...</result> first. Falls through to existing
        // brace-finding extraction on miss so legacy fenced JSON / raw JSON
        // keep working unchanged.
        var match = ResultTagRegex.Match(trimmed);
        if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
        {
            trimmed = match.Groups[1].Value.Trim();
        }

        // EXISTING below (unchanged):
        var payloadStart = FindPayloadStart(trimmed);
        if (payloadStart < 0) { return trimmed; }

        var payloadEnd = FindPayloadEnd(trimmed, payloadStart);
        if (payloadEnd < 0) { return trimmed[payloadStart..].Trim(); }

        return trimmed[payloadStart..(payloadEnd + 1)].Trim();
    }

    // FindPayloadStart / FindPayloadEnd unchanged
}
```

**Per RESEARCH.md Pattern 5 + Pitfall 1:** the regex is non-greedy and
`Singleline`. On miss, fall through silently — never throw. This single
insertion covers all three response paths: `ParseAnalysisResponse:24`,
`ParseSetUpgradeResponse:55`, `ParseComparisonResponse:795` (all call
`ChatGptJsonTextFormatterService.ExtractJsonPayload`). The CedhMetaGap parser
also funnels through this helper.

> **Anti-pattern (RESEARCH.md):** Do NOT edit `ChatGptPacketArtifactStore.cs`
> private `ExtractJsonObject:408`. That helper runs at zip-build time on
> already-pasted user JSON; its job is fence-stripping for storage, not
> response parsing. The `<result>` shim does NOT belong there.

---

### `deck-sync.ts` — D-14 download debounce (modified)

**Role:** utility / DOM event handler
**Data flow:** click event → debounced state machine
**Analog:** self — existing `registerChatGptDownloadDebounce:754`

**Existing function body** (lines 754-771):

```typescript
const registerChatGptDownloadDebounce = (): void => {
  document.querySelectorAll<HTMLButtonElement>('button[data-chatgpt-download-submit]').forEach(button => {
    button.addEventListener('click', () => {
      if (button.disabled) {
        return;
      }

      const originalText = button.textContent;
      button.disabled = true;
      button.textContent = 'Preparing download...';

      window.setTimeout(() => {
        button.disabled = false;
        button.textContent = originalText;
      }, 3000);
    });
  });
};
```

**Pattern to copy: option (a) named-constant minimum** (per RESEARCH.md
Pitfall 7 — the safe path that does NOT touch the busy-overlay code path):

```typescript
// Phase 10 (D-14): hardened download debounce.
// Render Starter cold-response can take ~2s; 3s gives 1s margin for re-enable.
// Re-enabling earlier risks duplicate POST on rapid double-click; re-enabling
// later annoys users on fast responses. If users still report missed clicks,
// raise this rather than coupling re-enable to navigation events — that path
// re-introduces the sticky-busy-overlay regression fixed in commit b09fd46
// (the data-no-busy attribute on the download button is what currently
// prevents that regression and MUST be preserved).
const CHATGPT_DOWNLOAD_DEBOUNCE_MS = 3000;

const registerChatGptDownloadDebounce = (): void => {
  document.querySelectorAll<HTMLButtonElement>('button[data-chatgpt-download-submit]').forEach(button => {
    button.addEventListener('click', () => {
      if (button.disabled) {
        return;
      }

      const originalText = button.textContent;
      button.disabled = true;
      button.textContent = 'Preparing download...';

      window.setTimeout(() => {
        button.disabled = false;
        button.textContent = originalText;
      }, CHATGPT_DOWNLOAD_DEBOUNCE_MS);
    });
  });
};
```

> **Constraint (RESEARCH.md Pitfall 7 + CONTEXT.md D-14):** the
> `data-no-busy` attribute on the submitter is what `registerBusyIndicator:723`
> checks at line 727 to skip showing the busy overlay. That's the b09fd46 fix.
> Do NOT remove `data-no-busy`. Option (b) ("re-enable on navigation /
> visibility-change") is acceptable ONLY if the planner can prove it does not
> couple back into the busy overlay path.

---

### `deck-sync.ts` — D-15 `skipPersistence` cleanup (modified)

**Role:** utility / DOM event handler
**Data flow:** change event → form dataset flag
**Analog:** self — existing `wireChatGptZipUpload:2386`

**Existing function body** (lines 2386-2409):

```typescript
const wireChatGptZipUpload = (): void => {
  document.querySelectorAll<HTMLInputElement>('[data-chatgpt-zip-upload]').forEach(input => {
    input.addEventListener('change', () => {
      const file = input.files?.[0];
      if (!file) { return; }

      // The file-picker change event bubbled to the form and already triggered persistFormState
      // with pre-upload (mostly empty) values. After the upload POST navigates back, the
      // upload-rendered server values would be overwritten by hydrateFormState reading that
      // stale state. Clear it here, and disable further persistence on this page until navigation.
      const form = input.closest<HTMLFormElement>('form[data-cache-key]');
      if (form) {
        clearPersistedFormState(form);
        form.dataset.skipPersistence = 'true';
      }

      const wrapper = input.closest('details');
      const submit = wrapper?.querySelector<HTMLButtonElement>('button[formaction$="/upload"]');
      submit?.click();
    });
  });
};
```

**Existing consumer** at `persistFormState:1083-1086` (the only reader of
`skipPersistence`):

```typescript
const persistFormState = (form: HTMLFormElement): void => {
  if (form.dataset.skipPersistence === 'true') {
    return;
  }
  // ... rest of body
};
```

**Pattern to copy: option (c) auto-clear timeout** (per RESEARCH.md Pitfall 8
recommendation — simplest correct fix):

```typescript
const wireChatGptZipUpload = (): void => {
  document.querySelectorAll<HTMLInputElement>('[data-chatgpt-zip-upload]').forEach(input => {
    input.addEventListener('change', () => {
      const file = input.files?.[0];
      if (!file) { return; }

      const form = input.closest<HTMLFormElement>('form[data-cache-key]');
      if (form) {
        clearPersistedFormState(form);
        form.dataset.skipPersistence = 'true';

        // Phase 10 (D-15): if the upload POST errors before navigation,
        // skipPersistence would otherwise stay true for the rest of the page
        // lifetime, silently disabling form-state persistence. Auto-clear
        // after 30s — by then the upload either navigated us away (this
        // handler is gone) or definitively failed (clear so subsequent user
        // input is persisted normally).
        window.setTimeout(() => {
          if (form.dataset.skipPersistence === 'true') {
            delete form.dataset.skipPersistence;
          }
        }, 30000);
      }

      const wrapper = input.closest('details');
      const submit = wrapper?.querySelector<HTMLButtonElement>('button[formaction$="/upload"]');
      submit?.click();
    });
  });
};
```

> **Other accepted options per CONTEXT.md D-15:** clear on a fetch-error path
> (more invasive — the current code uses `submit?.click()` not `fetch`, so
> there's no error path to subscribe to without rewriting the upload mechanism)
> OR replace flag with a narrower mechanism (the only consumer is one
> conditional in `persistFormState`, so a one-shot would work; but the timeout
> is the smallest-diff correct fix).

---

## Shared Patterns

### Switch-expression dispatch on `TargetAiPlatform`

**Source:** Phase 9 plumbing already wires `TargetAiPlatform` from form to
service via the request DTO. The model setter normalizes null/unknown to
`"ChatGPT"`, making the default arm always safe.

**Apply to:** all 5 prompt builders —
`ChatGptDeckPacketService.BuildAnalysisPrompt:839`,
`ChatGptDeckPacketService.BuildSetUpgradePrompt:1051`,
`ChatGptDeckComparisonService.BuildComparisonPrompt:609`,
`ChatGptDeckComparisonService.BuildFollowUpPrompt:710`,
`ChatGptCedhMetaGapService.BuildPrompt:301`.

```csharp
return targetAiPlatform switch
{
    "Claude" => /* Claude variant */,
    "Gemini" => /* Gemini variant */,
    _        => /* ChatGPT variant — existing body, plus appended <result> instruction */,
};
```

> **Constraint (CONTEXT.md D-13):** dispatch happens INSIDE the service.
> Controllers stay AI-agnostic. The orchestration callers in `BuildAsync`
> (Comparison line 151, CedhMetaGap line 160) need a small change to pass
> `request.TargetAiPlatform` down to the builder.

### Request-context round-trip envelope

**Source:** `ChatGptDeckPacketService.BuildRequestContextText:1655` writes,
`ChatGptRequestContextParser.Parse:29` reads,
`ChatGptPacketArtifactStore.LoadFromZip:142-234` applies parsed values back
to the request.

**Apply to:** Comparison and CedhMetaGap zips. New per-page
`BuildRequestContextText` writers; same `ChatGptRequestContextParser` reader
(it already handles `target_ai_platform` and ignores unknown keys);
extension of `LoadComparisonFromZip:244` and `LoadCedhMetaGapFromZip:276` to
apply parsed values.

### Append-only ChatGPT path: `<result>` wrap instruction

**Source:** Per D-07. ChatGPT prompt content is otherwise unchanged.

**Apply to:** the `## OUTPUT FORMAT` section of every ChatGPT-path prompt
variant in all 5 builders. One line, end of section, instructing the model to
wrap the JSON response in `<result>...</result>` tags AFTER the existing
fenced ```json block.

### Defense-in-depth response parsing

**Source:** `ChatGptJsonTextFormatterService.ExtractJsonPayload:7`.

**Apply to:** All response parse paths. The `<result>` regex shim is the
new first step; brace-finding is the existing second step; the parser falls
through silently on shim miss so old artifacts (no `<result>` wrap) keep
working.

### Field-by-field "if-not-null-then-assign" restoration

**Source:** `ChatGptPacketArtifactStore.LoadFromZip:170-233`.

**Pattern:**

```csharp
if (parsed.SomeField is not null)
{
    request.SomeField = parsed.SomeField;
}
```

This pattern preserves existing-request defaults for fields absent from the
zip (legacy zips, partial zips). Apply to `LoadComparisonFromZip` and
`LoadCedhMetaGapFromZip` extensions for `target_ai_platform` and any other
form-state fields the planner carries.

### `using static`-style helper reuse across per-AI variants

**Source:** Pitfall 6 in RESEARCH.md. The 200-line `BuildAnalysisPrompt` body
contains pure-data composition (decklist text formatting, bracket guidance
list, banned cards line, combo reference text) that should NOT be duplicated
across the three per-AI variants.

**Apply to:** Extract pure-data sections to shared `private static` helpers
(`AppendDeckContextSection`, `AppendBracketGuidanceSection`,
`AppendBannedCardsLine`, `BuildComboReferenceText` — the last already exists)
and reuse them across `*ChatGpt`, `*Claude`, `*Gemini` variants. Only the
structural skeleton and instruction layer differ between variants.

### Plain-text artifact files in the zip

**Source:** D-12 enforces `.txt` extensions even for XML-shaped Claude
content. `01-request-context.txt` continues to be plain-text key-value scalar
format (NOT YAML, NOT JSON).

**Apply to:** all new per-AI artifacts (`31-analysis-prompt.txt` keeps that
filename whether content is XML, markdown, or markdown+tweaks; same for
`30-comparison-prompt.txt` and the meta-gap prompt file).

---

## No Analog Found

None. Every Phase 10 modification has a strong existing in-repo analog.

The closest thing to "no analog" is the **per-AI structural divergence
itself** — the literal Claude XML skeleton and Gemini markdown+tweaks
content. Those have no in-repo predecessor, but the planner has explicit
verified vendor-doc patterns from RESEARCH.md (Anthropic XML-tagging guide,
Google AI prompting strategies) plus the CONTEXT.md `<specifics>` user
preference. Per RESEARCH.md Pattern 2 / Pattern 3, those external references
ARE the analog source for the new content.

---

## Metadata

**Analog search scope:**
- `DeckFlow.Web/Services/` (full file Read for builder analogs and zip store)
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (targeted Read for D-14 / D-15 sites)
- `DeckFlow.Web/Models/` (verified `TargetAiPlatform` already on all 3
  request DTOs — no model changes required per memory observation 6067)

**Files scanned:** 9 (8 modification targets + 1 model verification)

**Pattern extraction date:** 2026-05-09

**Key insight from analog scan:** The Phase 9 Packets round-trip
implementation IS the analog for Phase 10's Comparison and CedhMetaGap
extensions. RESEARCH.md correctly identified this as "Phase 10 starts where
Phase 9 leaves off" — every zip-extension change has a direct line-for-line
predecessor in the same file (`ChatGptPacketArtifactStore.cs`). The planner
should bias toward "extend existing pattern" over "introduce new abstraction"
for every decision.

**Decomposition note:** RESEARCH.md recommends a four-plan slice
(10-01 dispatch primitive + Packets-Claude derisking, 10-02 remaining
content fanout, 10-03 zip round-trip + parser shim, 10-04 TS polish). The
analog map above supports that decomposition: each plan touches only files
that share an analog cluster.
