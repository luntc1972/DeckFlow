---
quick_id: 260527-mfl
slug: dedup-analysis-prompt-variants
date: 2026-05-27
status: planned
implementer: codex
reviewer: claude
---

# Quick Task 260527-mfl: Dedup deck-analysis prompt variants (narrow)

## Goal

Remove duplicated prompt text across the three deck-analysis prompt variant
classes and fix a phrasing-drift bug. **Maintainability + drift fix — NOT token
reduction.** These are paste-artifact prompts; the user pays tokens, not the app.

## Files

- `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs`
- `DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs`
- `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs`
- NEW: `DeckFlow.Web/Services/PromptBuilders/Analysis/AnalysisPromptShared.cs`
- `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs` (add lock tests)

## Hard constraints

- **Byte-for-byte output preservation** for the two non-drift blocks. The shared
  helpers must emit exactly the same characters (including leading whitespace and
  bullet prefixes) each variant emits today. The ONLY intended output change is
  the MDFC sentence on the Claude variant (drift fix).
- Do NOT touch evidence rules, the bracket-options foreach, the analysis-questions
  block, or the OUTPUT FORMAT A/B/C/D body. Platform framing there is intentional.
- Follow project CLAUDE.md: file-scoped namespace, Allman braces, 4-space indent,
  XML doc comments on the new public/internal type + members, `sealed`/`static`
  as appropriate, LF endings. Do NOT reformat untouched lines.

## Task 1 — Create AnalysisPromptShared with three append helpers

Create `internal static class AnalysisPromptShared` in namespace
`DeckFlow.Web.Services.PromptBuilders.Analysis`. Three helpers that append to a
caller-supplied `StringBuilder`, matching the existing `builder.AppendLine(...)`
pattern:

### 1a. `AppendBracketWeightingGuidance(StringBuilder builder)`
Emits these 4 lines verbatim (NO leading indent, NO bullet prefix — identical in
all three variants today):
```
The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.
Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.
Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.
Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.
```

### 1b. `AppendMdfcLandGuidance(StringBuilder builder, string linePrefix)`
Emits ONE canonical MDFC sentence with the caller's `linePrefix` prepended.
**Canonical sentence (chosen = the ChatGpt/Gemini phrasing — reads correctly both
as a bullet and as standalone prose):**
```
Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.
```
- ChatGpt + Gemini call with `linePrefix = "- "` → output unchanged from today.
- Claude calls with `linePrefix = ""` → output changes from today's
  `"...base, and weight them higher than a plain land since..."` to the canonical
  `"...base. Weight them higher than a plain land, since..."`. **This is the
  intended drift fix.**

### 1c. `AppendDeckProfileFieldDetails(StringBuilder builder, string indent)`
Emits the field-detail block. Each line = `indent` + the line body. Bodies
(identical word-for-word across all variants today):
```
Field-level detail requirements for the deck_profile JSON:
- game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.
- speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.
- estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.
- can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.
- assessed_bracket: your bracket verdict for this deck (e.g. "Bracket 3: Upgraded"), driven primarily by estimated_win_turn and can_answer_win_turn.
- bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.
- strengths: each item should be 1-2 sentences with a specific card or interaction reference.
- weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.
- deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.
- weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.
```
- ChatGpt + Gemini call with `indent = "   "` (3 spaces) → output unchanged.
- Claude calls with `indent = ""` → output unchanged.

NOTE on the first body line: ChatGpt/Gemini today emit `"   Field-level detail
requirements..."` and Claude emits `"Field-level detail requirements..."`. The
indent param covers this. Verify the `"   "` is exactly 3 spaces (the embedded `- `
in bodies stays literal).

## Task 2 — Repoint the three variants

Replace the inline `AppendLine` calls with the helper calls, in the SAME position:
- **ChatGpt**: lines 106-109 → `AnalysisPromptShared.AppendBracketWeightingGuidance(builder);`
  line 96 → `AnalysisPromptShared.AppendMdfcLandGuidance(builder, "- ");`
  lines 218-228 → `AnalysisPromptShared.AppendDeckProfileFieldDetails(builder, "   ");`
- **Gemini**: lines 107-110 → bracket weighting; line 98 → MDFC `"- "`;
  lines 220-230 → deck-profile field details `"   "`.
- **Claude**: lines 75-78 → bracket weighting (keep inside the existing
  `if (bracket is not null)` block — call replaces the 4 inline lines, control flow
  unchanged); line 141 → MDFC `""`; lines 227-237 → deck-profile field details `""`.

Do not move any other lines. Update the doc-comment on each variant if it claims
"byte-for-byte copy of the pre-refactor switch arm" is no longer literally true —
amend to note shared helpers were extracted (Phase note: quick task 260527-mfl).

## Task 3 — Lock tests (DeckFlow.Web.Tests, xUnit)

Add tests asserting the shared phrases appear in all three variant outputs (closes
the contract gap — no current test asserts MDFC/win-turn wording). Use the existing
`DeckAnalysisPacketServiceTests.cs` harness/builders. Assert, for each of the 3
platforms, that `BuildAnalysisPrompt` output `Assert.Contains`:
1. Win-turn reliability sentence: `"Weight the win turn by reliability, not raw speed:"`
2. Canonical MDFC sentence fragment: `"count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since"` (this also pins the drift fix — Claude must now match).
3. Deck-profile field instruction: `"estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer."`

Prefer a `[Theory]` over the three `AiPlatform` values if the harness allows it.

## Verify

- `dotnet build` clean, no new warnings.
- New tests pass. Existing `ResultContractTests` + `DeckAnalysisPacketServiceTests`
  still pass (they assert bracket/evidence presence + per-platform wrapper rules —
  must remain green).
- Spot-check: ChatGpt + Gemini analysis-prompt output is byte-identical to pre-change
  (diff a generated sample); Claude differs ONLY in the MDFC sentence.

## Done when

- AnalysisPromptShared created with 3 helpers.
- All 3 variants repointed; only intended output change is Claude's MDFC sentence.
- Lock tests added and green; full build clean.
- Commit per logical change, plain default-author (project convention), commit body
  notes the canonical MDFC phrasing choice.
