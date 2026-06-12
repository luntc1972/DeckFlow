# Phase 39 — Architecture Audit (ARCH-01)

**Date:** 2026-06-12 · **Scope:** whole solution (Web, Core, CLI, both test projects) · **Method:** 4 parallel read-only auditors, synthesized + ranked. Read-only — no code changed.

## Context

Phase 38 decomposed the two headline god-classes — `DeckController` (deleted → 8 controllers) and `DeckFlow.CLI/CommandRunners.cs` (deleted → 3 classes). This audit asks: **what is the next SRP/cohesion target now that the controllers/CLI-top are slim?**

The slim controllers call into **services that are still god-files**. That is the consistent theme below.

### Stale CONCERNS.md entries (2026-05-29 doc, now closed)
- DeckController 1555-LOC god-file — **killed by Phase 38**.
- CommandRunners god-class — **killed by Phase 38**.
- Phase 23 doc-comment NoWarn backlog — **closed by Phase 29**.
- Prompt-variant duplication — **NOT debt**: intentionally decoupled (tried + reverted; see `reference_prompt_variants_intentionally_decoupled`). Do not merge.
- **Good news — the flagged test gaps are now CLOSED**: Tagger TTL invariant (`TaggerSessionCacheInvariantTests`), Polly pipeline registration (`ResiliencePipelineFactoryTests`), EdhTop16 JSON roundtrip (`EdhTop16ClientTests`) all now tested.

---

## Ranked refactor backlog

### TOP TIER — candidates for ARCH-02 (execute now)

**A. Extract `IDeckEntryLoader` + `IScryfallCardResolver` from the 4 packet services** — *highest leverage*
- Files: `DeckAnalysisPacketService.cs` (1626), `DeckComparisonService.cs` (1305), `MetaGapService.cs` (1018), `DeckPrimerPacketService.cs` (791)
- Problem: `LoadDeckEntriesAsync` (URL/paste → Moxfield/Archidekt cascade) is re-implemented in all 4; `SearchFallbackCardAsync` is **byte-identical** between Comparison and MetaGap. Each service re-owns deck IO + Scryfall hydration + the triple `Func<RestRequest,…>` ctor plumbing — none of which is prompt-building.
- Why top: only finding that shrinks **3 of the 1000+ LOC files at once**; the seam is already proven (`DeckSyncService` uses an `IDeckEntryLoader`). Directly serves the core value path (ChatGPT packets).
- Effort **M** · Risk **M** — *blast radius is the core value path; behavior-preservation needs a golden-output packet-text parity harness built FIRST (does not exist yet). Cache-key parity is fragile (load-bearing "mirror these lines EXACTLY" comments).*

**B. Split `CategoryKnowledgeRepository` (Core) into Schema / Queue / CardCategory** — *safest high-value*
- File: `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` (1276 LOC, 24 public methods, 65+ members)
- Problem: one class owns schema/DDL+migration, the deck-harvest queue, category read/query, persistence/upsert, AND filtering/normalization — 5 reasons to change. Every public method re-runs `EnsureSchemaAsync`.
- Why strong: biggest non-content god-file, on the **live request path** (card + commander lookups) and the harvest job; **strongest existing safety net** (17 round-trip facts + `CategoryCacheSchemaParityTests` + `ContentHashDedupTests`) → behavior provable by current tests, no new harness. Mirrors the Phase 38 split pattern exactly.
- Effort **L** · Risk **M** (24 public methods; callers in Web + CLI + hosted job repoint — mitigate with a thin facade-then-extract).

**C. Split `ContentKbCommandRunners` (CLI) into Harvest / Distill / Source runners**
- File: `DeckFlow.CLI/ContentKbCommandRunners.cs` (1508 LOC, ~61 members)
- Problem: Phase 38 moved the Deck/ContentKb boundary but left a 5-concern monolith — source CRUD, harvest pipeline, distill pipeline, block/unblock/list, corpus-reset, index-export — all in one class.
- Why: clean mechanical move-method split; the internal-overload **test seams already pin every runner** → provable by existing Core.Tests + clean build. Lower user-facing value (CLI is internal tooling).
- Effort **M** · Risk **M**.

### MID TIER — backlog

**D. Finish `Services/` concern-foldering + extract `Program.cs` DI extensions** — *lowest risk*
- `DeckFlow.Web/Services/` = 48 flat files at root (13,971 LOC) despite an already-started sub-folder convention (Analytics/Harvest/FeatureFlags/Http exist; **`Services/Content/` exists but is empty — the smoking gun the migration stalled**). `Program.cs` = 557 lines with ~340 lines of inline HTTP-client + service-factory wiring despite the `AddDeckFlowXxx()` pattern being established for 4 concerns.
- Fix: move Scryfall→`Services/Scryfall/`, stores→`Services/Persistence/`, content→existing `Services/Content/` (namespaces unchanged = pure file moves); extract `AddDeckFlowHttpClients/ScryfallServices/PromptVariants/PacketServices()`.
- Effort **M** · Risk **L** (build+test is full proof). *Good "clean hygiene" pick if you want the lowest-risk option.*

**E. Relocate misplaced domain logic into `DeckFlow.Core`**
- Deck-stat classifiers (`IsRampCard`/`IsDrawCard`/curve math, ~150 LOC) live in `DeckComparisonService`; distill cost/validation (`ComputeProjectedVideoCostUsd`, `ValidateClips`, `EstimateTokenCount`) live in `ContentKbCommandRunners`. Both are pure CPU domain logic CLAUDE.md says belongs in Core; both currently untestable except through the web/CLI assembly.
- Effort **M** · Risk **L**.

**F. Strengthen the dual-dialect storage abstraction**
- `IRelationalDialect` only abstracts a column type + 3 strings; real divergence (CREATE TABLE DDL, UPSERT vs ON CONFLICT) leaks into **33 `IsPostgres`/`IsSqlite` branches across 7 stores**. Also: 3 Web-only `Feedback*` SQL members sit in the Core dialect interface (layering violation). **Risk caveat: all 11 parity tests are SQLite-only — the Postgres DDL path has no automated guard.**
- Effort **M** (S to remove feedback leak) · Risk **M**.

### LOW TIER — ADR notes / small follow-ups
- **G.** Packet cache-key parity → single `IPacketCacheKeyStrategy` (kills the "mirror exactly" fragility). Pairs naturally with A.
- **H.** `ScryfallThrottle` behind `IScryfallThrottle` (testability; keep the single-gate semaphore semantics).
- **I.** `IMemoryCache` has no `SizeLimit` on the 512MB host — the large caches (PacketSession 10MB, CardLookup 10k) already opted out with own instances; document the shared-cache-is-TTL-bounded design at `Program.cs:72`.
- **J.** `System.CommandLine` 2.0.0-beta4 — record as deliberate pin (GA rewrites `SetHandler`→`SetAction`); mirror the package vs unlisting.
- **K.** Residual test gaps: middleware-ordering integration test (UseForwardedHeaders-before-CSRF — the existing `ForwardedHeadersOptionsTests` actually tests feedback-IP-key, not ordering); Polly **policy-shape** assertion (current test proves resolve, not retry/timeout config).

---

## Recommendation for ARCH-02

Two defensible "top findings":
- **B (CategoryKnowledgeRepository split)** — *recommended default*: biggest live-path god-file, **provable by existing tests** (no new harness), lowest execution risk for a high-value SRP win, continues the Phase 38 arc.
- **A (IDeckEntryLoader/IScryfallCardResolver)** — *highest architectural leverage* (collapses duplication across all 4 packet services) but touches the core value path and requires building a golden-output parity harness first → higher risk/effort.

D is the lowest-risk option if the goal is clean hygiene over depth. Whichever is chosen for ARCH-02, the rest land in backlog (ADR stubs / `/gsd:review-backlog`) per SC3.
