# Codebase Concerns

**Analysis Date:** 2026-04-29

## Tech Debt

**God-class controller (`DeckController`):**
- Issue: `DeckController` is 1,007 lines and dispatches 25+ public actions across deck sync, ChatGPT analysis, deck comparison, cEDH meta-gap, card lookup, mechanic lookup, judge questions, category suggestions, and commander search. Each unrelated feature shares the same controller, set of 12+ injected services, and exception-handling boilerplate.
- Files: `DeckFlow.Web/Controllers/DeckController.cs`
- Impact: Every new feature widens the constructor. Tests must instantiate the full graph to exercise one endpoint. Cross-feature regressions are easy because a single edit touches state shared between unrelated flows.
- Fix approach: Split per-feature into `ChatGptAnalysisController`, `ChatGptComparisonController`, `ChatGptCedhMetaGapController`, `CardLookupController`, `MechanicLookupController`, `CategorySuggestionsController`, and `DeckSyncController` — most of those views (`DeckFlow.Web/Views/Deck/*.cshtml`) already exist as discrete pages, and the corresponding services are already separate.

**Monster service files:**
- Issue: Three ChatGPT services exceed 900 lines and mix prompt building, Scryfall resolution, Spellbook lookup, artifact persistence, and JSON parsing in the same class.
- Files:
  - `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` (1,945 lines)
  - `DeckFlow.Web/Services/ChatGptDeckComparisonService.cs` (1,260 lines)
  - `DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs` (922 lines)
- Impact: Ownership boundaries are unclear; new questions or output formats require touching massive files. ChatGptDeckPacketService.cs has only 7 `_logger.*` calls — observability inside this critical path is thin.
- Fix approach: Extract `PromptBuilder`, `ScryfallReferenceResolver`, `SpellbookEnricher`, and `ArtifactWriter` collaborators per service. The existing `ChatGptPacketArtifactStore.cs` shows the extraction pattern.

**Legacy environment-variable name:**
- Issue: Auto-browser launch reads `MTGDECKSTUDIO_DISABLE_AUTO_BROWSER`, the only surviving reference to the old `MtgDeckStudio`/`DeckSyncWorkbench` project names. README and rest of codebase use `DECKFLOW_*` or `MTG_DATA_DIR`.
- Files: `DeckFlow.Web/Program.cs:234`
- Impact: Documentation drift — developers won't guess this name. Inconsistent env-var prefix.
- Fix approach: Rename to `DECKFLOW_DISABLE_AUTO_BROWSER` with a one-release fallback that also reads the old name.

**Duplicate user-local solution settings files committed:**
- Issue: Two old-named user files coexist with the new ones: `DeckFlow.Web/MtgDeckStudio.Web.csproj.user`, `DeckFlow.Web/DeckSyncWorkbench.Web.csproj.user`, `MtgDeckStudio.sln.DotSettings.user`, `DeckSyncWorkbench.sln.DotSettings.user`. `.gitignore` has `*.user` but `git ls-files` shows none are tracked — they are untracked dead files left from the renames.
- Files: `DeckFlow.Web/MtgDeckStudio.Web.csproj.user`, `DeckFlow.Web/DeckSyncWorkbench.Web.csproj.user`, `MtgDeckStudio.sln.DotSettings.user`, `DeckSyncWorkbench.sln.DotSettings.user`
- Impact: Confusing local working tree; they show up in IDE searches. Harmless to git but pollute the repo.
- Fix approach: Delete the four files from disk.

**Stray probe artifact at repo root:**
- Issue: `moxfield_probe.json` (~647 KB) sits at the repo root, untracked but never cleaned up since 2026-03-29.
- Files: `moxfield_probe.json`, `build.log`
- Impact: Noise; risks accidental `git add -A` commits of large binary blobs.
- Fix approach: Delete and add explicit pattern (e.g. `*_probe.json`, `build.log`) to `.gitignore` if these are produced by ad-hoc scripts.

**Tracked log directory:**
- Issue: `DeckFlow.Web/logs/web-*.log` rolling files are present in the working tree. `.gitignore` has `*.log` so they aren't tracked, but they keep accumulating per `appsettings`/Serilog file sink which writes to `logs/web-.log` with `retainedFileCountLimit: 14`.
- Files: `DeckFlow.Web/logs/`, `logs/`, `Program.cs:32-46`
- Impact: Old `cli-2026032{1,9}.log` files are months stale, never cleaned up. Two log roots coexist (`logs/` at repo root and `DeckFlow.Web/logs/`).
- Fix approach: Pick one log root (the `MTG_DATA_DIR/logs` pattern). Delete the stray repo-root `logs/` directory.

**Test-only factory shipped in production assembly:**
- Issue: `NullHttpClientFactory` and `NullScryfallRestClientFactory` are public types in `DeckFlow.Web/Services/Http/` whose XML doc explicitly says "Test-only".
- Files: `DeckFlow.Web/Services/Http/NullHttpClientFactory.cs`, `DeckFlow.Web/Services/Http/NullScryfallRestClientFactory.cs`
- Impact: Types intended for the test convenience constructor pattern (D-10) bleed into the production surface and can be discovered by IntelliSense in apps that consume `DeckFlow.Web` types.
- Fix approach: Move both to `DeckFlow.Web.Tests/TestDoubles/` (alongside `FakeHttpClientFactory.cs`) and update the test-compat ctors to consume them via `internal` visibility through `[InternalsVisibleTo]`.

**Generated JavaScript checked into the repo:**
- Issue: `DeckFlow.Web/wwwroot/js/*.js` files are TypeScript build output (target `es2017`, single-file `module: "none"`) yet they are tracked in git alongside `DeckFlow.Web/wwwroot/ts/*.ts`. The `CompileTypeScriptAssets` MSBuild target regenerates them on every build.
- Files: `DeckFlow.Web/wwwroot/js/deck-sync.js`, `DeckFlow.Web/wwwroot/js/site.js`, `DeckFlow.Web/wwwroot/js/df-select.js`, etc.
- Impact: TS and JS drift if a developer edits the .js by mistake. Diffs noisy on every TS change. Build target re-emits files even when unchanged in source, dirtying the working tree.
- Fix approach: Add `wwwroot/js/*.js` to `.gitignore` and keep only the generated artifacts in publish output, OR remove the MSBuild compile step and rely on a pre-commit hook.

**Lingering test-only public ctors on services:**
- Issue: HTTP services expose internal "test-compat" constructors via `InternalsVisibleTo("DeckFlow.Web.Tests")` (per memory observation 2710-2711). Sprinkles ctor-disambiguation comments referencing checker B2 and `[ActivatorUtilitiesConstructor]` that future readers won't understand.
- Files: `DeckFlow.Web/Services/CommanderSpellbookService.cs:69-103`, `DeckFlow.Web/AssemblyInfo.cs:3`, plus `// InternalsVisibleTo for ...` comments in `CardSearchService.cs:83`, `CommanderBanListService.cs:80`
- Impact: New services copy this pattern by inertia; the indirection is hard to follow. DI ambiguity has already caused a runtime bug (memory 2710-2712).
- Fix approach: Standardize on one ctor per service plus a tiny named test-helper factory in `TestDoubles/`. Document the rule in `.planning/codebase/CONVENTIONS.md` (when written).

## Known Bugs

**Scryfall Tagger returns 404 for valid card names:**
- Symptoms: Tagger lookup for "Sol Ring" returns HTTP 200 with empty suggestions; raw Scryfall Tagger responds 404 for all card lookups (per memory observations 2722, 2724-2725).
- Files: `DeckFlow.Web/Services/ScryfallTaggerService.cs:90-117`, `DeckFlow.Web/Services/ScryfallTaggerService.cs` set/collector resolution path
- Trigger: Use the AI Category Suggestions page in `ScryfallTagger` mode for any card.
- Cause: URL construction uses the wrong set code for the card lookup (memory 2725).
- Workaround: `CategorySuggestionMode.All` falls back to cached store + EDHREC, so users still get suggestions via other paths. Pure-Tagger mode is effectively broken.

**Path-base safety relies on view discipline alone:**
- Symptoms: Static asset paths embedded in `.cshtml` views must use `~/...` for IIS sub-app deployment (`/deckflow` example in README:78-83). Nothing enforces this.
- Files: `DeckFlow.Web/Views/Shared/_Layout.cshtml`, all `Views/**/*.cshtml`
- Trigger: A developer hardcodes `/css/...` instead of `~/css/...` and tests only locally; deploys to IIS sub-application.
- Workaround: Manual review at PR time.

## Security Considerations

**Forwarded-headers fully trusted from any upstream:**
- Risk: `Program.cs:117-128` clears `KnownIPNetworks` and `KnownProxies`, so any upstream can spoof `X-Forwarded-For`/`X-Forwarded-Proto`/`X-Forwarded-Host`. The code comments acknowledge this and justify it ("DeckFlow does not authenticate requests").
- Files: `DeckFlow.Web/Program.cs:117-128`
- Current mitigation: Comments document the threat model. Admin endpoints behind `BasicAuthMiddleware` use real basic-auth credentials, not the forwarded scheme/host.
- Recommendations: When platform IP ranges are stable (Fly.io, Render publish their CIDR blocks), tighten `KnownIPNetworks`. The feedback rate limiter at `Program.cs:130-146` partitions on `RemoteIpAddress` which is itself influenced by forwarded headers — a malicious upstream could spoof IPs to dodge rate limits.

**Admin basic-auth lacks lockout / failed-attempt logging:**
- Risk: `BasicAuthMiddleware` performs constant-time compare but never logs failures, so brute-force attempts against `/Admin/Feedback` are invisible. `Program.cs:130-146` rate-limit policy applies only to `feedback-submit`, not admin auth.
- Files: `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs`
- Current mitigation: Constant-time string compare via `CryptographicOperations.FixedTimeEquals`; service returns 503 if creds env vars are unset.
- Recommendations: Log failed auth attempts at `Warning` with request IP. Add a per-IP rate-limit policy on `/Admin/*` paths to throttle attackers.

**Mass-assignment surface on admin actions:**
- Risk: `AdminFeedbackController.Apply` accepts `[FromRoute] op` strings (`markread`, `archive`, `delete`) without an enum binding, defaulting unknown values to `BadRequest()`. CSRF token is enforced. Low risk.
- Files: `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs:60-83`
- Current mitigation: `[ValidateAntiForgeryToken]` on the POST. Switch on lowered op name.
- Recommendations: Bind `op` to a typed enum (`AdminFeedbackAction`) so the model binder rejects unknown values before the action body runs.

**Help content rendered with `@Html.Raw`:**
- Risk: `Views/Help/Topic.cshtml:13` writes `@Html.Raw(Model.HtmlContent)` from Markdig output. Markdown source is local files in `DeckFlow.Web/Help/` (committed to repo), so user-controlled HTML cannot reach this path today. If `Help/` ever ingests external content, this becomes XSS.
- Files: `DeckFlow.Web/Views/Help/Topic.cshtml:13`, `DeckFlow.Web/Services/HelpContentService.cs:13-14`
- Current mitigation: Source content fully under repo control; pipeline uses `UseAdvancedExtensions` without raw HTML allowance toggled explicitly.
- Recommendations: Disable raw HTML in the Markdig pipeline via `DisableHtml()` so any future external content cannot inject script tags.

## Performance Bottlenecks

**Process-wide static Scryfall throttle is a global serialization point:**
- Problem: `ScryfallThrottle` uses a single `static SemaphoreSlim Gate = new(1, 1)` and `static DateTime _lastCallUtc` to enforce a 200ms minimum interval across every request and every user.
- Files: `DeckFlow.Web/Services/ScryfallThrottle.cs:24-25`
- Cause: Static state is shared across all in-flight requests in the entire `DeckFlow.Web` process. Concurrent users sequentialize through a single 200ms gate.
- Improvement path: Per-route async fairness queue (e.g. one gate per Scryfall endpoint), or push pacing into the Polly v8 pipeline as a `RateLimiterStrategy` so the throttle is observable, configurable, and stops tying the host's internal scheduling to a `static` mutable timestamp.

**ChatGPT services do work serially that could parallelize further:**
- Problem: Even with `ChatGptDeckPacketService` running banned-list, set-packet, and Spellbook fetches concurrently (per README:521), the per-card Scryfall fallback search runs through `ScryfallThrottle` one-at-a-time.
- Files: `DeckFlow.Web/Services/ChatGptDeckPacketService.cs:116-124`
- Cause: `ScryfallThrottle.Gate` forces serial execution; large packet builds with many alternate-art fallbacks block on the gate.
- Improvement path: Chunk fallback searches into a Polly resilience pipeline with a `RateLimiter` strategy that allows N concurrent requests under the 9 req/s ceiling.

**Tagger `GetSetOptions` cache is in-process only:**
- Problem: `IScryfallSetService` caches the full set catalog in memory for 6 hours; cold starts re-fetch from Scryfall. Each Render/Fly instance pays the cost separately.
- Files: `DeckFlow.Web/Services/ScryfallSetService.cs`
- Cause: `IMemoryCache` is per-process. No distributed cache.
- Improvement path: Add a disk-backed JSON snapshot under `MTG_DATA_DIR/cache/scryfall-sets.json` with the same 6-hour TTL so multi-instance deployments and restarts skip the cold load.

**Background tagger session refresh fires unawaited:**
- Problem: `ScryfallTaggerService.RecordHit` line 102 spawns `Task.Run(async () => ...)` to refresh the session; failures only log at `Debug`. If the refresh enters a 4xx/5xx loop, the next user-facing call still sees the stale cached session for up to 30s.
- Files: `DeckFlow.Web/Services/ScryfallTaggerService.cs:96-114`
- Cause: Fire-and-forget pattern with no observability.
- Improvement path: Use `IHostedService` with a `Channel<TaggerRefreshRequest>` consumer. Surface refresh failures at `Warning`. Cap concurrent in-flight refreshes to 1.

## Fragile Areas

**HTTP service constructor wiring:**
- Files: `DeckFlow.Web/Services/CommanderSpellbookService.cs:67-103`, `DeckFlow.Web/Services/ScryfallTaggerService.cs`, `DeckFlow.Web/Services/ChatGptDeckPacketService.cs`
- Why fragile: Multi-overload ctors with `[ActivatorUtilitiesConstructor]`-style ambiguity already caused a runtime DI crash (memory 2710-2712). Test-compat ctors use `internal` visibility to avoid binding, which means publishing `DeckFlow.Web` as a library would re-expose them. Per-call `new RestClient(_taggerHttpClient.Inner)` couples consumers to RestSharp's `HttpClient` wrapping pattern.
- Safe modification: Resolve via the public ctor only; do not add overloads. Run the full test suite after changing any ctor signature.
- Test coverage: 328 tests; ctor binding regressions already exercised through `DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs` and `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs`.

**Dual database-provider abstraction:**
- Files: `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` (747 lines), `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs`, `DeckFlow.Web/Services/FeedbackStore.cs`
- Why fragile: SQL is hand-written and branches on `_connectionInfo.IsSqlite`/`IsPostgres` per call. Schema differs subtly: `CategoryKnowledgeRepository.cs:46` only calls `Directory.CreateDirectory` for SQLite paths; date columns store ISO strings in SQLite vs DateTime in Postgres (`FeedbackStore.cs:51`). Any schema migration must be authored twice.
- Safe modification: When adding columns, update both DDL paths in `EnsureSchemaAsync` and add an `ALTER TABLE` migration mirroring the existing `EnsureDeckQueueColumnsAsync` pattern. Add an integration test that spins up both providers (Testcontainers for Postgres) before merging.
- Test coverage: SQLite covered by `DeckFlow.Web.Tests/CategoryKnowledgeStoreTests.cs` and `DeckFlow.Web.Tests/FeedbackStoreTests.cs`. No Postgres integration tests visible.

**Browser-extension Bridge contract:**
- Files: `browser-extensions/deckflow-bridge/manifest.json`, `browser-extensions/deckflow-bridge/background.js`, `DeckFlow.Web/wwwroot/ts/deck-sync.ts:2415` (datalist clears around bridge prompts)
- Why fragile: Extension communicates with the page via content-script injection; allowed-origin list is user-managed in extension options. Any DeckFlow domain change requires the user to reconfigure the extension. ZIP packaging happens in `DeckFlow.Web.csproj` MSBuild via `ZipDirectory` task.
- Safe modification: Bump `manifest.json:version` whenever the protocol changes so installed copies surface a clear mismatch, and update both the extension and the server-side prompt detection in `deck-sync.ts` together.
- Test coverage: No automated tests for the extension or the bridge handshake.

**Hand-built CSS theme system (25 stylesheets):**
- Files: `DeckFlow.Web/wwwroot/css/site*.css` (25 theme files), `site-common.css`, `site-mobile.css`, `theme-normalization-report.md`
- Why fragile: 25 separate full-stylesheet forks; the in-progress `theme-normalization-report.md` documents that token leakage between guild themes already caused stacking-context bugs (memory 2852). New CSS rules added to one theme are easy to forget in the other 24.
- Safe modification: New layout CSS goes in `site-common.css` (per `feedback_themed_pages.md` global instructions). New theme tokens follow `--theme-secondary*` pattern documented in `theme-normalization-report.md`. Test the new rule against at least Default + one guild + Planeswalker Dark.
- Test coverage: No automated visual regression tests; manual smoke testing only.

## Scaling Limits

**SQLite default storage at single-host scale:**
- Current capacity: SQLite at `MTG_DATA_DIR/feedback.db` and `MTG_DATA_DIR/category-knowledge.db`. Single-writer; concurrent feedback submissions serialize via `_schemaGate` semaphore.
- Limit: Multi-instance Render/Fly deployments will fight over the same file unless a persistent volume is mounted exclusively to one instance. README:88 documents this.
- Scaling path: `DECKFLOW_DATABASE_PROVIDER=Postgres` with `DECKFLOW_DATABASE_CONNECTION_STRING` (already implemented per README:39-43). Migrate users to Postgres before scaling horizontally.

**Archidekt cache-job singleton model:**
- Current capacity: `ArchidektCacheJobService` is registered as singleton + hosted service; uses `ConcurrentDictionary<Guid, ...>` for jobs but only one harvest may run at a time (README:412).
- Limit: Web-app instance restart loses in-flight jobs and the queue (no persistence). Multi-instance deploys can't coordinate; both would try to harvest.
- Scaling path: Persist job state (next deck IDs, status) into the relational store so the work survives restart. Promote the job to a separate worker process when scaling out.

**ChatGPT artifact directory size:**
- Current capacity: `ChatGptArtifactsDirectory` writes per-session subfolders under `MTG_DATA_DIR/ChatGPT Analysis/<commander>/<timestamp>` with no retention policy.
- Limit: Disk fills up on long-running deployments; `EnumerateSessions` reads the entire directory tree on every "Saved Sessions" load.
- Scaling path: Add a retention sweep (e.g. delete sessions older than 30 days) and paginate / cache the enumeration.

## Dependencies at Risk

**Polly version pinning split between projects:**
- Risk: `DeckFlow.Web/DeckFlow.Web.csproj` uses `<PackageReference Include="Polly" Version="8.*" />` (resolves to 8.6.6 per memory 2619), while `DeckFlow.Core/DeckFlow.Core.csproj` pins `Version="8.1.0"`.
- Impact: Diamond-dependency risk if Polly 8.x introduces breaking changes between 8.1.0 and 8.6.x. Wildcard makes builds non-reproducible across machines and times.
- Migration plan: Pin both projects to the same explicit version (e.g. `8.6.6`) and renovate via a single bump. Add `Directory.Build.props` `<PackageVersion>` central management.

**Wildcard Polly + tight coupling to RestSharp 114.0.0:**
- Risk: `<PackageReference Include="RestSharp" Version="114.0.0" />` hard-pinned across `DeckFlow.Web`, `DeckFlow.Core`, and `DeckFlow.Web.Tests`. RestSharp 114 is a recent major rewrite. Future RestSharp upgrades will need to be coordinated across all three projects in a single PR.
- Impact: Lock-in to RestSharp's API surface. Polly v8 pipelines wrap `RestResponse`, embedding RestSharp into the resilience layer.
- Migration plan: Hide RestSharp behind an `IUpstreamHttpClient` abstraction in `DeckFlow.Web/Services/Http/` so the choice of HTTP library is swappable. Already partially done via `IScryfallTaggerHttpClient` (typed wrapper).

**Cloudflare-fronted upstreams without contract pinning:**
- Risk: Scryfall, Moxfield, Archidekt, Commander Spellbook, EDHREC, EDH Top 16 all sit behind Cloudflare and can change rate-limit thresholds, response shapes, or block datacenter IPs at any time. Moxfield already does this (README:91).
- Impact: Silent breakage when upstream JSON shape shifts. Moxfield fallback through Spellbook degrades metadata (set codes lost).
- Migration plan: Maintain golden-response fixtures in `DeckFlow.Web.Tests` (already partially in `TestDoubles/StubHttpMessageHandler.cs`) and snapshot the live shape weekly via a CI smoke job.

## Missing Critical Features

**No request-trace correlation:**
- Problem: `Program.cs:34-47` configures Serilog with `Enrich.FromLogContext()` and `UseSerilogRequestLogging()` but no explicit correlation-id middleware. Multiple Scryfall calls per request can't be tied back to the originating user request in logs.
- Blocks: Debugging production incidents that span ChatGPT packet building → Scryfall → Spellbook chains.

**No structured error responses on API endpoints:**
- Problem: `DeckFlow.Web/Controllers/Api/*` controllers return `BadRequest("string message")` or `StatusCode(403, new { Message = ... })` ad-hoc.
- Blocks: Client-side error UX consistency. The TypeScript callers in `wwwroot/ts/deck-sync.ts:2372` resort to `'<option value="">Could not load saved sessions</option>'` as a static error string.

**No explicit health endpoint:**
- Problem: `Program.cs` does not register `/health` or `/ready`. Render and Fly default deploys treat the root MVC route as the health probe.
- Blocks: Cleaner deployment integration; faster cold-start signal; allows `/ready` to gate on database connectivity (`ValidateDatabaseConnectionsAsync` in `Program.cs:274` already does this on startup but not as a probe).

**No offline-first / PWA support despite localStorage usage:**
- Problem: The app stores theme preferences and session state in `localStorage` but does not register a service worker. Mobile users on flaky connections see plain failure pages.
- Blocks: Mobile UX promised by `site-mobile.css` and the new responsive layout (memory 2538).

## Test Coverage Gaps

**No Postgres integration tests:**
- What's not tested: `RelationalDatabaseProvider.Postgres` branches in `CategoryKnowledgeRepository.cs:46-705` and `FeedbackStore.cs:51`. SQLite paths are tested in `CategoryKnowledgeStoreTests.cs`/`FeedbackStoreTests.cs` only.
- Files: `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`, `DeckFlow.Web/Services/FeedbackStore.cs`
- Risk: Postgres-only schema or query bugs (e.g. ISO-string vs DateTime literal mismatch) only surface in production.
- Priority: High — Postgres is the documented hosted-deployment path (README:38-43).

**No browser-extension tests:**
- What's not tested: `browser-extensions/deckflow-bridge/*.js` has zero automated coverage. Bridge handshake and allowed-origin checks are manual-only.
- Files: `browser-extensions/deckflow-bridge/background.js`, `browser-extensions/deckflow-bridge/deckflow-bridge.js`, the matching server detection in `DeckFlow.Web/wwwroot/ts/deck-sync.ts`
- Risk: Bridge regressions silently break the recommended Moxfield fallback path.
- Priority: Medium.

**No visual regression tests for 25 themes:**
- What's not tested: Theme application is verified manually. `theme-normalization-report.md` documents in-progress refactor; the Simic stacking-context bug (memory 2852) was caught by user feedback, not tests.
- Files: `DeckFlow.Web/wwwroot/css/site-*.css`
- Risk: Token leakage between themes recurs whenever a new component is added.
- Priority: Medium — would benefit from a Playwright snapshot harness across all themes.

**Light coverage for `DeckController` action surface:**
- What's not tested: `DeckController` (1,007 lines) has only `DeckControllerTests.cs` (~141 lines per grep). Most of the 25 actions go through one happy-path test.
- Files: `DeckFlow.Web/Controllers/DeckController.cs`
- Risk: Cross-feature regressions when one of the 12 injected services changes shape.
- Priority: High — DeckController is the highest-risk file by both size and dependency count.

**No tests for resilience-pipeline behavior:**
- What's not tested: `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` defines five named pipelines (commander-banlist, commander-spellbook, scryfall-rest, tagger-page, tagger-post), with retry/timeout settings per pipeline. No tests confirm retry counts or back-off curves.
- Files: `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`
- Risk: Tagger 404 bug (above) might have been masked by misconfigured retry behavior; impossible to verify without tests.
- Priority: Medium.

---

*Concerns audit: 2026-04-29*
