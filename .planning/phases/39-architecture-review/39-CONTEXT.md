# Phase 39 — Context (discuss-phase substitute)

**Source of context:** Two independent read-only architecture audits (`39-AUDIT.md` Claude, `39-AUDIT-CODEX.md` Codex/gpt-5.5) — both converged on the same #1. No discuss-phase Q&A needed; the audits + the operator's pick ARE the gathered context. Research skipped (no external/unknown tech; pure in-repo refactor).

## Phase shape
- **ARCH-01 (audit + ranked backlog):** DONE — `39-AUDIT.md` + `39-AUDIT-CODEX.md` committed (82034f1); full ranked backlog recorded in ROADMAP Backlog. SC1/SC3 met for the audit half.
- **ARCH-02 (execute top finding):** this planning effort. Scope = **Finding A ONLY**.

## Locked decisions

**D-01 — Scope is Finding A, nothing else.** Extract the duplicated deck-IO + Scryfall-hydration concerns out of the four ChatGPT-packet services. All other audit findings (B/C/D/E/F + ADR notes G–K) are explicitly OUT of scope → ROADMAP backlog. Do not opportunistically refactor them.

**D-02 — The four in-scope services:** `DeckAnalysisPacketService.cs` (~1626), `DeckComparisonService.cs` (~1305), `MetaGapService.cs` (~1018), `DeckPrimerPacketService.cs` (~791). `CardLookupService.cs` ALSO has a private `LoadDeckEntriesAsync`, but it is Lookup-family, not a packet service — leave it for a backlog follow-up; do NOT expand scope to it.

**D-03 — Reuse the EXISTING `IDeckEntryLoader`, do NOT invent a parallel loader.** `DeckFlow.Core/Loading/DeckEntryLoader.cs` already defines `IDeckEntryLoader.LoadAsync(DeckLoadRequest, CancellationToken)` and is already injected by `DeckSyncService` + `DeckConvertService` (registered in Program.cs). The packet services each carry a private `LoadDeckEntriesAsync` that duplicates this. The plan must:
  1. First VERIFY the existing loader covers each packet service's deck-load needs — especially URL-vs-paste routing AND the per-service **import/fallback-notice** handling (the Slice-1 audit flagged that `LoadDeckEntriesAsync` bodies differ slightly in fallback-notice handling). If the existing loader's output is behaviorally equivalent, repoint to it and delete the private copies. If there is a genuine behavioral difference (e.g. a notice the packet flow surfaces that `IDeckEntryLoader` does not), EXTEND `IDeckEntryLoader` to cover it (additive, behavior-preserving) rather than keeping a divergent private copy — and call out the difference in the plan.
  2. Do NOT change observable deck-loading behavior for any service.

**D-04 — Extract a new `IScryfallCardResolver` for the duplicated Scryfall hydration.** `SearchFallbackCardAsync` is byte-identical between DeckComparisonService and MetaGapService; the collection-batch fetch + fuzzy single-card fallback + lookup-name normalization plumbing (the triple `Func<RestRequest,…>` execute delegates) is repeated across the services. Extract a single injected `IScryfallCardResolver` owning: collection batch fetch, `SearchFallbackCardAsync` fuzzy fallback, and the name-normalization helpers it uses. The existing internal `Func<RestRequest, CancellationToken, Task<RestResponse<T>>>` test seams MOVE onto the resolver's internal test ctor — preserve that seam pattern (canonical per CLAUDE.md). Must continue to route through `ScryfallThrottle` and the named Polly pipeline exactly as today (no pacing/resilience change).

**D-05 — Behavior-preservation is proven by the EXISTING test net + clean build. No new golden-output harness required.** Guarding tests (verified present, content-asserting): `DeckAnalysisPacketServiceTests` (1542 LOC, 40 facts, 125 `Assert.Contains` on packet text), `DeckComparisonServiceTests` (492), `MetaGapServiceTests` (783), `DeckPrimerPacketServiceTests` (290), `AiPlatformPhase10RoundTripTests` (1053), `ResultContractTests` (519). The plan's verification = these suites stay green + `dotnet build DeckFlow.sln` clean (0 new warnings). VSTest is unreliable in WSL → rely on build-clean locally + CI for the test run; the plan should state that.

**D-06 — Cache-key parity must NOT change.** `LoadDeckEntriesAsync` output feeds the packet cache-key canonical text. Repointing deck-load/Scryfall-resolve must keep the cache-key computation byte-identical (the "mirror these lines EXACTLY" fragility, audit Finding G, is OUT of scope — do not refactor the cache-key strategy here, just don't perturb it). If extraction would alter canonical input text, STOP and flag.

**D-07 — Prompt variants + prompt prose are untouchable.** `Services/PromptBuilders/**` per-platform variants are intentionally decoupled (tried+reverted). Zero edits there.

**D-08 — Per CLAUDE.md execution model:** Claude plans (this), Codex reviews the PLAN before execute, Codex implements, Claude reviews. Scope-fence every Codex dispatch.

## Success criteria (ARCH-02 portion of the phase)
1. The four packet services no longer each own a private deck-load + Scryfall-fallback copy: deck loading routes through the existing `IDeckEntryLoader`; Scryfall hydration/fallback routes through the new injected `IScryfallCardResolver`. Duplicated `SearchFallbackCardAsync`/`LoadDeckEntriesAsync` deleted from the four services.
2. `dotnet build DeckFlow.sln` clean — 0 errors, 0 new warnings.
3. All existing packet-service + round-trip + contract tests pass unchanged (behavior preserved); cache-key output unchanged.
4. The new abstraction(s) registered in Program.cs DI; internal test seams preserved on the resolver.

## Out of scope (→ backlog, do not touch)
Findings B, C, D, E, F; ADR notes G–K; CardLookupService dedup; cache-key strategy refactor; any prompt-variant change; the deck-stat classifiers relocation (Finding E) even though it lives in DeckComparisonService.
