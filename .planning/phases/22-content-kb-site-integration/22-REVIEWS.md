**Verdict: BLOCK**

The architecture is close, but the plans are not ready to execute. The biggest risks are artifact delivery/path mismatches, a missed CSRF path, and invalid wave dependencies.

**High Concerns**

- **HIGH - 22-03 depends on 22-02 but does not declare it.**  
  Plan 03 reads Plan 02’s `runtime_artifact_base`, seed, and artifact delivery decision, yet `depends_on` is only `["22-01"]` and both are Wave 2. Make `22-03` depend on `22-02`, or lock artifact base before both plans run.

- **HIGH - 22-02 artifact Docker delivery misses `.dockerignore`.**  
  The Dockerfile runtime copies only `/app/publish` ([Dockerfile](/mnt/c/users/chrislunt/source/personal/deckflow/Dockerfile:39)), and `.dockerignore` excludes `*.md` ([.dockerignore](/mnt/c/users/chrislunt/source/personal/deckflow/.dockerignore:32)). `COPY content-kb/ ./content-kb/` will not reliably include markdown artifacts unless `.dockerignore` adds explicit `!content-kb/**` exceptions. Plan 02 must include this.

- **HIGH - artifact path convention is inconsistent.**  
  Existing contract writes `artifact_path` as `content-kb/{source}/{id}.md` ([ContentArtifactWriter.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Core/Knowledge/ContentArtifactWriter.cs:18)), but Plans 02/03 sometimes treat it as `{source}/{id}.md` relative to the `content-kb/` root. That can resolve to `/app/content-kb/content-kb/...`. Pick one convention and make export, seed loader, controller, tests, and docs match.

- **HIGH - 22-04 misses CSRF on the reused flag toggle.**  
  The admin KB page posts to existing `AdminFlagsController.Toggle`, which has `[ValidateAntiForgeryToken]` but no `SameOriginRequestValidator` ([AdminFlagsController.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs:77)). Locked criteria say every mutating admin POST gets both. Add same-origin validation there, and update the grep gate to include it.

- **HIGH - 22-02 CLI runner references the wrong layer.**  
  Plan 02 says to use `DeckFlowDatabaseConnectionFactory.CreateLocalContentKbConnection`, but that lives in Web and needs `IWebHostEnvironment`; CLI currently references Core only. Use the existing CLI `ResolveContentKbDatabasePath` + `new ContentSiteIndexStore(dbPath)` pattern instead.

- **HIGH - 22-03 artifact base must work in both local dev and Docker.**  
  In Docker runtime, `ContentRootPath` is `/app`; in local dev it is `DeckFlow.Web`. A single hardcoded “runtime base” will break one side. Add a resolver with candidates like `ContentRootPath/content-kb` and `ContentRootPath/../content-kb`, or a config override, then log the chosen base.

- **HIGH - 22-01 interface expansion breaks existing tests.**  
  Adding methods to `IContentSiteIndexStore` will break `FakeContentSiteIndexStore` in [RunDistillAsyncTests.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Core.Tests/RunDistillAsyncTests.cs:603). Plan 01 must include updating existing fakes, not only adding the new visibility test fixture.

**Medium Concerns**

- **MED - Flag-off behavior is still 503, not 404.**  
  Plan 03 accepts the existing `FeatureFlagGateAttribute` 503 behavior. If “hidden/404” is a hard requirement, add a not-found mode or separate gate. If 503 is acceptable, update the success criteria and UAT text so tests do not expect 404.

- **MED - Copy button reuse is underspecified.**  
  `attachDynamicCopyButton` in `card-lookup.ts` is not exported/global, and layout does not load that script. Plan 03 should either implement local copy logic in `content-kb.ts` or extract a shared copy helper loaded by the detail page.

- **MED - Detail route is unsafe for podcast GUIDs.**  
  `/content-kb/{sourceSlug}--{naturalKey}` works for YouTube IDs, but RSS GUIDs can contain `/`, `?`, `#`, or `--`. Use `id` for the route, or encode natural keys with a reversible URL-safe format.

- **MED - “last-loaded timestamp” has no source of truth.**  
  Plan 04 asks for last-loaded status, but no table/field stores seed load time. Define it as max `indexed_utc`, seed file mtime, or add a small status row. Otherwise the UI will fake it.

- **MED - Postgres coverage is thin.**  
  Plan 01 asserts both dialects but only describes SQLite tests. Add at least a Postgres integration check for `is_visible` DDL, bool read/write, and preserving upsert if the existing Postgres fixture is available.

**What Is Sound**

- The curation-preserving upsert contract is right: `is_visible` must be omitted from `DO UPDATE SET`.
- Additive migration via column introspection is the right pattern.
- Public browse uses server-side published-only filtering, which prevents hidden rows from leaking into client-side filters.
- No new NuGet dependencies are needed.
- The UI plans include 375px checks and theme-bleed gates.

**Required Fixes Before Execute**

1. Make `22-03` depend on `22-02`.
2. Fix `.dockerignore` and artifact runtime delivery.
3. Normalize the `artifact_path` convention across writer, seed, DB, and controller.
4. Add same-origin validation to `AdminFlagsController.Toggle`.
5. Fix CLI export to stay in the CLI/Core layer.
6. Add local/Docker artifact-base resolution.
7. Update existing `IContentSiteIndexStore` fakes/tests.

Final verdict: **BLOCK** until those HIGH items are corrected.
