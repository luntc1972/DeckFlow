---
phase: 30-content-kb-integration
plan: 02
status: complete
requirements: [KBI-02, KBI-05]
one_liner: "Relevance-scoring core shipped: flag-gated parse-then-score pipeline with AND gate + K=5 + up-front budget trim, D-07 category-knowledge archetype deriver, and 32-expert-context.json zip round-trip into DeckAnalysisRequest.ExpertContextJson."
key_files:
  created:
    - DeckFlow.Web/Models/ContentKbExcerpt.cs
    - DeckFlow.Web/Services/ContentKbClipParser.cs
    - DeckFlow.Web/Services/ContentKbArchetypeDeriver.cs
    - DeckFlow.Web/Services/ContentKbRelevanceService.cs
    - DeckFlow.Web.Tests/ContentKbExcerptTests.cs
    - DeckFlow.Web.Tests/ContentKbClipParserTests.cs
    - DeckFlow.Web.Tests/ContentKbArchetypeDeriverTests.cs
    - DeckFlow.Web.Tests/ContentKbRelevanceServiceTests.cs
  modified:
    - DeckFlow.Web/Program.cs (DI registrations with real loggers)
    - DeckFlow.Web/Services/PacketArtifactStore.cs
    - DeckFlow.Web/Models/DeckAnalysisRequest.cs
    - DeckFlow.Web.Tests/PacketArtifactStoreTests.cs
---

# 30-02 Summary — Relevance-Scoring Core

## What happened

- **Task 1 (4d4fa4f):** `ContentKbExcerpt` sealed record (all `{ get; init; }`, JSON round-trip test guards the regression) + `ContentKbClipParser` static class: `ParseKeyClips(body)` extracts `- **[MM:SS]** text` bullets between `## Key Clips` and the next `## ` heading with 150-word sentence-boundary truncation (D-04); `BuildDeepLink(sourceUrl, timestampLabel)` appends YouTube `t={seconds}s` (`[1:02:14]` → 3734s) and falls back to the bare URL for non-YouTube hosts.
- **Task 2 (703ff97):** `ContentKbArchetypeDeriver` (D-07) + `IContentKbRelevanceService`/`ContentKbRelevanceService` (CommanderSpellbookService layout, internal test ctor seam). Codex implemented (gpt-5.4); Claude review found 2 issues (DI lambdas missing loggers → silent prod warnings; dead conditional in `NormalizeBracket`), both fixed before commit.
- **Task 3 (a74007b):** `32-expert-context.json` allowlist + `BuildZip` last optional param + `LoadFromZip` read into new `DeckAnalysisRequest.ExpertContextJson` (D-03 / HIGH-2 re-upload destination), all one commit because `ReadEntries` throws on unlisted entries. Comparison/CEDH allowlists untouched.

## Shipped contracts (for plans 03/04)

```csharp
public interface IContentKbRelevanceService
{
    Task<IReadOnlyList<ContentKbExcerpt>?> GetRelevantClipsAsync(
        string? commanderName, string? bracket,
        IReadOnlySet<string>? deckArchetypes = null,
        int maxRenderedChars = 4500,            // HIGH-1 budget; tightest-variant cap
        CancellationToken ct = default);        // null when flag off or no qualifying clips
    Task<IReadOnlyList<(ContentSiteIndexRow Row, double Score)>> ScoreAllAsync(
        string? commanderName, string? bracket, CancellationToken ct = default);
}
```

- `GetRelevantClipsAsync` pipeline (HIGH-3 order): flag check FIRST → normalize commander (partner-aware) → derive archetypes when null (D-07) → `GetPublishedRowsAsync` → per-row resolve+read+parse (one read per artifact; per-row failures logged + skipped) → `ScoreArtifact(parsedInput, …)` → ≥2-dimension AND gate → K=5 best-artifact-first document order (D-02) → up-front budget trim drops lowest-scoring last (HIGH-1). Returned set is final and identical for all prompt variants — prompt == zip == panel by construction.
- `DeckAnalysisRequest.ExpertContextJson` — round-tripped (null-coalescing backing field, mirrors `DeckProfileJson`). Plan 03 prefers it on re-upload over a fresh fetch.

## Calibrated constants (from 30-TAG-AUDIT.md, 2026-06-05)

| Constant | Value | Audit basis |
|----------|-------|-------------|
| BracketWeight | 0.75 | 45% all / 50% visible rows have empty brackets → bracket is score bonus, not gate |
| ArchetypeWeight | 1.25 | Archetype tags dense → primary structured signal |
| CommanderWeight | 1.5 | Tiny visible corpus → commander free-text hits must qualify with one other dim |
| MinSelectionScore | 2.0 | Any commander+X pair survives; archetype+bracket alone only with strong overlap |
| MaxClips | 5 | Plan contract (D-02) |
| DefaultMaxRenderedChars | 4500 | Pitfall-4 ~4KB injection cap |
| ArchetypeSpecificityWeights | 0.55 (value-engine) … 1.5 (blink) | Ubiquitous tags (value-engine 12/20, ramp 11/20) discounted; rare tags boosted |

Corpus = 2 visible / 20 total rows → per-request artifact reads, no IMemoryCache (`// Perf:` comment records the assumption).

## Category → archetype map (deriver, audit-constrained)

tutor/combo→combo; counter/removal/board-wipe/control→control; protection→control,voltron; ramp→ramp; draw/utility→value-engine(+midrange); recursion→reanimator,value-engine; sacrifice/aristocrat(s)→aristocrats; token(s)→tokens; land(s)→lands; blink→blink; tribal→tribal; spells/spellslinger→spellslinger; stax→stax; aggro→aggro; midrange→midrange; voltron→voltron; reanimator→reanimator; win-cons→combo,aggro; finishers→aggro,midrange. Support threshold: archetype kept when weight ≥ max(1, 0.35 × top support). Commander-name keyword fallback ONLY when zero category rows (D-07 binding honored).

## Verification

- Windows `dotnet build` DeckFlow.Web + DeckFlow.Web.Tests: 0 errors / 0 warnings (CS1591 gate active).
- Full Web suite after Task 2: **558 pass / 0 fail / 5 PG-skips**; after Task 3: **561 pass / 0 fail / 5 PG-skips**.
- Zip round-trip test passes (`BuildZip_with_expert_context_round_trips_into_request`), plus null-omits-entry and allowlist-no-throw tests.
- Acceptance greps all green: flag-check-first, `dimensionsHit >= 2`, `Calibrated from 30-TAG-AUDIT` (×6), `ICategoryKnowledgeStore` in deriver, DI registrations, `32-expert-context.json` ×3.

## Deviations

- None of scope. Two in-review fixes (logger wiring in DI lambdas; dead conditional) applied by Codex before commit.

## Commits

- 4d4fa4f feat(30-02): add ContentKbExcerpt and clip parser
- 703ff97 feat(30-02): add archetype deriver and relevance service
- a74007b feat(30-02): round-trip expert context through analysis zip
