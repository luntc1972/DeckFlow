# Phase 112: Cycle 17 Code Port - Context

**Gathered:** 2026-07-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Cycle 17's Core engine (Phases 94-98 — profile records/store, measured extraction, stated-rules extraction, profile fusion, card-grounding guard) and the creator-style Web services, seed loader, and DI registrations land on `feat/personal-tools` from `plan/cycle-17-creator-style` (head `6da5eb42`, forked at `5709f37c`, 777 commits behind). The solution builds clean, DI resolves, and the ported creator-style test suites pass.

**Not this phase:** the admin controller/view surface (114), the shared-infra re-derivation against main's manabase and Scryfall consumers (113), and real seed data (115).

</domain>

<decisions>
## Implementation Decisions

### Shared-infra build order

- **D-01: Port the four Scryfall helpers as new files at 112.** `ScryfallCollectionResolver`, `ScryfallLimits`, `CachedNameResolution`, `ScryfallBatching` — 147 lines total, all ABSENT on main. Adding them is purely additive: no existing main callsite references them, so 112 builds clean with zero conflict surface. Phase 113 retains its real job — re-deriving the `ManabaseAnalysisService.cs:560` dedup and the dedicated `archidekt` pipeline against current main.
- **D-02: 112 does NOT port Cycle 17's edits to main's existing Scryfall files.** `CardLookupService.cs`, `ScryfallCardResolver.cs`, `ScryfallDtos.cs`, and `ScryfallReferenceResolver.cs` stay byte-identical to main. The new helpers are consumed only by newly ported creator-style code. Every rewire of a pre-existing consumer belongs to Phase 113's line-by-line re-derivation. This gives 112 a zero-regression-risk profile against Cut Lab's Cycle 18/19 edits.
- **D-03: The `archidekt` resilience pipeline registration is deferred to Phase 113.** `ArchidektOwnerClient.cs:74` resolves `pipelineProvider.GetPipeline<RestResponse>("archidekt")` with a `?? ResiliencePipeline<RestResponse>.Empty` fallback; main registers only `banlist`/`spellbook`/`tagger`/`tagger-post`/`scryfall`.
  ⚠ **Planner must verify before locking this:** Polly's `ResiliencePipelineProvider<string>.GetPipeline<T>` conventionally *throws* `KeyNotFoundException` on an unregistered key rather than returning null, so the `??` fallback may never fire. Determine whether the throw happens at construction or at first call. If `IArchidektOwnerClient` is resolved by the D-10 DI-resolution test, this deferral will trip success criterion 3 — in that case register the pipeline at 112 after all (a one-line additive entry matching the existing five).
- **D-04: The format gate must pass on every port commit.** A port makes ~120 files of entirely new lines, and the changed-lines gate judges all of them. Codex runs `scripts/format-check-changed.sh staged` and fixes violations inside the same commit. The five `.editorconfig` carve-outs are non-negotiable — never convert `{ get; init; }` to `{ get; }` (silently breaks System.Text.Json deserialization), never inline `[Attribute]` onto the property line, never re-indent raw-string literals, preserve switch expressions and xmldoc single-space indent, preserve LF endings.

### Port mechanism

- **D-05: Path-allowlist checkout, not diff-apply and not cherry-pick.** `git checkout plan/cycle-17-creator-style -- <explicit path list>` for added files; the approved path list is a plan artifact. **Critical reason:** the raw branch diff (`5709f37c..plan/cycle-17-creator-style`) contains Cycle-16 Content-KB work that has since landed on main *independently* — `ContentBodyHashBackfill.cs`, `SeedManagedBackfill.cs`, `SeedIndexFileReader.cs`, `WebSeedKeyMembershipSource.cs` are all already PRESENT on main. A wholesale diff apply would fight main and could revert shipped work. The allowlist is structurally incapable of that.
- **D-06: Allowlist boundary is the compile closure.** 112 takes exactly what the Core engine + Web services need to compile — so `Models/CreatorStyleRequest.cs` comes along (referenced by `CreatorStylePacketService`), while `CreatorStyleViewModel` does not (no service references it). Controller, views, and `Help/creator-style.md` stay out (114 / dropped). Rule is self-checking: if it doesn't build, the closure was wrong.
- **D-07: Two commits, per the design spec.** Commit 1 = Core engine (P94-98). Commit 2 = Web services + the four Scryfall helpers + `CardGroundingGuard` + `ScryfallCardNameGrounder` + seed loader + DI + `CreatorStyleRequest`. Core has zero Web coupling (it reaches Scryfall only through the Core-side `ICardNameGrounder` interface), so commit 1 is genuinely additive.
- **D-08: Diff-vs-main path audit gates each port commit.** After each commit, `git diff --name-status main` must contain only allowlisted paths — anything outside fails the commit. Pair with a grep proving no `tool.creator-style.enabled`, `ToolRegistry`, `SeoPaths`/sitemap, or `PacketSessionCache` bypass strings arrived. Build+tests alone are insufficient: an older copy of a file main has since improved would compile and pass green while silently reverting Cycle 18/19 work.

### Modified-file hunk policy

- **D-09: Deny by default; hunk-apply by hand when required.** A file that already exists on main is touched at 112 ONLY if the compile closure demands it, and then Codex applies the specific hunk to *main's current version* — never `git checkout branch -- <file>`, which would clobber Cycle 16/18/19 edits. Every M-file touched carries a one-line justification in the plan.
- **D-10: Creator-style DI goes in a new dedicated extension.** `AddDeckFlowCreatorStyle()` in a new `Extensions/CreatorStyleServiceCollectionExtensions.cs`, invoked from a single added line in `Program.cs`. Cycle 17 spread these registrations across `Program.cs` plus `HttpClientServiceCollectionExtensions`, `PacketServiceCollectionExtensions`, and `ScryfallServiceCollectionExtensions` — four files Cut Lab has been rewriting. One line in the most-contested file instead of four conflict surfaces. Matches the existing `AddDeckFlowResiliencePipelines()` precedent.
- **D-11: Seed loader is wired AND invoked at 112, with `[]` placeholders committed.** Port `CreatorStyleSeedLoader`, register it, keep the `LoadIfPresentAsync()` startup call, and commit `content-kb/seed/creator-style-profiles.json` and `content-kb/seed/creator-deck-cache.json` as `[]` placeholders (neither exists on main; both exist on the c17 branch). This exercises the real startup hydration path now instead of discovering a defect in Phase 115. Phase 115 overwrites the contents with real data.
- **D-12: Phase-100 public plumbing is never ported — not ported-then-deleted.** No hunks land in `FeatureFlagCatalog.cs`, `FeatureFlagStore.cs`, `ToolRegistry.cs`, `PacketSessionCache.cs`, `Models/DeckPageTab.cs`, or `Help/creator-style.md`. Locked by the design spec; restated here because these all appear as M-files in the branch diff and would otherwise look like port candidates.

### Test port and DI proof

- **D-13: A DI-resolution xUnit test proves success criterion 3.** Build the real service provider and resolve every creator-style interface. Runs in CI on every future change and permanently catches a missing registration — unlike a one-time manual boot. It will also force the D-03 `archidekt` question into the open, which is desirable.
- **D-14: Tests travel with their code.** Core engine tests AND the Web-service tests for anything ported at 112 land at 112. The Phase-100 public-surface suites (flag lockstep ×6, `ToolRegistryTests` counts, route-gate coverage, SEO/sitemap assertions) are never ported — the design spec's reframe-during-port rule exists precisely so those don't arrive failing. Phase 114 keeps PORT-04's job: proving the drop list is clean and adding admin-route BasicAuth coverage.
- **D-15: Postgres integration test files stay out.** `DeckFlow.Core.Tests/Integration/PostgresContainerFixture.cs`, `PostgresFactAttribute.cs`, and `CreatorStyleProfileStorePostgresTests.cs` are not ported — Postgres migration of the creator-style stores is explicitly out of scope for Cycle 20 (stores bind to local `content-kb.db`; production hydrates from git-shipped seeds).

### Post-research amendments (ratified 2026-07-24, after `112-RESEARCH.md`)

Research built the port in a disposable worktree and compiled it. Four decisions are amended by build evidence. These amendments are binding and supersede the conflicting text above.

- **D-16: D-02 is narrowed — two of its four files take additive hunks.** `CardLookupService.cs` and `ScryfallReferenceResolver.cs` stay byte-identical as written. `ScryfallDtos.cs` and `ScryfallCardResolver.cs` do NOT — `CardGroundingGuard` fails to compile without them (`CS1061` on `ScryfallCard.Legalities` and `IScryfallCardResolver.ExecuteNamedFuzzyAsync`). Both hunks are pure additions: one optional trailing record property (`[property: JsonPropertyName("legalities")] IReadOnlyDictionary<string, string>? Legalities = null` — last position, preserves every positional and named caller) and one interface method with a `NotSupportedException`-throwing default implementation plus its concrete override. No existing member changes; no caller breaks. D-02's zero-regression intent is preserved even though byte-identity is not. See RESEARCH Finding 1.
- **D-17: D-03 is FLIPPED — register the `archidekt` resilience pipeline at 112.** This exercises D-03's own pre-authorized escape hatch with proof in hand. Verified against the pinned Polly 8.6.6: `ResiliencePipelineProvider<string>.GetPipeline<T>(key)` throws `KeyNotFoundException` on an unregistered key, so `ArchidektOwnerClient.cs:74`'s `?? ResiliencePipeline<RestResponse>.Empty` fallback is unreachable dead code — and the call sits in the **constructor**, so the throw fires at DI-resolution time, not at first HTTP call. `CreatorProfileDeckCrawler` (in the 112 closure) takes `IArchidektOwnerClient` directly, so success criterion 3 fails without the registration. Add the one additive entry to `AddDeckFlowResiliencePipelines()` plus its `BuildArchidekt` builder (exact code in RESEARCH Finding 2; main's `ResiliencePipelineFactory.cs` is byte-identical to the merge-base, so the c17 hunk applies with zero adaptation). The named `"archidekt-owner"` `HttpClient` registration goes inside the NEW `AddDeckFlowCreatorStyle()` extension — never by editing the shared `HttpClientServiceCollectionExtensions`. D-03's Phase-113 deferral of the `ManabaseAnalysisService.cs:560` dedup is unaffected.
- **D-18: D-12 is narrowed by one file.** `PacketSessionCache.cs` takes an additive hunk: the `internal sealed record CreatorStyleCacheInputs` (defined nowhere else on the c17 branch) plus one `PacketSizeEstimator.EstimateSizeBytes(CreatorStylePacketResult)` overload. `CreatorStylePacketService` fails with `CS0246` without it. Verified unrelated to PTOOL-02's actual concern — the bypass list D-12 cares about (`PromptMutatingCreatorStyleFlags`) lives in `CreatorStylePacketService.cs`, and creator-style adds no entry to any shared list because it has no public feature flags. The rest of D-12's never-port list (`FeatureFlagCatalog.cs`, `FeatureFlagStore.cs`, `ToolRegistry.cs`, `Models/DeckPageTab.cs`, `Help/creator-style.md`) stands unchanged. See RESEARCH Finding 3.
- **D-19: `Program.cs` takes two edits, not one, and `ProgramStartupTests.cs` ports.** (a) the D-10 DI line `builder.Services.AddDeckFlowCreatorStyle(builder.Environment);` directly after the existing `AddDeckFlowScryfallServices();`, and (b) the startup seed-load rewrite from sequential awaits to a fork-join over both `IContentKbSeedLoader` and `ICreatorStyleSeedLoader`, with `AwaitStartupSeedTasksAsync` + `LogFaultedSeedTask` static helpers (~35 lines, pure additions). Chosen over the simpler sequential two-liner because it is what D-11's "wired AND invoked" means, it is what the ported test asserts, and it is a strict improvement — both seed sources get logged on fault instead of the first failure aborting silently. See RESEARCH Finding 4.
- **D-20: The D-13 DI-resolution test must resolve the REAL `ArchidektOwnerClient` for at least one assertion.** The c17 branch's own `CreatorStyleDiRegistrationTests.cs` injects `FakeArchidektOwnerClient`, which masks the D-17 failure mode entirely — a faked test would pass on paper while the real app throws on first resolution of `CreatorProfileDeckCrawler`. Port the existing test as-is, then strengthen it with a companion assertion that resolves through the real implementation.

### Claude's Discretion

- Exact composition of the path allowlist (the plan must publish it, but its derivation is mechanical: A-status paths under the creator-style prefixes, minus anything already PRESENT on main).
- Ordering of file groups within each of the two commits.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone authority
- `docs/research/personal-tools-admin-reframe-design.md` — **Authoritative for this milestone.** D-01..D-07 decision table, the surface reframe, the "Dropped from Phase 100" table, the six-commit port plan, and the risk table. §"Port plan" commits 1-2 are this phase; commit 3 is Phase 113.
- `.planning/REQUIREMENTS.md` — PORT-01 and PORT-02 are this phase's requirements; PORT-03 (113) and PORT-04 (114) bound what must NOT be done here.
- `.planning/ROADMAP.md` §"Phase 112" — the four success criteria this phase is graded against.

### Source of the ported code
- Git ref `plan/cycle-17-creator-style` (head `6da5eb42`) — the port source. Branch is preserved untouched at origin as the historical record; it is NOT rebased and NOT resumed.
- Git ref `5709f37c` — the merge-base with main (2026-07-06). Any diff taken from this base contains Cycle-16 work already on main; see D-05.

### Downstream data (not needed at 112, needed at 115)
- `docs/research/p89-p90-prototype-snail.md` — source of the hand-authored stated rules and the P90 fusion verdict table. Referenced here only so the planner knows the engine's goldens trace back to it.

### Project standing rules
- `CLAUDE.md` §Constraints — `.editorconfig` changed-lines format gate, the five carve-outs, LF line endings, 512MB Render tier.
- `CLAUDE.md` §Anti-Patterns — no `new HttpClient()`, no per-call Polly pipelines, no Scryfall calls bypassing `ScryfallThrottle`.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DeckFlowResiliencePipelineRegistry.AddResiliencePipeline` (`DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs:27-31`) — the five existing named pipelines show the exact additive shape the `archidekt` entry takes if D-03 flips.
- `AddDeckFlowResiliencePipelines()` — the repo's existing precedent for a dedicated DI extension method, the model for D-10's `AddDeckFlowCreatorStyle()`.
- `AdminCreatorProfileController` (Deck Tendencies, on `feature/deck-tendencies`) — an admin controller that is a plain `Controller`, not `DeckToolControllerBase`. Relevant at 114, noted here because it sets the pattern the port must not violate.
- `content-kb/seed/index-seed.json` — the existing git-shipped seed on main; the creator-style seeds follow the same hydrate-at-startup pattern.

### Established Patterns
- Core reaches Scryfall only through Core-side interfaces (`ICardNameGrounder`), with Web supplying the implementation (`ScryfallCardNameGrounder`, 38 lines). This is why the Core commit is conflict-free.
- Services expose a public DI constructor plus an `internal` test-seam constructor taking a delegate, with `[InternalsVisibleTo("DeckFlow.Web.Tests")]`. Ported services already follow this.
- Named `IHttpClientFactory` clients + `ResiliencePipelineProvider<string>` resolved by string key — never a per-call pipeline build.

### Integration Points
- `Program.cs` — one added line calling `AddDeckFlowCreatorStyle()`, plus the existing `CreatorStyleSeedLoader.LoadIfPresentAsync()` startup call (c17 `Program.cs:115` and `:290` show the original placement).
- `DeckFlowDatabaseConnectionFactory.cs:72` — `content-kb.db` binding that `CreatorStyleProfileStore` depends on. Verify main's current shape before applying any hunk.
- `DeckFlow.Core/AssemblyInfo.cs` — `InternalsVisibleTo` additions may be required for the ported Core tests; an M-file, so D-09 applies.

### Contamination map (verified this session)
Already PRESENT on main, must NOT be ported: `ContentBodyHashBackfill.cs`, `SeedManagedBackfill.cs`, `SeedIndexFileReader.cs`, `WebSeedKeyMembershipSource.cs`.
ABSENT on main, must be ported: `ScryfallCollectionResolver.cs`, `ScryfallLimits.cs`, `CachedNameResolution.cs`, `ScryfallBatching.cs`, `SeedJson.cs`, `GlobalCategoryBaseline.cs`, and everything under `Core/Knowledge/{CardGrounding,CreatorStyleRubric,MeasuredStyleExtraction,ProfileFusion,StatedRulesExtraction}/`, `Core/Content/Creator*`, `Web/Services/CreatorStyle/*`.

</code_context>

<specifics>
## Specific Ideas

- The user explicitly chose to defer the `archidekt` pipeline registration despite the flagged throw risk — the deferral is deliberate, and the verification obligation in D-03 is the condition on it, not a re-litigation of the choice.
- "Zero-regression-risk profile" is the organizing principle the user selected repeatedly across three separate questions (D-01, D-02, D-09): 112 should be as close to purely additive as the compile closure allows, pushing every contested edit into 113.

</specifics>

<deferred>
## Deferred Ideas

- `ManabaseAnalysisService.cs:560` callsite dedup onto `ScryfallCollectionResolver` — Phase 113 (PORT-03).
- `archidekt` resilience pipeline registration — Phase 113, unless D-03's verification forces it earlier.
- Rewiring `CardLookupService` / `ScryfallCardResolver` / `ScryfallDtos` / `ScryfallReferenceResolver` onto the new helpers — Phase 113.
- Admin controller, views, `/Admin` landing personal-tools section, `CreatorStyleViewModel` — Phase 114.
- Deletion proof for Phase-100 public plumbing (repo-wide grep) — Phase 114 (PTOOL-02).
- Real seed data, `creator-style-import-stated` CLI, `fuse-profile` run — Phase 115.
- Postgres migration of the creator-style stores — out of scope for Cycle 20 entirely.

</deferred>

---

*Phase: 112-Cycle 17 Code Port*
*Context gathered: 2026-07-24*
