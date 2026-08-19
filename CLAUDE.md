<!-- GSD:project-start source:PROJECT.md -->
## Project

**DeckFlow**

DeckFlow is a Magic: The Gathering deck analysis tool for cEDH and Commander players, deployed live at https://www.deckflow.gg. It pulls deck data from Archidekt and Moxfield, generates ChatGPT-ready prompt artifacts for deck analysis, and provides synergy/category knowledge derived from the user's own crawled deck history. Audience: serious deck-builders who want a structured "compare, analyze, decide" workflow rather than a one-click recommender.

**Core Value:** **Every supported workflow must produce output the user can paste into ChatGPT and get back a useful answer in one round-trip — without the user reformatting anything.** Visual polish, theme variety, and admin tooling all serve that core. If the prompt artifacts are wrong or missing, nothing else matters.

### Constraints

- **Tech stack**: ASP.NET 10 + Razor — pinned by deployed app; no framework migration in this milestone
- **Hosting**: Render Starter web + Basic-256mb Postgres — 512MB RAM cap on web tier, mind allocations
- **Theme system**: Guild themes are full standalone CSS forks; layout CSS must go in `site-common.css`, not `site.css` — token additions go in `:root` of each theme file
- **HTTP resilience**: Use existing RestSharp + direct Polly v8 pattern — do NOT migrate to standard handler
- **Public repo**: `luntc1972/DeckFlow` is public — no secrets in commits ever; secrets live in Render dashboard with `sync: false`
- **Testing**: VSTest unreliable in WSL; rely on `dotnet build` clean + targeted manual harness or push-and-watch CI. **UI testing must NEVER open a browser on the Windows host** — start the web app with `scripts/run-web-test.sh` (or `.ps1`), which sets `DECKFLOW_DISABLE_AUTO_BROWSER=true` so the Development auto-launch is suppressed. Drive live UI checks with `npx --no-install playwright test` (WSL can run it) or a manually-opened browser against the headless server; do NOT rely on the WSL `gstack` headless daemon (observed unstable — crashes / won't follow form-POST navigation).
- **Commits**: Plain default-author commits, no Co-Authored-By trailer; README updated when behavior changes; commit per logical change
- **Formatting**: `.editorconfig` is the enforced, tool-agnostic source of truth and `.gitattributes` pins line endings per type (LF by default; **CRLF for `.ps1`/`.psm1`/`.bat`/`.cmd`**). New and changed C# lines must satisfy the changed-lines gate locally (`git config core.hooksPath .githooks` opt-in, then the versioned pre-commit hook runs `scripts/format-check-changed.sh staged`) and in CI (`format-gate`, which is the authoritative enforcer). Existing files are not mass-reflowed; the gate is changed-lines-only, so when editing a file, touch only the lines that need touching. The five bug-driven carve-outs override any conflicting formatter preference: never auto-convert `{ get; init; }` to `{ get; }` (System.Text.Json silently skips get-only properties in .NET 9+ — has broken `EdhTop16Client` deserialization before), never inline `[Attribute]` onto the property line, never re-indent C# raw-string literals (changes the literal value shipped to the AI), preserve switch expressions, preserve xmldoc single-space indent, preserve each file's committed line endings (`.gitattributes` pins LF by default, CRLF for `.ps1`/`.psm1`/`.bat`/`.cmd`). The carve-outs live authoritatively in `.editorconfig` and are guarded by the `CarveOutGuard` test.
<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->
## Technology Stack

ASP.NET 10 / C# 12 (7 projects), TypeScript 6 compiled by MSBuild, Razor MVC, xUnit. RestSharp +
Polly v8 for all egress; SQLite by default, Postgres via `DECKFLOW_DATABASE_PROVIDER`; Serilog.
Deployed to Render via Docker.

**Detail — read only on the stated trigger, not by default:**
`.planning/codebase/STACK.md` — when adding or upgrading a dependency, or when a config env var is
in question. `INTEGRATIONS.md` — when changing a call to an upstream API. `TESTING.md` — when
adding a test project or changing test layout. `AGENTS.md` at the repo root is the short module map
and build/test guide, and is already loaded for Codex; prefer it over the `.planning/codebase/`
files, which total ~36k tokens and are not cached.
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

File-scoped namespaces, Allman braces, nullable enabled, one public type per file (name matches),
`sealed` by default, `sealed record` for DTOs. Async methods end `Async` and take
`CancellationToken cancellationToken = default` last. Public surface returns `IReadOnlyList<T>` /
`IReadOnlyDictionary<K,V>`, never mutable `List<T>`. Private fields `_camelCase`; constants and
static readonly `PascalCase`. XML docs on public members; comments explain **why**. Structured
logging only — never string interpolation into a log template. Test doubles: `Fake*` stateful,
`Stub*` queue-driven, `Throwing*` for exception injection; internal test-seam constructors are
exposed via `[InternalsVisibleTo]`.

**Detail — read only when a convention above is ambiguous for the file you are editing:** `.planning/codebase/CONVENTIONS.md`. The same rules are in `AGENTS.md` (Codex-facing), which is cheaper.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

`DeckFlow.Core` is pure domain (no ASP.NET). `DeckFlow.Web` is controllers -> `I*Service` ->
`DeckFlow.Core`, with HTTP egress centralized in `Services/Http/`. `DeckFlow.CLI` and
`DeckFlow.Studio` are separate hosts over the same Core. Composition root is
`DeckFlow.Web/Program.cs`.

**Detail — read only on the stated trigger:** `.planning/codebase/ARCHITECTURE.md` and
`STRUCTURE.md` (component tables, layers, data flow, entry points) — when adding a component or
changing a layer boundary. `CONCERNS.md` — when triaging a known issue. The constraints below are
the part you need for ordinary work; they are inline precisely so those files stay unread.

### Architectural Constraints — violating these causes real bugs

- **`ScryfallThrottle`** (static `SemaphoreSlim`, `DeckFlow.Web/Services/Scryfall/ScryfallThrottle.cs`) wraps
  **every** Scryfall call. Bypassing it has caused live Cloudflare IP blocks.
- **`TaggerSessionCache` TTL (270s) MUST stay strictly below** the tagger handler lifetime (5 min).
  Wired in `DeckFlow.Web/Extensions/HttpClientServiceCollectionExtensions.cs`
  (`.SetHandlerLifetime(TaggerSessionCache.HandlerLifetime)`); guarded by
  `DeckFlow.Web.Tests/Services/TaggerSessionCacheInvariantTests.cs`.
- **`UseForwardedHeaders()` MUST run before** HTTPS redirect, security headers, and
  `SameOriginRequestValidator`, or scheme mismatch breaks the CSRF check.
- **TS build coupling:** `tsc` runs on every build; `wwwroot/js/*.js` is **gitignored** — never stage
  compiled JS. The Docker build recompiles at deploy, so committed `.js` only creates drift.
- **Layout CSS goes in `site-common.css`, not `site.css`** (guild themes are standalone forks).
- Building from the VS-shared NuGet path on Windows can leave a stale `project.assets.json` — build
  from WSL or clean `obj/`.

### Anti-patterns

`new HttpClient()` in a service; building Polly pipelines per call; migrating to
`Microsoft.Extensions.Http.Resilience`; calling Scryfall without `ScryfallThrottle`; skipping
`SameOriginRequestValidator` on an API endpoint; putting layout CSS in `site.css`.
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->
## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->



<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
