# Domain Pitfalls — DeckFlow v1.5: Deck Primer Generator + Content KB Integration + Housekeeping

**Domain:** Adding (a) large multi-section AI prompt-generator workflow, (b) curated-content RAG-style injection into existing analysis prompts, and (c) doc-gate widening + third LLM backend to an existing ASP.NET 10 / Razor / prompt-artifact app.
**Researched:** 2026-06-03
**Overall confidence:** HIGH for pitfalls grounded in codebase inspection and prior milestone post-mortems; MEDIUM for AI output-quality pitfalls (drawn from project history + known LLM behavior patterns)

> **Scope boundary:** This file covers v1.5 pitfalls ONLY. v1.4 pitfalls (YouTube API 403, Whisper cost race, Postgres pool starvation, admin CSS bleed, WDG-04 modal, etc.) are archived in the prior PITFALLS.md and are not repeated here — those concerns are fully shipped.

Pitfalls ordered by **likelihood × impact** (highest first). Each is calibrated to THIS system, not a generic warning.

---

## Critical Pitfalls

### Pitfall 1: Primer prompt blows the Gemini paste cap before users ever try it

**What goes wrong:**
The primer workflow targets ChatGPT first, but the AI selector on every existing workflow page shows all three AIs. A user selects Gemini, generates a primer with 20+ sections enabled, and the output is silently truncated in the Gemini web UI. The section marked "## COMBO LINES" may be cut mid-block. The AI never sees the combo ground truth, generates speculative combos for the entire section, and the user pastes a hallucination-dense prompt with no visible warning.

The existing analysis prompt for a well-equipped Commander deck is already ~30–50KB. A 20-section primer with Spellbook combo data, EdhTop16 archetypes, and category breakdowns will routinely hit 60–100KB — 2–3× the existing size. Gemini web UI's effective paste cap is in the 30–60KB range depending on user tier (confirmed in .planning/RETROSPECTIVE.md v1.2 Key Lesson #4: "Web UIs have paste caps that the model's context window doesn't").

**Why it happens:**
The primer is a NEW workflow with a NEW section-combinatorics axis. The paste-cap risk was identified for the analysis workflow (and addressed by flag-gating Gemini), but the primer's 31-section model multiplies the output size in a way the existing prompts don't. Nobody will measure the generated prompt length until a user files a truncation bug.

**How to avoid:**
- Measure generated prompt size during the spike phase (`spike-combo-data-to-primer-grounding`). For a representative cEDH deck with all 31 sections enabled and EdhTop16 data injected, record the byte count of the generated prompt.
- Add a server-side `PromptSizeWarning` to the primer result: if `promptText.Length > 45_000` bytes, surface an inline warning on the UI ("This primer is large — paste into ChatGPT or Claude; Gemini web may truncate it").
- Primer workflow should default Gemini radio to DISABLED (same flag-gate pattern as existing `DECKFLOW_GEMINI_ENABLED`). The primer is even larger than the analysis packet. Do not lift the flag-gate for the primer until Gemini direct-API integration lands (v1.6+).
- Section preset defaults matter: cEDH preset enabling 24 of 31 sections is the most dangerous configuration. Measure that specifically.

**Warning signs:**
- Generated primer text file in the zip is larger than the existing `31-analysis-prompt.txt` for the same deck.
- Any user report of "ChatGPT gave me incomplete combos" — the AI never saw the grounded data.
- Spike UAT round-trip where AI-generated combo lines include cards NOT in the deck — ground truth was truncated.

**Phase to address:**
Spike phase (`spike-combo-data-to-primer-grounding`) — measure before planning the primer service. Primer packet service implementation phase — add `PromptSizeWarning` to the result record. Do not leave this to verification.

---

### Pitfall 2: AI hallucinates combo lines because grounded and speculative sections are not visibly fenced in the emitted prompt

**What goes wrong:**
The design intent is clear (seed + design note): inject Spellbook combos as "ground truth" and ask AI to extend with speculative synergies under a "speculative — verify these" heading. But when the prompt is built, the fencing gets muddled. Common failures:
- The "speculative" heading is added as markdown `###` but the AI treats the whole combo section as one block and freely mixes invented lines with Spellbook-verified lines.
- The primer consumer (user) reads the AI output and cannot tell which combo lines came from Spellbook and which the AI invented. They post an incorrect combo line as fact on Moxfield.
- "Almost-included" combos (cards 1 missing from deck) are presented as "your deck can do this" rather than "add X to unlock this."

This is specifically dangerous because `CommanderSpellbookService.FindCombosAsync` returns `null` on API failure (graceful-degradation pattern confirmed at `DeckAnalysisPacketService.cs:563-564`). If the service returns null, the primer prompt may silently drop the grounded section entirely and leave only the speculative ask — the AI invents all combos with no reality anchor.

**Why it happens:**
Prompt construction relies on `StringBuilder` string concatenation with markdown headers. There is no structural enforcement that the grounded block appears before the speculative block, or that a null combo result suppresses the speculative ask rather than expands it.

**How to avoid:**
- Model the combo section as TWO structurally distinct prompt blocks: `KnownCombosBlock` (present only when `comboResult != null && comboResult.IncludedCombos.Count > 0`) and `SpeculativeComboAsk` (always present but explicitly labeled). Make each block a separate method so their independence is testable.
- When `comboResult` is null: emit the speculative ask with a preamble "Commander Spellbook is unavailable; treat all combo suggestions as speculative." Do NOT silently omit the null-state disclosure.
- The speculative ask MUST use the word "speculative" in the section heading injected into the prompt — not just in a comment in code. Test: the generated prompt text must contain the literal string "speculative" adjacent to the AI combo-extension instruction.
- "Almost-included" combos must be labeled "NEAR-COMBO (missing: [card])" — never listed inline with confirmed combos.
- Unit test: `BuildPrimerPrompt_NullComboResult_EmitsSpeculativeDisclosure` — assert the generated text contains the null-state disclosure message, NOT a section that looks like confirmed combos.

**Warning signs:**
- Spike UAT: AI-generated primer includes a combo line using a card not in the decklist.
- Primer prompt text file contains a "## Combo Lines" section with no "speculative" heading anywhere following it.
- `comboResult` is null in the service but the generated primer text does not contain a null-state disclosure.

**Phase to address:**
Spike phase — validate combo data richness AND fencing strategy before committing to the full primer implementation. Primer service implementation phase — structural combo block separation as a first-class design constraint, not a later cleanup.

---

### Pitfall 3: Content KB injection injects irrelevant content (tag-mismatch relevance failure)

**What goes wrong:**
The KB tags content by archetype/strategy/format/bracket/card-category (per `ContentTagVocabulary`). When injecting into deck-analysis prompts, the relevance matching queries: "find KB entries tagged with the deck's apparent archetype + bracket." But the deck's apparent archetype is inferred from Scryfall Tagger categories, which are functional (ramp/draw/removal/win-cons) rather than strategic (voltron/aristocrats/combo/control). The mismatch causes:
- A reanimator deck retrieves stax content (both tagged "control" but by different semantic paths).
- A cEDH combo deck retrieves casual combo content (both tagged "combo").
- A deck with no clear tagger archetype match retrieves nothing — empty injection — silently.

The silent empty-injection case is actually the safest failure: the prompt continues without KB content and the user gets the same output as before. The dangerous case is the wrong-content injection: the AI confidently applies advice for the wrong archetype.

**Why it happens:**
KB tags were designed for content discoverability (v1.4 KB tagging model), not for deck-to-content relevance matching. The tag vocabulary overlap between "what Tagger assigns" and "what KB content authors tag their videos" was never validated. This is the primary open risk called out in `.planning/notes/deck-primer-prompt-design.md`: "Reliable category classification for mulligan / engine-breakdown buckets → research question logged."

**How to avoid:**
- Before wiring KB injection into any prompt: audit the live KB content (post v1.4 harvest) to understand the actual tag distribution. How many entries are tagged "combo"? "control"? "cEDH"? If 80% are tagged "combo" and "cEDH" because those are the most-published MTG content topics, relevance matching will over-retrieve for those and under-retrieve for everything else.
- Relevance matching must use AT MINIMUM two tag dimensions in AND (not OR): bracket + at least one archetype/strategy tag. Single-dimension matching is too broad.
- Add a `RelevanceScore` threshold: only inject KB content scoring above a minimum threshold (e.g., ≥2 matching tag dimensions out of queried dimensions). Surface the match count in the prompt header so the AI knows how curated the content is.
- Hard cap on injection length: KB injection must never exceed N characters (recommend 4,000 chars = ~3KB) per the prompt budget analysis (see Pitfall 5). Prefer excerpt clips over full summaries when space is constrained.
- A zero-match result must be handled as "no KB injection, no disclosure needed" — not as an error, and not as an empty "## What Experts Say" section with no content (which confuses the AI).

**Warning signs:**
- KB injection output contains a "What Experts Say" section mentioning stax or hatebear strategies for a pure-combo deck.
- The KB injection section is empty but the section header is still emitted in the prompt.
- All deck analysis prompts receive the same KB injection regardless of deck archetype.

**Phase to address:**
Content KB integration phase — relevance matching design must be specced before the injection service is implemented, not derived from the prompt builder. Run the tag-distribution audit before writing any matching code.

---

### Pitfall 4: Prompt budget competition between KB injection and existing analysis sections causes silent truncation at the paste destination

**What goes wrong:**
The deck-analysis prompt already includes: deck context, bracket/format data, reference text (Scryfall card data), combo reference (Spellbook), questions, and formatting instructions. Adding KB injection pushes total prompt size over the paste cap for at least one AI target. The AI receives a truncated prompt missing the tail — usually the questions section or the format instructions — and returns a structurally wrong response.

This is silent: the server emits the full prompt into the zip file, the user pastes, the AI truncates the input without warning in the web UI, and the user gets a partial analysis. There is no server-side signal that truncation happened.

Estimated additive risk: existing analysis prompt for a 99-card Commander deck with combos is ~35-50KB. Adding 4KB of KB content pushes the total to ~39-54KB. ChatGPT (100K token context, ~400KB) is safe. Claude (200K context) is safe. Gemini web UI at 30-60KB threshold is the risk.

**Why it happens:**
Each feature (combos, KB injection, bracket guidance) independently adds content to the prompt without a global budget authority. There is no prompt-length measurement at the packet service level that would signal "this deck's prompt is near-cap."

**How to avoid:**
- Add `PromptLengthBytes` to the `DeckAnalysisPacketResult` record alongside the existing fields. Compute it at packet build time. Display it (or a size tier: S/M/L/XL) in the UI so users can see when a prompt is large.
- Establish a prompt budget hierarchy: deck context → combo reference → questions → KB injection. KB injection is LAST and can be truncated or omitted if total prompt exceeds a soft cap (recommended: 50,000 chars for Gemini safety; 150,000 chars for ChatGPT/Claude soft cap). Implement as: measure size before injection; add KB content only if remaining budget allows.
- For the initial KB integration phase, KB injection is additive to the ChatGPT/Claude variants only (Gemini is already flag-gated). This naturally limits the risk surface.
- Test: for a representative large Commander deck (100 cards, 5+ Spellbook combos, all questions selected), measure the full analysis prompt size and assert it is below 50,000 chars after KB injection.

**Warning signs:**
- Generated zip `31-analysis-prompt.txt` exceeds 50KB.
- User reports "Claude didn't answer any of my questions" — questions section was truncated.
- The "What Experts Say" KB section appears in the zip but not in the AI response.

**Phase to address:**
Content KB integration phase — prompt budget authority must be designed before KB injection is added to any prompt builder. The budget check must be a first-class step in the packet service, not a post-ship observation.

---

### Pitfall 5: PacketArtifactStore `PrimerAllowedNames` whitelist not added — zip round-trip silently drops all primer artifacts

**What goes wrong:**
`PacketArtifactStore` uses three separate `HashSet<string>` allowlists for artifact names: `PacketAllowedNames`, `ComparisonAllowedNames`, `CedhAllowedNames` (verified at `PacketArtifactStore.cs:27-70`). Any entry name NOT in the allowlist is silently dropped on `ReadEntries` (line 598). The primer workflow adds a new artifact set (e.g., `31-primer-prompt.txt`, `10-primer-decklist.txt`). If the implementer adds `BuildZipPrimer` and `LoadZipPrimer` methods but forgets to add `PrimerAllowedNames` and instead reuses `PacketAllowedNames`, the primer-specific artifacts are silently dropped on reload. The "re-upload existing primer" UX shows an empty or wrong state with no error.

This is a silent data loss: `ReadEntries` returns a dict with the known-good analysis artifacts (if any overlap) and silently omits the primer ones. No exception is thrown.

**Why it happens:**
The allowlist is a security/safety measure (prevents reading arbitrary zip entries), but its enforcement is silent. The `PacketAllowedNames` set is a `static readonly` — it is not extensible at runtime. A new workflow requires a new named set. Easy to miss during implementation because `dotnet build` is clean and the feature "works" until round-trip is tested.

**How to avoid:**
- Make `PrimerAllowedNames` the FIRST task in the primer artifact store implementation, not an afterthought. Add it alongside `BuildZipPrimer` in the same commit.
- SC for primer workflow must include: "Re-upload a downloaded primer zip; verify all sections and selections are restored exactly." This tests the round-trip, which is the only path where the allowlist gap manifests.
- Add a unit test: `BuildAndLoadZipPrimer_RoundTrip_AllArtifactsPreserved` — build a primer zip, load it, assert every expected artifact is present with matching content.
- Plan-checker rule: any PR adding a new workflow (non-analysis, non-comparison, non-cedh) must show a new `*AllowedNames` hashset in `PacketArtifactStore` or explain why reuse is correct.

**Warning signs:**
- Primer upload path loads a form with empty section selections even though the download contained selections.
- `ReadEntries` log shows 0 entries loaded for a known-good primer zip.
- Integration test for primer upload fails to restore any state.

**Phase to address:**
Primer packet service implementation phase — allowlist addition is mandatory alongside `BuildZipPrimer`/`LoadZipPrimer`. Verification SC must include round-trip test.

---

### Pitfall 6: `get; init;` → `get;` auto-conversion on new primer/KB result record types silently breaks JSON serialization

**What goes wrong:**
New C# records are added for the primer workflow (`DeckPrimerRequest`, `DeckPrimerResult`, `PrimerSectionSelection`) or for KB injection results. Codex's formatting pass (or an IDE Format Document run) converts `{ get; init; }` to `{ get; }` on init-only properties. `System.Text.Json` in .NET 9+ silently skips get-only properties during serialization. The JSON artifact written to the zip is missing fields. On re-upload, the state is not restored. No exception; no warning.

This has already broken `EdhTop16Client` deserialization before (confirmed in `CLAUDE.md` constraints: "never auto-convert `{ get; init; }` to `{ get; }` (System.Text.Json silently skips get-only properties in .NET 9+ — has broken `EdhTop16Client` deserialization before)").

**Why it happens:**
Codex executor may not have the constraint fully internalized on every dispatch. The failure is silent (no build error, no test failure unless a round-trip test exists for the specific record). Each new milestone introduces new record types that are fresh targets for this regression.

**How to avoid:**
- Every CONTEXT.md for primer and KB phases must include the explicit constraint verbatim: "Never convert `{ get; init; }` to `{ get; }` — System.Text.Json silently skips get-only properties in .NET 9+."
- Plan-checker grep gate: `grep -rn "{ get; }" DeckFlow.Web/Models/ DeckFlow.Core/Models/` — any new `get;` without `init;` on a record property that is also in a serialized DTO is a BLOCKER.
- Add `ResultContractTests`-style serialization round-trip tests (already established in the codebase for `AiPlatform`) for every new request/response record used in primer zip artifacts.
- The diff review for every primer/KB phase must include a visual check: are there any `{ get; }` on record properties that were `{ get; init; }` in the source?

**Warning signs:**
- Primer zip file `31-primer-prompt.txt` exists but section selection artifacts are empty/missing after reload.
- JSON artifact contains `{}` or partial object for a record type with multiple properties.
- Unit test round-trip passes but manual round-trip via UI fails.

**Phase to address:**
Every primer and KB phase. This is a cross-cutting constraint, not a single-phase fix. Reinforce in CONTEXT.md for each phase.

---

### Pitfall 7: Stale Content KB content injected into analysis prompts (prod flag OFF → content harvested months ago → outdated meta advice)

**What goes wrong:**
The `content.kb.enabled` flag is currently OFF in production (confirmed: v1.4 milestone audit tech_debt). When the KB integration phase flips it ON, the content in the KB was harvested at v1.4 time. MTG meta evolves quickly (ban list changes, new set releases, dominant strategy shifts). A user running a deck-analysis prompt in v1.5 receives KB content that references cards that are now banned or archetypes that have fallen out of the meta. The AI, instructed to treat KB content as expert guidance, applies stale advice confidently.

This is uniquely dangerous because the KB content is presented as curated expert knowledge — it has more authority in the prompt than generic AI inference.

**Why it happens:**
Content harvest is manual (no scheduler in v1.4). If no harvest is run between v1.4 ship and v1.5 KB integration, the content is stale by definition. The system has no content-freshness signal to the prompt builder.

**How to avoid:**
- Before flipping `content.kb.enabled` to ON in prod, trigger a fresh harvest run to refresh the KB content. This is an ops prerequisite for the KB integration phase, not an optional step.
- Add a `ContentHarvestedAt` field to the KB injection prompt header: "The following expert content was harvested [date]; content may not reflect the current meta." This honest disclosure lets the AI weight the content appropriately.
- The KB integration phase success criteria must include: "KB content was harvested within [N] days before the integration UAT run." Stale-by-default is not acceptable for the initial prod flip.
- Add a staleness warning in the Admin Flags UI next to the `content.kb.enabled` toggle: "Last harvest: [date]. Flip only after a recent harvest."

**Warning signs:**
- KB injection references a card that was banned more than 30 days ago.
- The "What Experts Say" panel references a meta archetype that has not appeared in EdhTop16 in over 90 days.
- `ContentHarvestedAt` date in the prompt header is more than 60 days before the analysis date.

**Phase to address:**
Content KB integration phase — freshness disclosure added to the injection prompt AND the Admin Flags UI staleness warning added before `content.kb.enabled` can be flipped. Run a fresh harvest as the first step of the integration phase UAT.

---

### Pitfall 8: Doc-warning gate widened to DeckFlow.Core before the 186-site backfill is complete — build breaks

**What goes wrong:**
The current `.editorconfig` state (verified at lines 93-98 and 111-115): CS1591 severity is `none` globally, then overridden to `warning` in `[DeckFlow.Web/**.cs]` only. Widening to Core means changing the global `none` to `warning` OR adding a `[DeckFlow.Core/**.cs]` section. Either way, if done before backfilling the 186 undocumented sites, the build immediately emits 186 warnings. If `GenerateDocumentationFile=true` is already set on `DeckFlow.Core.csproj` (verified: it is, at line 7), those warnings are live the moment the severity changes.

Scenario A (worst): Codex changes the editorconfig global severity to `warning` as step 1. Every CI run now fails on 186 warnings. Other developers (Codex dispatches for other features) cannot get a clean build. The feature is a multi-session blocker.

Scenario B (moderate): Backfill is done in one pass, gate is widened in the same commit. Looks clean. But raw-string literals in Core (e.g., SQL constants in `SqliteRelationalDialect.cs`) get re-indented by Codex's formatter when touching nearby files. The literal value changes, the query breaks, the build is clean but runtime is broken.

**Why it happens:**
The v1.4 lesson (Pitfall 8 in the prior PITFALLS.md) established the sequencing fix for DeckFlow.Web: backfill FIRST, gate LAST. The same lesson must be applied to DeckFlow.Core but the prior fix was Web-only. The Core backfill is 186 sites — more than the 88 Web sites — and Core contains more raw-string literals (SQL DDL constants, prompt templates).

**How to avoid:**
- Same sequencing discipline as v1.4 Phase 23: backfill ALL 186 Core doc-comment sites BEFORE touching the editorconfig.
- Backfill must be split into multiple plans (each plan targeting a namespace: `Models/`, `Parsing/`, `Knowledge/`, `Integration/`, `Storage/`, `Content/`). Do not try to backfill 186 sites in a single Codex dispatch — the diff will be unreviable.
- The editorconfig change (widening the gate) is the LAST commit of the last plan. SC: "`dotnet build -warnaserror:CS1591` from a clean `obj/` returns 0 errors and 0 warnings."
- CONTEXT.md for every Core backfill plan must include: "Do not touch the `.editorconfig` in this plan. Do not re-indent raw-string literals. Touch only lines that need a `<summary>` tag added."
- Verify Razor-generated CS1591 behavior in Core (Core has no Razor files, so this is less of a concern than in Web). The gate-widen for Core is safer than the Web case.

**Warning signs:**
- Any plan that has both "add doc-comments" and "widen editorconfig gate" in the same task list — those must be separate plans.
- Build log showing CS1591 warnings on files that have not yet received backfill.
- Diff for a doc-comment plan shows changes to SQL string literals or indentation of raw strings.

**Phase to address:**
Housekeeping phase for Core doc backfill. Must use the same two-SC structure as Phase 23: SC-1 = "all 186 sites documented with NoWarn still suppressed"; SC-final = "NoWarn widened AND `dotnet build -warnaserror:CS1591` clean from clean `obj/`."

---

### Pitfall 9: KB-12 codex distill backend adds a string literal provider instead of extending `LlmDistillationProviderFactory`

**What goes wrong:**
`LlmDistillationProviderFactory` currently has three string constants: `"openai"`, `"claude"`, `"codex"` (verified at `LlmDistillationProviderFactory.cs:13-15`). The `"codex"` case throws `NotSupportedException` with a deferral message. KB-12 is the deferred phase to implement it. The risk: implementer adds `"codex"` handling by copy-pasting the `"claude"` CliLlmDistillationService pattern without reading the existing factory structure, and adds new string literals in `CommandRunners.cs` call sites rather than routing through the factory constant. Alternatively, the implementer adds a fourth provider (`"codex2"` or `"anthropic-codex"`) as a new string, bypassing the factory constant.

Unlike the `AiPlatform` value-object risk (Pitfall 5 in the prior PITFALLS.md), this is a CLI-layer factory, not a web-layer registry. But the pattern failure is the same: stringly-typed extension instead of the established factory extension point.

**Why it happens:**
The factory comment ("deferred to Phase 21.3 / KB-12") is in code but the resolver logic pattern is not enforced by a type system. Any Codex dispatch that has `LlmDistillationProviderFactory.cs` in scope could add a string case without realizing the `LlmDistillationServiceTests` tests need a corresponding extension. The test `LlmDistillationProviderFactoryTests` (verified to exist in `DeckFlow.Core.Tests`) is the regression guard, but only if the implementer runs it.

**How to avoid:**
- CONTEXT.md for KB-12: "The `codex` case is already stubbed in `LlmDistillationProviderFactory.cs` at line 49-53. Replace the `NotSupportedException` with a real implementation. Do NOT add new string constants anywhere. The factory constant `CodexProvider = "codex"` is already defined."
- Plan task should be explicit: "In `LlmDistillationProviderFactory.cs`, replace the `throw new NotSupportedException(...)` in the `codex` branch with `return new CliLlmDistillationService(CodexProvider);`" — leaving nothing to creative interpretation.
- Add a test: `LlmDistillationProviderFactory_Codex_ReturnsCliBackend` — the existing test class structure supports this directly.
- Code review gate: the diff for KB-12 must show ONLY the `NotSupportedException` block replaced, no new string literals elsewhere in the codebase.
- The "untrusted-input read boundary" concern (noted in v1.4 audit tech_debt as the reason KB-12 was deferred) must be explicitly addressed in the spec: what inputs does the codex CLI receive, and how are they validated before shelling out?

**Warning signs:**
- Diff for KB-12 shows new string literals in `CommandRunners.cs` referencing a codex provider name.
- A new `ILlmDistillationService` implementation is added without a corresponding `LlmDistillationProviderFactory` entry.
- `LlmDistillationProviderFactoryTests` does not gain a new test case in the KB-12 diff.

**Phase to address:**
KB-12 housekeeping phase. Bounded fix: replace one `throw` with a `return`. The real work is the untrusted-input boundary validation.

---

### Pitfall 10: Section-combinatorics explosion: 31 sections × 2 AIs × 2 bracket presets = undertested surface

**What goes wrong:**
The primer has 31 sections, two preset configurations (cEDH / Casual-Upgraded), per-section overrides, and at least two AI targets (ChatGPT, Claude). The combinatoric space is enormous. In practice, the prompt builder will have conditional branches: "if section 24 selected AND bracket == cEDH, inject EdhTop16 archetypes; else if bracket <= 4, inject generic buckets; else omit." A bug in any conditional emits a wrong or missing section without a visible error. Common failure modes:
- Section 24 (Must-Counter Guide) appears in Casual/Upgraded output because the bracket preset conditional is inverted.
- Section 11 (Combo Lines) renders correctly in ChatGPT variant but the Claude variant's XML-structure format wraps the Spellbook ground-truth block inside a speculative container (copy-paste error in the Claude variant builder).
- Section 22 (Matchup Overview) for a bracket 3 deck shows EdhTop16 named archetypes instead of generic buckets because the bracket routing condition uses `>= 5` instead of `== 5`.

**Why it happens:**
The existing prompt builders (Analysis, Comparison, MetaGap) each have three AI variants (ChatGPT, Claude, Gemini) that are intentionally duplicated (CLAUDE.md memory note: "prompt variants decoupled — never extract shared guidance"). The primer adds another layer: 31 sections × 2 preset modes × 3 AI variants = ~186 possible section-render paths. Manual verification covers a tiny fraction of this space.

**How to avoid:**
- Build a `PrimerSectionRenderTests` unit test class that parameterizes over `[SectionId, BracketPreset, AiPlatform]` tuples covering at least:
  - cEDH preset: assert sections 24/25 ARE present, section 26 is NOT present.
  - Casual/Upgraded preset: assert sections 24/25 are NOT present, section 26 IS present.
  - Both presets: assert section 11 (Combo Lines) contains the grounded block when `comboResult != null`.
  - Claude variant: assert the XML structural markers are present in combo and matchup sections.
  - Bracket routing: bracket == 5 → EdhTop16 data present; bracket < 5 → generic bucket text present, EdhTop16 absent.
- Each bracket-routing condition in the primer builder must have an inline comment explaining the condition: "Why: bracket 5 is cEDH; buckets 1-4 use generic strategies (no EdhTop16)." This is the CLAUDE.md "Why:" comment convention applied to conditional logic.
- The primer workflow spike should produce a "section matrix" artifact: a simple table of which sections appear under which bracket/AI combinations, to use as a test oracle.

**Warning signs:**
- The primer output for a bracket 3 deck contains named archetypes like "Tymna-Thrasios Food Chain" (EdhTop16 data) — should only appear for bracket 5.
- Section 24 (Must-Counter Guide) appears in the Casual/Upgraded output.
- The Claude variant's combo section is missing the `<grounded-combos>` XML wrapper or the `<speculative-combos>` separator.

**Phase to address:**
Primer packet service implementation phase. Section-routing tests must be written alongside the conditional logic — not deferred to verification.

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Reusing `PrimerAllowedNames = PacketAllowedNames` in zip store | Faster to implement; no new set needed | Silent data loss on primer artifact round-trip — missing artifact names dropped without error | Never. New workflow always needs its own named set. |
| Single `DeckPrimerPacketService` handling both ChatGPT and Claude variants inline (no `IPrimerPromptVariant` registry) | Simpler for first ship | Violates the `AiPlatform` OCP pattern established in Phase 15; adding a 4th AI requires surgery to a large service class | Acceptable IF the primer is explicitly scoped to ChatGPT-only in v1; must add the registry pattern before enabling Claude or Gemini |
| KB injection using simple substring search for relevance (e.g., `if deckText.Contains("combo")`) | Zero new query logic needed | Extremely noisy false positives; a deck with combo pieces but control strategy gets combo-focused KB content | Never. Relevance must use the structured `ContentTagVocabulary` dimensions from the KB index. |
| Emitting KB content directly from raw distilled markdown without length-checking | Simpler injection logic | A single long KB artifact overflows the prompt budget and crowds out combo/question sections | Never. KB injection must check remaining prompt budget before appending. |
| Widening the editorconfig doc gate in the same commit as the first 50 Core backfill doc-comments | One commit is cleaner | Build fails on the remaining 136 undocumented sites immediately; blocks CI | Never. Gate must widen in the final commit only. |

---

## Integration Gotchas

Common mistakes when connecting to existing services in this specific system.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| `CommanderSpellbookService` in primer | Treat non-null result as guaranteed; skip null check | `FindCombosAsync` returns `null` on API failure (graceful degradation). Primer builder MUST handle null: emit speculative-only disclosure. |
| `EdhTop16Client` in primer matchup routing | Call it on every primer request regardless of bracket | Only call when bracket == 5 (cEDH). Brackets 1–4 use hardcoded generic strategy buckets. Use the existing `MetaGapRequest.TargetCommanderBracket` routing pattern. |
| `ContentArtifactParser.SplitHeader` for KB content | Assume all KB entries have well-formed front matter | `SplitHeader` returns an empty header dict (not an exception) when `---` delimiter is missing. KB injection code must handle empty-header entries gracefully. |
| `PacketArtifactStore` for primer zip | Extend `PacketAllowedNames` with primer entry names | Use a new dedicated `PrimerAllowedNames` set. `PacketAllowedNames` is a `static readonly` not designed for extension; cross-workflow name pollution is a security/safety concern. |
| `IFeatureFlagCache` for KB enabled check | Check flag once at service construction time | Flag is designed to be checked per-request (runtime togglable). Check `IFeatureFlagCache.IsEnabled("content.kb.enabled")` per packet-build call, not at DI resolution time. |
| `ContentKbArtifactPathResolver` for KB content lookup | Assume `ContentBase` always resolves to a non-empty content-kb dir | Resolver logs a warning and falls back to `ContentRootPath` when `content-kb` directory is absent. KB injection service must handle the "no content available" state gracefully (return null injection, no exception). |

---

## Performance Traps

Patterns that work for small decks but degrade at scale.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Loading all KB content entries for relevance matching on every primer/analysis request | Slow first request as KB grows; 512MB RAM cap pressure | Cache the KB slim index in `IMemoryCache` (TTL ~5 min); load once, match in-memory. The v1.4 KB design includes a "slim Postgres index" for exactly this. | When KB has 500+ entries and every request scans them. |
| Injecting full distilled markdown summaries (can be 2–10KB each) per KB entry | Prompt budget blown by a single high-quality KB entry | Prefer injecting excerpt clips (timestamped sub-sections) over full summaries. Clip excerpts are already a first-class artifact in the v1.4 KB schema. | Immediately, for any deck with ≥1 long-form KB match. |
| Running `FindCombosAsync` AND `EdhTop16Client` on every primer build regardless of selected sections | Unnecessary latency when combo/matchup sections are deselected | Check `selectedSections.Contains(SectionId.CoreComboLines)` before calling `FindCombosAsync`. Mirror the existing `AnalysisQuestionCatalog.RequiresComboLookup` pattern at `DeckAnalysisPacketService.cs:562-564`. | Every primer build for a user who deselects those sections — adds 1-2s latency unnecessarily. |
| Building all 31 section text blocks then filtering | Simple builder logic | Build only selected sections — avoid generating text for deselected sections even if it seems faster to build-then-filter | Not a production problem at 31 sections, but establishes the wrong pattern for maintainability. |

---

## Security Mistakes

Domain-specific security issues beyond general web security.

| Mistake | Risk | Prevention |
|---------|------|------------|
| KB content injected into prompt without sanitization | If KB content contains prompt-injection text (e.g., "Ignore previous instructions and..."), it could influence AI behavior for the deck-analysis response | KB content goes through the Markdig `DisableHtml()` pipeline before injection (same as help content). Additionally: KB content is admin-curated, so the threat surface is lower than user-submitted content. Apply the existing HelpContentService sanitization pattern. |
| Primer section selection submitted via form without server-side validation | User can craft a POST with 31 sections all enabled including bracket-restricted sections (e.g., section 24 for bracket 1) | Server-side: re-validate selected sections against bracket × section compatibility matrix. Do not trust the client's preset-application logic. The server must enforce "section 24 is only valid for bracket 5." |
| `content.kb.enabled` flag bypassed by direct service injection | A future phase wires KB injection directly without checking the flag | Always check `IFeatureFlagCache.IsEnabled("content.kb.enabled")` as the outermost gate in the KB injection service, before any DB or filesystem access. Make the gate check the first statement in `InjectKbContentAsync`. |

---

## UX Pitfalls

Common user experience mistakes specific to this domain.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| 31 sections displayed as a flat checklist | User sees a wall of checkboxes; ignores it; selects nothing; gets a useless primer prompt | Collapsible group design as specified in seed (Identity/Combos/Gameplay/Matchups/Maintenance). The UX design is already decided — do not simplify to a flat list during implementation. |
| Section deselection not persisted in zip round-trip | User customizes sections, downloads, re-uploads → all sections reset to preset defaults | Section selection state must be stored in the primer zip (separate artifact, e.g., `20-primer-selections.json`) and restored on load. Test this round-trip explicitly. |
| Combo section shows "No combos found" with no explanation | User thinks their deck has no combos; Commander Spellbook may be down | When `FindCombosAsync` returns null, display "Commander Spellbook lookup unavailable — AI will suggest combos speculatively." When it returns an empty list, display "No verified combos found in Commander Spellbook for this decklist." Two distinct states, two distinct messages. |
| "What Experts Say" panel shown with empty content | Confuses users; suggests a bug | Only render the panel when KB content was injected. If KB flag is OFF or no relevant content found, omit the panel entirely. An empty panel is worse than no panel. |
| Primer prompts labeled "ChatGPT prompt" in UI copy | Misleads users about AI target | Primer tab follows the same AI-agnostic naming convention established in v1.3 (RENAME-01..03). Tab label: "Primer Prompt." Download filename: `deck-primer-{commander}.zip`, not `chatgpt-primer-*.zip`. |

---

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Primer round-trip:** Primer prompt generates successfully — verify the zip round-trip (re-upload the downloaded zip, check section selections are restored).
- [ ] **Combo null handling:** Primer works when Spellbook is live — verify behavior when `CommanderSpellbookService.FindCombosAsync` returns null (call with a malformed deck or in a network-isolated test).
- [ ] **Bracket routing:** Primer produces correct output for bracket 5 — verify a bracket 2 deck gets generic buckets, NOT EdhTop16 archetypes, in sections 22/23.
- [ ] **KB injection with no content:** KB integration works for a deck with strong archetype signal — verify a deck with ambiguous/no-match tags receives no KB injection without breaking the packet build.
- [ ] **Prompt size gate:** Analysis prompt produces correct output — verify that KB injection does not push the total prompt over 50KB for a large deck with combos and all questions.
- [ ] **Content freshness:** KB content.kb.enabled flag is flipped — verify a fresh harvest was run within the last 30 days before the flag is enabled in production.
- [ ] **Doc gate build clean:** Core doc-comments are backfilled — verify `dotnet build -warnaserror:CS1591` from a **clean** `obj/` directory returns 0 warnings (not just a cached build).
- [ ] **KB-12 codex provider:** Codex distill backend is implemented — verify `DECKFLOW_LLM_PROVIDER=codex` resolves to a working `CliLlmDistillationService` instance, and the existing `"codex"` `NotSupportedException` is gone.

---

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Primer paste-cap truncation reported by user | LOW | Add `PromptSizeWarning` to result record (additive, no behavior change). Disable Gemini for primer (flag already exists). Deploy in next push. |
| AI hallucinated combo lines (grounded/speculative fence failure) | MEDIUM | Re-examine primer prompt text in the user's zip artifact. If ground truth is present but AI ignored it, add stronger fencing language. If ground truth was absent (null comboResult), add null-state disclosure. Either fix is prompt-text-only — no schema change. |
| KB injection injects irrelevant content | MEDIUM | Add minimum-threshold tag-match requirement (query change only). Or disable KB injection temporarily by flipping `content.kb.enabled` OFF in Admin Flags (zero-downtime). |
| Primer zip round-trip drops section selections | MEDIUM | Add `PrimerAllowedNames` with missing entry names. Existing zips with the bug cannot be fixed (re-generate). New zips work correctly. |
| Core doc gate widened prematurely (build breaks) | MEDIUM | Revert the `.editorconfig` change. Restore suppression. Complete backfill. Re-widen. No data loss; build recovers immediately on revert. |
| `get; init;` → `get;` regression on new records (JSON silent drop) | HIGH | Add missing `init;` keyword (1-character change per property). If already deployed, stale zip artifacts will reload incorrectly until user re-generates. Cannot fix in-place zips. New zips work correctly after the fix. Test coverage prevents recurrence. |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| P1: Primer paste-cap blowout | Spike phase (measure size) + Primer service (size warning) | Assert primer text < 50KB for cEDH full preset |
| P2: Hallucinated combo lines (grounded/speculative fence failure) | Spike phase (fence strategy) + Primer service (structural blocks) | Unit test: null comboResult → disclosure text present; non-null → grounded block present before speculative |
| P3: Irrelevant KB content injection | KB integration (tag-match design before code) | Unit test: deck with zero-match tags → no KB injection, no error |
| P4: Prompt budget competition (KB + existing sections) | KB integration (budget hierarchy + measurement) | Integration test: large deck with all questions + KB injection < 50KB |
| P5: `PrimerAllowedNames` missing — silent zip data loss | Primer packet service (allowlist first commit) | Round-trip unit test: build zip, load zip, assert all artifacts present |
| P6: `get; init;` → `get;` on new records | Every primer/KB phase (CONTEXT.md constraint + plan review) | Serialization round-trip test for every new request/result record |
| P7: Stale KB content in production | KB integration (freshness disclosure + admin warning) | UAT: harvest date in prompt header ≤ 30 days before analysis date |
| P8: Doc gate widened before Core backfill complete | Core doc housekeeping phase (two-SC sequencing) | `dotnet build -warnaserror:CS1591` from clean `obj/` returns 0 warnings |
| P9: KB-12 adds string literal instead of factory extension | KB-12 housekeeping phase (constrained diff) | `LlmDistillationProviderFactoryTests` gains `Codex_ReturnsCliBackend` test |
| P10: Section-combinatorics under-tested | Primer service (section-routing tests written alongside logic) | `PrimerSectionRenderTests` covers bracket-routing, preset-section-presence, AI-variant structural markers |

---

## Sources

- DeckFlow `DeckFlow.Web/Services/PacketArtifactStore.cs` — HIGH (allowlist pattern verified by direct inspection)
- DeckFlow `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — HIGH (combo null-handling pattern at lines 562-564 verified)
- DeckFlow `DeckFlow.Core/Integration/LlmDistillationProviderFactory.cs` — HIGH (three-provider factory with `codex` deferral verified)
- DeckFlow `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` — HIGH (15 archetypes, 5 brackets, 11 card categories — vocabulary mismatch risk assessed)
- DeckFlow `.planning/notes/deck-primer-prompt-design.md` — HIGH (31-section catalog, bracket routing decisions, open risks)
- DeckFlow `.planning/seeds/deck-primer-generator.md` — HIGH (v1.5 feature scope)
- DeckFlow `.planning/RETROSPECTIVE.md` v1.0 + v1.2 + v1.3 — HIGH (paste-cap lesson v1.2; `{ get; init; }` regression history; raw-string re-indent risk)
- DeckFlow `.planning/v1.4-MILESTONE-AUDIT.md` — HIGH (186-site Core doc debt; KB-12 deferral; `content.kb.enabled` still OFF)
- DeckFlow `.editorconfig` lines 93-115 — HIGH (CS1591 gate scope verified: `none` globally, `warning` in `[DeckFlow.Web/**.cs]` only)
- DeckFlow `CLAUDE.md` — HIGH (`{ get; init; }` constraint; prompt-variant duplication intent; raw-string literal preservation)
- DeckFlow memory `reference_prompt_variants_intentionally_decoupled.md` — HIGH (ChatGPT/Claude/Gemini prose duplication is deliberate — never extract)
- DeckFlow memory `project_phase21_2_shipped.md` — HIGH (pluggable claude LLM-CLI backend; existing CliLlmDistillationService pattern)

---
*Pitfalls research for: DeckFlow v1.5 — Deck Primer Generator + Content KB Integration + Housekeeping*
*Researched: 2026-06-03*
