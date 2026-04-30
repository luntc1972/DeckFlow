# Phase 03: Tech-Debt Cleanup - Context

**Gathered:** 2026-04-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Move test-only types out of the production assembly, collapse multi-ctor test-compat seams to a single ctor with named test-helper factory access, untrack generated `*.js` from git, and tighten `ForwardedHeadersOptions.KnownIPNetworks` to a documented CIDR. No UI surface impact; brownfield production at deckflow.gg must stay green.

In scope (from ROADMAP.md):
- TD-01: Move `NullHttpClientFactory` + `NullScryfallRestClientFactory` out of `DeckFlow.Web/Services/Http/`; production assembly stops exposing test-only types.
- TD-02: Each service in `DeckFlow.Web/Services/` exposes exactly one constructor; tests previously requiring a "test-compat" ctor route through a named test-helper factory in `DeckFlow.Web.Tests`.
- TD-03: `DeckFlow.Web/wwwroot/js/*.js` no longer tracked; `.gitignore` excludes them; existing `tsc` MSBuild step still produces them at build.
- TD-04: `ForwardedHeadersOptions.KnownIPNetworks` restricted to Render's documented CIDR (with code comment citing the source); spoofed `X-Forwarded-For` from non-Render upstream cannot dodge feedback rate limiter.

Out of scope: any controller/service business-logic refactor, the convenience-overload ctors on `HelpContentService` / `VersionService` / `FeedbackStore` (they're real overloads, not test seams), `*.js` from outside `wwwroot/js/`, alternate hosting providers (Fly is in `fly.toml` but only Render is the deploy target).

</domain>

<decisions>
## Implementation Decisions

### TD-01: Null factory cleanup strategy

- **D-01:** Delete `DeckFlow.Web/Services/Http/NullHttpClientFactory.cs` and `NullScryfallRestClientFactory.cs` outright. They're the default values for the `internal` test-compat ctors of 9 production services and have zero references anywhere else (verified: `grep` returns zero hits in `DeckFlow.Web.Tests/`). Once TD-02 collapses those ctors, both files become orphans — deletion is the cleanest path. No migration to `DeckFlow.Web.Tests/TestDoubles/`; the `Fake*` family already in `TestDoubles/` is parameterized for actual test scenarios and serves a different role.
- **D-02:** TD-01 plan ships AFTER TD-02 plan. Plan 03-01 (TD-02) does the structural ctor collapse; plan 03-02 (TD-01) is a verification-and-delete step (`grep` returns zero references → delete the two files → `dotnet build` clean).

### TD-02: Single-ctor collapse — scope and mechanism

- **D-03:** Scope = the 10 services that follow the test-compat seam pattern (`public DI ctor` + `internal test ctor` taking `executeAsyncOverride` delegates):
  - `ScryfallCardLookupService` (`CardLookupService.cs`)
  - `ScryfallCardSearchService` (`CardSearchService.cs`)
  - `ScryfallSetService`
  - `ScryfallCommanderSearchService`
  - `ChatGptCedhMetaGapService`
  - `ChatGptDeckComparisonService`
  - `ChatGptDeckPacketService`
  - `CommanderBanListService`
  - `DeckConvertService`
  - `CommanderSpellbookService`
  Convenience-overload ctors on `HelpContentService` (`IWebHostEnvironment` → `string rootPath`), `VersionService` (`()` → `(Assembly)`), and `FeedbackStore` (3 ctors over `string` / `RelationalDatabaseConnection` / `IWebHostEnvironment`) are explicitly out of scope — they aren't test seams; they're domain overloads, and the ROADMAP wording is specifically about "test-compat ctor → test-helper factory."
- **D-04:** Mechanism = each affected service has exactly one constructor, marked `internal`, signature accepts production deps (e.g. `IScryfallRestClientFactory`, `ResiliencePipelineProvider<string>`) PLUS the override delegates as nullable params with default `null` (= real path). No more `Null*` factory defaults — the master ctor itself does null-handling for the override delegates.
- **D-05:** DI registration in `Program.cs` adapts to the `internal` ctor: where stock `AddScoped<TService, TImpl>()` would fail to bind to an internal ctor (DI activator typically requires public), use an explicit factory delegate `services.AddScoped<TService>(sp => new TImpl(sp.GetRequiredService<TDep1>(), sp.GetRequiredService<TDep2>()))`. Planner verifies the activator behavior in research and adjusts the registration shape accordingly.
- **D-06:** Test-helper named factory class lives at `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` (or split per service in the same folder — planner's call). Static methods like `TestServiceFactory.CreateScryfallCardLookupService(executeAsync: ..., executeSearchAsync: ..., ...)` call the internal ctor directly via the existing `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` declaration in `DeckFlow.Web/AssemblyInfo.cs`. Existing test sites that do `new ScryfallCardLookupService(...)` migrate to `TestServiceFactory.CreateScryfallCardLookupService(...)`.

### TD-03: Drop tracked `*.js` from git

- **D-07:** Bootstrap = README addition + a verification commit. No new tooling, no MSBuild changes. The existing `CompileTypeScriptAssets` target (BeforeTargets="Build") in `DeckFlow.Web.csproj` continues to invoke `node ./node_modules/typescript/bin/tsc -p tsconfig.json` on every build.
- **D-08:** `.gitignore` entry = single glob `DeckFlow.Web/wwwroot/js/*.js`. Future TS modules added under `wwwroot/ts/` are auto-handled. Confirm during planning that `wwwroot/lib/` (vendored 3rd-party JS, if any) is NOT under `wwwroot/js/`, so the glob can't catch it.
- **D-09:** Pre-removal verification step (mandatory in plan): on a clean checkout, run `dotnet build`, capture the produced `wwwroot/js/*.js`, and `diff` against currently tracked content. Files MUST be byte-identical before the `git rm --cached` happens. This eliminates the silent-runtime-change risk of "ship the .gitignore + remove tracked files" landing differently than the source-of-truth `.ts` produces.
- **D-10:** README adds a one-time setup section: `cd DeckFlow.Web && npm install typescript`. Currently README has no npm/Node onboarding text. Render Docker build does its own `RUN npm install typescript` (Dockerfile L36) so production is unaffected.

### TD-04: ForwardedHeadersOptions CIDR tightening

- **D-11:** Plan starts with a research task confirming Render's authoritative inbound-proxy CIDR (canonical sources: render.com docs on networking / load balancers / static IPs, official Render support forum, Render dashboard if it exposes proxy IPs). Two outcomes:
  - **Found:** populate `KnownIPNetworks` with the documented CIDRs + a code comment citing the URL with retrieval date (auditor-style: docs change). TD-04 SC satisfied.
  - **Not found:** TD-04 is unsatisfiable as written. Surface this honestly via a roadmap concern; ship a defense-in-depth alternative (e.g. switch the feedback rate-limit partition key off `X-Forwarded-For` onto a different identity such as session cookie + CSRF token, OR enforce HTTPS via a separate mechanism that doesn't rely on `X-Forwarded-Proto` trust). Defer the partition-key change to its own future phase if it doesn't fit Phase 03's scope.
- **D-12:** Restriction applies in Production only, branched on `IWebHostEnvironment.IsProduction()`. In Development: keep `KnownIPNetworks = { 127.0.0.1, ::1 }` (loopback) and skip the CIDR list — local dev runs without a reverse proxy and must remain zero-config. In Production: `KnownIPNetworks = Render CIDR + loopback`. Loopback retained in prod too because Kestrel health checks and any in-container probes hit `localhost`.

### Plan grouping and sequence

- **D-13:** One plan per requirement, four plans total:
  - **03-01-PLAN.md = TD-02** (collapse 10 test-seam ctors to single internal ctor; introduce `TestServiceFactory` in `DeckFlow.Web.Tests/TestDoubles/`; migrate test sites; adapt DI registrations in `Program.cs`).
  - **03-02-PLAN.md = TD-01** (verify `grep` shows zero references to `NullHttpClientFactory` / `NullScryfallRestClientFactory` after 03-01 lands; delete the two files; `dotnet build` clean).
  - **03-03-PLAN.md = TD-03** (verification commit confirming byte-identical JS production from clean build; `.gitignore` glob; `git rm --cached` the 10 files; README onboarding text).
  - **03-04-PLAN.md = TD-04** (research Render CIDR → either restrict KnownIPNetworks production-only with cited source, or surface unsatisfiable + ship alternative).
- **D-14:** Sequence: 03-01 → 03-02 must run in order (03-02 depends on 03-01 having removed the only callers). 03-03 and 03-04 are independent and can ship in any order or in parallel with 03-01/03-02. Recommended commit order: 03-01, 03-02, 03-03, 03-04.

### Claude's Discretion

- Internal master ctor parameter ordering for the 10 collapsed services — planner picks based on consistency with current call sites.
- Whether `TestServiceFactory.cs` is one big file or split per-service files in `TestDoubles/` — planner decides on file-size grounds.
- Exact `KnownIPNetworks` CIDR list — researcher fills in from authoritative Render docs.
- Whether the `Production-only` branch in `Program.cs` lives inline in the existing `Configure<ForwardedHeadersOptions>` block or extracts to a small helper extension method — planner's call.
- README onboarding section copy and placement — planner picks between a new `## Local Development` header or extending an existing build section.

</decisions>

<specifics>
## Specific Ideas

- "We already have `[assembly: InternalsVisibleTo(\"DeckFlow.Web.Tests\")]` — exploit it for the test-seam mechanism instead of inventing new patterns."
- "Plain commits, no Co-Authored-By trailer." (project convention from PROJECT.md)
- "VSTest is unreliable in WSL — verification is `dotnet build` clean + push-and-watch CI, not local test execution." (PROJECT.md constraint)
- "Render Docker stage already does `RUN npm install typescript` (no package.json) — local dev needs the same one-time `npm install typescript` to mirror prod."
- "Brownfield: every plan must keep deckflow.gg green; commit-per-logical-change so revert blast radius stays small."

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap and requirements
- `.planning/ROADMAP.md` §"Phase 3: Tech-Debt Cleanup" — Goal, Depends-on, Requirements (TD-01..04), Success Criteria 1–4.
- `.planning/REQUIREMENTS.md` §"Tech-Debt Cleanup (TD)" lines 35–42 — TD-01..04 verbatim wording.
- `.planning/PROJECT.md` §"Constraints" lines 117–125 — testing convention (VSTest unreliable in WSL), commit convention (no Co-Authored-By), public-repo discipline.
- `.planning/PROJECT.md` §"Active" §"Tractable code-quality cleanup" — the four TD items as user-facing punch-list.

### Codebase intel
- `.planning/codebase/CONCERNS.md` — original audit items that produced TD-01..04. Read for full context on the "why" behind each requirement.
- `.planning/codebase/CONVENTIONS.md` — naming, error handling, DI conventions, test-seam pattern documented.
- `.planning/codebase/STRUCTURE.md` — `DeckFlow.Web/Services/`, `DeckFlow.Web/Services/Http/`, `DeckFlow.Web.Tests/TestDoubles/` layouts.
- `.planning/codebase/TESTING.md` — current test infrastructure, `Fake*` vs `Stub*` distinction.
- `CLAUDE.md` §"Constraints" — pinned `RestSharp + direct Polly v8` HTTP layer, public-repo, plain-author commits, README-current-with-commits.

### Code reference points (TD-01 / TD-02)
- `DeckFlow.Web/Services/Http/NullHttpClientFactory.cs` — 17 lines, the file to delete (TD-01).
- `DeckFlow.Web/Services/Http/NullScryfallRestClientFactory.cs` — 38 lines, the second file to delete (TD-01).
- `DeckFlow.Web/AssemblyInfo.cs:3` — existing `[InternalsVisibleTo("DeckFlow.Web.Tests")]` (no new attribute work needed for TD-02).
- `DeckFlow.Web/Services/CardLookupService.cs:91-121` — canonical example of the 2-ctor test-seam pattern (public DI ctor delegates to private; internal test ctor delegates to private with `NullScryfallRestClientFactory.Instance` as default). Other 9 services follow the same shape.
- `DeckFlow.Web.Tests/TestDoubles/` — existing `FakeHttpClientFactory.cs`, `FakeScryfallRestClientFactory.cs`, `FakeResiliencePipelineProvider.cs`, `FakeCategoryKnowledgeStore.cs`, `StubHttpMessageHandler.cs`. New `TestServiceFactory.cs` for TD-02 lands here.
- `DeckFlow.Web.Tests/CardLookupServiceTests.cs:16-248+` — 9+ call sites of `new ScryfallCardLookupService(...)` that need to migrate to `TestServiceFactory.CreateScryfallCardLookupService(...)`.

### Code reference points (TD-03)
- `DeckFlow.Web/DeckFlow.Web.csproj` — `<Target Name="CompileTypeScriptAssets" BeforeTargets="Build">` invokes `node ./node_modules/typescript/bin/tsc -p tsconfig.json`.
- `DeckFlow.Web/tsconfig.json` — strict TS config; verify `outDir` actually points at `wwwroot/js/`.
- `Dockerfile` L7-19 + L36 — Node 20 install + `RUN npm install typescript`. Reference for the README onboarding paragraph.
- `.gitignore` — current state has `node_modules/`, `package.json`, `package-lock.json` excluded. New entry: `DeckFlow.Web/wwwroot/js/*.js`.
- `DeckFlow.Web/wwwroot/js/{card-lookup,card-search,category-suggestions,commander-search,deck-sync,df-select,df-typeahead,feedback,judge-questions,site}.js` — the 10 files to untrack.
- `README.md` — currently has zero npm/Node onboarding text (verified: `grep -n "npm install\|node_modules" README.md` returns nothing). New section to add.

### Code reference points (TD-04)
- `DeckFlow.Web/Program.cs:114-128` — current `Configure<ForwardedHeadersOptions>` block with `KnownIPNetworks.Clear(); KnownProxies.Clear();` and the comment that contradicts TD-04 SC.
- `DeckFlow.Web/Program.cs:194-196` — `app.UseForwardedHeaders()` placement (must run before HTTPS redirect / security headers / SameOrigin per CLAUDE.md architectural constraint).
- External (research target): Render documentation portal — `render.com/docs/networking`, `render.com/docs/static-outbound-ip-addresses`, Render community forum. Research task in 03-04-PLAN must locate authoritative inbound-proxy CIDR or document its absence.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`[InternalsVisibleTo("DeckFlow.Web.Tests")]`** at `DeckFlow.Web/AssemblyInfo.cs:3` — already present, unblocks the TD-02 internal-ctor mechanism with zero new boilerplate.
- **`DeckFlow.Web.Tests/TestDoubles/`** — established convention for test fakes (`Fake*`) and stubs (`Stub*`); `TestServiceFactory.cs` slots in cleanly.
- **`FakeHttpClientFactory`** + **`FakeScryfallRestClientFactory`** — already exist as parameterized fakes; tests that need real handler behavior can use these instead of the deleted `Null*` defaults.
- **`CompileTypeScriptAssets` MSBuild target** — already wired into `BeforeTargets="Build"`; TD-03 piggybacks on it without modification.

### Established Patterns
- **2-ctor test-seam (public DI + internal test) → being deprecated by TD-02.** All 10 affected services follow the same shape; collapse can be templated. ScryfallCardLookupService (`CardLookupService.cs:91-121`) is the canonical example.
- **Funnelled master ctor** — every test-seam service has a private/internal master ctor that both public and internal ctors delegate to. After TD-02, the master ctor IS the only ctor; both old delegations vanish.
- **`Fake*` vs `Stub*` vs `Null*` naming** (`TestDoubles/`) — Fakes are parameterized stateful test doubles; Stubs are queue-driven; Nulls are no-arg defaults. TD-01 retires the Null pattern entirely.
- **Plain commit, README-current** (CLAUDE.md, PROJECT.md) — every commit in this phase plain-author + README updated when behavior changes (TD-03 README addition, possibly TD-04 if the research outcome alters operational doc).

### Integration Points
- **`Program.cs` DI registrations** — `AddSingleton`/`AddScoped` calls for the 10 collapsed services (see `Program.cs:108-184` block) need to switch from stock `AddScoped<TService, TImpl>()` to factory-delegate form (`AddScoped<TService>(sp => new TImpl(...))`) to bind through internal ctors.
- **`Program.cs:117-128` ForwardedHeadersOptions** — the lone TD-04 surface area; environment-conditional logic lands here.
- **Test files at `DeckFlow.Web.Tests/*ServiceTests.cs`** — every site that does `new ScryfallCardLookupService(...)` (and 9 sibling patterns) migrates to `TestServiceFactory.Create*Service(...)`.
- **`tsconfig.json` outDir** — verify it actually emits to `wwwroot/js/` so the `.gitignore` glob matches what tsc produces.

### Constraints carried from earlier phases
- **Phase 01 (Visual System Tokens):** No carry-forward into Phase 03 — different surfaces (CSS vs C#).
- **Phase 02 (Layout/Hierarchy):** No carry-forward — different surfaces (Razor/CSS vs C#/build).
- **PROJECT.md global:** RestSharp + direct Polly v8 HTTP pattern stays; testing leans on `dotnet build` clean + push-and-watch CI; commits plain-author no Co-Authored-By; brownfield discipline (each commit must keep deckflow.gg green).

</code_context>

<deferred>
## Deferred Ideas

- **Track `DeckFlow.Web/package.json` + `package-lock.json` in git** — currently both are gitignored (`.gitignore` L9-10). CLAUDE.md cites pinned versions (TS 6.0.2, ESLint 10.2.0) but those pins are fictional without the lockfile in source control. Adopting `npm ci` from a tracked lockfile would make builds reproducible. Out of TD-03 scope as written; capture in roadmap backlog as a future polish item.
- **`feedback` rate-limit partition key off `X-Forwarded-For`** — only relevant if TD-04 research finds Render publishes no inbound CIDR. Switching the `feedback-submit` rate-limit partition (`Program.cs:130-146`) onto a session/CSRF identity removes the spoofability risk independent of forwarded-headers trust. Future-phase candidate; flagged conditionally on TD-04 research outcome.
- **Convenience-overload ctor consolidation** for `HelpContentService`, `VersionService`, `FeedbackStore` — explicitly out of TD-02 scope per D-03. Could be a future cleanup if "exactly one constructor" gets re-interpreted strictly later.
- **Migrate Fly.io configuration** — `fly.toml` exists but Render is the live target; no need to maintain Fly. Capture as a deletion candidate in a future cleanup phase.

</deferred>

---

*Phase: 03-tech-debt-cleanup*
*Context gathered: 2026-04-30*
