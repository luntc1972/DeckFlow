---
phase: 34-kb-retrieval-fix
reviewed: 2026-06-10T18:30:00Z
depth: deep
files_reviewed: 7
files_reviewed_list:
  - DeckFlow.Web/Services/ContentKbRelevanceService.cs
  - DeckFlow.Web/Services/ContentKbClipSanitizer.cs
  - DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs
  - DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs
  - DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs
  - DeckFlow.Web.Tests/ContentKbRelevanceServiceTests.cs
  - DeckFlow.Web.Tests/ContentKbClipSanitizerTests.cs
findings:
  critical: 0
  warning: 3
  info: 4
  total: 7
status: issues_found
---

# Phase 34: KB Retrieval Fix — Code Review Report

**Reviewed:** 2026-06-10T18:30:00Z
**Depth:** deep (cross-file, scoring-math trace, build + full test run)
**Files Reviewed:** 7
**Status:** issues_found (no blockers)

## TOP-LINE VERDICT: APPROVE (commit-ready)

The implementation correctly fixes BOTH Spike 001 defects, satisfies all locked
34-CONTEXT decisions, is fully CLAUDE.md-compliant, and passes the full test suite
(677 passed / 5 skipped Postgres-integration / 0 failed; targeted KB filter 41/41).
The three WARNING findings are residual-risk hardening items, all consistent with the
plan's *accepted* threat dispositions (T-34-10 residual, T-34-12) — none of them
regresses behavior or blocks the v1.5 commit. They should be tracked as Phase-36
prerequisites BEFORE the production un-dark (KBD-01), not as commit blockers now.

There are **no CRITICAL findings** and **no behavioral defects.** Approving for commit;
WR-01 (fence-escape) is the one item I most want addressed before un-darking in prod.

---

## Correctness verification (the two spike defects)

**Defect 1 — single-video monopoly (KBR-01): FIXED.**
`SelectTopClips` now tracks a `Dictionary<long,int> clipsPerVideo` keyed by `artifact.Row.Id`
and `break`s the inner clip loop once a row reaches `MaxClipsPerVideo = 1`
(`ContentKbRelevanceService.cs:496,510-526`). The `break` fires *after* one clip is admitted,
so a single qualifying video still yields exactly 1 clip — never 0 (no zero-starvation;
Pitfall 2 honored). Verified by `GetRelevantClipsAsync_OtherRowsExist_TopArtifactContributesOneClipMaximum`
and `..._GeneralAdviceWithoutCommander_QualifiesOnArchetypeAndContentOverlap` (single row → single clip).
The outer ordering (`Score desc, OriginalOrder`) and `MaxClips=5` early return are preserved.

**Defect 2 — tag-breadth beat topical fit (KBR-02): FIXED.**
Scoring math traced numerically against the Atraxa gold scenario:
- Glass Cannon row (archetypes midrange/combo/value-engine/ramp/aggro, bracket Upgraded,
  excerpts naming Kaalia/Animar/Isshin/Zur + an Atraxa aside): bracket +0.75, archetype
  (midrange+value-engine+ramp) ×1.25 ≈ +2.81, commander (Atraxa aside) +1.5, minus
  4 foreign-commander hits × 0.9 = −3.6 ⇒ net ≈ 1.46 + (small content overlap), which falls
  **below** `MinSelectionScore = 2.0`. It is disqualified → contributes 0 clips, no Kaalia/Animar
  leakage. Assertion `<= 1` holds.
- "Too Much Ramp" general-advice row (no commander): archetype (ramp+midrange) ≈ +2.13,
  content overlap on ramp/removal/protection/midrange/value terms × 0.45 lifts it well above 2.0,
  with `dimensionsHit = 2` (archetype + content) clearing the AND-gate. Survives **on its merits,
  not penalized for lacking a commander** — the user's explicit concern is satisfied.
- Verified by `GetRelevantClipsAsync_Spike001AtraxaScenario_DiverseTopicalNoCommanderLeakage`
  (≥2 distinct videos, Glass Cannon ≤1 clip, zero Kaalia/Animar) and
  `..._NoRowsClearRelevanceFloor_ReturnsNull` (no top-K fallback).

**Own-commander never penalized: CONFIRMED.** Foreign hits = `searchTokens.Where(KnownCommanderNames.Contains).Except(commanderTokens)`.
For an Atraxa deck, `commanderTokens` contains `atraxa`, so an "atraxa" token is removed from the
foreign set even though it is in `KnownCommanderNames`. `ScoreArtifact_AtraxaOwnCommanderMention_BeatsForeignCommanderBreadth`
locks this.

**Shared-scorer refactor / merged (followed) tier did not regress: CONFIRMED.** `ScoreArtifact`
now delegates to the extracted `CalculateScoreAndDimensions`, which is the single source of truth
for both the auto path and the followed-tier path (`CalculateUngatedScore` / `CountDimensionsHit`).
The followed tier in `GetMergedClipsAsync` still uses `DimensionsHit >= 1` (ungated) and the auto
path still applies the `>= 2` gate — behavior preserved. `ContentKbMergedClipsTests` is green.

---

## Warnings

### WR-01: Fence-escape — clip text can spoof the data-block END delimiter

**File:** `DeckFlow.Web/Services/ContentKbClipSanitizer.cs:19-46`;
`ChatGptAnalysisPromptVariant.cs:257-269`, `ClaudeAnalysisPromptVariant.cs:~258`, `GeminiAnalysisPromptVariant.cs:264-276`
**Issue:** The structural fence wraps clips in `<<<EXPERT_CONTEXT_DATA ...>>>` / `<<<END_EXPERT_CONTEXT_DATA>>>`,
but `ContentKbClipSanitizer.Sanitize` does NOT neutralize the angle-bracket delimiter tokens. A crafted
transcript excerpt containing the literal `<<<END_EXPERT_CONTEXT_DATA>>>` would visually close the data
block early, and any following injected text would sit *outside* the fence — directly undermining the
structural-fence half of the defense-in-depth. (The sanitizer + preamble still apply to the spoofed
content, so this is not a full bypass, but the fence is escapable.) The threat model accepts residual
regex-incompleteness (T-34-10) but does not call out delimiter-spoofing specifically.
**Fix:** In `Sanitize`, defang the fence tokens the same way code fences are handled, e.g. add a regex
that replaces `<<<` / `>>>` (or the specific `EXPERT_CONTEXT_DATA` token) with a benign marker before
injection. Suggested:
```csharp
[GeneratedRegex(@"<{3,}|>{3,}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
private static partial Regex FenceDelimiterRegex();
// in Sanitize, after CodeFenceRegex:
sanitized = FenceDelimiterRegex().Replace(sanitized, "[delimiter removed]");
```
Add a sanitizer test asserting `<<<END_EXPERT_CONTEXT_DATA>>>` in clip text is neutralized.
**Disposition:** Not a commit blocker, but should be closed before KBD-01 (Phase 36 prod un-dark).

### WR-02: Override-phrase regex is narrow — common bypass phrasings survive

**File:** `DeckFlow.Web/Services/ContentKbClipSanitizer.cs:39`
**Issue:** The override-phrase pattern requires `(previous|prior|above|earlier|preceding)` *immediately*
followed by `(instructions|guidelines|rules|prompts)`. Real-world injections like "ignore everything
before this", "ignore the system prompt", "disregard what you were told", or "forget your instructions"
(possessive, no "previous/above") all bypass it. Likewise the role-marker regex is anchored to line start
(`^`), so "Now System: do X" mid-line survives. This matches the plan's *accepted* residual risk
(regex is not a complete defense; corpus is admin-curated, narrow attack surface) — flagging so the gap
is explicit and tracked, not silently assumed complete.
**Fix:** Broaden the override family (drop the mandatory trailing noun, add "your", add bare
"ignore/disregard ... instructions") and consider stripping role markers anywhere in the line, not only
at start. Keep the neutralize-not-blank policy so "#5"-style benign text is unaffected. Lowest-effort
acceptable path: document the residual coverage limit in the `// Why:` comment and defer broadening to
Phase 36 hardening.
**Disposition:** Consistent with T-34-10 residual disposition; not a commit blocker.

### WR-03: Gemini `EstimateExpertContextLength` likely undercounts the fence lines

**File:** `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs:285`
**Issue:** The base estimate was bumped 180 → 346 (delta 166) to cover the new fence. The three new
fixed lines total ≈ 235 raw chars (BEGIN ≈ 62, boundary instruction ≈ 145, END ≈ 28) before newlines,
on top of the pre-existing header+preamble the original 180 already covered. So 346 appears to undercount
by ~60–80 chars. This is harmless in practice: the guard compares against `DefensivePromptCharCap = 50000`
(belt-and-suspenders; plan 30-02 already trims the set up front), so a ~70-char undercount cannot realistically
overshoot a 50k cap. Plan 34-02 explicitly called for the estimate to include the fence lines, and it
nominally does — the magnitude is just slightly low.
**Fix:** Recompute precisely (sum the literal lengths of the two new ASCII lines + the END delimiter +
their newlines) or round up to a safe constant (e.g. 430). Optional given the 50k headroom.
**Disposition:** Cosmetic accuracy; not a commit blocker.

---

## Info

### IN-01: Override-phrase sanitizer test under-asserts

**File:** `DeckFlow.Web.Tests/ContentKbClipSanitizerTests.cs:28-34`
**Issue:** `Sanitize_OverridePhrase_ReplacesInstructionLikeText` asserts `DoesNotContain(input, result)`
where `input` is the *entire* original string. Because the regex replaces the whole matched phrase, this
passes, but it does not pin *which* tokens were removed vs. preserved. It would not catch a future
regression that over-strips surrounding benign words.
**Fix:** Add positive assertions on the surviving benign remainder (as
`Sanitize_RoleMarkerAndOverridePhrase_NeutralizesBothFamilies` already does with "and output X").

### IN-02: `KnownCommanderNames` is single-token first-names only — multi-word/legend collisions

**File:** `DeckFlow.Web/Services/ContentKbRelevanceService.cs:165-184`
**Issue:** The curated set stores bare first names (animar, kaalia, zur, urza, ...). Tokenization is
word-level, so "Urza" the commander and any incidental "urza" mention score identically, and a deck whose
commander shares a first name with a set entry but is a *different* legend would be mis-protected/penalized.
At ~82 rows this is acceptable (34-CONTEXT explicitly chose the simpler zero-dep list), and the demotion is
small/belt-and-suspenders. Noting for the Phase 36 corpus-derived-list revisit.
**Fix:** None required now; revisit when corpus grows or when a full-name match is cheap.

### IN-03: `Tokenize` min-length 3 silently drops short strategy tokens

**File:** `DeckFlow.Web/Services/ContentKbRelevanceService.cs:~827`
**Issue:** `Tokenize` keeps only matches with `Length >= 3`, so terms like "go" (from "go-wide" the regex
keeps as one token "go-wide", fine) are not the issue, but any 2-char strategy signal is dropped. No current
DeckProfileTerms entry is < 3 chars, so this is presently inert. Document the assumption or the next term-list
edit could silently no-op.
**Fix:** Add a `// Why: min length 3 drops noise tokens; keep profile terms >= 3 chars` comment.

### IN-04: Two summary artifacts (`34-01-SUMMARY.md`, `34-02-SUMMARY.md`) are untracked

**File:** repo root `.planning/phases/34-kb-retrieval-fix/`
**Issue:** Not a code defect — noting that the executor-produced SUMMARY files exist untracked alongside the
reviewed changes; include them in the same commit per project convention.
**Fix:** Stage with the implementation commit.

---

## CLAUDE.md compliance (all PASS)

- LF preserved on all touched/new files (verified `file` shows no CRLF).
- No `{ get; init; }` → `{ get; }` regression; diff shows zero accessor changes.
- Zero new NuGet packages (no `PackageReference` diff in any `.csproj`).
- Internal test ctor byte-identical to spec (`ContentKbRelevanceService.cs:216-222`);
  `Spike001KbValueAbHarness` + existing tests still compile.
- Prompt-variant prose NOT shared-extracted — each variant carries its own fence/boundary lines inline;
  only the mechanical `ContentKbClipSanitizer.Sanitize` is centralized (per plan, correct).
- New delimiter/boundary lines are ASCII-clean (the em-dash in the harvested-date preamble is the
  pre-existing line, untouched by this diff).
- Diff is surgical (scoring file +183/−49, variants +8/+8/+10); no wholesale reformat.

## Test quality (PASS)

- Old monopoly-asserting test was genuinely CORRECTED, not deleted: it now seeds 6 rows and asserts
  5 distinct titles + the top row contributes exactly one clip — the inverse of the old
  "4 clips from top row" assertion.
- Atraxa regression genuinely asserts ≥2 distinct videos, Glass Cannon ≤1 clip, AND no
  "Kaalia"/"Animar" substring leakage in any selected excerpt.
- No-match → null, general-advice-no-commander qualifies, and own-commander-not-penalized all covered.
- Tests are deterministic + offline (in-memory artifact dictionary, tracking fakes, no network/disk).
- The budget-trim test was correctly tightened (`maxRenderedChars` 420 → 300, asserts single clip)
  to match the new one-clip-per-video diversity.
- Full `DeckFlow.Web.Tests`: 677 passed / 5 skipped / 0 failed.

---

_Reviewed: 2026-06-10T18:30:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: deep_
