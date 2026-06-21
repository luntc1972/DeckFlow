# Phase 63: Studio Self-Contained Executable - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning
**Source:** Orchestrator-locked decisions (no discuss-phase; small, well-understood packaging phase) + 63-RESEARCH.md

<domain>
## Phase Boundary

Package `DeckFlow.Studio` (Blazor Server, .NET 10) as a self-contained `win-x64` build the operator runs on a clean Windows box with NO .NET runtime installed. Produce a repeatable, documented publish step. ONLY `DeckFlow.Studio` is in scope — not `DeckFlow.Web`. No behavior change to the app itself; this is build/packaging + docs only.
</domain>

<decisions>
## Implementation Decisions (LOCKED)

### Publish shape
- **Self-contained, `win-x64`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`.** Verified by trial publish: yields one `DeckFlow.Studio.exe` (~116 MB) with the only native dep (`e_sqlite3.dll`) embedded + auto-extracted to `%TEMP%`.
- **Trimming OFF** (`PublishTrimmed=false`). Blazor Server + Dapper + reflection-heavy DI is trim-unsafe.
- **ReadyToRun OFF** by default (optional, +~15 MB, faster cold start; not worth it — Studio starts once). Document as an optional toggle only.

### "Single-file" reality — distribution = exe + `wwwroot/`
- Blazor Server's `UseStaticFiles()` reads `wwwroot/` from disk relative to the exe; `wwwroot/` (~1.6 MB: `_framework/blazor.server.js`, CSS) **cannot** be embedded. So DIST-01's "single-file executable" is satisfied as a **single-file exe shipped in a small zip alongside `wwwroot/`**. The publish step produces the publish folder; the deliverable to the operator is that folder (optionally zipped). Capture this honestly in the docs — do not claim a literal one-file drop.

### Where the publish config lives — do NOT change the default build
- Put publish properties in a **publish profile** `DeckFlow.Studio/Properties/PublishProfiles/win-x64-selfcontained.pubxml`, NOT unconditionally in the `.csproj`. Rationale: unconditional `<PublishSingleFile>`/`<SelfContained>` in csproj would alter every `dotnet build`/`dotnet run`/CI publish and the Docker web build conventions. The profile applies only when explicitly selected. The `scripts/` publish script selects the profile.
- New files (pubxml, scripts) are fine; respect `.gitattributes` LF/CRLF rules. No edits to `Dockerfile`/`render.yaml` (out of the "Do Not Modify" infra list and out of scope).

### Build invocation (WSL → Windows)
- This repo builds Windows-side; the publish script invokes the **Windows `dotnet.exe`** to produce a `win-x64` artifact. Provide a PowerShell script (`scripts/publish-studio.ps1`, primary) and a thin bash wrapper (`scripts/publish-studio.sh`) that calls `dotnet.exe` for WSL users. Script must be re-runnable (clean output dir) and print the artifact path + size.

### Secrets on a clean machine = environment variables
- `Program.cs` calls `AddUserSecrets<Program>()` (dev-only; harmless/no-op when absent — do NOT remove it). A packaged exe on a fresh box has no user-secrets, so the operator supplies secrets via **environment variables** (`Studio__ProdConnectionString`, `Studio__Scp__*`, `DECKFLOW_LLM_PROVIDER`, etc.).
- **The basic harvest → distill → review → approve flow must work with NO secrets.** Only the DirectPush (SCP + prod Postgres) and git commit-publish paths need extra config (prod conn string / SSH creds / `git.exe` on PATH). Document this split clearly.

### No new packages
- Pure SDK publish properties only. No NuGet additions (project rule).

### Documentation target
- Update the existing Studio setup doc (find it — `STUDIO-SETUP.md` or the Studio README) with a "Run the standalone Windows executable" section: prerequisites (none beyond Windows x64), how to publish (`scripts/publish-studio.ps1`), where the artifact lands, how to launch + which URL, how to supply secrets via env vars for DirectPush, and the wwwroot-must-stay-beside-exe note. Per project rule, update README if behavior/workflow docs live there.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Research
- `.planning/phases/63-studio-self-contained-executable-package-deckflow-studio-as-/63-RESEARCH.md` — trial-publish-verified findings: exact publish command, 116 MB output, native-dep closure (only `e_sqlite3.dll`), wwwroot-on-disk constraint, secrets-via-env, trimming-off.

### Code
- `DeckFlow.Studio/DeckFlow.Studio.csproj` — target framework + package refs.
- `DeckFlow.Studio/Program.cs` — boot, data-dir resolution, config/secrets reads.
- `DeckFlow.Studio/Properties/launchSettings.json` — current dev URLs (:5271) / env.

### Project rules
- `./CLAUDE.md` — DeckFlow constraints (no new packages, public repo / no secrets, .editorconfig carve-outs, LF via .gitattributes, README-on-behavior-change).
</canonical_refs>

<specifics>
## Specific Ideas
- Trial publish command that worked: `dotnet.exe publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false` (planner moves these into the .pubxml so the script is a one-liner `dotnet.exe publish DeckFlow.Studio -p:PublishProfile=win-x64-selfcontained`).
- Expected artifact: `DeckFlow.Studio.exe` ~116 MB + `wwwroot/` ~1.6 MB; total zip ~118 MB.
- Default Kestrel URL when launched without args = `http://localhost:5000`. Decide whether the doc tells the operator to open :5000 or whether the script/profile pins a URL. Lean: document `--urls`/`ASPNETCORE_URLS` (matches the known WSL→Windows env-doesn't-propagate gotcha) rather than hard-pinning.
</specifics>

<deferred>
## Deferred Ideas
- ReadyToRun cold-start optimization (optional toggle only).
- Cross-platform self-contained builds (linux/osx) — out of scope; operator is Windows.
- Code-signing the exe — out of scope (single operator, local tool).
- Installer / MSI — out of scope; zip-and-run is sufficient.
</deferred>

---

*Phase: 63-studio-self-contained-executable*
*Context locked: 2026-06-20 by orchestrator + 63-RESEARCH.md*
</content>
</invoke>
