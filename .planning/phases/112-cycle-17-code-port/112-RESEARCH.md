# Phase 112: Cycle 17 Code Port - Research

**Researched:** 2026-07-24
**Domain:** Git-history code port (C#/.NET 10, ASP.NET Core) — porting forward Cycle 17's
creator-style engine from a stale branch onto current `main`, avoiding regression against
independently-landed Cycle 16/18/19 work.
**Confidence:** HIGH — every claim below is `[VERIFIED]` against a live `git diff`/`git show`
against this repo's actual history, or against an **actual local build** of the exact proposed
port set in a disposable git worktree (`dotnet build` clean, 0 errors, 0 new warnings, across
`DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`). No web search was
needed or used — this is a pure archaeology-of-this-repo task.

**Primary recommendation:** The compile closure is **not** identical to the design spec's
"purely additive" framing. Five concrete, load-bearing exceptions were found and proven by an
actual build (see `## Critical Findings`). All five are small, additive, zero-regression-risk
hunks — but the planner MUST include them or the port will not compile / will not satisfy D-13's
DI-resolution test. The two path allowlists below, plus the M-file hunk table, are the complete
and build-verified port manifest.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01: Port the four Scryfall helpers as new files at 112.** `ScryfallCollectionResolver`, `ScryfallLimits`, `CachedNameResolution`, `ScryfallBatching` — 147 lines total, all ABSENT on main. Adding them is purely additive: no existing main callsite references them, so 112 builds clean with zero conflict surface. Phase 113 retains its real job — re-deriving the `ManabaseAnalysisService.cs:560` dedup and the dedicated `archidekt` pipeline against current main.
- **D-02: 112 does NOT port Cycle 17's edits to main's existing Scryfall files.** `CardLookupService.cs`, `ScryfallCardResolver.cs`, `ScryfallDtos.cs`, and `ScryfallReferenceResolver.cs` stay byte-identical to main. The new helpers are consumed only by newly ported creator-style code. Every rewire of a pre-existing consumer belongs to Phase 113's line-by-line re-derivation. This gives 112 a zero-regression-risk profile against Cut Lab's Cycle 18/19 edits.
  **⚠ RESEARCH FINDING: this decision is only 50% true as stated.** `CardLookupService.cs` and `ScryfallReferenceResolver.cs` are correctly byte-identical-safe (verified). `ScryfallDtos.cs` and `ScryfallCardResolver.cs` are NOT — `CardGroundingGuard.cs` (explicitly in-scope) has a hard compile dependency on a new `ScryfallCard.Legalities` property and a new `IScryfallCardResolver.ExecuteNamedFuzzyAsync` method. See `## Critical Findings` #1.
- **D-03: The `archidekt` resilience pipeline registration is deferred to Phase 113.** `ArchidektOwnerClient.cs:74` resolves `pipelineProvider.GetPipeline<RestResponse>("archidekt")` with a `?? ResiliencePipeline<RestResponse>.Empty` fallback; main registers only `banlist`/`spellbook`/`tagger`/`tagger-post`/`scryfall`.
  ⚠ **Planner must verify before locking this:** Polly's `ResiliencePipelineProvider<string>.GetPipeline<T>` conventionally *throws* `KeyNotFoundException` on an unregistered key rather than returning null, so the `??` fallback may never fire. Determine whether the throw happens at construction or at first call. If `IArchidektOwnerClient` is resolved by the D-10 DI-resolution test, this deferral will trip success criterion 3 — in that case register the pipeline at 112 after all (a one-line additive entry matching the existing five).
  **⚠ RESEARCH VERDICT: the deferral is UNSAFE — this decision is FLIPPED.** See `## Critical Findings` #2. Register `archidekt` at 112.
- **D-04: The format gate must pass on every port commit.** A port makes ~120 files of entirely new lines, and the changed-lines gate judges all of them. Codex runs `scripts/format-check-changed.sh staged` and fixes violations inside the same commit. The five `.editorconfig` carve-outs are non-negotiable — never convert `{ get; init; }` to `{ get; }` (silently breaks System.Text.Json deserialization), never inline `[Attribute]` onto the property line, never re-indent raw-string literals, preserve switch expressions and xmldoc single-space indent, preserve LF line endings.
  **[VERIFIED]:** all 99 candidate `.cs` files, as-authored on `plan/cycle-17-creator-style`, already pass `dotnet format DeckFlow.sln --include <99 files> --verify-no-changes` with **zero violations**, in ~21s. No reformatting is expected to be needed for the ported files themselves (only for the hand-written M-file hunks the planner/Codex author fresh).
- **D-05: Path-allowlist checkout, not diff-apply and not cherry-pick.** `git checkout plan/cycle-17-creator-style -- <explicit path list>` for added files; the approved path list is a plan artifact. **Critical reason:** the raw branch diff (`5709f37c..plan/cycle-17-creator-style`) contains Cycle-16 Content-KB work that has since landed on main *independently* — `ContentBodyHashBackfill.cs`, `SeedManagedBackfill.cs`, `SeedIndexFileReader.cs`, `WebSeedKeyMembershipSource.cs` are all already PRESENT on main. A wholesale diff apply would fight main and could revert shipped work. The allowlist is structurally incapable of that.
  **⚠ RESEARCH FINDING: the actual contamination set is 37 files, not 4** (see `## Contamination Audit`). Of those 37, all but 2 are byte-identical to main and need zero action. The 2 that differ (`ContentKbPaths.cs`, `RoundTripSyncLoopTests.cs`) are handled: the first needs a small additive hunk (below), the second is irrelevant Postgres-test noise safely left untouched.
- **D-06: Allowlist boundary is the compile closure.** 112 takes exactly what the Core engine + Web services need to compile — so `Models/CreatorStyleRequest.cs` comes along (referenced by `CreatorStylePacketService`), while `CreatorStyleViewModel` does not (no service references it). Controller, views, and `Help/creator-style.md` stay out (114 / dropped). Rule is self-checking: if it doesn't build, the closure was wrong.
  **[VERIFIED]** via actual build: `CreatorStyleViewModel.cs`, `CreatorStyleController.cs`, `Views/Deck/CreatorStyle.cshtml`, `Help/creator-style.md`, `DeckFlow.Web/e2e/creator-style.spec.ts` are all genuinely excludable — the full solution (minus these) builds with 0 errors once the hunks in `## Critical Findings` are applied.
- **D-07: Two commits, per the design spec.** Commit 1 = Core engine (P94-98). Commit 2 = Web services + the four Scryfall helpers + `CardGroundingGuard` + `ScryfallCardNameGrounder` + seed loader + DI + `CreatorStyleRequest`. Core has zero Web coupling (it reaches Scryfall only through the Core-side `ICardNameGrounder` interface), so commit 1 is genuinely additive.
  **[VERIFIED]:** `DeckFlow.Core` (Commit 1's full candidate set) builds standalone with 0 errors once its 6 required M-file hunks (all Core-internal, see below) are applied — genuinely zero Web coupling, confirmed.
- **D-08: Diff-vs-main path audit gates each port commit.** After each commit, `git diff --name-status main` must contain only allowlisted paths — anything outside fails the commit. Pair with a grep proving no `tool.creator-style.enabled`, `ToolRegistry`, `SeoPaths`/sitemap, or `PacketSessionCache` bypass strings arrived. Build+tests alone are insufficient: an older copy of a file main has since improved would compile and pass green while silently reverting Cycle 18/19 work.
  **⚠ Sharpened by research:** this audit would have caught the `DeckFlowDatabaseConnectionFactory.cs`/`ILlmDistillationService.cs` wholesale-overwrite mistake this research session made and caught mid-probe (see `## Critical Findings` #5) — reinforces that D-08's gate is necessary, not belt-and-suspenders.
- **D-09: Deny by default; hunk-apply by hand when required.** A file that already exists on main is touched at 112 ONLY if the compile closure demands it, and then Codex applies the specific hunk to *main's current version* — never `git checkout branch -- <file>`, which would clobber Cycle 16/18/19 edits. Every M-file touched carries a one-line justification in the plan.
  **[VERIFIED]** — see the full M-file hunk table below; every hunk was hand-derived against main's CURRENT HEAD content (not the stale merge-base), and 3 of the 9 required hunks would have been WRONG if taken as a wholesale branch-file copy (main independently added members the branch never had).
- **D-10: Creator-style DI goes in a new dedicated extension.** `AddDeckFlowCreatorStyle()` in a new `Extensions/CreatorStyleServiceCollectionExtensions.cs`, invoked from a single added line in `Program.cs`. Cycle 17 spread these registrations across `Program.cs` plus `HttpClientServiceCollectionExtensions`, `PacketServiceCollectionExtensions`, and `ScryfallServiceCollectionExtensions` — four files Cut Lab has been rewriting. One line in the most-contested file instead of four conflict surfaces. Matches the existing `AddDeckFlowResiliencePipelines()` precedent.
  **⚠ Sharpened:** Program.cs needs slightly MORE than "a single added line" — it also needs the startup seed-load fork-join rewrite (`AwaitStartupSeedTasksAsync`) if `ProgramStartupTests.cs` is ported. See `## Critical Findings` #4.
- **D-11: Seed loader is wired AND invoked at 112, with `[]` placeholders committed.** Port `CreatorStyleSeedLoader`, register it, keep the `LoadIfPresentAsync()` startup call, and commit `content-kb/seed/creator-style-profiles.json` and `content-kb/seed/creator-deck-cache.json` as `[]` placeholders (neither exists on main; both exist on the c17 branch). This exercises the real startup hydration path now instead of discovering a defect in Phase 115. Phase 115 overwrites the contents with real data.
  **[VERIFIED]** — see `## Seed Placeholder Mechanics`. `[]` is handled with zero throw risk; no guard task needed.
- **D-12: Phase-100 public plumbing is never ported — not ported-then-deleted.** No hunks land in `FeatureFlagCatalog.cs`, `FeatureFlagStore.cs`, `ToolRegistry.cs`, `PacketSessionCache.cs`, `Models/DeckPageTab.cs`, or `Help/creator-style.md`. Locked by the design spec; restated here because these all appear as M-files in the branch diff and would otherwise look like port candidates.
  **⚠ RESEARCH FINDING: `PacketSessionCache.cs` is a genuine, unavoidable exception.** `CreatorStylePacketService.cs` (explicitly retained per the design doc) hard-references a `CreatorStyleCacheInputs` record that is DEFINED INSIDE `PacketSessionCache.cs` on the c17 branch and does not exist anywhere else. See `## Critical Findings` #3 — this is the loudest finding in this research and directly narrows D-12.
- **D-13: A DI-resolution xUnit test proves success criterion 3.** Build the real service provider and resolve every creator-style interface. Runs in CI on every future change and permanently catches a missing registration — unlike a one-time manual boot. It will also force the D-03 `archidekt` question into the open, which is desirable.
  **[VERIFIED — this is exactly what happened.]** The c17 branch's own `CreatorStyleDiRegistrationTests.cs` uses a HAND-FAKED `IArchidektOwnerClient` and therefore does NOT exercise the real pipeline-throw risk. The planner's NEW D-13 test (or Program.cs's real DI graph at any runtime resolution of `CreatorProfileDeckCrawler`) WILL exercise the real `ArchidektOwnerClient`, and WILL throw `KeyNotFoundException` at construction unless `archidekt` is registered. Confirmed via an isolated Polly 8.6.6 repro (see `## Critical Findings` #2).
- **D-14: Tests travel with their code.** Core engine tests AND the Web-service tests for anything ported at 112 land at 112. The Phase-100 public-surface suites (flag lockstep ×6, `ToolRegistryTests` counts, route-gate coverage, SEO/sitemap assertions) are never ported — the design spec's reframe-during-port rule exists precisely so those don't arrive failing. Phase 114 keeps PORT-04's job: proving the drop list is clean and adding admin-route BasicAuth coverage.
  **⚠ Sharpened:** 3 more Core.Tests files must ALSO stay out beyond D-15's named 3 (CLI-layer tests: `CreatorStyleSeedSerializationTests.cs`, `FuseProfileRunnerTests.cs`; these test the `creator-style-index-export`/`fuse-profile` CLI commands, which are Phase 115 scope, not 112). See `## Test Port Inventory`.
- **D-15: Postgres integration test files stay out.** `DeckFlow.Core.Tests/Integration/PostgresContainerFixture.cs`, `PostgresFactAttribute.cs`, and `CreatorStyleProfileStorePostgresTests.cs` are not ported — Postgres migration of the creator-style stores is explicitly out of scope for Cycle 20 (stores bind to local `content-kb.db`; production hydrates from git-shipped seeds).
  **⚠ RESEARCH FINDING: incomplete by 3 files.** `ContentVideoStoreStatedRulesReadTests.cs` also depends on `PostgresContainerFixture` and has no SQLite variant — exclude wholesale. `CreatorDeckCacheStoreTests.cs` and `CreatorProfileSourceStoreTests.cs` each contain TWO test classes in one file: a plain-SQLite class (port) and a `*TestsPostgres` nested class (strip before porting). See `## Test Port Inventory`.

### Claude's Discretion

- Exact composition of the path allowlist (the plan must publish it, but its derivation is mechanical: A-status paths under the creator-style prefixes, minus anything already PRESENT on main).
- Ordering of file groups within each of the two commits.

### Deferred Ideas (OUT OF SCOPE)

- `ManabaseAnalysisService.cs:560` callsite dedup onto `ScryfallCollectionResolver` — Phase 113 (PORT-03). **[VERIFIED safe to defer]** — this file is untouched in the 112 build and compiles fine unmodified.
- Rewiring `CardLookupService` / `ScryfallReferenceResolver` onto the new helpers — Phase 113. **[VERIFIED safe to defer]** — both files compile fine byte-identical to main.
- Admin controller, views, `/Admin` landing personal-tools section, `CreatorStyleViewModel` — Phase 114.
- Deletion proof for Phase-100 public plumbing (repo-wide grep) — Phase 114 (PTOOL-02).
- Real seed data, `creator-style-import-stated` CLI, `fuse-profile` run — Phase 115. **Confirmed to also include:** `CreatorStyleCommandRunners.cs` (CLI), and its 2 dependent Core.Tests files (`CreatorStyleSeedSerializationTests.cs`, `FuseProfileRunnerTests.cs`) plus the `Core.Content.ContentVideoStore.InsertStatedRuleAsync` / `Core.Knowledge.ContentKbCommandRunners.RunFuseProfileAsync` methods those tests exercise.
- Postgres migration of the creator-style stores — out of scope for Cycle 20 entirely.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PORT-01 | Cycle 17's Core engine (Phases 94–98) is present on `feat/personal-tools`, solution builds with no new errors or warnings | `## Port Allowlist — Commit 1 (Core)`, `## M-File Hunk Inventory` (Core hunks), `## Build Verification` (actual `dotnet build` proof, 0 errors) |
| PORT-02 | Creator-style Web services, seed loader, DI registrations ported and resolve at startup | `## Port Allowlist — Commit 2 (Web)`, `## Critical Findings` #1–4, `## Validation Architecture` (DI-resolution test design) |
</phase_requirements>

---

## Summary

This phase is a mechanical port, and mechanical claims deserve mechanical proof rather than
static diff-reading. This research went further than a diff audit: it **actually built** the
proposed port set in a disposable git worktree (`git worktree add`, never touching the real
repo), applying every M-file hunk by hand against `main`'s CURRENT content (never a wholesale
branch checkout), and iterated until `dotnet build` returned 0 errors across `DeckFlow.Core`,
`DeckFlow.Web`, `DeckFlow.Core.Tests`, and `DeckFlow.Web.Tests`.

That process surfaced five load-bearing facts CONTEXT.md's decisions did not anticipate:

1. **D-02 is half-wrong.** `ScryfallDtos.cs` and `ScryfallCardResolver.cs` need small additive
   hunks (new `Legalities` property, new `ExecuteNamedFuzzyAsync` method) because `CardGroundingGuard`
   — which IS in scope — needs them to compile. `CardLookupService.cs` and
   `ScryfallReferenceResolver.cs` remain correctly byte-identical.
2. **D-03's deferral is unsafe and must flip.** Polly 8.6.6's `ResiliencePipelineProvider<string>.GetPipeline<T>`
   throws `KeyNotFoundException` on an unregistered key — proven with an isolated repro — and the
   throw happens inside `ArchidektOwnerClient`'s CONSTRUCTOR, not on first HTTP call. Any DI
   resolution of `CreatorProfileDeckCrawler` (explicitly in scope) will throw at 112 unless the
   `archidekt` pipeline is registered. **Register it at 112.**
3. **D-12 has one unavoidable, narrow exception.** `PacketSessionCache.cs` must receive a small
   additive hunk (a `CreatorStyleCacheInputs` record + one `PacketSizeEstimator` overload) because
   `CreatorStylePacketService` — explicitly retained — references a type defined nowhere else.
4. **`Program.cs`'s edit is bigger than "one line."** Beyond the `AddDeckFlowCreatorStyle()` call,
   the startup seed-load sequence needs a fork-join rewrite (`AwaitStartupSeedTasksAsync` +
   `LogFaultedSeedTask`) if the ported `ProgramStartupTests.cs` is to compile and pass.
5. **Six more M-file hunks are required** beyond what CONTEXT.md's canonical_refs called out:
   `ContentKbPaths.cs`, `DeckFlowDatabaseConnectionFactory.cs`, `AssemblyInfo.cs`,
   `ILlmDistillationService.cs`, `DistillationResults.cs`, `DistillationValidation.cs`,
   `ContentTagVocabulary.cs`, `CommanderInference.cs`, `CategoryKnowledgeRepository.cs`,
   `CardCategoryRepository.cs`. All are pure-additive (new members only, zero deletions), all
   were hand-verified against main's CURRENT content, and 3 of them (`DeckFlowDatabaseConnectionFactory.cs`,
   `ILlmDistillationService.cs`, `CategoryKnowledgeRepository.cs`'s sibling `CardCategoryRepository.cs`)
   would have SILENTLY REVERTED shipped main-only members (`CreateManabaseBaselineConnection`,
   `ExtractCombinedAsync`) if the plan naively took "the c17 branch's version of the file" instead
   of hand-applying just the new members onto main's current file — this is the exact failure
   mode D-05/D-09 exist to prevent, caught and corrected in this research session.

Every other locked decision (D-01, D-04, D-05, D-06, D-07, D-08, D-09, D-10's core shape, D-11,
D-13's design, D-14's principle, D-15's principle) held up under the build probe and is confirmed
correct, with sharpened detail below.

## Critical Findings

> Read this section before the path lists — it changes what goes in them.

### Finding 1 — D-02 partial reversal: `ScryfallDtos.cs` and `ScryfallCardResolver.cs` need additive hunks

`CardGroundingGuard.cs` (in-scope, named explicitly in D-07) fails to compile against main's
current `ScryfallDtos.cs`/`ScryfallCardResolver.cs` with:

```
CardGroundingGuard.cs(345,71): error CS1061: 'ScryfallCard' does not contain a definition for 'Legalities'
CardGroundingGuard.cs(260,43): error CS1061: 'IScryfallCardResolver' does not contain a definition for 'ExecuteNamedFuzzyAsync'
```

Both fixes are pure-additive on the c17 branch (confirmed via `git diff 5709f37c..plan/cycle-17-creator-style`):
- `ScryfallDtos.cs`: add one optional record parameter, `[property: JsonPropertyName("legalities")] IReadOnlyDictionary<string, string>? Legalities = null`, to the `ScryfallCard` record's parameter list (last position — preserves all positional/named callers).
- `ScryfallCardResolver.cs`: add one interface method with a `NotSupportedException`-throwing default implementation (so no other implementer breaks), plus the concrete override in `ScryfallCardResolver`:
  ```csharp
  // interface
  Task<RestResponse<ScryfallCard>> ExecuteNamedFuzzyAsync(string cardName, CancellationToken cancellationToken)
      => throw new NotSupportedException(
          $"{nameof(ExecuteNamedFuzzyAsync)} requires a concrete {nameof(ScryfallCardResolver)} implementation.");

  // concrete class
  public async Task<RestResponse<ScryfallCard>> ExecuteNamedFuzzyAsync(string cardName, CancellationToken cancellationToken)
  {
      ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
      var request = new RestRequest("cards/named", Method.Get);
      request.AddQueryParameter("fuzzy", NormalizeForScryfall(cardName));
      var response = await _executeNamedAsync(request, cancellationToken).ConfigureAwait(false);
      ScryfallThrottle.ThrowIfUpstreamUnavailable(response.StatusCode);
      return response;
  }
  ```
`CardLookupService.cs` and `ScryfallReferenceResolver.cs` (the other two D-02-named files) remain
correctly untouched — verified: their c17 diffs only rewire them onto `ScryfallLimits`/`ScryfallBatching`,
which is genuinely Phase-113 work and not required for the 112 closure to compile.

**Both hunks are additive-only and preserve D-02's zero-regression-risk spirit even though they
are not byte-identical.** Update the M-file exclusion list: 2 of the 4 named files stay
byte-identical (`CardLookupService.cs`, `ScryfallReferenceResolver.cs`); 2 need a 1-property /
1-method additive hunk (`ScryfallDtos.cs`, `ScryfallCardResolver.cs`).

### Finding 2 — D-03 VERDICT: register `archidekt` at 112, the deferral is unsafe

Isolated repro against the pinned `Polly 8.6.6` package (same version as this repo, verified via
`DeckFlow.Web.csproj`/`DeckFlow.Core.csproj`):

```csharp
var registry = new ResiliencePipelineRegistry<string>();
registry.GetOrAddPipeline("scryfall", b => b.AddTimeout(TimeSpan.FromSeconds(1)));
ResiliencePipelineProvider<string> provider = registry;

provider.GetPipeline<HttpResponseMessage>("archidekt");
// THROWS: System.Collections.Generic.KeyNotFoundException:
//   Unable to find a generic resilience pipeline of 'HttpResponseMessage' associated with the
//   key 'archidekt'. Please ensure that either the generic resilience pipeline or the generic
//   builder is registered.
```

`TryGetPipeline` returns `false`/`null` cleanly (no throw) — but `ArchidektOwnerClient.cs:74` calls
`GetPipeline`, not `TryGetPipeline`, so the `?? ResiliencePipeline<RestResponse>.Empty` fallback
**never executes** — the exception fires before the `??` right-hand side is evaluated.

**And the call site is inside the constructor**, not a lazily-invoked method:

```csharp
internal ArchidektOwnerClient(
    ResiliencePipelineProvider<string> pipelineProvider,
    RestClient restClient,
    ILogger<ArchidektOwnerClient>? logger = null)
{
    ...
    _resiliencePipeline = pipelineProvider.GetPipeline<RestResponse>("archidekt") ?? ResiliencePipeline<RestResponse>.Empty;
    // ^ throws HERE, at object-construction time, not at first HTTP call
    ...
}
```

`CreatorProfileDeckCrawler` (explicitly "Retained unchanged" per the design doc, confirmed in the
112 compile closure by an actual successful build) takes `IArchidektOwnerClient` as a direct
constructor parameter. **Any DI resolution of `CreatorProfileDeckCrawler` — including any D-13
test that resolves it with the REAL `ArchidektOwnerClient` rather than a fake — throws unless
`archidekt` is registered.**

Note: the c17 branch's own `CreatorStyleDiRegistrationTests.cs` sidesteps this entirely by
injecting `FakeArchidektOwnerClient`, so it does not exercise this failure mode. If the planner's
new D-13 test does the same (uses a fake), success criterion 3 could pass on paper while the REAL
app still throws at first real resolution of `CreatorProfileDeckCrawler` at runtime. **Recommend
the D-13 test resolve the REAL `ArchidektOwnerClient` for at least one assertion**, specifically to
catch this class of bug.

**Verdict: register `archidekt` at 112.** The exact one-line additive entry (verified: main's
`ResiliencePipelineFactory.cs` is byte-identical to the merge-base, so the c17 hunk applies
cleanly with zero adaptation):

```csharp
// In AddDeckFlowResiliencePipelines(), alongside the existing five:
DeckFlowResiliencePipelineRegistry.AddResiliencePipeline<string, RestResponse>(services, "archidekt", builder => BuildArchidekt(builder));

// New private builder method, matching the Scryfall-shaped total-budget pattern:
private static void BuildArchidekt(ResiliencePipelineBuilder<RestResponse> builder) => builder
    .AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(30), Name = "archidekt-total" })
    .AddRetry(new RetryStrategyOptions<RestResponse>
    {
        MaxRetryAttempts = 2,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = new PredicateBuilder<RestResponse>()
            .HandleResult(static r => r.StatusCode >= HttpStatusCode.InternalServerError)
            .Handle<Exception>(static ex => IsTransientException(ex)),
    });
```
This is a **fork from CONTEXT.md's D-03 as written** — CONTEXT.md pre-authorized exactly this flip
conditional on verification ("in that case register the pipeline at 112 after all"), so this is
not a new decision, it is D-03's own escape hatch being exercised with proof in hand.
`DeckFlow.Web.Extensions.HttpClientServiceCollectionExtensions` also needs (additively, inside the
NEW `AddDeckFlowCreatorStyle()` extension, not by editing that shared file) the named
`"archidekt-owner"` `HttpClient` registration `ArchidektOwnerClient`'s public constructor requires.

### Finding 3 — D-12 narrow exception: `PacketSessionCache.cs` needs one additive hunk

`CreatorStylePacketService.cs` (explicitly retained, D-07/design-doc) fails to compile with:
```
CreatorStylePacketService.cs(404,20): error CS0246: The type or namespace name 'CreatorStyleCacheInputs' could not be found
```
`CreatorStyleCacheInputs` is a `internal sealed record` **defined inside `PacketSessionCache.cs`**
on the c17 branch — nowhere else. The c17 diff for this file is purely additive (confirmed:
`git diff 5709f37c..plan/cycle-17-creator-style -- DeckFlow.Web/Services/PacketSessionCache.cs`
shows only insertions):

```csharp
internal sealed record CreatorStyleCacheInputs(
    string CreatorSlug,
    string NormalizedDeckSource,
    string Format);
```

plus one `PacketSizeEstimator.EstimateSizeBytes(CreatorStylePacketResult result)` overload (pure
addition, ~20 lines, sums string/collection lengths — no interaction with existing overloads).

**This directly narrows D-12.** D-12 says "no hunks land in ... `PacketSessionCache.cs`" — that
must become "no hunks land in `PacketSessionCache.cs` **except** the additive
`CreatorStyleCacheInputs` record + `PacketSizeEstimator.EstimateSizeBytes(CreatorStylePacketResult)`
overload, both required by `CreatorStylePacketService`." Verified this is unrelated to the
"`PacketSessionCache` bypass-list entry" PTOOL-02/D-12 actually cares about — that bypass list
(`PromptMutatingCreatorStyleFlags`, referenced from `CreatorStylePacketService.ShouldBypassPacketCache()`)
lives in `CreatorStylePacketService.cs` itself, not in `PacketSessionCache.cs`, and creator-style
adds no entry to any shared list because it has no public feature flags. No conflict with
PTOOL-02's actual intent.

### Finding 4 — `Program.cs` needs more than "one added line"

Confirmed via the c17 diff and a successful build reproduction. Two edits, not one:

**(a) DI registration** — one line, exactly as D-10 describes:
```csharp
builder.Services.AddDeckFlowCreatorStyle(builder.Environment);
// placed directly after the existing: builder.Services.AddDeckFlowScryfallServices();
```

**(b) Startup seed-load sequencing** — main's CURRENT shape (verified, line ~279):
```csharp
await app.Services.GetRequiredService<DeckFlow.Core.Content.IContentSiteIndexStore>().EnsureSchemaAsync();
await app.Services.GetRequiredService<IContentKbSeedLoader>().LoadIfPresentAsync();
app.Logger.LogInformation("Content site-index schema ensured and seed load completed during startup.");
```
must become (fork-join, both seed loads run concurrently, faults from BOTH logged before rethrow):
```csharp
await app.Services.GetRequiredService<DeckFlow.Core.Content.IContentSiteIndexStore>().EnsureSchemaAsync();
Task contentKbSeedTask = app.Services.GetRequiredService<IContentKbSeedLoader>().LoadIfPresentAsync();
Task creatorStyleSeedTask = app.Services.GetRequiredService<ICreatorStyleSeedLoader>().LoadIfPresentAsync();
await AwaitStartupSeedTasksAsync(contentKbSeedTask, creatorStyleSeedTask, app.Logger);
app.Logger.LogInformation("Content site-index schema ensured and seed load completed during startup.");
```
plus two new `internal`/`private` static helper methods (`AwaitStartupSeedTasksAsync`,
`LogFaultedSeedTask` — ~35 lines total, both pure additions after `DeriveAdminPartitionKey`).
This rewrite is **only required if `DeckFlow.Web.Tests/ProgramStartupTests.cs` is ported** — that
test directly asserts on `Program.AwaitStartupSeedTasksAsync`'s dual-fault-logging behavior. If
the planner chooses NOT to port `ProgramStartupTests.cs`, the simpler two-line sequential form
(`await ...LoadIfPresentAsync(); await ...LoadIfPresentAsync();`) also compiles and satisfies
PORT-02's "resolve through DI" bar — **recommend porting the fork-join version**, since it is what
D-11 means by "wired AND invoked," it is what the ported test asserts, and it is a strict
improvement (both seed sources get logged on fault instead of the first one aborting silently).

### Finding 5 — three more files where a wholesale branch-file copy would have reverted main-only work

This is the concrete proof that D-05/D-09's "never checkout branch -- file" rule is necessary, not
theoretical. During this research session's build probe, wholesale-copying these 3 files from the
c17 branch (instead of hand-applying only the new members onto main's current content) **broke the
build by deleting members main added independently after the branch forked**:

| File | What main added after the fork that c17 lacks | Consequence of wholesale copy |
|------|------------------------------------------------|-------------------------------|
| `DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs` | `CreateManabaseBaselineConnection(...)` | `ManabaseBaselineStore.cs` fails: `CS0117: 'DeckFlowDatabaseConnectionFactory' does not contain a definition for 'CreateManabaseBaselineConnection'` |
| `DeckFlow.Core/Integration/ILlmDistillationService.cs` | `ExtractCombinedAsync(...)` default-interface method | `ContentKbOrchestrator.cs` fails: `CS1061: 'ILlmDistillationService' does not contain a definition for 'ExtractCombinedAsync'` |
| `DeckFlow.Core/Knowledge/DistillationResults.cs` | `CombinedExtractionResult` record | (would silently reorder/lose the record if blindly diff-applied at the old anchor point) |

**Fix applied and verified:** in every case, start from `git show HEAD:<path>` (main's CURRENT
content) and append ONLY the new members c17 introduces, never take the branch's file wholesale.
This is exactly D-09's mandate; this finding is the empirical proof it matters, not new guidance.

## Contamination Audit

Raw branch diff `git diff --name-status 5709f37c..plan/cycle-17-creator-style`: **397 added, 305
deleted, 110 modified** files (deletions and most modifications are the −57,732-line
planning-doc archival churn, out of scope entirely — never touch `.planning/` on this branch).

Of the 397 added files, 153 fall under the creator-style-relevant prefixes
(`DeckFlow.Core/**`, `DeckFlow.Core.Tests/**`, `DeckFlow.Web/**`, `DeckFlow.Web.Tests/**`,
`DeckFlow.CLI/**`, `content-kb/**`). **37 of those 153 are already present on `main`** (Cycle 16
Content-KB work landed independently) — CONTEXT.md named 4; research found all 37:

```
DeckFlow.Core/Content/ContentBodyHashBackfill.cs
DeckFlow.Core/Content/ContentKbArtifactPath.cs
DeckFlow.Core/Content/ContentKbPaths.cs                    <- DIFFERS from main, see below
DeckFlow.Core/Content/ContentKbReconcileClassifier.cs
DeckFlow.Core/Content/ContentKbReconcileDiscrepancy.cs
DeckFlow.Core/Content/ContentNaturalKey.cs
DeckFlow.Core/Content/ContentSiteIndexReadModel.cs
DeckFlow.Core/Content/IContentArtifactBodyResolver.cs
DeckFlow.Core/Content/SeedIndexFileReader.cs
DeckFlow.Core/Content/SeedManagedBackfill.cs
DeckFlow.Core.Tests/Content/ContentBodyHashBackfillTests.cs
DeckFlow.Core.Tests/Content/ContentKbArtifactPathTests.cs
DeckFlow.Core.Tests/Content/ContentKbReconcileClassifierTests.cs
DeckFlow.Core.Tests/Content/ContentSiteIndexContentSignatureTests.cs
DeckFlow.Core.Tests/Content/ContentSiteIndexReadModelTests.cs
DeckFlow.Core.Tests/Content/ContentSiteIndexStoreAwaitingConfirmSetClearTests.cs
DeckFlow.Core.Tests/Content/ContentSiteIndexStoreAwaitingConfirmTests.cs
DeckFlow.Core.Tests/Content/ContentSiteIndexStoreBodyHashTests.cs
DeckFlow.Core.Tests/Content/ContentSiteIndexStoreSchemaEnsureSwitchTests.cs
DeckFlow.Core.Tests/Content/OneSignatureSurfaceGuardTests.cs
DeckFlow.Core.Tests/Content/SeedIndexFileReaderTests.cs
DeckFlow.Core.Tests/Content/SeedManagedBackfillTests.cs
DeckFlow.Core.Tests/Content/SeedManagedSchemaTests.cs
DeckFlow.Core.Tests/Content/SeedManagedWritePathTests.cs
DeckFlow.Core.Tests/Orchestration/ContentIndexExportRowTests.cs
DeckFlow.Core.Tests/Orchestration/ContentKbOrchestratorBodyHashTests.cs
DeckFlow.Web.Tests/Controllers/ContentKbDeployedBodyControllerTests.cs
DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripHarness.cs
DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSeams.cs
DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSmokeTests.cs
DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSyncLoopTests.cs   <- DIFFERS, but irrelevant (Postgres-test assertion tweak, not creator-style)
DeckFlow.Web.Tests/Services/FeatureFlags/ReconcileFeatureFlagTests.cs
DeckFlow.Web.Tests/TestDoubles/FakeLogger.cs
DeckFlow.Web/Controllers/Admin/ContentKbDeployedBodyController.cs
DeckFlow.Web/Services/Content/ContentKbArtifactBodyResolver.cs
DeckFlow.Web/Services/Content/WebSeedKeyMembershipSource.cs
DeckFlow.Web/e2e/content-kb-pending-hidden.spec.ts
```

**Do NOT port any of these 37 — all are already on main.** For 35 of them, main's copy is
byte-identical to the branch's; verified via `git diff --quiet HEAD plan/cycle-17-creator-style --
<path>` for every file. The 2 exceptions:
- `ContentKbPaths.cs` — DIFFERS, and this difference IS load-bearing for the port (see M-file
  hunk table: `CreatorStyleSeedLoader` needs the two new path constants it defines).
- `RoundTripSyncLoopTests.cs` — DIFFERS, but the diff is an unrelated Postgres-integration-test
  assertion tweak (`Assert.False(string.IsNullOrEmpty(commitResult.Sha))` →
  `Assert.False(commitResult.LocalStampFailed)`), pre-dating and unconnected to creator-style. No
  action; leave main's version as-is.

## Port Allowlist — Commit 1 (Core)

**Build-verified: `dotnet build DeckFlow.Core/DeckFlow.Core.csproj` → 0 errors, 0 warnings** with
this exact file set plus the Core-side M-file hunks below.

```
git checkout plan/cycle-17-creator-style -- \
  DeckFlow.Core/Content/CreatorDeckCacheEntry.cs \
  DeckFlow.Core/Content/CreatorDeckCacheStore.cs \
  DeckFlow.Core/Content/CreatorProfileSource.cs \
  DeckFlow.Core/Content/CreatorProfileSourceStore.cs \
  DeckFlow.Core/Content/CreatorStyleProfileReadModel.cs \
  DeckFlow.Core/Content/CreatorStyleProfileStore.cs \
  DeckFlow.Core/Content/CreatorStyleProfileSummary.cs \
  DeckFlow.Core/Content/ICreatorDeckCacheStore.cs \
  DeckFlow.Core/Content/ICreatorProfileSourceStore.cs \
  DeckFlow.Core/Content/ICreatorStyleProfileStore.cs \
  DeckFlow.Core/Knowledge/CardGrounding/CardGroundingBatchResult.cs \
  DeckFlow.Core/Knowledge/CardGrounding/CardGroundingDeckContext.cs \
  DeckFlow.Core/Knowledge/CardGrounding/CardGroundingRejectReason.cs \
  DeckFlow.Core/Knowledge/CardGrounding/CardGroundingRules.cs \
  DeckFlow.Core/Knowledge/CardGrounding/CardGroundingVerdict.cs \
  DeckFlow.Core/Knowledge/CardGrounding/ICardGroundingGuard.cs \
  DeckFlow.Core/Knowledge/CreatorStyleProfile.cs \
  DeckFlow.Core/Knowledge/CreatorStyleProfileSections.cs \
  DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs \
  DeckFlow.Core/Knowledge/CreatorStyleRubric/RubricScoreResult.cs \
  DeckFlow.Core/Knowledge/CreatorStyleRubric/SubmittedDeckStats.cs \
  DeckFlow.Core/Knowledge/GlobalCategoryBaseline.cs \
  DeckFlow.Core/Knowledge/MeasuredStyleExtraction/CategoryCounter.cs \
  DeckFlow.Core/Knowledge/MeasuredStyleExtraction/CreatorDeckSample.cs \
  DeckFlow.Core/Knowledge/MeasuredStyleExtraction/FolderWeighting.cs \
  DeckFlow.Core/Knowledge/MeasuredStyleExtraction/LiftCalculator.cs \
  DeckFlow.Core/Knowledge/MeasuredStyleExtraction/MeasuredStyleInputs.cs \
  DeckFlow.Core/Knowledge/MeasuredStyleExtraction/StapleStripper.cs \
  DeckFlow.Core/Knowledge/ProfileFusion/ConflictCalculator.cs \
  DeckFlow.Core/Knowledge/ProfileFusion/MetricClassification.cs \
  DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs \
  DeckFlow.Core/Knowledge/ProfileFusion/StatedMetricKeyMapper.cs \
  DeckFlow.Core/Knowledge/ProfileFusion/StatedRuleRecencyCollapser.cs \
  DeckFlow.Core/Knowledge/StatedRulesExtraction/ContentTypeHeuristic.cs \
  DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs \
  DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleCandidate.cs \
  DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleReducer.cs \
  DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRulesExtractor.cs \
  DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRulesMetricVocabulary.cs \
  DeckFlow.Core/Knowledge/StatedRulesExtraction/TranscriptChunker.cs \
  DeckFlow.Core.Tests/CreatorDeckCacheStoreTests.cs \
  DeckFlow.Core.Tests/CreatorProfileSourceStoreTests.cs \
  DeckFlow.Core.Tests/CreatorStyleProfileAdditiveRoundTripTests.cs \
  DeckFlow.Core.Tests/CreatorStyleProfileStoreTests.cs \
  DeckFlow.Core.Tests/CreatorStyleProfileTestData.cs \
  DeckFlow.Core.Tests/Knowledge/CardGrounding/CardGroundingRulesTests.cs \
  DeckFlow.Core.Tests/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorerTests.cs \
  DeckFlow.Core.Tests/MeasuredStyleExtraction/CategoryCounterTests.cs \
  DeckFlow.Core.Tests/MeasuredStyleExtraction/FolderWeightingTests.cs \
  DeckFlow.Core.Tests/MeasuredStyleExtraction/LiftCalculatorTests.cs \
  DeckFlow.Core.Tests/MeasuredStyleExtraction/StapleStripperTests.cs \
  DeckFlow.Core.Tests/ProfileFusion/ConflictCalculatorTests.cs \
  DeckFlow.Core.Tests/ProfileFusion/MetricClassificationTests.cs \
  DeckFlow.Core.Tests/ProfileFusion/ProfileFusionEngineTests.cs \
  DeckFlow.Core.Tests/ProfileFusion/StatedMetricKeyMapperTests.cs \
  DeckFlow.Core.Tests/ProfileFusion/StatedRuleRecencyCollapserTests.cs \
  DeckFlow.Core.Tests/StatedRulesExtraction/CliLlmDistillationStatedRulesGoldenTests.cs \
  DeckFlow.Core.Tests/StatedRulesExtraction/ContentTypeHeuristicTests.cs \
  DeckFlow.Core.Tests/StatedRulesExtraction/Fixtures/salubrious-snail-transcript.txt \
  DeckFlow.Core.Tests/StatedRulesExtraction/StatedRuleCandidateVocabularyTests.cs \
  DeckFlow.Core.Tests/StatedRulesExtraction/StatedRuleReducerTests.cs \
  DeckFlow.Core.Tests/StatedRulesExtraction/StatedRulesExtractorTests.cs \
  DeckFlow.Core.Tests/StatedRulesExtraction/TranscriptChunkerTests.cs \
  DeckFlow.Core.Tests/StatedRulesExtraction/ValidateStatedRulesTests.cs
```

**Two files explicitly excluded from `CreatorDeckCacheStoreTests.cs` and
`CreatorProfileSourceStoreTests.cs` above** need a post-checkout trim, not a straight copy — see
`## Test Port Inventory` for the exact class to delete from each.

**Then apply the Core-side hunks** from `## M-File Hunk Inventory` (`ContentKbPaths.cs`,
`AssemblyInfo.cs`, `ILlmDistillationService.cs`, `DistillationResults.cs`,
`DistillationValidation.cs`, `ContentTagVocabulary.cs`, `CommanderInference.cs`,
`CategoryKnowledgeRepository.cs`, `CardCategoryRepository.cs`), plus the
`DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` `<None Include>` addition (fixture copy item —
**do NOT add the `Testcontainers.PostgreSql` PackageReference**, D-15 excludes the tests that need it).

## Port Allowlist — Commit 2 (Web)

**Build-verified: `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` and
`DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` → 0 errors, 0 warnings**, given Commit 1 already
landed plus the Web-side M-file hunks below.

```
git checkout plan/cycle-17-creator-style -- \
  DeckFlow.Web/Models/CreatorStyleRequest.cs \
  DeckFlow.Web/Services/Content/CreatorStyleSeedLoader.cs \
  DeckFlow.Web/Services/Content/ICreatorStyleSeedLoader.cs \
  DeckFlow.Web/Services/CreatorStyle/ArchidektOwnerClient.cs \
  DeckFlow.Web/Services/CreatorStyle/ArchidektOwnerUrl.cs \
  DeckFlow.Web/Services/CreatorStyle/CreatorDeckCategoryResolver.cs \
  DeckFlow.Web/Services/CreatorStyle/CreatorDeckExemplarSelector.cs \
  DeckFlow.Web/Services/CreatorStyle/CreatorProfileDeckCrawler.cs \
  DeckFlow.Web/Services/CreatorStyle/CreatorStyleDeckAnalysis.cs \
  DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs \
  DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs \
  DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs \
  DeckFlow.Web/Services/CreatorStyle/SubmittedDeckStatsBuilder.cs \
  DeckFlow.Web/Services/Scryfall/CachedNameResolution.cs \
  DeckFlow.Web/Services/Scryfall/CardGroundingGuard.cs \
  DeckFlow.Web/Services/Scryfall/ScryfallBatching.cs \
  DeckFlow.Web/Services/Scryfall/ScryfallCardNameGrounder.cs \
  DeckFlow.Web/Services/Scryfall/ScryfallCollectionResolver.cs \
  DeckFlow.Web/Services/Scryfall/ScryfallErrorResponse.cs \
  DeckFlow.Web/Services/Scryfall/ScryfallLimits.cs \
  DeckFlow.Web/Services/SeedJson.cs \
  DeckFlow.Web.Tests/CreatorStyleSeedLoaderTests.cs \
  DeckFlow.Web.Tests/ProgramStartupTests.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/ArchidektOwnerClientTests.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/CreatorDeckExemplarSelectorTests.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/CreatorProfileDeckCrawlerTests.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStyleDeckAnalysisTests.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStyleDiRegistrationTests.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStylePacketServiceTests.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/CreatorWhitelistPoolBuilderTests.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/MeasuredStyleProfileBuilderTests.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/SnailSeedCorpusFixture.cs \
  DeckFlow.Web.Tests/Services/CreatorStyle/SubmittedDeckStatsBuilderTests.cs \
  DeckFlow.Web.Tests/Services/Scryfall/CardGroundingGuardTests.cs \
  DeckFlow.Web.Tests/Services/Scryfall/CardGroundingHallucinationFixtureTests.cs \
  DeckFlow.Web.Tests/Services/Scryfall/ScryfallCardNameGrounderTests.cs

# new files (not from the branch — write fresh):
#   DeckFlow.Web/Extensions/CreatorStyleServiceCollectionExtensions.cs   (D-10's new DI extension)

# seed placeholders (D-11):
git checkout plan/cycle-17-creator-style -- \
  content-kb/seed/creator-style-profiles.json \
  content-kb/seed/creator-deck-cache.json
```

**Explicitly excluded** (D-06, confirmed by successful build without them):
`DeckFlow.Web/Controllers/CreatorStyleController.cs`, `DeckFlow.Web/Models/CreatorStyleViewModel.cs`,
`DeckFlow.Web/Views/Deck/CreatorStyle.cshtml`, `DeckFlow.Web/Help/creator-style.md`,
`DeckFlow.Web/e2e/creator-style.spec.ts`, `DeckFlow.Web.Tests/CreatorStyleControllerTests.cs`,
`DeckFlow.Web.Tests/CreatorStyleViewRenderTests.cs` — all Phase 114.
`DeckFlow.CLI/CreatorStyleCommandRunners.cs` — Phase 115.

**Then apply the Web-side hunks** from `## M-File Hunk Inventory`
(`DeckFlowDatabaseConnectionFactory.cs`, `ScryfallDtos.cs`, `ScryfallCardResolver.cs`,
`PacketSessionCache.cs`, `Program.cs`) plus write the new
`Extensions/CreatorStyleServiceCollectionExtensions.cs` (full contents below).

### New file: `DeckFlow.Web/Extensions/CreatorStyleServiceCollectionExtensions.cs`

Build-verified skeleton (the planner/Codex should add XML doc comments per repo convention —
the probe below has minimal comments only):

```csharp
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
using DeckFlow.Web.Services.Scryfall;

namespace DeckFlow.Web.Extensions;

public static class CreatorStyleServiceCollectionExtensions
{
    public static IServiceCollection AddDeckFlowCreatorStyle(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddHttpClient("archidekt-owner", c =>
        {
            c.BaseAddress = new Uri("https://archidekt.com/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json;q=0.9,*/*;q=0.8");
        });

        services.AddSingleton<ICardNameGrounder, ScryfallCardNameGrounder>();
        services.AddSingleton<ICardGroundingGuard, CardGroundingGuard>();

        services.AddSingleton<ICreatorDeckCacheStore>(_ =>
            new CreatorDeckCacheStore(DeckFlowDatabaseConnectionFactory.CreateCreatorDeckCacheConnection(environment)));
        services.AddSingleton<ICreatorProfileSourceStore>(_ =>
            new CreatorProfileSourceStore(DeckFlowDatabaseConnectionFactory.CreateCreatorDeckCacheConnection(environment)));
        services.AddSingleton<CategoryKnowledgeRepository>(_ =>
            new CategoryKnowledgeRepository(DeckFlowDatabaseConnectionFactory.CreateCategoryKnowledgeConnection(environment)));
        services.AddSingleton<ICreatorStyleProfileStore>(_ =>
            // Why: creator-style profiles live in local-only content-kb.db (D-14 in the c17 design;
            // Cycle 20's design doc reaffirms it — production never crawls, only reads seeds).
            new CreatorStyleProfileStore(DeckFlowDatabaseConnectionFactory.CreateLocalContentKbConnection(environment)));
        services.AddSingleton<CreatorWhitelistPoolBuilder>();
        services.AddSingleton<ICreatorStyleSeedLoader, CreatorStyleSeedLoader>();

        services.AddSingleton<IArchidektOwnerClient, ArchidektOwnerClient>();
        services.AddScoped<CreatorProfileDeckCrawler>();
        services.AddScoped<CreatorDeckCategoryResolver>();
        services.AddScoped<MeasuredStyleProfileBuilder>();
        services.AddScoped<ISubmittedDeckStatsBuilder, SubmittedDeckStatsBuilder>();
        services.AddScoped<ICreatorStylePacketService, CreatorStylePacketService>();

        return services;
    }
}
```
Invoked from `Program.cs` as `builder.Services.AddDeckFlowCreatorStyle(builder.Environment);`
directly after the existing `builder.Services.AddDeckFlowScryfallServices();` line.

## M-File Hunk Inventory

Every entry below was hand-derived against `main`'s CURRENT `HEAD` content (not the stale
merge-base) and build-verified. All are additive-only (new members; zero deletions, zero
signature changes to existing members) — this preserves the zero-regression-risk spirit even
where they touch files CONTEXT.md didn't originally list.

| File | Tier | What's added | Why required | Drift risk if copied wholesale from branch |
|------|------|---------------|---------------|---------------------------------------------|
| `DeckFlow.Core/Content/ContentKbPaths.cs` | Core | 2 new `const string` path constants (`CreatorStyleProfileSeedRelativePath`, `CreatorDeckCacheSeedRelativePath`) | `CreatorStyleSeedLoader` references both | LOW — main's file is a strict subset of c17's; wholesale copy would actually be safe here, but hand-apply for consistency |
| `DeckFlow.Core/AssemblyInfo.cs` | Core | `[assembly: InternalsVisibleTo("DeckFlow.Web")]` | `CreatorProfileDeckCrawler.cs` (Web, production code) reads `StapleStripper.MaxDeckSize`, an `internal const int` in Core | LOW — additive line, main hasn't touched this file |
| `DeckFlow.Core/Integration/ILlmDistillationService.cs` | Core | 4 new interface methods (`SelectStatedClaimsAsync`, `DisambiguateStatedClaimsAsync`, `DecomposeStatedClaimsAsync`, `ReduceStatedRulesAsync`), all with `NotSupportedException`-throwing default impls | `StatedRulesExtractor.cs` calls all 4 | **HIGH if copied wholesale** — main independently added `ExtractCombinedAsync()` after the branch forked; a raw branch-file copy deletes it and breaks `ContentKbOrchestrator.cs`. Must append new members onto main's current file. |
| `DeckFlow.Core/Knowledge/DistillationResults.cs` | Core | 4 new `sealed record` result types (`SelectResult`, `DisambiguateResult`, `DecomposeResult`, `ReduceResult`) | Return types for the 4 interface methods above | **MEDIUM if copied wholesale** — main independently added a `CombinedExtractionResult` record at a different position; append rather than replace |
| `DeckFlow.Core/Knowledge/DistillationValidation.cs` | Core | 1 new const (`MaxStatedRulesPerVideo = 40`), 2 new internal static methods (`SanitizeStatedRules`, `ValidateStatedRules`), 4 new payload records (`SelectPayload`, `DisambiguatePayload`, `StatedRulePayload`, `RulesPayload`) | `StatedRulesExtractor.cs` calls `SanitizeStatedRules`/`ValidateStatedRules`; only depends on `StatedRuleCandidate` + `StatedRulesMetricVocabulary`, both in the closure | MEDIUM — main has diverged elsewhere in this file since the fork (unrelated additions); anchor points must be re-verified, not assumed stable |
| `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` | Core | 1 new `IReadOnlySet<string> Staples` field (11 curated card names) | `StapleStripper.cs` references `ContentTagVocabulary.Staples` | LOW — main's insertion anchor is byte-identical to the branch's; safe to hand-apply directly |
| `DeckFlow.Core/Loading/CommanderInference.cs` | Core | 1 new public static method `ReflagInferredCommanders(List<DeckEntry>)` | `SubmittedDeckStatsBuilder.cs` (Web) calls it | LOW — pure append at end of file |
| `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | Core | 1 new public method `GetGlobalCategoryBaselineAsync` (delegates to `CardCategoryRepository`) | `MeasuredStyleProfileBuilder.cs` (Web) calls it | LOW — pure append |
| `DeckFlow.Core/Knowledge/CardCategoryRepository.cs` | Core | 1 new `internal async Task<GlobalCategoryBaseline> GetGlobalCategoryBaselineAsync(...)` (SQL query + row mapper), 1 new private helper `BuildCategoryPairKey`, 1 new nested `GlobalCategoryBaselineRow` class | Backs the method above | LOW — pure append at two verified-stable anchor points |
| `DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs` | Web | 1 new public method `CreateCreatorDeckCacheConnection` → `"creator-deck-cache.db"` | `CreatorDeckCacheStore`/`CreatorProfileSourceStore` DI factories need it | **HIGH if copied wholesale** — main independently added `CreateManabaseBaselineConnection` after the fork; wholesale copy deletes it and breaks `ManabaseBaselineStore.cs`. **Note:** CONTEXT.md's canonical_refs pointed at `:72` expecting a `content-kb.db` binding hunk — that binding (`CreateLocalContentKbConnection`) is ALREADY on main (Cycle-16 contamination); the real gap is this NEW `creator-deck-cache.db` method. |
| `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs` | Web | 1 new optional record property `Legalities` on `ScryfallCard` | `CardGroundingGuard.cs` reads it | LOW — additive record parameter, last position |
| `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` | Web | 1 new interface method + concrete override, `ExecuteNamedFuzzyAsync` | `CardGroundingGuard.cs` calls it | LOW — default-impl pattern means no other implementer breaks |
| `DeckFlow.Web/Services/PacketSessionCache.cs` | Web | 1 new `internal sealed record CreatorStyleCacheInputs`, 1 new `PacketSizeEstimator.EstimateSizeBytes(CreatorStylePacketResult)` overload | `CreatorStylePacketService.cs` requires both | LOW — additive; **this is the D-12 exception, flag prominently in the plan** |
| `DeckFlow.Web/Program.cs` | Web | 1 line (`AddDeckFlowCreatorStyle` call) + startup fork-join rewrite + 2 new static helper methods | D-10's DI wiring + D-11's real startup exercise + (if `ProgramStartupTests.cs` ported) test target | LOW — additive, but touches the most-contested file in the repo; keep the diff as small as physically possible |

**Not required** (verified by successful build without touching them): `PublishStateDeriver`
already registered on main; `ContentKbSeedLoader.cs`'s `SeedJson.Options` dedup (superficially in
the c17 diff, but main's file already has independent Cycle-16 improvements — `BodySha256`,
`SeedManaged`, `ApprovalStatus` — that make it a strict superset; the `SeedJson` refactor there is
optional Phase-113-flavored cleanup, not a 112 requirement); `ManabaseAnalysisService.cs`,
`CardLookupService.cs`, `ScryfallReferenceResolver.cs` (Phase 113, confirmed compile-clean
untouched); `FeatureFlagCatalog.cs`, `FeatureFlagStore.cs`, `ToolRegistry.cs`,
`Models/DeckPageTab.cs`, `Help/creator-style.md` (D-12, confirmed no closure pull-in).

**New package references: NONE required.** `DeckFlow.Web.csproj` and `DeckFlow.Core.csproj` have
zero diff between merge-base and branch head — confirmed via `git diff`. The only package-adjacent
change in the whole port (`Testcontainers.PostgreSql` in `DeckFlow.Core.Tests.csproj`) is
correctly NOT needed once the Postgres-fixture test files are excluded per D-15 (sharpened below).

## Test Port Inventory

### Core.Tests — port (24 files, post-trim)
All files listed in `## Port Allowlist — Commit 1`'s `DeckFlow.Core.Tests/**` block, **with two
files requiring a trim before checkout, not a straight copy:**

- **`CreatorDeckCacheStoreTests.cs`** — contains TWO test classes in one file. Keep
  `CreatorDeckCacheStoreTests` (plain SQLite, `IDisposable`, no Postgres dependency). **Delete**
  the trailing `CreatorDeckCacheStoreTestsPostgres : IClassFixture<PostgresContainerFixture>`
  class (starts ~line 282 on the branch).
- **`CreatorProfileSourceStoreTests.cs`** — same pattern. Keep `CreatorProfileSourceStoreTests`
  (SQLite). **Delete** `CreatorProfileSourceStoreTestsPostgres : IClassFixture<PostgresContainerFixture>`
  (starts ~line 166).

### Core.Tests — exclude (5 files beyond D-15's named 3)

| File | Reason |
|------|--------|
| `DeckFlow.Core.Tests/Integration/PostgresContainerFixture.cs` | D-15, named |
| `DeckFlow.Core.Tests/Integration/PostgresFactAttribute.cs` | D-15, named |
| `DeckFlow.Core.Tests/CreatorStyleProfileStorePostgresTests.cs` | D-15, named |
| `DeckFlow.Core.Tests/ContentVideoStoreStatedRulesReadTests.cs` | **⚠ D-15 gap, found this session.** Entire class is `IClassFixture<PostgresContainerFixture>` — no SQLite variant exists in this file to salvage. Wholesale exclude. |
| `DeckFlow.Core.Tests/Content/CreatorStyleSeedSerializationTests.cs` | Tests `CreatorStyleCommandRunners` (CLI, Phase 115 scope) — build fails `CS0103` without the CLI class ported. Belongs at 115. |
| `DeckFlow.Core.Tests/ProfileFusion/FuseProfileRunnerTests.cs` | Tests `ContentKbCommandRunners.RunFuseProfileAsync` and `ContentVideoStore.InsertStatedRuleAsync` (CLI-layer orchestration + a Core method that doesn't exist yet on any branch state relevant to 112) — this is literally PSEED-03's Phase 115 acceptance test (`fuse-profile produces FusedTarget[]...reproduces P89/P90 verdicts`). Belongs at 115, not 112. |

`ProfileFusionEngineTests.cs` (tests the engine directly, no CLI dependency) stays IN — do not
confuse it with `FuseProfileRunnerTests.cs` (tests the CLI wrapper around the engine).

### Web.Tests — port (15 files)
All files listed in `## Port Allowlist — Commit 2`'s `DeckFlow.Web.Tests/**` block. None require
trimming — no Postgres-fixture usage found in any Web.Tests candidate.

`CreatorStyleDiRegistrationTests.cs` ports as-is but **recommend the planner add one more
assertion or a companion test** that resolves `IArchidektOwnerClient` through the REAL DI graph
(not the c17 file's `FakeArchidektOwnerClient`) — see Critical Finding #2. The existing test alone
does not prove success criterion 3 against the real `archidekt`-pipeline risk.

### Web.Tests / Core.Tests — exclude (Phase 114 admin-surface tests)
`DeckFlow.Web.Tests/CreatorStyleControllerTests.cs`, `DeckFlow.Web.Tests/CreatorStyleViewRenderTests.cs`
— test the dropped public controller/view. Phase 114 rewrites `CreatorStyleViewRenderTests`
against the new admin view per the design doc.

### Phase-100 public-surface suites — never port (D-14)
Not present in the 112 candidate set at all (none of them live under `DeckFlow.Core.Tests/**` or
`DeckFlow.Web.Tests/CreatorStyle*`/`Services/CreatorStyle/**` — they're Phase-100-only additions
this port doesn't touch). For Phase 114's PORT-04 exclusion-proof, the class/file names to confirm
absent are: the 6 feature-flag lockstep suites gated on `tool.creator-style.enabled`,
`ToolRegistryTests` count assertions for a creator-style tile, any route-gate coverage test for
`/creator-style`, and any sitemap/SEO assertion mentioning creator-style. (112 never introduces
these — nothing to actively strip.)

### Test framework / run commands

- Framework: **xUnit 2.9.3** (`DeckFlow.Core.Tests.csproj`, `DeckFlow.Web.Tests.csproj` — both
  already pinned, confirmed no version change needed).
- **VSTest is documented-unreliable in WSL** (CLAUDE.md). Prefer `dotnet build` as the PRIMARY
  success gate for this phase (success criteria 2–4 are build/DI/compile concerns, not full-suite
  execution concerns). For the ported-tests-pass criterion, run:
  ```bash
  DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"
  "$DOTNET" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~CreatorStyle|FullyQualifiedName~StatedRules|FullyQualifiedName~ProfileFusion|FullyQualifiedName~MeasuredStyleExtraction|FullyQualifiedName~CardGrounding"
  "$DOTNET" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CreatorStyle|FullyQualifiedName~CardGrounding|FullyQualifiedName~ScryfallCardNameGrounder|FullyQualifiedName~ProgramStartup"
  ```
  scoped to just the ported suites first (fast, isolates port-specific failures), THEN the full
  suite (see Validation Architecture) to prove no regression. Do NOT set `MTG_DATA_DIR`.

### Current green baseline (feat/personal-tools, BEFORE this port — captured this session)

- `dotnet build DeckFlow.sln` → **0 errors, 9 warnings** (all `CS8629` nullable-value-may-be-null
  in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`, pre-existing and unrelated
  to this port). **This 9-warning count is the baseline for success criterion 2** — after the
  port, `dotnet build DeckFlow.sln` must still show exactly 9 (or fewer) `CS8629` warnings and
  zero warnings of any other kind. Any new warning ID, or a warning count above 9, fails criterion 2.
- Static `[Fact]`/`[Theory]` attribute count (proxy for test count — actual executed count is
  higher once `[Theory]`/`[InlineData]` combinations expand):
  - `DeckFlow.Core.Tests`: **1334** `[Fact]`/`[Theory]` declarations
  - `DeckFlow.Web.Tests`: **1576** `[Fact]`/`[Theory]` declarations
  - (Design doc's stated ~1433 Core / ~1374 Web are the counts AT THE C17 BRANCH HEAD, a
    different, larger population that includes all the Phase-100 public-surface + already-shipped
    Cycle-16/18/19 suites — not directly comparable to the pre-port `feat/personal-tools` baseline
    above. Use the numbers in this bullet as the "before" baseline for this specific phase.)

## Seed Placeholder Mechanics

- **Shipping mechanism (matches `index-seed.json` precedent exactly):** `content-kb/**` is NOT
  wired through any `.csproj` `<Content>` item. It is un-ignored in `.dockerignore`
  (`!content-kb/`, `!content-kb/**`, `!content-kb/**/*.md`) and copied verbatim by the Dockerfile
  (`COPY content-kb/ ./content-kb/`, `Dockerfile:55`). At runtime, `ContentKbArtifactPathResolver`
  resolves the seed path relative to `ContentBase` (content root), read directly with
  `File.OpenRead` — no build step involved. Placing `creator-style-profiles.json` /
  `creator-deck-cache.json` under `content-kb/seed/` is sufficient; no csproj change needed.
- **Empty-array handling: [VERIFIED SAFE, no guard task needed.]** `CreatorStyleSeedLoader.LoadProfilesIfPresentAsync`/
  `LoadDeckCacheIfPresentAsync` deserialize with `JsonSerializer.DeserializeAsync<T[]>(stream, SeedJson.Options, ct) ?? Array.Empty<T>()`.
  `"[]"` deserializes to a zero-length array (not `null`), so the `?? Array.Empty<T>()` fallback
  never even triggers; the subsequent `foreach` loop over zero elements is a no-op; the method
  returns `0`. Confirmed by reading the actual source — no defensive coding needed at 112.

## Format Gate Reality Check

`scripts/format-check-changed.sh staged` runs `dotnet format DeckFlow.sln --include <changed
files> --verify-no-changes --report ... --no-restore`, then intersects reported violations against
changed LINE RANGES from `git diff --cached --unified=0`. **For a brand-new file, every line is a
"changed line"** — there is no off-hunk exemption the way there is for a small edit to an existing
file. This means the ~99 net-new `.cs` files in this port get zero slack: every line must already
satisfy `dotnet format`'s house style.

**[VERIFIED]:** ran `dotnet format DeckFlow.sln --include <all 99 candidate .cs files>
--verify-no-changes --no-restore` against the exact port set in the disposable worktree —
**exit 0, zero violations, ~21 seconds.** The c17 branch's files, as originally authored, already
conform to this repo's CURRENT `.editorconfig` (unsurprising: Cycle 17 pre-dates this gate's
carve-out hardening but the base style rules — 4-space indent, Allman braces, file-scoped
namespaces — were already the house style then). **No reformatting pass should be needed for the
ported files themselves.** The planner should still run the gate for real once the actual git
`checkout`+hunk-apply is done (this research used hand-copied file content in a scratch worktree,
not `git checkout -- <path>`, so line-ending/BOM fidelity of the real checkout should be
independently confirmed — but the content-level formatting is proven clean).

The 9 hand-authored M-file hunks in this research (written fresh by this research session as a
build probe, not taken from any authoritative source) were NOT run through the format gate and
should NOT be treated as copy-paste-ready for the real port — they establish the correct MEMBERS
and are logically/structurally identical to the c17 branch's originals (copied verbatim in most
cases), but Codex should re-derive them fresh against the gate rather than trusting this
document's code blocks byte-for-byte.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Determining what's already on main vs. needs porting | Manual `.planning` doc archaeology | `git diff --name-status <merge-base>..<branch>` + a per-file `git diff --quiet HEAD <branch> -- <path>` byte-identity check | Purely mechanical; a byte-identity check catches drift a "looks similar" read would miss (see `ContentKbPaths.cs` finding) |
| Verifying a compile closure is complete | Reading imports/usings and reasoning about them | An actual `dotnet build` in a disposable worktree | Static reasoning missed 9 of the ~15 M-file hunks in this research; only the build surfaced them |
| Deciding if Polly's `GetPipeline<T>` throws | Trusting training-data recollection of Polly's API | A 15-line isolated repro against the pinned package version | Polly's throw/no-throw behavior is exactly the kind of API-shape claim that must be verified per-version, not assumed |

**Key insight:** for a port phase specifically, "does it compile" is not a plannable claim from
diff-reading alone — Core and Web have 9 independent additive-hunk dependencies on shared files
that no amount of `git diff` review would have found with full confidence without actually
invoking the compiler. Budget for an actual build-verification step (this research already did
it; the planner does not need to repeat the discovery, only the mechanical application).

## Common Pitfalls

### Pitfall 1: Wholesale branch-file copy on a file `main` has independently evolved
**What goes wrong:** `git checkout branch -- <path>` (or copying the branch's whole file content)
silently reverts any member main added to that file after the fork.
**Why it happens:** The diff LOOKS purely additive when read as `git diff merge-base..branch`, but
that diff is relative to a stale base — it says nothing about what main added independently.
**How to avoid:** For every M-file, diff `merge-base..HEAD` (not `merge-base..branch`) first. If
non-empty, hand-apply the branch's NEW members onto `git show HEAD:<path>`, never take the
branch's file wholesale.
**Warning signs:** `error CS0117`/`CS1061` referencing a member that "should" exist per the c17
diff but the build says doesn't — check whether you overwrote the file instead of hunking it.

### Pitfall 2: A "retained unchanged" service quietly depends on a file that's on the "never touch" list
**What goes wrong:** D-12 names 6 files as strictly hands-off; `CreatorStylePacketService`
(explicitly retained) turns out to need a type defined inside one of them (`PacketSessionCache.cs`).
**Why it happens:** The "never touch" list was derived from what those files' OWN diffs looked
like (mostly Phase-100 plumbing), not from a reverse-dependency scan of what retained files need
FROM them.
**How to avoid:** Before finalizing a "never touch" list, grep every in-scope file for references
to types/members declared in the excluded files, not just check what the excluded files' own
diffs contain.
**Warning signs:** `CS0246: type or namespace name '...' could not be found` where the type name
doesn't appear anywhere in your allowlisted files' git history except inside a "never touch" file.

### Pitfall 3: A DI-resolution test that uses fakes doesn't prove the real DI graph is safe
**What goes wrong:** `CreatorStyleDiRegistrationTests.cs` (ported as-is) uses
`FakeArchidektOwnerClient`, so it passes green regardless of whether the real `archidekt` Polly
pipeline is registered — giving false confidence that success criterion 3 is met.
**Why it happens:** Fakes are the correct choice for unit-testing business logic, but they defeat
the specific purpose of a "does DI wiring work" test when the fake stands in for the exact
component whose constructor has the risky external dependency.
**How to avoid:** For a DI-resolution smoke test whose job is "prove the real object graph
resolves," resolve the REAL implementation of any type whose constructor has a non-trivial
external dependency (a named HttpClient, a keyed pipeline, a file path) — reserve fakes for types
whose constructors are already known-safe (pure data stores, etc.).
**Warning signs:** A "DI resolution" test suite that registers `Fake*` for every single
interface — if everything is faked, the test proves the WIRING TOPOLOGY but not that any REAL
constructor succeeds.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) |
| Config file | none — plain `.csproj`-driven, no `xunit.runner.json` |
| Quick run command | `dotnet build DeckFlow.sln` (primary gate for PORT-01/02's build+DI claims) |
| Full suite command | `dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj && dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` (VSTest — documented unreliable in WSL; run via Windows dotnet, `"/mnt/c/Program Files/dotnet/dotnet.exe"`, or CI push-and-watch) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|--------------------|--------------|
| PORT-01 | Core engine present, builds with no new errors/warnings | build | `dotnet build DeckFlow.sln` — diff warning count against the 9-warning baseline captured this session | ✅ existing tool, no new file needed |
| PORT-01 | Ported Core test suite passes | unit | `dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~CreatorStyle\|StatedRules\|ProfileFusion\|MeasuredStyleExtraction\|CardGrounding"` | ✅ all files listed in `## Port Allowlist — Commit 1`, ported as-is |
| PORT-02 | Web services + seed loader + DI registrations ported | build | `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` | ✅ existing tool |
| PORT-02 | DI resolves at startup, no missing-registration failures | integration | `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CreatorStyleDiRegistration"` | ⚠ Wave 0 gap — port `CreatorStyleDiRegistrationTests.cs` AS-IS, then STRENGTHEN it: add a variant/companion assertion that resolves `IArchidektOwnerClient` (or anything transitively requiring it, e.g. `CreatorProfileDeckCrawler`) through the REAL implementation, not `FakeArchidektOwnerClient`, specifically to catch the archidekt-pipeline-missing failure mode (Critical Finding #2) |

### Sampling Rate
- **Per task commit:** `dotnet build DeckFlow.sln` (fast — ~6-25s observed this session)
- **Per wave merge (Commit 1 then Commit 2):** targeted `dotnet test --filter` scoped to
  creator-style suites (fast isolation of port-specific breaks)
- **Phase gate:** full `dotnet test` on both `DeckFlow.Core.Tests` and `DeckFlow.Web.Tests`
  projects before `/gsd:verify-work` — VSTest-in-WSL caveat applies; prefer Windows-side dotnet or
  CI push-and-watch per project convention

### Wave 0 Gaps
- [ ] Strengthen `CreatorStyleDiRegistrationTests.cs` per the note above (real `ArchidektOwnerClient`
      resolution, not faked) — this is the single test most load-bearing for proving success
      criterion 3 given Critical Finding #2.
- [ ] `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` needs the `<None Include="StatedRulesExtraction/Fixtures/salubrious-snail-transcript.txt">`
      item added (build-verified requirement for `StatedRulesExtractorTests.cs`'s golden fixture) —
      **do NOT** add the `Testcontainers.PostgreSql` `PackageReference` alongside it (D-15 excludes
      the tests that would need it).

## Security Domain

`security_enforcement` not set in `.planning/config.json` → treat as enabled. This phase is a
code port of already-reviewed Cycle 17 code onto a private admin-only surface (no public route is
wired at 112 — the admin controller lands at Phase 114). No new external attack surface is
introduced by this phase specifically.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Not touched at 112 — admin BasicAuth gate is Phase 114's concern (PTOOL-01) |
| V4 Access Control | No | No route wired yet at 112 |
| V5 Input Validation | Partial | `CardGroundingGuard`/`ScryfallCardNameGrounder` (ported at 112) already implement anti-hallucination card-name validation against live Scryfall data — this is existing, reviewed Cycle-17 logic, not new design surface |
| V6 Cryptography | No | Not applicable |

### Known Threat Patterns for this phase
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| New named `HttpClient("archidekt-owner")` calling a third-party API (archidekt.com) with no resilience pipeline | Denial of Service (unbounded retry/hang on a flaky upstream) | Exactly what Critical Finding #2 fixes — register the `archidekt` Polly pipeline with a bounded total timeout (30s) and capped retries (2), matching the existing five pipelines' pattern; do not ship this port with the pipeline missing |
| LLM-hallucinated card names entering deck-analysis output | Tampering (invalid card data reaching the user) | `CardGroundingGuard`/`ICardNameGrounder` (ported unmodified) already enforce this via live Scryfall lookups — no new work needed, just confirm the guard is wired into every path that accepts free-text card names in Phase 112's DI graph |

## Sources

### Primary (HIGH confidence — all `[VERIFIED]` by direct tool invocation this session)
- `git diff --name-status 5709f37c..plan/cycle-17-creator-style` — full branch diff inventory (397A/305D/110M)
- `git diff <merge-base> HEAD -- <path>` for every M-file candidate — main-drift detection
- `git show HEAD:<path>` / `git show plan/cycle-17-creator-style:<path>` — exact file content comparisons
- Actual `dotnet build` in a disposable `git worktree` (created via `git worktree add --detach`,
  removed via `git worktree remove --force` at end of session — real repo never touched, never
  staged, never committed) — the authoritative proof for the entire compile-closure claim
- Isolated Polly 8.6.6 repro (`dotnet run` against a throwaway console project referencing the
  exact pinned `PackageReference Include="Polly" Version="8.6.6"`) — the authoritative proof for
  Critical Finding #2
- `dotnet format DeckFlow.sln --include <99 files> --verify-no-changes` — the authoritative proof
  for the Format Gate Reality Check section
- `scripts/format-check-changed.sh` (read directly) — gate mechanism understanding
- `Dockerfile`, `.dockerignore` (read directly) — seed-shipping mechanism confirmation
- `docs/research/personal-tools-admin-reframe-design.md` — milestone authority for scope boundaries
- `.planning/phases/112-cycle-17-code-port/112-CONTEXT.md`, `.planning/REQUIREMENTS.md`,
  `.planning/ROADMAP.md`, `.planning/STATE.md`, `.planning/config.json` — phase scope and workflow settings

No Context7/WebSearch/WebFetch was used — this phase is pure repo archaeology with no external
library-API surface beyond Polly (verified directly, not via docs).

## Metadata

**Confidence breakdown:**
- Path allowlists / compile closure: HIGH — proven by an actual successful build, not inferred
- D-03 archidekt verdict: HIGH — proven by an isolated runtime repro against the pinned package version
- M-file hunk correctness: HIGH — every hunk build-verified; 3 of them caught a wholesale-copy
  regression risk empirically, not hypothetically
- Test port inventory / D-15 gaps: HIGH — every exclusion verified by an actual compile failure
  and root-caused before being added to the exclusion list
- Format gate behavior: HIGH — verified by running the actual formatter against the actual file set
- Baseline warning/test counts: HIGH for warnings (actual `dotnet build` output); MEDIUM for test
  counts (static attribute-count proxy, not an actual `dotnet test` run — VSTest-in-WSL
  unreliability per project convention made a full test run out of scope for research; planner
  should treat the 1334/1576 figures as a lower bound, confirmed via `dotnet test` at execution time)

**Research date:** 2026-07-24
**Valid until:** Until `main`/`feat/personal-tools` receives further commits touching any of the
9 M-file hunk targets (highest risk: `Program.cs`, `PacketSessionCache.cs`,
`DeckFlowDatabaseConnectionFactory.cs` — all "most-contested files" per D-10's own framing; Cut
Lab or other concurrent work could re-drift them within days). Re-verify the M-file diffs against
`HEAD` immediately before executing the plan if more than ~3 days have elapsed since this research.
