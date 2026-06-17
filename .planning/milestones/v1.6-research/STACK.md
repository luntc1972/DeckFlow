# Stack Research — v1.6 Content KB Retrieval Fix + Value Re-Validation

**Project:** DeckFlow v1.6
**Researched:** 2026-06-10
**Confidence:** HIGH (retrieval fix, SRP split); MEDIUM (RAG/philosophy-profile — conditional on gate clearing; quality of in-process keyword grounding depends on synthesized principle specificity)
**Scope:** Three v1.6 work areas: (A) retrieval relevance + diversity fix, (B) conditional Creator
Philosophy-Profile (style-card synthesis + RAG grounding), (C) DeckController / CommandRunners SRP
split.

---

## Verdict: Zero New Dependencies

All three areas are deliverable with the packages already installed. The retrieval defects are pure
algorithm problems inside `ContentKbRelevanceService.cs`. The philosophy-profile distillation
reuses the existing `OpenAI 2.10.0` SDK (already in `DeckFlow.Core.csproj`) or the claude CLI
backend. The SRP split is a C# reorganization with no new types beyond the controllers themselves.

**No new NuGet packages. No new npm packages. No new external services.**

---

## Area A — Retrieval Relevance + Diversity Fix

### What the current scorer does and why it fails

`ScoreArtifact` produces a weighted sum across three dimensions (bracket, archetype tag overlap,
commander name in free text) behind a "dimensionsHit >= 2" AND gate. `SelectTopClips` iterates
artifacts ordered by descending score and grabs clips sequentially — filling all 5 slots from the
first artifact before moving to the next.

Two mechanism defects confirmed by Spike 001 VERDICT.md Run 2 (real scorer, real corpus):

1. **Single-video monopoly.** `SelectTopClips` has no per-video cap. The highest-scoring artifact
   fills every slot. On the Atraxa deck all 5 clips came from one video ("Glass Cannon Commanders")
   — 3 of 5 were about unrelated commanders.

2. **Tag-breadth beats topical fit.** "Glass Cannon Commanders" has broad tags
   (midrange/combo/value-engine/ramp/aggro + Upgraded/Optimized/cEDH) that overlap with nearly any
   deck profile. Its score reflects tag coverage, not whether its content is relevant to the queried
   commander. Directly on-topic videos ("Too Much Ramp", "5 Most Common Mistakes") scored lower
   because they have narrower tags.

### Fix: zero new dependencies

**Defect 1 — per-video diversity cap:**

Add a `MaxClipsPerVideo` constant (recommend 1) to `SelectTopClips`. Iterate passing once across
all qualifying artifacts taking at most `MaxClipsPerVideo` clips each before looping for a second
pass. This ensures up to 5 distinct videos fill the 5 slots. Pure algorithmic change — existing
LINQ, existing `ContentKbExcerpt` construction, no new types.

**Defect 2 — commander-noise penalty:**

After computing the raw score, apply a penalty multiplier when artifact clip excerpts contain
commander names that do not match the queried commander. Implementation:

- Add a private `ApplyCommanderNoisePenalty(ScoreInput, NormalizedCommander?) → double` helper in
  `ContentKbRelevanceService`.
- Use the existing `ContainsCommanderName` helper (already present) to check each clip excerpt.
- If more than half the clips mention a specific commander that is NOT the queried commander, apply
  a penalty multiplier (e.g. 0.2). A video genuinely about "Glass Cannon Commanders" as a class
  will not trigger this if its clips don't name specific unrelated commanders.
- The penalty is applied after `ScoreArtifact` returns, before the `MinSelectionScore` gate.

Both fixes are isolated to `ContentKbRelevanceService.cs` and its unit tests in
`DeckFlow.Web.Tests`. No interface changes. No new service registrations.

### Why not BM25, dense embeddings, or a vector store

**BM25 (e.g. Lucene.NET):** BM25 ranks documents by term frequency weighted by inverse document
frequency across the corpus. At ~80–200 distilled-markdown rows, the IDF component adds negligible
signal — there are too few documents for IDF to differentiate between "ramp" appearing in 5 videos
vs. 50. The existing tag-overlap + free-text matching pattern is BM25-equivalent at this corpus
size. Adding Lucene.NET would contribute ~5MB to the binary with no measurable retrieval quality
gain. Not worth it.

**Dense embeddings in-process (e.g. all-MiniLM-L6-v2 via ONNX Runtime + ML.NET):** The smallest
useful sentence transformer model is ~90MB. ONNX Runtime adds another ~40–80MB. On a 512MB Render
Starter web tier, loading a 90–170MB model at startup consumes 18–34% of the RAM budget before
handling a single request — a hard blocker. Ruled out.

**Vector store (pgvector, Qdrant, Chroma):** Requires either a schema migration + Npgsql extension
(pgvector) or a separate sidecar service (Qdrant, Chroma). Render Starter has no sidecar support.
pgvector would require a new migration plus per-row embedding generation at harvest time — a
significant pipeline change for a corpus that fits comfortably in a single in-memory
`List<ParsedArtifactRow>`. Ruled out.

### Implementation summary

| Change | File | New dependency |
|--------|------|---------------|
| `MaxClipsPerVideo = 1` cap + diversity-pass loop in `SelectTopClips` | `ContentKbRelevanceService.cs` | None |
| `ApplyCommanderNoisePenalty` helper using existing `ContainsCommanderName` | `ContentKbRelevanceService.cs` | None |
| Unit tests for both fixes, including regression for Run 2 scenario | `DeckFlow.Web.Tests` | None |

---

## Area B — Creator Philosophy-Profile (CONDITIONAL on gate clearing)

This area is only built if the fixed retriever clears the re-run of the Spike 001 gold A/B gate.
Do not build on current evidence.

### Shape

From `creator-philosophy-profile.md` seed: a per-creator **style-card** (synthesized persona of
deckbuilding principles/heuristics/biases, distilled across the whole channel) plus **RAG
grounding** (retrieve evidential transcript/clip passages at query time). Each principle carries
source video id + date. Contradictions preserved, not averaged. Recency-weighted. No fine-tuning.

### Style-card synthesis

**Already present:** `OpenAI 2.10.0` in `DeckFlow.Core.csproj` — used today for Whisper and the
openai distill CLI backend. The pluggable LLM-CLI dispatch pattern (claude vs openai, shelling out
to a CLI) handles all per-video distillation today. Style-card synthesis is a new prompt type
against the same backend — no new package required.

New class: `ContentKbPhilosophyDistiller` in `DeckFlow.Core/Content/`. It reads all existing
artifact markdown for a creator via `IContentSiteIndexStore`, submits them in batches to the LLM
backend using the existing `ILlmBackend` / CLI dispatch pattern, and produces a
`creator-profile.md` artifact with YAML frontmatter (`creator_slug`, `generated_utc`,
`source_video_ids`) plus a `## Principles` section (bullet list: principle text + provenance marker
`[source: video_id @ mm:ss]`).

The profile is stored in a new `creator_profiles` table (one row per creator) using the existing
`IRelationalDialect` pattern (SQLite + Postgres). The table schema is minimal: `id`, `creator_slug`,
`artifact_path`, `generated_utc`.

**Hallucination gate:** The synthesis prompt must instruct the LLM to emit provenance markers for
every principle. `ContentKbPhilosophyDistiller` validates after parsing that each cited `video_id`
exists in the live corpus before persisting the profile. Principles without verifiable provenance
are rejected. This is prompt engineering + post-parse validation, zero new deps.

### RAG grounding

Two options, both zero-dependency:

**Option 1 — In-process keyword similarity (recommended v1.6 baseline):**

Extend `ScoreArtifact` or add a post-score step that matches style-card principle keywords against
artifact clip excerpts using the existing `ContainsCommanderName`-style substring matching extended
to principle keywords. Score clips against the profile principles at query time. No LLM call, zero
latency, zero cost. Accuracy is lower than re-ranking but acceptable if synthesized principles are
specific enough (e.g. "prioritize removal over ramp in midrange builds" gives concrete keywords).

**Option 2 — LLM re-ranking via existing OpenAI SDK:**

A second LLM call per analysis request: given the injected style-card + deck context, ask the LLM
to select the most evidentially relevant clips from the corpus. ~100–300ms added latency,
~$0.001–0.005 cost at gpt-4o-mini / claude-haiku rates. No new dependency — `OpenAI 2.10.0` is
already present. Gate behind a new `content.kb.rag.enabled` sub-flag.

**Option 3 — Dense embedding retrieval:** Ruled out (same 512MB RAM blocker as Area A).

Recommendation: start with Option 1 as the v1.6 baseline. The style-card synthesis is the
higher-value part; RAG grounding is incremental. If Option 1 produces weak grounding (principle
keywords too generic), upgrade to Option 2 in a follow-on phase.

### Prompt injection

The style-card injects into the deck-analysis prompt as a `## Creator Lens` block (analogous to
the existing `## Expert Context` block). Same post-build append pattern used today by
`ContentKbInjectionService` — no variant interface signature changes in v1.6. Per-AI formatting
differences (if needed) are a follow-on concern.

### Implementation summary

| Artifact | Follows existing pattern | New dependency |
|----------|--------------------------|---------------|
| `ContentKbPhilosophyDistiller` in `DeckFlow.Core/Content/` | Existing LLM-CLI dispatch (`ILlmBackend` / CLI shelling) | None |
| `creator_profiles` table via `IRelationalDialect` | Existing `CategoryKnowledgeStore` table pattern | None |
| CLI command `RunPhilosophyDistillAsync` in `CommandRunners.cs` | Existing `RunDistillAsync` | None |
| Style-card prompt injection (`## Creator Lens` block) | Existing `ContentKbInjectionService` post-build append | None |
| In-process principle-keyword grounding | Extension of existing `ScoreArtifact` keyword matching | None |
| LLM re-ranking (Option 2, if needed) | Existing `OpenAI 2.10.0` SDK already in Core | None (already present) |

---

## Area C — DeckController / CommandRunners SRP Split

### Current state

- `DeckController.cs`: 1,840 lines, ~35 action methods across 6 distinct workflows.
- `CommandRunners.cs`: 1,902 lines, ~15 static `Run*Async` methods across harvest, distill,
  compare, probe, export, archidekt, content-source, card-lookup, and scryfall-probe commands.

### DeckController split

Natural split by workflow. Each new controller is a direct extraction — route attributes stay the
same, DI constructor changes minimally.

| New Controller | Actions Extracted | Shared DI deps to carry |
|---------------|-------------------|------------------------|
| `DeckSyncController` | `Index` (GET+POST), `Resolve`, `RenderDiffAsync` | `IDeckSyncService`, `ICommanderBanListService` |
| `DeckConvertController` | `Convert` (GET+POST), `ConvertCommanderSearch` | `IDeckConvertService`, `IScryfallCardLookupService` |
| `CardLookupController` | `CardLookup`, `SingleCardLookup`, `DownloadCardLookup`, `DownloadCardLookupJson`, `MechanicLookup`, `CardSearch`, `GetSetOptions` | `IScryfallCardLookupService`, `ICardSearchService` |
| `DeckAnalysisController` | `DeckAnalysis` (GET+POST), `DeckAnalysisDownload`, `DeckAnalysisUpload` | `IDeckAnalysisPacketService`, `IContentKbRelevanceService` |
| `DeckComparisonController` | `DeckComparison` (GET+POST), `DeckComparisonDownload`, `DeckComparisonUpload` | `IDeckComparisonPacketService` |
| `CedhMetaGapController` | `CedhMetaGap` (GET+POST), `CedhMetaGapDownload`, `CedhMetaGapUpload` | `IMetaGapPacketService`, `IEdhTop16Client` |
| `DeckPrimerController` | `DeckPrimer` (GET+POST), `DeckPrimerDownload`, `DeckPrimerUpload` | `IDeckPrimerPacketService` |
| `DeckController` (shell) | `Home`, `Error`, `SuggestCategories` (GET+POST), `JudgeQuestions` | Minimal shared deps |

**Cross-cutting risk:** `_WorkflowStepTabs.cshtml` uses `ViewContext.RouteData.Values["controller"]`
for active-tab detection. Controller renames require a corresponding update to the tab partial's
comparison strings. This is not a dependency issue — it is a tracked change requirement in the
execution plan.

### CommandRunners split

Extract static method groups into separate static classes, keeping the `public static async Task<int>`
signature contract unchanged. `DeckFlow.CLI/Program.cs` calls are updated to point to the new class.

| New Class | Methods Extracted |
|-----------|------------------|
| `ContentCommandRunners` | `RunContentSourceAddAsync`, `RunContentSourceSetEnabledAsync`, `RunDistillAsync`, `RunContentIndexExportAsync`, `RunHarvestAsync` |
| `ArchidektCommandRunners` | `RunArchidektCategoriesAsync`, `RunArchidektCategoryCardsAsync`, `RunArchidektHarvestRecentAsync`, `RunArchidektCacheAsync` |
| `ScryfallCommandRunners` | `RunScryfallProbeAsync`, `RunCardLookupAsync` |
| `DeckCommandRunners` | `RunCompareAsync`, `RunProbeAsync`, `RunExportMoxfieldAsync`, `LoadMoxfieldEntriesAsync`, `LoadArchidektEntriesAsync` |

`CommandRunners.cs` either becomes a thin re-export shim or is deleted, depending on whether any
tests reference it directly.

### Implementation summary

| Change | New dependency |
|--------|---------------|
| Extract 7 feature controllers from `DeckController.cs` | None |
| Update `_WorkflowStepTabs.cshtml` controller name strings | None |
| Extract 4 static runner classes from `CommandRunners.cs` | None |
| Update `DeckFlow.CLI/Program.cs` call sites | None |

---

## Alternatives Considered

| Area | Recommended | Alternative | Why Not |
|------|-------------|-------------|---------|
| Retrieval fix | In-process diversity cap + commander-noise penalty | Lucene.NET BM25 | IDF adds no signal at 80–200 docs; ~5MB binary overhead; same quality achievable in-process |
| Retrieval fix | In-process (zero new deps) | Dense embeddings (ONNX + ML.NET) | 90–400MB model RAM blows 512MB Render Starter cap |
| Retrieval fix | In-process (zero new deps) | pgvector / Qdrant | Schema migration or sidecar; corpus fits in a List; not worth the operational complexity |
| RAG grounding | In-process keyword match (v1.6 baseline) | LLM re-ranking (Option 2) | Adds ~200ms + ~$0.003/request to every analysis; defer until keyword baseline proven insufficient |
| Style-card distillation | Existing LLM-CLI backend + OpenAI 2.10.0 | Semantic Kernel | SK 2.x pulls ~15 transitive deps; no leverage over direct SDK calls at this scale |
| SRP split | Static classes grouped by domain (CommandRunners) | Interface-extract + DI injection | CLI is a command-runner, not a DI container; static cohesion is appropriate here |

---

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `Lucene.NET` | ~5MB binary, IDF meaningless at 80–200 docs | In-process tag + free-text scoring (existing pattern) |
| `Microsoft.ML` / ONNX Runtime | 90–400MB model RAM at startup — exceeds 512MB Render Starter cap | LLM-CLI re-ranking via existing `OpenAI 2.10.0` if keyword match proves insufficient |
| pgvector extension | Schema migration + Npgsql plugin for a corpus that already fits in memory | Existing `IRelationalDialect` + in-process scoring |
| `Microsoft.SemanticKernel` | ~15 transitive deps, no leverage over direct SDK calls | Direct `OpenAI 2.10.0` calls for style-card synthesis |
| Any new npm package | All retrieval is server-side; browser has no role in KB scoring | No change to Vitest toolchain |
| `Microsoft.Extensions.Http.Resilience` standard handler | Prohibited by project constraints | Existing `RestSharp + direct Polly v8` pattern |

---

## Confidence Assessment

| Area | Confidence | Basis |
|------|------------|-------|
| Zero-new-dependencies verdict (retrieval fix) | HIGH | Both defects are pure algorithmic changes to existing code; no new capabilities needed |
| Zero-new-dependencies verdict (SRP split) | HIGH | Pure C# reorganization; all types already exist |
| Zero-new-dependencies verdict (philosophy-profile) | HIGH | `OpenAI 2.10.0` confirmed present in `DeckFlow.Core.csproj`; distillation follows existing CLI pattern |
| Diversity cap fix quality | HIGH | Defect is structural (loop order); fix is mechanical; correctness is directly testable |
| Commander-noise penalty quality | MEDIUM | Penalty heuristic is empirically derived; clip-level commander-name detection may miss implicit references (e.g. "proliferate commander" without naming Atraxa); requires gold-set testing in the A/B re-run |
| RAG grounding Option 1 quality | MEDIUM | Depends on how specific the synthesized principles are; generic principles produce generic keyword matches, defeating the purpose |
| Style-card hallucination gate | MEDIUM | Provenance validation via video_id lookup is sound; the LLM may still conflate creator opinions across videos; provenance at `mm:ss` level is aspirational and depends on clip quality |
| `_WorkflowStepTabs.cshtml` controller-name dependency | HIGH (risk flagged) | Confirmed as a real cross-cutting dependency; must be updated in the SRP plan |

---

## Sources

All findings from direct codebase inspection plus Spike 001 VERDICT.md. No external library
research performed because no new libraries are being added.

- `DeckFlow.Web/Services/ContentKbRelevanceService.cs` — current scorer, `SelectTopClips`,
  `ScoreArtifact`, `ContainsCommanderName`, `ComposeSearchText` — all reviewed directly
- `DeckFlow.Web/Services/ContentKbClipParser.cs` — clip parsing and excerpt structure confirmed
- `.planning/spikes/001-kb-value-ab/VERDICT.md` — Run 2 defects documented; tag-breadth and
  single-video monopoly confirmed as root causes
- `.planning/seeds/creator-philosophy-profile.md` — style-card shape, must-handle list,
  hallucination gate requirement
- `DeckFlow.Core/DeckFlow.Core.csproj` — `OpenAI 2.10.0` confirmed present; no new SDK needed
- `DeckFlow.Web/DeckFlow.Web.csproj` — full installed dependency set confirmed; no gaps
- `DeckFlow.Web/Controllers/DeckController.cs` — 1,840 lines, 35 action methods; split points
  confirmed by workflow grouping (grep of `public.*IActionResult` + `public.*async Task`)
- `DeckFlow.CLI/CommandRunners.cs` — 1,902 lines, 15 static methods; domain grouping confirmed
- `.planning/PROJECT.md` — milestone scope, 512MB RAM constraint, "no new packages without
  approval" rule
- Render Starter plan RAM cap of 512MB — rules out ONNX in-process; documented in PROJECT.md
  and CLAUDE.md constraints

---

*Stack research for: DeckFlow v1.6 — Content KB Retrieval Fix + Creator Philosophy-Profile (conditional) + SRP Split*
*Researched: 2026-06-10*
