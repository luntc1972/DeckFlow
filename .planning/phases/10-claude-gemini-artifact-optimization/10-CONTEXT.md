# Phase 10: Claude + Gemini Artifact Optimization - Context

**Gathered:** 2026-05-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Generate per-AI prompt artifacts (file content + instructions) tuned to Claude's
and Gemini's documented strengths, layered onto the existing AI-selector
infrastructure shipped in Phase 9. Ship across all three ChatGPT analysis pages
(Packets, Deck Comparison, CEDH Meta Gap). Default ChatGPT flow stays unchanged
in content and behavior — Claude/Gemini paths are additive.

Out of phase scope: changing the JSON response schemas; rewriting the response
parser into a full XML pipeline; introducing API-mode integrations (this phase
remains paste-into-web-UI).

</domain>

<decisions>
## Implementation Decisions

### Claude artifact format

- **D-01:** Claude prompt uses an XML skeleton with semantic domain tags. Markdown
  is allowed inside content tags for prose. Reasoning: Anthropic explicitly
  trained Claude on prompts containing semantic XML tags; in long inputs
  (5–10k tokens of decklist + reference + combos) XML tag boundaries are
  stronger signal than markdown headings, and they let the task instruction
  reference sections by name ("answer using only data from `<reference>` and
  `<deck>`").
- **D-02:** Tag taxonomy for the analysis prompt: `<role>`, `<deck>`,
  `<commander>`, `<bracket>`, `<reference>` (with optional nested
  `<cards>`/`<combos>`/`<banlist>` if it helps composition), `<questions>`,
  `<output_schema>`, `<task>`. Adapt the same vocabulary for the comparison
  prompt (`<deck_a>`, `<deck_b>`, etc.) and the meta-gap prompt
  (`<reference_decks>`, etc.) — keep the per-page structure aligned so the
  format is recognizable across the three pages.
- **D-03:** Data sections come first, instructions/task last. Claude is more
  reliable when long context precedes the task framing.
- **D-04:** No API-mode role blocks (`<system>` / `<human>` / `<assistant>`).
  Those are Anthropic API conventions; Claude.ai's web UI does not parse them
  and they read as cosmetic noise to a human pasting the artifact in.

### Gemini artifact format

- **D-05:** Gemini gets light differentiation from the ChatGPT artifact, not a
  full restructure. Same markdown skeleton, same content shape. Differentiation
  lives in the instruction layer:
  - explicit "think step-by-step before answering" scaffolding (Gemini benefits
    from this more than ChatGPT does);
  - stronger persona framing at the top (e.g., "You are an expert MTG analyst
    with deep cEDH metagame knowledge");
  - firmer schema-strictness language to suppress Gemini's tendency to add
    surrounding prose around JSON;
  - the unified `<result>...</result>` wrapper requested for all three AIs.
- **D-06:** Honors AISEL-03 ("distinct from ChatGPT and Claude formats") via the
  instruction-section divergence and the markdown-vs-XML skeleton split. No
  separate Gemini structural layout invented.

### ChatGPT artifact format

- **D-07:** ChatGPT prompt content stays substantially the same to honor "zero
  regression on the default flow" (Phase 10 SC #4). Only change: append the
  unified `<result>...</result>` wrap instruction to the existing fenced-JSON
  request, so the parser can extract responses uniformly across all three AIs.
  Existing fenced-JSON request stays in the prompt as a fallback signal so
  ChatGPT can satisfy the schema even if it ignores the new wrapper.

### Response contract / unified import

- **D-08:** All three AIs are instructed to wrap their JSON response in
  `<result>...</result>` tags with no prose outside the tags. Server response
  parser does XML-tag extract first; on miss, falls back to the existing fenced
  JSON detection (`ExtractJsonObject` at
  `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs:408`). Single import code
  path; old saved zips with raw fenced JSON keep importing.
- **D-09:** No full-XML response output. Inner content stays JSON, parsed by
  existing `ChatGptResponseParsers.Parse*Response` methods into the existing
  typed records. Switching the response shape to XML would force rewriting the
  schema files, the parsers, and the rendering paths on Step 3 / Step 5 of all
  three pages — out of scope for this phase.

### Surface scope

- **D-10:** Ship Claude+Gemini artifact paths on all three ChatGPT analysis
  pages (`/chatgpt-packets`, `/chatgpt-deck-comparison`,
  `/chatgpt-cedh-meta-gap`). Each page's prompt builder branches on
  `request.TargetAiPlatform` to pick the per-AI builder.
- **D-11:** Extend zip round-trip to Comparison and CedhMetaGap so AI selection
  persists across upload/resume on all three pages — i.e. add a request-context
  file (analogous to `01-request-context.txt` in Packets) to the Comparison and
  CedhMetaGap zip layouts. This closes AISEL-04 fully (Phase 9 only delivered
  Packets round-trip).
- **D-12:** Filename and zip layout for Claude/Gemini artifacts unchanged —
  same `31-analysis-prompt.txt`, same `30-comparison-prompt.txt`, same content
  position. The user identifies which AI flavor the zip contains via the new
  request-context entry, not the filenames.

### Branching strategy (planner discretion, but constrained)

- **D-13:** Each page's existing prompt-build method (`BuildAnalysisPrompt`,
  `BuildSetUpgradePrompt`, `BuildComparisonPrompt`, the meta-gap equivalent)
  splits into a per-AI builder dispatched on `TargetAiPlatform`. Planner
  decides whether that's Strategy-pattern interfaces, switch expressions in a
  composing wrapper, or three separate builder classes — Claude does not need
  to micro-manage that choice. Constraint: the dispatch happens inside the
  service, not at the controller; controllers stay AI-agnostic.

### Claude's Discretion

- Exact wording and tone of the per-AI instruction layers (researcher and
  planner pick wording from current Anthropic / Google docs).
- File organization for the new per-AI prompt builders (one class, three
  classes, or strategy interface — planner's call within D-13's constraint).
- Whether the new request-context file for Comparison / CedhMetaGap zips
  carries only `target_ai_platform` or also adds the form-state fields that
  Packets persists today (Packets persists more than just AI platform — there
  are deck name, bracket, etc.). Planner decides scope of the new file based
  on what Comparison and CedhMetaGap actually need to round-trip.
- Test approach (manual round-trip check by user vs golden-file tests).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 10 inputs

- `.planning/ROADMAP.md` — Phase 10 goal + success criteria. Single source of
  truth for what "done" looks like.
- `.planning/REQUIREMENTS.md` §"v1.2 Requirements" — AISEL-02, AISEL-03,
  AISEL-04. Especially the AISEL-04 traceability row (currently "Not started"
  for Phase 9 — Phase 9 closed it for Packets only; Phase 10 must close it for
  the other two pages).
- `.planning/PROJECT.md` §"Current Milestone: v1.2 Multi-AI Prompts" —
  milestone framing.

### Phase 9 (foundation Phase 10 builds on)

- `.planning/phases/09-bracket-ux-ai-selector-foundation/09-RESEARCH.md` —
  Already maps the prompt-build entry points across the three services and
  documents how `TargetAiPlatform` flows from form → request → zip on Packets.
  Phase 10 starts where this leaves off.
- `.planning/phases/09-bracket-ux-ai-selector-foundation/09-UI-SPEC.md` — UI
  contract for the AI selector (no UI changes expected in Phase 10, but check
  if any selector-state plumbing is needed for the new round-trip work on
  Comparison / CedhMetaGap).

### Codebase maps (for researcher/planner orientation)

- `.planning/codebase/ARCHITECTURE.md` — service-tier layout, prompt-builder
  ownership.
- `.planning/codebase/STRUCTURE.md` — file/folder mapping.
- `.planning/codebase/CONVENTIONS.md` — naming, DI, test seams.

### External docs (researcher must consult current versions)

- Anthropic prompt engineering guide for Claude (XML tagging, long-input
  ordering, paste-vs-API differences). Researcher fetches the current version.
- Google AI Studio / Gemini prompt guidance (markdown handling, structured
  output via paste-in flow, persona framing). Researcher fetches the current
  version.
- OpenAI / ChatGPT prompt-engineering reference if a regression-risk question
  surfaces around the `<result>` wrapper instruction interacting with the
  existing fenced-JSON request.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- `ChatGptDeckPacketService.BuildAnalysisPrompt`
  (`DeckFlow.Web/Services/ChatGptDeckPacketService.cs:839`) — existing analysis
  prompt builder; the dispatch site for the per-AI branch.
- `ChatGptDeckPacketService.BuildSetUpgradePrompt` — set-upgrade prompt; same
  per-AI dispatch.
- `ChatGptDeckComparisonService.BuildComparisonPrompt`
  (`DeckFlow.Web/Services/ChatGptDeckComparisonService.cs:151`).
- `ChatGptCedhMetaGapService` prompt builder (location to be confirmed by
  researcher; mirrors the others' shape).
- `ChatGptPacketArtifactStore.BuildZip` (Packets), `BuildComparisonZip`,
  `BuildCedhMetaGapZip` — entry points where new request-context files for
  Comparison and CedhMetaGap will be added.
- `ChatGptRequestContextParser` + `ParsedRequestContext` — pattern Phase 9
  used for the Packets request-context round-trip; Phase 10 reuses or
  generalizes this for Comparison and CedhMetaGap zips.
- `ChatGptResponseParsers.ParseAnalysisResponse` / `ParseComparisonResponse` /
  `ParseCedhMetaGapResponse` — existing response parsers; Phase 10 inserts a
  new XML-tag extraction step in front of these (or in front of the existing
  `ExtractJsonObject` helper at
  `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs:408`).

### Established Patterns

- **Service-internal dispatch.** Phase 9's TargetAiPlatform plumbing flows
  from form → `ChatGptDeckRequest.TargetAiPlatform` → service method →
  written to `01-request-context.txt`. The per-AI prompt branching in Phase
  10 follows the same flow: the service consults `request.TargetAiPlatform`
  and selects a builder. Controllers stay AI-agnostic.
- **Request-context as round-trip envelope.** The pattern `BuildRequestContextText`
  / `ChatGptRequestContextParser` / `LoadFromZip` (Packets) is the canonical
  way to round-trip form state through the zip on this milestone. Comparison
  and CedhMetaGap need their own analogs.
- **Fence-extract response parsing.** Today's parser uses
  `ExtractJsonObject` to strip ` ```json `…` ``` ` fences before
  `JsonSerializer.Deserialize<T>`. Phase 10 inserts an XML-tag-extract probe
  in front of that without removing it.
- **Plain-text artifact files in the zip** (`.txt`, not `.xml`). D-12 keeps
  this convention; the Claude artifact's content is XML-shaped but the file
  extension stays `.txt`.

### Integration Points

- Form post → `DeckController` action → service `BuildAsync` → prompt builder
  dispatch on `TargetAiPlatform` → zip artifact write → `File(...)` response.
- Zip upload → `*Upload` controller action → `ChatGptPacketArtifactStore.LoadFromZip`
  (or per-page equivalent) → `ParsedRequestContext.ApplyTo(request)` →
  Razor view re-render with restored form state, including the AI selector.
- Response paste-back → Step 3 / Step 5 form post → `ChatGptResponseParsers.Parse*` →
  this is where the new `<result>` tag extraction lives.

</code_context>

<specifics>
## Specific Ideas

- The user's concrete preference for Claude format came up during discussion:
  flat XML skeleton with semantic domain tags (`<deck>`, `<commander>`,
  `<reference>`, `<schema>`, `<task>`), markdown allowed inside content tags,
  data first / instructions last. This was selected from a three-option
  comparison that included role-block format and tagged-with-preamble — the
  user picked the flat XML option. Researcher and planner should not revisit
  this without strong cause.
- The user's concrete preference for response handling: unified `<result>` tag
  envelope across all three AIs, with the parser falling back to existing
  fenced-JSON detection. This was the user's own engineering instinct ("only
  one importer/parser") and is the central architectural decision of this
  phase. Do not split the parser into per-AI variants.

</specifics>

<deferred>
## Deferred Ideas

- **Full-XML response pipeline** — Claude could plausibly return XML directly
  instead of JSON-inside-XML. Deferred: would force a rewrite of the response
  schema definitions, the response parsers, and the Step 3 / Step 5 rendering
  paths on all three pages. Massive blast radius for marginal robustness gain.
  If a real reliability problem surfaces post-ship, revisit as its own phase.
- **API-mode integration** (Anthropic Messages API, Gemini API, OpenAI API).
  Deferred: this milestone stays paste-into-web-UI. API integration is a
  separate product direction with its own auth, billing, and rate-limit
  concerns. Capture as a v1.3+ candidate.
- **Per-AI golden-file tests for prompt content.** Researcher/planner may
  recommend manual round-trip verification only for this phase. If they want
  golden tests, they're additive scope and the planner can tag them as
  optional.
- **AI-selector keyboard hint** (e.g., `<kbd>1</kbd>` shortcuts to switch AI).
  Came up tangentially as a mental aside. Not in v1.2 scope.

</deferred>

---

*Phase: 10-Claude + Gemini Artifact Optimization*
*Context gathered: 2026-05-09*
