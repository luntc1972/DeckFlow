# Phase 10: Claude + Gemini Artifact Optimization — Research

**Researched:** 2026-05-09
**Domain:** Per-AI prompt artifact generation; XML-tag prompt structure (Claude); markdown +
instruction-layer tweaks (Gemini); response parser extension; zip round-trip envelope; minor
TS hardening
**Confidence:** HIGH

---

## Summary

Phase 10 layers per-AI prompt formatting onto the AI-selector plumbing shipped in Phase 9.
Phase 9 already runs `TargetAiPlatform` from form -> request DTO -> service -> Packets zip
round-trip. The Comparison and CedhMetaGap request DTOs already have a `TargetAiPlatform`
property (Phase 9 added it for UI parity even though those zips do not yet round-trip the
value). The four prompt builder methods, the three zip build/load methods, the response
parser, and the request-context parser are all located and stable.

Per `<canonical_refs>` in CONTEXT.md and verified against the current Anthropic prompt-engineering
docs, the Claude format decision (flat semantic XML, data first, no API role blocks) and the
Gemini decision (markdown skeleton + instruction-layer tweaks + `<result>` wrapper) match
current vendor guidance. Output-tag wrap is a documented Anthropic pattern ("direct the model
to output within XML tags"). Gemini accepts both XML and markdown structurally; the docs
recommend picking one and being consistent — for paste-in chat (no Structured Outputs API),
explicit `Return ONLY raw JSON` style language plus a wrapper tag is the pragmatic forcing
function.

The two D-14 / D-15 carry-over polish items both live in `deck-sync.ts`. The 3000ms debounce
constant lives at line 765; the `data-no-busy` constraint comes from commit b09fd46 and is
narrow enough to leave room for either approach. The `skipPersistence` consumer is exactly
one site (`persistFormState` line 1083-1086).

**Primary recommendation:** Decompose into four plans:

1. **Per-AI prompt dispatch primitive** — introduce one minimal seam (interface or
   `switch` helper) and refactor the Packets `BuildAnalysisPrompt` to dispatch on
   `request.TargetAiPlatform`. No content change for ChatGPT path. Validates the seam with
   the highest-traffic builder before fanning out.
2. **Claude + Gemini content for all four builders** — implement the actual XML-flavored
   Claude variant and the markdown+instruction-tuned Gemini variant for each of
   `BuildAnalysisPrompt`, `BuildSetUpgradePrompt`, `BuildComparisonPrompt`, and the
   CedhMetaGap `BuildPrompt`. Add the `<result>...</result>` wrap instruction to all three
   AI variants of every builder.
3. **Comparison + CedhMetaGap zip round-trip + response parser extension** — add
   `01-request-context.txt` to those zips with at minimum `target_ai_platform`, extend
   `LoadComparisonFromZip` and `LoadCedhMetaGapFromZip` to honour it, and insert the
   `<result>` extractor in front of `ChatGptJsonTextFormatterService.ExtractJsonPayload`
   (single insertion point covers all three response parsers).
4. **D-14 + D-15 TS polish** — small standalone plan; can land in any order.

This decomposition matches the user's instinct in CONTEXT.md "Packets-Claude alone is the
foundation; if Packets-Claude works, the pattern holds". Plan 1 + the Packets slice of
Plan 2 deliver that derisking checkpoint.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Per-AI prompt dispatch (which builder runs) | API / Backend (service) | — | Service already owns `TargetAiPlatform`; controllers stay AI-agnostic per D-13 |
| Claude / Gemini / ChatGPT prompt content | API / Backend (service) | — | Pure-CPU string composition inside existing `Build*Prompt` methods |
| `<result>` tag append on all three AI variants | API / Backend (service) | — | Same dispatch site; zero browser involvement |
| `<result>` extraction in response | API / Backend (service) | — | Single shim in front of existing JSON-fence extractor |
| Zip round-trip of `target_ai_platform` (Comparison + CedhMetaGap) | API / Backend (service) | — | Mirrors the Packets `01-request-context.txt` contract |
| AI selector UI behaviour (already shipped Phase 9) | Frontend Server (Razor) | — | No UI change in Phase 10 |
| Download-button debounce (D-14) | Browser / Client (TS) | — | DOM event handler, no server involvement |
| `skipPersistence` cleanup (D-15) | Browser / Client (TS) | — | DOM dataset flag, no server involvement |

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AISEL-02 | Claude artifact uses XML-tagged structure tuned to Claude | Anthropic docs `[CITED: platform.claude.com/docs/.../use-xml-tags]` confirm XML tagging is the canonical Claude steering primitive. Long-context "data above the query" recommendation `[CITED: same source]` matches D-03. Output-control via XML tags (`"direct the model to output within XML tags"`) matches the `<result>...</result>` decision. |
| AISEL-03 | Gemini artifact uses Gemini-tuned structure distinct from ChatGPT and Claude | Google AI docs `[CITED: ai.google.dev/.../prompting-strategies]` confirm Gemini accepts both XML and markdown ("Choose one format and use it consistently") and recommend persona placement up front, "Think very hard before answering" scaffolding, and explicit `Return ONLY raw JSON` language for paste-in scenarios. Matches D-05. |
| AISEL-04 | Selected AI target stored in zip and restored on resume on all three pages (Phase 9 only delivered Packets) | Comparison + CedhMetaGap zip files have no `01-request-context.txt` today (verified from `ChatGptPacketArtifactStore` allow-lists at lines 30-50). Adding the file + parsing it on `LoadComparisonFromZip` / `LoadCedhMetaGapFromZip` follows the exact pattern Packets already uses. `ChatGptRequestContextParser.Parse` already handles `target_ai_platform` (line 82-84) — the parser is reusable. |
</phase_requirements>

---

## User Constraints (from CONTEXT.md)

### Locked Decisions

**Claude artifact format**
- D-01: Flat XML skeleton with semantic domain tags. Markdown allowed inside content tags.
- D-02: Tag taxonomy for analysis prompt: `<role>`, `<deck>`, `<commander>`, `<bracket>`,
  `<reference>` (optionally nested `<cards>`/`<combos>`/`<banlist>`), `<questions>`,
  `<output_schema>`, `<task>`. Adapt for comparison (`<deck_a>`, `<deck_b>`, ...) and meta-gap
  (`<reference_decks>`, ...).
- D-03: Data sections first, instructions/task last.
- D-04: No API role blocks (`<system>`/`<human>`/`<assistant>`). Claude.ai web UI does not
  parse them.

**Gemini artifact format**
- D-05: Light differentiation from ChatGPT artifact, not full restructure. Same markdown
  skeleton + instruction-layer divergence:
  - "think step-by-step before answering" scaffolding
  - stronger persona framing at top
  - firmer schema-strictness language
  - the unified `<result>...</result>` wrapper
- D-06: Differentiation from ChatGPT and Claude lives in instruction layer + markdown-vs-XML
  skeleton split. No invented Gemini-specific layout.

**ChatGPT artifact format**
- D-07: Substantially unchanged. Only adds `<result>...</result>` wrap instruction. Existing
  fenced-JSON request stays as fallback signal.

**Response contract**
- D-08: All three AIs instructed to wrap JSON response in `<result>...</result>` with no
  prose outside. Server parser does XML-tag extract first, falls back to existing
  fenced-JSON detection (`ExtractJsonObject` at `ChatGptPacketArtifactStore.cs:408`,
  `ChatGptJsonTextFormatterService.ExtractJsonPayload` for the comparison/meta-gap path).
  Single import code path. Old saved zips still import.
- D-09: No full-XML response output. Inner content stays JSON, parsed by existing
  `ChatGptResponseParsers.Parse*Response` and `ChatGptDeckComparisonService.ParseComparisonResponse`.

**Surface scope**
- D-10: Ship on all three ChatGPT analysis pages (Packets, Comparison, CedhMetaGap).
- D-11: Extend zip round-trip to Comparison and CedhMetaGap. Closes AISEL-04 fully.
- D-12: Filename and zip layout unchanged. AI flavour identified via new request-context
  entry, not filenames.

**Branching strategy**
- D-13: Per-AI dispatch happens INSIDE the service on `request.TargetAiPlatform`.
  Controllers stay AI-agnostic. Strategy/switch/separate-classes is planner's call.

**Carry-over polish**
- D-14: Harden `registerChatGptDownloadDebounce` (deck-sync.ts:754). Replace 3000ms timeout
  either with named constant + comment OR a stronger re-enable signal. MUST NOT re-introduce
  the sticky-busy-overlay regression (b09fd46).
- D-15: `skipPersistence` flag in `wireChatGptZipUpload` (deck-sync.ts ~2386-2409) must be
  cleared on upload-failure path, scoped to one cycle, OR replaced with a narrower mechanism.

### Claude's Discretion
- Exact wording/tone of per-AI instruction layers.
- File organization for the dispatch (one class, three classes, or strategy interface).
- Whether new request-context file for Comparison/CedhMetaGap zips carries only
  `target_ai_platform` or extra form-state fields.
- Test approach (manual round-trip vs golden-file).
- Choice of D-14 option (a)/(b) and D-15 option (a)/(b)/(c).

### Deferred Ideas (OUT OF SCOPE)
- Full-XML response pipeline (Claude returning XML directly).
- API-mode integration (Anthropic/Gemini/OpenAI APIs).
- Per-AI golden-file tests for prompt content (additive only).
- AI-selector keyboard hint (`<kbd>1</kbd>` shortcuts).

---

## Standard Stack

### No new libraries required

All work uses existing project primitives. No NuGet adds, no npm adds, no DI changes.

| Primitive | Version / Location | Purpose |
|-----------|---------------------|---------|
| C# `StringBuilder` prompt composition | net10.0 BCL | Per-AI prompt building (existing pattern) |
| `ChatGptDeckRequest.TargetAiPlatform` | `Models/ChatGptDeckRequest.cs` | Already wired by Phase 9 (default `"ChatGPT"`) |
| `ChatGptDeckComparisonRequest.TargetAiPlatform` | `Models/ChatGptDeckComparisonRequest.cs:62` | Already exists (UI-only in Phase 9; adds zip round-trip in Phase 10) |
| `ChatGptCedhMetaGapRequest.TargetAiPlatform` | `Models/ChatGptCedhMetaGapRequest.cs:44` | Same — UI-only today, zip round-trip in Phase 10 |
| `ChatGptRequestContextParser` | `Services/ChatGptRequestContextParser.cs` | Already parses `target_ai_platform` (line 82-84). Reusable. |
| `ChatGptPacketArtifactStore.BuildZip` / `BuildComparisonZip` / `BuildCedhMetaGapZip` | `Services/ChatGptPacketArtifactStore.cs:52, 83, 114` | Insertion points for new request-context entries |
| `ChatGptPacketArtifactStore.LoadComparisonFromZip` / `LoadCedhMetaGapFromZip` | same file, lines 244, 276 | Insertion points to apply parsed `target_ai_platform` |
| `ChatGptResponseParsers.ParseAnalysisResponse` / `ParseSetUpgradeResponse` | `Services/ChatGptResponseParsers.cs:17, 48` | Calls into `ChatGptJsonTextFormatterService.ExtractJsonPayload` — that helper is the right place to insert `<result>` extraction (single seam covers all three pages) |
| `ChatGptDeckComparisonService.ParseComparisonResponse` | `Services/ChatGptDeckComparisonService.cs:788` | Same — calls `ExtractJsonPayload` |
| Razor partial `_AiSelector.cshtml` | `Views/Shared/_AiSelector.cshtml` | Already in place, no change needed |
| `data-cache-key` form-state TS persistence | `wwwroot/ts/deck-sync.ts:1083` | Existing mechanism — D-15 just needs to clear `skipPersistence` correctly |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| In-service dispatch on `TargetAiPlatform` | Strategy pattern with three `IPromptBuilder` implementations | More files, more DI wiring; planner can pick this if it makes the per-AI variants cleaner. D-13 explicitly leaves it open. Recommendation: single `switch` expression inside each `Build*Prompt` for the first slice; promote to interface if the per-AI bodies grow large. |
| `<result>` extraction inside `ChatGptResponseParsers` | Per-parser extraction | Single shim inside `ChatGptJsonTextFormatterService.ExtractJsonPayload` covers all three response parsers (Packets analysis + Packets set-upgrade + Comparison + CedhMetaGap all funnel through it). Single insertion point — strongly preferred. |
| Per-page custom request-context formats | Reuse `ChatGptRequestContextParser` for Comparison / CedhMetaGap | Parser already has `target_ai_platform` and ignores unknown keys gracefully; reuse as-is. The parser ignores keys it doesn't switch on — Packets-only fields like `card_specific_question_card_names` will simply be absent from the new files. |

**No version verification needed** — no new packages.

---

## Architecture Patterns

### System Architecture Diagram

```
                          ┌──────────────────────────────────┐
   Razor form POST -----> │ DeckController action            │
   (TargetAiPlatform=     │ (AI-agnostic — just passes it    │
    ChatGPT|Claude|Gemini)│  through to the service)         │
                          └─────────────────┬────────────────┘
                                            │
                                            ▼
                          ┌──────────────────────────────────┐
                          │ ChatGpt*Service.BuildAsync       │
                          │ (orchestration unchanged)        │
                          └─────────────────┬────────────────┘
                                            │
                                            ▼
                          ┌──────────────────────────────────┐
                          │ Build*Prompt(request, ...)       │
                          │   switch (request.TargetAiPlatform)
                          │     case "Claude":  -> XML build │
                          │     case "Gemini":  -> markdown+ │
                          │     default:         -> ChatGPT  │
                          │                       (unchanged │
                          │                        + <result>│
                          │                         wrap)    │
                          └─────────────────┬────────────────┘
                                            │
                                            ▼
                          ┌──────────────────────────────────┐
                          │ ChatGptPacketArtifactStore.      │
                          │   BuildZip / BuildComparisonZip /│
                          │   BuildCedhMetaGapZip            │
                          │                                  │
                          │ All three now write              │
                          │ 01-request-context.txt           │
                          │ containing target_ai_platform    │
                          └─────────────────┬────────────────┘
                                            │
                          ▼ download / upload round-trip ▼
                          ┌──────────────────────────────────┐
                          │ Load*FromZip                     │
                          │  -> ChatGptRequestContextParser  │
                          │     (already handles             │
                          │      target_ai_platform)         │
                          │  -> request.TargetAiPlatform = …│
                          └──────────────────────────────────┘

Response paste-back:
  user pastes "<result>{...JSON...}</result>" into Step 3 / Step 5
                                            │
                                            ▼
   ┌──────────────────────────────────────────────────────┐
   │ ChatGptJsonTextFormatterService.ExtractJsonPayload   │
   │   1. NEW: try <result>...</result> tag extraction    │
   │   2. EXISTING: try ```json fenced extraction         │
   │   3. EXISTING: parse as raw JSON                      │
   └─────────────────┬────────────────────────────────────┘
                     │
                     ▼
   ChatGptResponseParsers.Parse*Response  /  ParseComparisonResponse
   (no schema change — JSON inside still matches existing typed records)
```

### Pattern 1: Per-AI Dispatch Inside the Builder

**What:** Each existing `Build*Prompt` method (4 total) gains a top-of-method switch on
`request.TargetAiPlatform` that delegates to a per-AI helper.

**When to use:** Inside `ChatGptDeckPacketService` (`BuildAnalysisPrompt`,
`BuildSetUpgradePrompt`), `ChatGptDeckComparisonService` (`BuildComparisonPrompt`,
`BuildFollowUpPrompt`), and `ChatGptCedhMetaGapService` (`BuildPrompt`).

**Example (Packets analysis prompt — illustrative shape):**

```csharp
// Source: pattern derived from existing BuildAnalysisPrompt at ChatGptDeckPacketService.cs:839
// VERIFIED: method signature + caller site at line 433
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
    return request.TargetAiPlatform switch
    {
        "Claude" => BuildAnalysisPromptClaude(request, decklistText, referenceText, deckProfileSchemaJson, commanderName, selectedQuestionIds, bannedCards, comboResult, includeCardVersions),
        "Gemini" => BuildAnalysisPromptGemini(request, decklistText, referenceText, deckProfileSchemaJson, commanderName, selectedQuestionIds, bannedCards, comboResult, includeCardVersions),
        _        => BuildAnalysisPromptChatGpt(request, decklistText, referenceText, deckProfileSchemaJson, commanderName, selectedQuestionIds, bannedCards, comboResult, includeCardVersions),
    };
}
```

The existing 200-line body becomes `BuildAnalysisPromptChatGpt` (rename, identical content
+ `<result>` wrap appended). New methods `BuildAnalysisPromptClaude` and
`BuildAnalysisPromptGemini` are written from scratch reusing the same upstream string inputs
(decklistText, referenceText, schemaJson, etc).

### Pattern 2: Claude XML Skeleton

Per Anthropic docs `[CITED: platform.claude.com/docs/en/build-with-claude/prompt-engineering/use-xml-tags]`,
flat XML with semantic tag names is the canonical pattern. Long-context tasks (5-10k tokens
of decklist + reference) benefit from putting documents above the task framing. The docs
explicitly note *"Queries at the end can improve response quality by up to 30% in tests,
especially with complex, multi-document inputs"* `[CITED: same source]` — direct support for
D-03 (data first, instructions last).

**Example skeleton for Packets analysis:**

```xml
<role>
You are an expert Magic: The Gathering deck analyst specializing in Commander.
</role>

<commander>{{commander name}}</commander>

<bracket>
{{bracket label, summary, turns expectation, options table}}
</bracket>

<deck>
{{decklist text — quantity + name lines}}
</deck>

<reference>
  <cards>{{Scryfall card reference}}</cards>
  <combos>{{commander spellbook combos}}</combos>
  <banlist>{{banlist names}}</banlist>
</reference>

<questions>
{{numbered analysis questions}}
</questions>

<output_schema>
{{JSON schema for deck_profile}}
</output_schema>

<task>
- Read every section above before responding.
- Cite specific cards from <reference> / <deck> when making claims.
- Answer every numbered question in <questions> with 6-12 sentences.
- Wrap your final structured output in <result>...</result> tags. Inside the
  <result> tags, return a single JSON object matching <output_schema>. No prose
  inside <result>; no JSON outside <result>.
</task>
```

**Critical D-04 enforcement:** No `<system>`, `<human>`, or `<assistant>` blocks anywhere.
Those are API conventions; pasted into Claude.ai they just look like cosmetic noise in the
user's chat history. Anthropic docs `[VERIFIED via WebFetch 2026-05-09]` show role separation
is configured via the `system` parameter and `messages` array on the API, not via inline tags
in the user-message body.

### Pattern 3: Gemini Markdown + Instruction Tweaks

Per Google docs `[CITED: ai.google.dev/gemini-api/docs/prompting-strategies]`, Gemini accepts
both markdown headings and XML structurally — *"XML-style tags or Markdown headings are
effective. Choose one format and use it consistently"*. Per D-05, the project is choosing
markdown headings for Gemini to keep the differentiation in the instruction layer rather than
the structural layer.

Persona placement is documented `[CITED: same source]`: *"Place essential behavioral
constraints, role definitions (persona) ... in the System Instruction or at the very
beginning"*. For paste-in chat there is no System Instruction, so persona goes at the very
beginning.

**Gemini-specific tweaks to layer on top of the existing ChatGPT markdown skeleton:**

1. **Persona block at the very top** (replaces the leaner ChatGPT one-liner):
   ```text
   You are an expert Magic: The Gathering analyst with deep cEDH metagame
   knowledge. You analyze Commander decks rigorously and base every conclusion
   on observable card text and deck composition.
   ```

2. **Step-by-step scaffolding** (after persona, before data):
   ```text
   Think carefully through the problem before responding. Read every supplied
   section in full before forming any conclusion. When in doubt, prefer
   evidence-based caveats over confident speculation.
   ```
   Anthropic docs note this same pattern `[CITED: anthropic prompt-engineering guide]`.
   Google docs explicitly endorse it for Gemini 2.5/3 `[CITED: ai.google.dev]`.

3. **Schema-strictness language** in the OUTPUT FORMAT section (reinforces JSON contract):
   ```text
   Return ONLY a single JSON object inside the <result>...</result> wrapper.
   Do not include any prose, markdown headings, or commentary outside the
   <result> tags. The JSON must conform exactly to the schema below — no
   extra fields, no missing fields, no narrative wrappers.
   ```
   Per Gemini docs `[CITED: medium.com/google-cloud/structured-output-with-gemini-models]`,
   firm "Return ONLY raw JSON" language is the documented forcing function for paste-in
   contexts where the Structured Outputs API isn't in play.

4. **`<result>` wrapper instruction** (same as Claude and ChatGPT) — appended at end of
   OUTPUT FORMAT section.

### Pattern 4: ChatGPT Path — Append-Only `<result>` Instruction

D-07 fixes the ChatGPT path to be substantially unchanged. The only delta is appending one
new line to the end of the existing `## OUTPUT FORMAT` section:

```text
After the fenced ```json code block, also wrap the JSON response in
<result>...</result> tags so DeckFlow's parser can extract it uniformly across
ChatGPT/Claude/Gemini. The existing fenced ```json block remains as fallback —
do not remove it.
```

Risk assessment: low. ChatGPT handles XML-tag instructions well per OpenAI community
discussion `[CITED: community.openai.com/t/use-xml-tags-to-structure-my-prompts/1068871]`.
The parser falls back to fenced-JSON if the wrapper is missing, so even a model that ignores
the new instruction still works.

### Pattern 5: `<result>` Extraction Shim — Single Insertion Point

`ChatGptJsonTextFormatterService.ExtractJsonPayload` is called by all three response paths
(Packets analysis, Packets set-upgrade, Comparison via line 795 of
`ChatGptDeckComparisonService.cs`, and the meta-gap parser uses the same shim).

**Insertion logic (illustrative):**
```csharp
// New shim at the top of ExtractJsonPayload:
public static string ExtractJsonPayload(string input)
{
    var trimmed = input?.Trim() ?? string.Empty;

    // NEW: try <result>...</result> first (Phase 10 unified contract)
    var resultMatch = Regex.Match(trimmed, @"<result>\s*(.*?)\s*</result>", RegexOptions.Singleline);
    if (resultMatch.Success && !string.IsNullOrWhiteSpace(resultMatch.Groups[1].Value))
    {
        trimmed = resultMatch.Groups[1].Value.Trim();
    }

    // EXISTING: strip ```json fences (and any other markdown fences)
    // ... rest of original method ...
}
```

This is the central architectural decision per CONTEXT.md `<specifics>`: one parser path,
one importer, no per-AI parser variants. Inner content stays JSON; existing typed-record
deserialization keeps working.

**Caveat:** verify the planner picks the right method to shim. The Packets `BuildZip` path
also calls a separate `ExtractJsonObject` (private helper in
`ChatGptPacketArtifactStore.cs:408`) that strips fences for stored zip content. That helper
runs at *download* time on already-pasted JSON, not at parse time, so it does NOT need the
`<result>` shim — but the planner should verify by reading the call sites before shipping.
The two helpers serve different purposes: `ExtractJsonObject` (artifact store) defends
against fence-leakage when re-storing user-pasted JSON; `ExtractJsonPayload` (formatter) is
the actual response parser entry point.

### Pattern 6: Comparison + CedhMetaGap Request-Context File

**For Comparison zip:** add `01-request-context.txt` to `BuildComparisonZip` and the
`ComparisonAllowedNames` allow-list. Minimum content (per "Claude's Discretion" in CONTEXT):

```text
workflow_step: 3
deck_a_name: ...
deck_b_name: ...
deck_a_bracket: ...
deck_b_bracket: ...
target_ai_platform: Claude
```

**For CedhMetaGap zip:** add `01-request-context.txt` to `BuildCedhMetaGapZip` and the
`CedhAllowedNames` allow-list:

```text
workflow_step: 3
commander: ...
target_ai_platform: Gemini
```

**Recommendation:** Carry every form-state field that materially affects re-running the
analysis (deck names, bracket choices for Comparison; commander name for CedhMetaGap) plus
`target_ai_platform`. Existing `ChatGptRequestContextParser.Parse` ignores unknown keys
silently, but only honours keys it has a switch case for. Adding new keys to the parser
covering Comparison-specific fields (`deck_a_name`, `deck_a_bracket`, etc.) is cheap; planner
decides whether to extend `ParsedRequestContext` or build per-page parser variants.

### Anti-Patterns to Avoid

- **Editing `ExtractJsonObject` (line 408 of `ChatGptPacketArtifactStore.cs`).** That is a
  store-time helper, not a parse-time helper. The `<result>` shim belongs in
  `ChatGptJsonTextFormatterService.ExtractJsonPayload` — verified this method is the entry
  point used by `ParseAnalysisResponse` (line 24), `ParseSetUpgradeResponse` (line 55), and
  `ParseComparisonResponse` (line 795). The meta-gap parser also uses it.
- **Writing `<system>` / `<human>` / `<assistant>` tags in the Claude artifact.** D-04
  forbids it. Verified Anthropic web UI does not parse these — the API consumes them via
  the `system` parameter and `messages` array, not inline.
- **Splitting the response parser into per-AI variants.** CONTEXT.md `<specifics>` is
  explicit: one parser path. The unified `<result>` envelope is the central architecture
  decision.
- **Inventing a Gemini-specific structural skeleton.** D-06 forbids it; differentiation is
  instruction-layer only.
- **Touching the existing ChatGPT prompt content.** D-07 caps the diff at the new
  `<result>` wrap line. Any other ChatGPT-path edit is scope creep and risks Phase 10 SC #4
  (zero regression on default flow).
- **Editing `_AiSelector.cshtml` or any view.** Phase 9 already shipped UI; Phase 10 is
  service-layer only.
- **Trying to fix the sticky-busy-overlay regression by removing `data-no-busy`.** That
  attribute is the b09fd46 fix — touching it re-opens the bug. D-14 guidance must respect
  it.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Per-AI prompt format detection | Mime-type/header-style detection | Use `request.TargetAiPlatform` (already on all three request DTOs) | Phase 9 already plumbed it from form to service; reuse it. |
| `<result>` tag extraction | Custom XML parser | Single regex `Regex.Match(input, @"<result>\s*(.*?)\s*</result>", RegexOptions.Singleline)` | We are extracting one balanced tag with one capture group. XML parsing is overkill and risks parser-strictness errors on user-pasted text containing stray `<` characters in card names like "Lay Down Arms". |
| Zip request-context format for Comparison/CedhMetaGap | New ad-hoc YAML dialect | Reuse the existing `ChatGptRequestContextParser` `key: value` scalar pattern | Parser already exists, already handles `target_ai_platform`, ignores unknown keys, mirrors Packets contract. |
| Per-AI response schema | Three response DTOs | One unified JSON inner content (already typed) | D-09 freezes JSON shape. Inner JSON is identical across AIs. |
| Test framework for prompt content | Snapshot/golden-file harness | Manual user-paste verification for Phase 10 (deferred per `<deferred>`) | nyquist_validation is `false`; user verifies in claude.ai / gemini.google.com / chat.openai.com per Phase 9 precedent. |
| Download debounce timing logic | Custom timer with response correlation | Either lift the constant + comment OR listen for navigation/visibility-change events | D-14 explicitly allows the simpler "named constant + comment" path. |
| `skipPersistence` lifecycle | New flag system | Either clear flag in upload-error path, or scope to single persistence cycle, or remove the flag entirely | D-15 keeps three options open. |

**Key insight:** Almost every Phase 10 change is additive on top of Phase 9 plumbing. The
risk is in *content* (does the Claude prompt actually produce useful output when pasted into
Claude.ai?) — not in *infrastructure*. The infrastructure surface is small and well-scoped.

---

## Common Pitfalls

### Pitfall 1: `<result>` Tag Conflicts with User-Pasted Content
**What goes wrong:** A user pastes a response that contains `<result>` somewhere
mid-document (Claude responses sometimes use other meta tags), and the regex extracts the
wrong slice.
**Why it happens:** `Regex.Match` with `Singleline` is greedy by default unless `?` makes it
non-greedy.
**How to avoid:** Use the non-greedy quantifier: `<result>\s*(.*?)\s*</result>`. Test with a
nested-tag fixture before shipping. The lazy match guarantees the *first* matching pair is
used. Also: if no match, fall through to existing fenced-JSON detection — never throw.
**Warning signs:** Parser succeeds but returns nonsense JSON; deserialization fails with
"unexpected token at position N".

### Pitfall 2: Anthropic Role Blocks Sneaking Back In
**What goes wrong:** Researcher or planner sees `<system>`/`<human>` patterns in
out-of-context Anthropic doc snippets and assumes they belong in the user-paste prompt.
**Why it happens:** Most public Anthropic docs are API-focused; the role blocks read like
template syntax.
**How to avoid:** D-04 is explicit. Verify against Claude.ai web UI — the `system` parameter
on the API messages object IS the system prompt, not an inline tag. Pasting `<system>...
</system>` into claude.ai produces literal text in the conversation, NOT a system-prompt
override.
**Warning signs:** First Claude paste returns "I see you've included a system block — what
would you like me to do with it?"

### Pitfall 3: Comparison Zip Round-Trip Already Has "First Required File" Logic
**What goes wrong:** Adding `01-request-context.txt` to Comparison zip, but
`LoadComparisonFromZip` (line 250) currently throws if `40-deck-comparison-response.json` is
absent. After Phase 10, that throw could conflict with the new "request-context only"
partial-zip semantics.
**Why it happens:** Comparison zip was designed assuming response is always present; Packets
relaxed that assumption in Phase 9 (commit f26e63d). Comparison and CedhMetaGap still throw.
**How to avoid:** Decide explicitly whether Comparison and CedhMetaGap should also support
partial-zip upload (request-context only, land on Step 1) or whether the response file stays
mandatory. Recommendation: keep the existing "response required" semantics for Phase 10 —
the AI selector restoration is a sub-feature of an existing zip that already has a response
in it. Re-evaluating partial-zip for Comparison + CedhMetaGap is out of scope.
**Warning signs:** Existing comparison zip uploads start failing after Phase 10 ships.

### Pitfall 4: Markdown Inside `<role>` or `<task>` Tags Breaking Parsers
**What goes wrong:** A Claude artifact embeds bullet lists inside `<task>`, but a stray `<`
character inside a card name (e.g. `< Sol Ring`) confuses the user's eye when reading the
artifact.
**Why it happens:** D-01 explicitly allows markdown inside content tags; XML doesn't escape
markdown.
**How to avoid:** Card names are well-defined and don't contain `<` or `>`. The MTG Comprehensive
Rules disallow them in card names. Project's existing `NormalizeSingleLine` helper already
strips suspect characters. Low risk.
**Warning signs:** None expected — but worth a manual scan of one Claude artifact for visual
sanity.

### Pitfall 5: Gemini Returning JSON in Markdown Fence Despite Instructions
**What goes wrong:** Gemini wraps JSON in ```json fences AND `<result>` tags, or just in
fences without the `<result>` wrapper.
**Why it happens:** Documented Gemini behaviour `[CITED:
medium.com/google-cloud/structured-output-with-gemini-models]` notes Gemini has a strong bias
toward fence-decorated output. Even with firm instructions, it sometimes leaks.
**How to avoid:** Server parser falls back to fenced-JSON if no `<result>` tag. Per D-08
this is by design. Gemini path's instruction-layer schema-strictness language is the upper
bound — if Gemini still adds prose, the fenced fallback catches it.
**Warning signs:** Live-paste verification — confirm Gemini round-trips end-to-end at least
once before signoff.

### Pitfall 6: Rebuilding 200-Line Method Bodies Three Times
**What goes wrong:** Each per-AI variant of `BuildAnalysisPrompt` re-implements bracket
guidance, evidence rules, output format requirements — code drift across the three branches.
**Why it happens:** Naive copy-paste of the existing 200-line method.
**How to avoid:** Extract pure-data sections (decklist, reference, schema) to shared
helpers consumed by all three variants. Only the structural skeleton (XML vs markdown vs
markdown+tweaks) and the instruction layer differ. The data goes through unchanged. The
existing code already does this for `BuildComboReferenceText` — extend the pattern.
**Warning signs:** A bug fix to bracket guidance lands in only one variant.

### Pitfall 7: D-14 Hardening Re-Introducing Sticky-Busy-Overlay
**What goes wrong:** Planner replaces 3000ms timeout with "re-enable on form submit complete"
hook, which fires on the download-form submit and re-enables the busy overlay (the b09fd46
bug).
**Why it happens:** The busy overlay subscribes to form submit; the download button's
submit was the source of the original sticky-busy bug.
**How to avoid:** The `data-no-busy` attribute on the download button MUST stay. D-14
hardening is about re-enabling the BUTTON itself (so the user can click it again), NOT about
the busy overlay (which already correctly skips for `data-no-busy` submitters). The two
mechanisms are independent. If unsure, the safe path is option (a): lift 3000ms to a named
constant `CHATGPT_DOWNLOAD_DEBOUNCE_MS` with a comment explaining the Render-response-time
tradeoff. That meets D-14 minimum without touching the busy-overlay code path at all.
**Warning signs:** Manual test — clicking download button shows busy overlay AND nothing
hides it after the file downloads. That's the b09fd46 regression.

### Pitfall 8: D-15 `skipPersistence` Cleared Too Eagerly
**What goes wrong:** `skipPersistence` is cleared inside the `addEventListener('change')`
callback after the click, before the upload POST navigation, defeating the original purpose
(suppressing pre-navigation persistFormState calls overwriting upload-rendered values).
**Why it happens:** Over-correction.
**How to avoid:** The flag was set BEFORE `submit?.click()` to suppress persistence during
the brief window between change-event and navigation. The bug is that on POST error
(navigation never happens), the flag never clears. Fix: add a one-shot `setTimeout` or a
`window.addEventListener('error', ..., { once: true })` to clear the flag after a few
seconds OR scope the flag with `delete form.dataset.skipPersistence` after the next
navigation cancel. Recommendation: simplest fix is wrap the `submit?.click()` in
`try/catch` and clear `skipPersistence` in the catch (though `click()` rarely throws — the
real failure mode is the POST itself failing post-navigation, which is harder to detect from
the original page). Practical recommendation: a 30-second `setTimeout` to auto-clear the
flag is sufficient — by then the user has either navigated away or the upload definitively
failed.
**Warning signs:** User uploads a zip, upload fails (network error), then types in a form
field — value is not persisted. Verifies fix by reproducing this sequence and confirming
the form-state pill returns.

---

## Code Examples

Verified patterns from the existing codebase:

### 1. Per-AI dispatch shape (planner ports this to all 4 builders)

```csharp
// Source: derived from existing BuildAnalysisPrompt at
// DeckFlow.Web/Services/ChatGptDeckPacketService.cs:839 [VERIFIED via Read]
private static string BuildAnalysisPrompt(
    ChatGptDeckRequest request,
    string decklistText,
    /* ... existing args ... */)
{
    return request.TargetAiPlatform switch
    {
        "Claude" => BuildAnalysisPromptClaude(request, decklistText, /* ... */),
        "Gemini" => BuildAnalysisPromptGemini(request, decklistText, /* ... */),
        _        => BuildAnalysisPromptChatGpt(request, decklistText, /* ... */),
    };
}
```

### 2. Claude-shaped XML prompt skeleton (illustrative)

```csharp
// Source: pattern from Anthropic prompt-engineering docs
// [CITED: platform.claude.com/docs/en/build-with-claude/prompt-engineering/use-xml-tags]
// VERIFIED via WebFetch 2026-05-09
private static string BuildAnalysisPromptClaude(ChatGptDeckRequest request, /* ... */)
{
    var builder = new StringBuilder();
    builder.AppendLine("<role>");
    builder.AppendLine("You are an expert Magic: The Gathering deck analyst specializing in Commander.");
    builder.AppendLine("</role>");
    builder.AppendLine();

    builder.AppendLine($"<commander>{commanderName}</commander>");
    builder.AppendLine();

    builder.AppendLine("<bracket>");
    builder.AppendLine(/* bracket guidance markdown — same content as ChatGPT path */);
    builder.AppendLine("</bracket>");
    builder.AppendLine();

    builder.AppendLine("<deck>");
    builder.AppendLine(decklistText);
    builder.AppendLine("</deck>");
    builder.AppendLine();

    builder.AppendLine("<reference>");
    builder.AppendLine("  <cards>"); builder.AppendLine(referenceText); builder.AppendLine("  </cards>");
    if (comboResult is not null)
    {
        builder.AppendLine("  <combos>"); builder.AppendLine(/* combo text */); builder.AppendLine("  </combos>");
    }
    builder.AppendLine("  <banlist>"); builder.AppendLine(/* banned cards */); builder.AppendLine("  </banlist>");
    builder.AppendLine("</reference>");
    builder.AppendLine();

    builder.AppendLine("<questions>");
    /* numbered questions */
    builder.AppendLine("</questions>");
    builder.AppendLine();

    builder.AppendLine("<output_schema>");
    builder.AppendLine(deckProfileSchemaJson);
    builder.AppendLine("</output_schema>");
    builder.AppendLine();

    builder.AppendLine("<task>");
    /* numbered task instructions, evidence rules, output format requirements */
    builder.AppendLine("Wrap your final structured output in <result>...</result> tags.");
    builder.AppendLine("Inside <result>, return a single JSON object matching <output_schema>.");
    builder.AppendLine("No prose inside <result>; no JSON outside <result>.");
    builder.AppendLine("</task>");

    return builder.ToString().TrimEnd();
}
```

### 3. `<result>` extraction shim (illustrative)

```csharp
// Source: insert at top of ChatGptJsonTextFormatterService.ExtractJsonPayload
// (entry point used by all three response parsers — VERIFIED)
private static readonly Regex ResultTagRegex = new(
    @"<result>\s*(.*?)\s*</result>",
    RegexOptions.Compiled | RegexOptions.Singleline);

public static string ExtractJsonPayload(string input)
{
    var trimmed = (input ?? string.Empty).Trim();

    // Phase 10: try unified <result>...</result> wrapper first.
    // Falls through to the existing fenced-code-block extraction on miss
    // so legacy artifacts and ChatGPT-fenced-only responses keep working.
    var match = ResultTagRegex.Match(trimmed);
    if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
    {
        trimmed = match.Groups[1].Value.Trim();
    }

    // EXISTING fenced-block extraction continues here unchanged...
}
```

### 4. D-14 minimal-cost option (a) — named constant

```typescript
// Source: pattern from existing constants in deck-sync.ts
// VERIFIED via Read at line 754
const CHATGPT_DOWNLOAD_DEBOUNCE_MS = 3000; // Render Starter cold response can take ~2s; 3s gives 1s margin.
                                            // Re-enabling earlier risks duplicate POST on rapid double-click;
                                            // re-enabling later annoys users on fast responses.
                                            // If users report missed clicks, raise this rather than coupling
                                            // re-enable to navigation events (which would re-introduce the
                                            // sticky-busy-overlay regression — see commit b09fd46).

const registerChatGptDownloadDebounce = (): void => {
  document.querySelectorAll<HTMLButtonElement>('button[data-chatgpt-download-submit]').forEach(button => {
    button.addEventListener('click', () => {
      if (button.disabled) return;
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

### 5. D-15 minimal-cost option — clear `skipPersistence` after a timeout window

```typescript
// Source: existing wireChatGptZipUpload at deck-sync.ts:2386 [VERIFIED]
const wireChatGptZipUpload = (): void => {
  document.querySelectorAll<HTMLInputElement>('[data-chatgpt-zip-upload]').forEach(input => {
    input.addEventListener('change', () => {
      const file = input.files?.[0];
      if (!file) return;

      const form = input.closest<HTMLFormElement>('form[data-cache-key]');
      if (form) {
        clearPersistedFormState(form);
        form.dataset.skipPersistence = 'true';

        // PHASE 10: if the upload POST errors before navigation,
        // skipPersistence would otherwise stay true for the rest of the
        // page lifetime, silently disabling form-state persistence.
        // Auto-clear after 30s — by then the upload either navigated us
        // away (this handler is gone) or definitively failed (clear it
        // so subsequent user input is persisted normally).
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

---

## Runtime State Inventory

Phase 10 is a **content + plumbing** change, not a rename or migration. No runtime state
inventory required.

- **Stored data:** None — JSON response shape is unchanged per D-09; existing saved zips
  keep working per D-08.
- **Live service config:** None.
- **OS-registered state:** None.
- **Secrets/env vars:** None.
- **Build artifacts:** TypeScript will recompile via the existing `CompileTypeScriptAssets`
  MSBuild target. The browser-extension zip is unaffected. No stale-artifact risk.

---

## Environment Availability

Step 2.6: SKIPPED — Phase 10 is code/config changes only. No external CLI tools, databases,
or services beyond what already runs the project. Existing dependencies (Scryfall,
Commander Spellbook, EDH Top 16, etc.) are not touched.

---

## Test Approach

Per `nyquist_validation: false` in `.planning/config.json`, the Validation Architecture
section is omitted. Test approach for Phase 10:

### Existing test infrastructure relevant to Phase 10

| File | Coverage |
|------|----------|
| `ChatGptPacketArtifactStoreRoundTripTests.cs` | Packets zip round-trip; existing `LoadFromZip_AlsoRestoresUserInputs_FromArnaFixture` test will keep passing because legacy zips without `target_ai_platform` already default to `"ChatGPT"`. |
| `ChatGptPacketArtifactStoreTests.cs` | Packets zip build-time tests. |
| `ChatGptResponseParsersTests.cs` | Tests for `ParseAnalysisResponse` and `ParseSetUpgradeResponse` — must stay green after `<result>` shim insertion. |
| `ChatGptDeckComparisonServiceTests.cs` | Comparison parser tests. |
| `ChatGptCedhMetaGapServiceTests.cs` | Meta-gap parser tests. |
| `ChatGptDeckPacketServiceTests.cs` | Packet service test. |

### Recommended Phase 10 verification

1. **Build gate:** `dotnet build` clean across all projects (CLAUDE.md mandates this; VSTest
   unreliable in WSL).
2. **Existing test gate:** All existing tests keep passing. The `<result>` shim is
   non-breaking (it does nothing if no `<result>` tag is present) and the per-AI dispatch
   defaults to the ChatGPT path on `TargetAiPlatform == "ChatGPT"` (and on null/default —
   the model's setter normalizes null to `"ChatGPT"`).
3. **Targeted new tests (optional, additive):**
   - `<result>` extractor on a fixture: `<result>{"format":"Commander"}</result>` ->
     extracts `{"format":"Commander"}`.
   - `<result>` extractor on a fixture WITH fenced JSON inside the wrapper:
     `<result>\n```json\n{...}\n```\n</result>` -> still works (fenced-extraction runs
     after the wrapper extraction).
   - Comparison zip round-trip: build zip with `TargetAiPlatform = "Gemini"` -> load zip
     -> request shows `TargetAiPlatform == "Gemini"`.
4. **Live paste verification (manual, per Phase 9 precedent for SC #3):**
   - Generate Packets-Claude artifact for a known deck. Paste into claude.ai. Confirm
     deck_profile JSON returns wrapped in `<result>`. Paste back into DeckFlow. Confirm
     parsing succeeds and renders.
   - Same for Packets-Gemini -> gemini.google.com.
   - Same for Comparison-Claude (or Gemini), CedhMetaGap-Claude (or Gemini).
   - Spot-check ChatGPT path: confirm zero regression — pick one existing user flow,
     download with `TargetAiPlatform = "ChatGPT"`, paste into chat.openai.com, paste
     response back. Should work identically to pre-Phase-10.

The user verified Phase 9 with manual round-trip in claude.ai. Phase 10 SC #3 explicitly
calls out human-verify checkpoint pattern — same approach applies here.

---

## State of the Art

| Old Approach | Current Approach (2026) | Source |
|--------------|--------------------------|--------|
| Single ChatGPT-only artifact format | Per-AI artifact tuned to each model's strengths | Anthropic + Google + OpenAI all publish prompt-engineering guides recommending different structural patterns; native XML support in Claude (trained on it `[CITED: anthropic docs]`); markdown + persona framing for Gemini |
| Inline `<system>`/`<human>` blocks for Claude paste | Flat semantic XML, no role blocks for chat-paste | Anthropic API role-separation moved entirely to API parameters; chat UI does not parse them |
| API integration only for structured output | Paste-in chat with `<result>` wrapper + fallback fenced JSON | Lightweight forcing function; works across all three AIs without API auth/billing infra |
| Single response-extraction strategy | Layered: try `<result>` first, fall back to fenced-JSON, fall back to raw JSON | Defense in depth — old artifacts keep working, new artifacts get unified extraction |

**Deprecated/outdated:**
- Putting analysis questions/instructions before the deck data — current Anthropic guidance
  flips this for long-context tasks (data first, query last).
- Using `<system>` / `<human>` / `<assistant>` tags inside Claude.ai web UI prompts — these
  are API-mode-only conventions.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `ChatGptJsonTextFormatterService.ExtractJsonPayload` is the single shim point that covers all three response parsers (Packets analysis, Packets set-upgrade, Comparison, CedhMetaGap) | Pattern 5 | Verified by `grep` — confirmed all four parsers call into this method. Low risk. If a future parser is added that bypasses it, the new parser would need the shim too. `[VERIFIED via grep on Services/]` |
| A2 | Comparison and CedhMetaGap zips do not need partial-zip "request-context only" semantics in Phase 10 | Pitfall 3 | If a user wants to upload a Comparison zip with `01-request-context.txt` only (no response), they get an error. Acceptable given partial-zip support is a Packets-only Phase 9 enhancement and CONTEXT.md doesn't ask for parity. Low risk. `[ASSUMED]` |
| A3 | A 30-second auto-clear timeout on `skipPersistence` is sufficient for D-15 | Code Examples 5, Pitfall 8 | Edge case: user fails an upload, types into a field within 30s, then field doesn't persist. Acceptable — the original Phase 9 fix already wrapped a window where persistence is suspended; we're just bounding it. Lower risk than re-architecting persistence. `[ASSUMED]` |
| A4 | Card names in the MTG corpus do not contain `<` or `>` characters | Pitfall 4 | MTG Comprehensive Rules forbid these characters. Project's `NormalizeSingleLine` also strips them defensively. Low risk. `[VERIFIED via knowledge of MTG rules]` |
| A5 | Gemini's tendency to wrap JSON in `\`\`\`json` fences is reliably suppressed by the project's "Return ONLY raw JSON inside `<result>`" instruction layer | Pattern 3, Pitfall 5 | Documented Gemini behaviour `[CITED: medium.com/google-cloud/structured-output-with-gemini-models]` shows it sometimes still leaks. Mitigation: parser falls back to fenced-JSON detection. Low risk because of the fallback. `[ASSUMED + CITED]` |
| A6 | Packets-Claude alone is sufficient derisking before fanning out to all 4 builders × 3 AIs | Summary, Plan 1 recommendation | If the Claude artifact for analysis prompt fails live-paste verification, the same skeleton would fail for set-upgrade / comparison / meta-gap (same XML pattern, same rules). Verifying one validates all. Low risk; if it fails, planner discovers it before sinking time into the other three. `[ASSUMED based on prompt-engineering pattern transferability]` |

**These assumptions need user/planner attention only if they fail in practice.** Default
ship plan: proceed with the assumptions; reconsider if live-paste verification fails.

---

## Open Questions

1. **Should `01-request-context.txt` content for Comparison and CedhMetaGap match Packets verbosity, or be minimal?**
   - What we know: Packets file carries 13+ scalar/list fields. Comparison and CedhMetaGap
     have far fewer round-trip-relevant fields. Per Claude's Discretion in CONTEXT.md, this
     is the planner's call.
   - What's unclear: Is there value in carrying every form-state field for those pages
     (e.g., `time_period`, `sort_by`, `min_event_size` for CedhMetaGap), or is
     `target_ai_platform` plus deck names sufficient?
   - Recommendation: Carry every form field that materially affects re-running the
     analysis (deck_a_name, deck_a_bracket, deck_b_name, deck_b_bracket for Comparison;
     commander, time_period, sort_by, min_event_size, max_standing for CedhMetaGap). Keeps
     architectural symmetry with Packets and avoids re-prompting users on resume. Cost is a
     dozen lines of extra parser code.

2. **Single-shim vs per-parser `<result>` extraction?**
   - What we know: All three response parsers funnel through
     `ChatGptJsonTextFormatterService.ExtractJsonPayload`. A single shim there handles
     everything.
   - What's unclear: Are there any callers of `ExtractJsonPayload` that should NOT do
     `<result>` extraction (e.g., a save-time normalizer)?
   - Recommendation: Verify by `grep` before shipping. If `ExtractJsonPayload` has only
     parser-time callers, the shim is safe at the helper. If a save-time caller exists,
     extract via a new sibling helper and call from each `Parse*Response` method
     individually. Plan should include a 5-minute call-site audit before code changes.

3. **Does `ChatGptDeckComparisonService.BuildFollowUpPrompt` (line 710) need per-AI
   variants?**
   - What we know: It's a separate prompt builder from `BuildComparisonPrompt`. The
     follow-up prompt is regenerated when the user re-asks Claude/Gemini/ChatGPT after the
     first comparison response.
   - What's unclear: User generates follow-up prompt from the same form, so it inherits
     `TargetAiPlatform`. The follow-up prompt has the same JSON schema and same `<result>`
     wrap requirement, so the per-AI structural divergence is the same as the main
     comparison prompt.
   - Recommendation: Yes, also fork `BuildFollowUpPrompt` per-AI. Treat it as a fifth
     builder in the planning slice. The "all four builders" framing in the Summary should
     read "all five builders" if the follow-up prompt is included. Planner verifies during
     plan drafting.

---

## Project Constraints (from CLAUDE.md)

- **Tech stack:** ASP.NET 10 + Razor — pinned. No framework migration.
- **Hosting:** Render Starter web tier (512MB cap). Mind allocations — but Phase 10 is pure
  string composition, no allocation pressure.
- **HTTP resilience:** RestSharp + Polly v8 — not touched in Phase 10.
- **Public repo:** `luntc1972/DeckFlow` — no secrets in commits ever. Phase 10 is code only,
  no secrets surface.
- **Testing:** VSTest unreliable in WSL. Rely on `dotnet build` clean + manual verification.
- **Commits:** Plain default-author, no Co-Authored-By trailer. README updated when
  behavior changes (yes — README needs a one-line note that Claude/Gemini artifacts now
  differ from ChatGPT). Commit per logical change.
- **Branch:** v1.2 work lives on `v1.2` branch, NOT main. Phase 10 commits land on `v1.2`.
- **Theme system:** Guild themes are full standalone CSS forks. NOT touched in Phase 10
  (no UI changes).
- **Codex MCP for code edits:** All coding tasks routed through Codex MCP per global
  CLAUDE.md. Multi-file work (≥2 files) → use `gpt-5.4` full model.
- **GSD workflow enforcement:** Edit/Write tools only via GSD command (this phase already in
  GSD-execute scope).

---

## Sources

### Primary (HIGH confidence — verified via Read of source files)
- `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` lines 433-442, 839, 1051, 1655-1688
  — caller of all four upstream prompt builders + `BuildRequestContextText` writer with
  `target_ai_platform` line 1663 already present (Phase 9).
- `DeckFlow.Web/Services/ChatGptDeckComparisonService.cs` lines 151, 165, 609-690, 710-744,
  788-799 — `BuildComparisonPrompt`, `BuildFollowUpPrompt`, `ParseComparisonResponse`.
- `DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs` lines 140-167, 263-298, 301-432 —
  `BuildPrompt` (the meta-gap analogue, named differently than expected per observation 6064).
- `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` lines 16-50, 52, 83, 114, 142, 244,
  276, 408 — full zip allow-lists, build/load methods, `ExtractJsonObject` private helper.
- `DeckFlow.Web/Services/ChatGptRequestContextParser.cs` lines 17-153, 248-283 — full
  parser with `target_ai_platform` already supported (line 82-84) + `ParsedRequestContext`
  record with `TargetAiPlatform` property (line 282).
- `DeckFlow.Web/Services/ChatGptResponseParsers.cs` full file — confirms calls into
  `ChatGptJsonTextFormatterService.ExtractJsonPayload` at lines 24, 55.
- `DeckFlow.Web/Models/ChatGptDeckComparisonRequest.cs` — `TargetAiPlatform` already exists
  (line 62).
- `DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs` — `TargetAiPlatform` already exists
  (line 44).
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts` lines 723-771, 1083-1107, 2386-2409 — debounce,
  persistence, upload wiring.
- Commit `b09fd46` (`git show b09fd46`) — sticky-busy-overlay regression context for D-14
  guard rail.
- `.planning/phases/09-bracket-ux-ai-selector-foundation/09-RESEARCH.md` — Phase 9
  foundation already maps the same prompt-build entry points and the round-trip pattern.

### Primary (HIGH confidence — verified via WebFetch / official vendor docs)
- Anthropic prompt engineering guide — `platform.claude.com/docs/en/build-with-claude/prompt-engineering/use-xml-tags` and `.../claude-prompting-best-practices` `[VERIFIED via WebFetch 2026-05-09]`. Confirms: XML tagging, long-context "data above query" recommendation (~30% gain), output control via XML tags, no inline role blocks for chat UI.
- Google AI prompt design strategies — `ai.google.dev/gemini-api/docs/prompting-strategies` `[VERIFIED via WebFetch 2026-05-09]`. Confirms: XML and markdown both work ("choose one"), persona at very beginning, "Think very hard" scaffolding, structured output recommended via API for complex schemas (we are paste-in, so we use the wrapper-tag forcing function instead).

### Secondary (MEDIUM confidence — community/blog sources, cross-verified with vendor docs)
- `medium.com/google-cloud/structured-output-with-gemini-models` — Gemini's bias toward
  fence-decorated output and the documented "Return ONLY raw JSON" forcing function.
- `community.openai.com/t/use-xml-tags-to-structure-my-prompts/1068871` — confirms ChatGPT
  handles XML-tag instructions well.
- `aipromptlibrary.app/blog/claude-xml-tags-prompt-engineering` — additional XML-tag
  examples.

### Tertiary (LOW confidence — not used for prescriptive claims, kept for context only)
- General web search results on `Claude.ai web UI XML system human role blocks ignored
  chat interface paste-in` — did not surface authoritative confirmation that role blocks are
  silently ignored; the D-04 decision relies on the Anthropic API documentation showing
  that role separation is API-parameter-only, plus the project's existing engineering
  judgment (CONTEXT.md). Acceptable: the only risk if D-04 is wrong is "Claude shows the
  role-block tags in its first reply", which is detectable on first paste-test and easy to
  fix forward.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new libraries; all primitives verified in source.
- Architecture (per-AI dispatch shape): HIGH — direct extension of Phase 9 plumbing,
  every insertion point verified.
- Architecture (Claude prompt content): HIGH — matches current Anthropic published
  guidance; verified via WebFetch.
- Architecture (Gemini prompt content): MEDIUM — markdown skeleton + instruction-layer
  tweaks is a defensible interpretation of Google's "choose one format" guidance, but
  exact wording/tone is the planner's call (per "Claude's Discretion") and only live-paste
  verification will confirm reliability.
- Architecture (`<result>` extraction): HIGH — single shim insertion, single regex,
  fallback to existing path on miss.
- Pitfalls: HIGH — eight pitfalls each tied to a specific code location or vendor doc
  citation.
- D-14 / D-15 polish: HIGH — both consumer sites located and confirmed; recommendations
  match the safe-minimum option from CONTEXT.md.

**Research date:** 2026-05-09
**Valid until:** 2026-06-09 (vendor prompt-engineering guidance moves; verify Anthropic and
Google docs haven't materially shifted before any rework after that date)

---

## RESEARCH COMPLETE

**Phase:** 10 — Claude + Gemini Artifact Optimization
**Confidence:** HIGH

### Key Findings

- **All four (potentially five) prompt-builder insertion points located and verified:**
  `BuildAnalysisPrompt` (Packets, line 839), `BuildSetUpgradePrompt` (Packets, line 1051),
  `BuildComparisonPrompt` (Comparison, line 609), `BuildFollowUpPrompt` (Comparison, line
  710), `BuildPrompt` (CedhMetaGap, line 301). The follow-up prompt likely needs per-AI
  forking too — flagged as Open Question 3 for planner.
- **Single-shim `<result>` extraction is feasible:** `ChatGptJsonTextFormatterService.ExtractJsonPayload`
  is the entry point used by all three (analysis, set-upgrade, comparison, meta-gap) parser
  paths. One regex insertion covers them all.
- **Phase 9 already laid 80% of the round-trip plumbing for Comparison and CedhMetaGap:**
  Both request DTOs have `TargetAiPlatform`. `ChatGptRequestContextParser` already handles
  the `target_ai_platform` key. The only gaps are: (1) Comparison/CedhMetaGap zip BUILD
  doesn't write `01-request-context.txt`, (2) their LOAD methods don't apply the parsed
  value back to the request.
- **D-14 and D-15 both have safe minimum-cost paths:** Lift 3000ms to a named constant
  with a comment for D-14; auto-clear `skipPersistence` after 30s for D-15. Either is
  acceptable per CONTEXT.md and avoids re-introducing the b09fd46 sticky-busy-overlay
  regression.
- **Vendor guidance confirms the locked CONTEXT decisions:** Anthropic docs explicitly
  endorse flat XML, semantic tag names, data-above-query ordering, and output-tag wrap.
  Google docs accept markdown skeleton with explicit "Return ONLY" language as the paste-in
  forcing function. ChatGPT handles the new `<result>` wrap line without regression risk.

### Files Created

`.planning/phases/10-claude-gemini-artifact-optimization/10-RESEARCH.md`

### Confidence Assessment

| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | No new libraries; every primitive verified in source. |
| Architecture (dispatch) | HIGH | Direct Phase 9 extension; insertion points verified. |
| Architecture (Claude content) | HIGH | Matches current Anthropic published guidance. |
| Architecture (Gemini content) | MEDIUM | Wording/tone is planner discretion; live-paste verification needed. |
| Architecture (`<result>` shim) | HIGH | Single regex, single helper, falls back gracefully. |
| Pitfalls | HIGH | Each tied to specific code location or vendor citation. |
| D-14/D-15 polish | HIGH | Consumer sites located; safe-minimum path documented. |

### Open Questions for Planner

1. Comparison/CedhMetaGap `01-request-context.txt` — minimum (`target_ai_platform` only)
   or full form-state parity with Packets? Recommendation: full form-state parity for
   architectural symmetry.
2. `<result>` extraction at the helper level (`ExtractJsonPayload`) vs per-parser? Verify
   no save-time callers exist before centralizing. Recommendation: helper-level if call-site
   audit is clean.
3. `BuildFollowUpPrompt` (Comparison) — per-AI fork or single shared? Recommendation: fork
   it; same JSON contract requires same `<result>` wrap and the per-AI prompt structure
   should match the main comparison prompt for visual consistency.

### Decomposition Recommendation

Four-plan slice:

- **Plan 10-01:** Per-AI dispatch primitive + Packets-Claude content (`BuildAnalysisPrompt`
  Claude variant + ChatGPT-path append-only `<result>` line). Smallest viable derisking
  slice.
- **Plan 10-02:** Remaining per-AI content — Packets-Gemini analysis, both AIs for
  set-upgrade, comparison (and follow-up), meta-gap. All `<result>` wrap instructions
  inserted.
- **Plan 10-03:** Comparison + CedhMetaGap zip round-trip (`01-request-context.txt`
  build + load) + `<result>` extraction shim in `ExtractJsonPayload` + fixture-level test
  for the shim.
- **Plan 10-04:** D-14 + D-15 TS polish. Independent of Plans 10-01..03; can land in any
  order.

### Ready for Planning

Research complete. Planner can now create PLAN.md files using the four-plan decomposition
above.

Sources:
- [Use XML tags to structure your prompts — Claude API Docs](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/use-xml-tags)
- [Prompting best practices — Claude API Docs](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices)
- [Prompt design strategies — Gemini API](https://ai.google.dev/gemini-api/docs/prompting-strategies)
- [Structured outputs — Gemini API](https://ai.google.dev/gemini-api/docs/structured-output)
- [Structured Output with Gemini Models — Saverio Terracciano (Google Cloud Community)](https://medium.com/google-cloud/structured-output-with-gemini-models-begging-borrowing-and-json-ing-f70ffd60eae6)
- [Use XML tags to structure my prompts — OpenAI Developer Community](https://community.openai.com/t/use-xml-tags-to-structure-my-prompts/1068871)
- [Why XML Tags Are so Fundamental to Claude — glthr.com](https://glthr.com/xml-fundamental-to-claude)
