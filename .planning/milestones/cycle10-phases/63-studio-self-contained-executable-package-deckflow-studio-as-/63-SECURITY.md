---
phase: 63
slug: studio-self-contained-executable
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-20
---

# Phase 63 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| operator workstation → distributed artifact (zip) | Published zip is hand-distributed; anything baked in is exposed | Binaries, appsettings.json, wwwroot assets |
| public git repo → committed files | `luntc1972/DeckFlow` is public; committed secrets are permanently leaked | Source, scripts, pubxml, docs |
| NuGet supply chain → published exe | All managed + native deps are bundled into the single-file exe | Runtime, SQLite native lib |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-63-01 | Information Disclosure | Published zip artifact | mitigate | Scripts strip `appsettings.Development.json`, `*.pdb`, `*.xml`, `web.config`; `appsettings.json` holds only logging + Kestrel port; secrets supplied at runtime via env vars | **CLOSED** |
| T-63-02 | Information Disclosure | Committed repo files | mitigate | No secrets in pubxml/scripts/appsettings.json/docs; `_launch-studio.bat` documented as NOT committed; public-repo rule; file does not exist in repo tree | **CLOSED** |
| T-63-03 | Tampering | npm/NuGet supply chain | mitigate | Base plan: zero new packages. Post-plan: `Serilog.Sinks.File 7.0.0` added to Studio (commit `3bb054da`); same package + version already in `DeckFlow.CLI.csproj`; vetted in-solution package, no new supply-chain surface | **CLOSED** |
| T-63-04 | Tampering | SQLitePCLRaw.lib.e_sqlite3 2.1.11 (NU1903) | accept | Known timing side-channel, not RCE; pinned transitively by `Microsoft.Data.Sqlite 10.0.0`; local operator tool, no public exposure; track for next Sqlite bump | **CLOSED (accepted)** |
| T-63-05 | Elevation of Privilege | Native lib self-extract to %TEMP% | accept | `e_sqlite3.dll` extracted to `%TEMP%\DeckFlow.Studio\<hash>\`; inherits OS temp permissions; standard .NET single-file behavior; single-operator local desktop tool | **CLOSED (accepted)** |
| T-63-06 | Information Disclosure | ASPNETCORE_ENVIRONMENT=Development on packaged exe | mitigate | `appsettings.Development.json` stripped from distribution zip; docs instruct operator not to set Development | **CLOSED** |
| T-63-SC | Tampering | NuGet installs | mitigate | No net-new (unvetted) package installs in this phase; see T-63-03 for in-solution Serilog.Sinks.File addition | **CLOSED** |
| T-63-07 | Information Disclosure | Log file (`<data dir>/logs/studio-<date>.log`) | mitigate (post-plan) | Presence-only logging: `Program.cs:148-149` logs `"configured"/"not configured"` for prod connection string and SCP; never logs secret values. `Program.cs:62` comment: "Why: presence-only check — never log values (D-07 / SC5)". `Program.cs:183` logs the server URL (non-sensitive). No secret values appear in any `Log.*` call. | **CLOSED** |
| T-63-08 | Execution | Auto-open browser (`Process.Start`) | mitigate (post-plan) | URL sourced exclusively from `IServerAddressesFeature` (server-controlled) with localhost fallback (`"http://localhost:5271"`); wildcard bindings normalized to localhost before use (`Program.cs:179-181`); no user/external input in URL; wrapped in `try/catch` so launch failure cannot crash the host (`Program.cs:185-189`) | **CLOSED** |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Evidence Detail (per threat)

### T-63-01 — Published zip artifact (CLOSED)

- `scripts/publish-studio.ps1:52` — `$StripPatterns = @('*.pdb', '*.xml', 'web.config', 'appsettings.Development.json')`; loop at lines 53-56 strips these from the output dir.
- `scripts/publish-studio.sh:45-50` — `find "$OUT_DIR" ... -name '*.pdb' -o -name '*.xml' -o -name 'web.config' -o -name 'appsettings.Development.json' ... -delete`
- `DeckFlow.Studio/appsettings.json` — contains only `Logging` and `Kestrel:Endpoints:Http:Url`; no secrets.
- `DeckFlow.Studio/Program.cs:58` — `builder.Configuration.AddUserSecrets<Program>().AddEnvironmentVariables()` — secrets come from environment at runtime, never baked into the artifact.

### T-63-02 — Committed repo files (CLOSED)

- `DeckFlow.Studio/Properties/PublishProfiles/win-x64-selfcontained.pubxml` — contains only MSBuild publish properties; no secrets.
- `scripts/publish-studio.ps1` and `scripts/publish-studio.sh` — no secrets embedded; scripts invoke Windows dotnet.exe with profile name only.
- `DeckFlow.Studio/appsettings.json` — no secrets (see T-63-01).
- `DeckFlow.Studio/STUDIO-SETUP.md:239-254` — `_launch-studio.bat` example marked "WARNING: This file holds secrets. DO NOT commit it." and explicitly flagged as "NOT committed or distributed".
- `git ls-files | grep -i _launch-studio` → no output (file not tracked by git).
- Caveat: `_launch-studio.bat` is not explicitly listed in `.gitignore` by name (`.gitignore` covers `DeckFlow.Studio/secrets.json` and `DeckFlow.Studio/appsettings*.local.json`, but not `*.bat` generally). The file does not exist in the repo tree and is documented as an operator-only artefact kept in the exe folder on the target machine. The documentation warning (`STUDIO-SETUP.md:244`) is the primary mitigation; no `.gitignore` entry exists for the bat file specifically. Assessed: LOW residual risk — documented operator guidance is clear; no bat file exists in the repo tree today.

### T-63-03 / T-63-SC — Supply chain (CLOSED)

- `DeckFlow.Studio/DeckFlow.Studio.csproj:22` — `<PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />` (commit `3bb054da`, post-plan addition).
- `DeckFlow.CLI/DeckFlow.CLI.csproj:11` — same package at same version 7.0.0 (pre-existing in solution); not a new unvetted package.
- `DeckFlow.Web/DeckFlow.Web.csproj:13` — uses `6.0.0`; Studio pins a newer minor version, already vetted in CLI.
- No new packages outside the existing solution dependency graph. Supply-chain surface unchanged.

### T-63-04 — SQLitePCLRaw.lib.e_sqlite3 NU1903 (CLOSED — accepted risk)

Accepted. See Accepted Risks Log below.

### T-63-05 — Native lib self-extract to %TEMP% (CLOSED — accepted risk)

Accepted. See Accepted Risks Log below.

### T-63-06 — ASPNETCORE_ENVIRONMENT=Development (CLOSED)

- Both scripts strip `appsettings.Development.json` from the output dir (see T-63-01 evidence).
- `DeckFlow.Studio/STUDIO-SETUP.md:94-99` — security rules section states secrets must not appear in `appsettings*.json`; no Development env guidance needed beyond stripping the file.
- `DeckFlow.Studio/Program.cs:151-155` — `if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }` — exception handler active in Production (the packaged exe default).

### T-63-07 — Log file Information Disclosure (CLOSED, post-plan addition)

- `DeckFlow.Studio/Program.cs:35-38` — bootstrap logger writes to `studio-.log` (rolling daily, 14-file cap).
- `DeckFlow.Studio/Program.cs:53-55` — host-level logger also writes to same file.
- `DeckFlow.Studio/Program.cs:62` — comment: "Why: presence-only check — never log values (D-07 / SC5)."
- `DeckFlow.Studio/Program.cs:148` — `Log.Information("Studio prod connection: {Status}", isProdConfigured ? "configured" : "not configured")` — boolean only.
- `DeckFlow.Studio/Program.cs:149` — `Log.Information("Studio SCP: {Status}", isScpConfigured ? "configured" : "not configured")` — boolean only.
- `DeckFlow.Studio/Program.cs:182` — `Log.Information("Opening default browser at {Url}", url)` — server-bound URL, non-sensitive.
- `DeckFlow.Studio/Program.cs:40` — `Log.Information("DeckFlow Studio starting. Data dir: {DataDir}; logs: {LogDir}", ...)` — filesystem paths, non-sensitive.
- No `Log.*` call anywhere in Program.cs passes a secret value. Grep of `DeckFlow.Studio/` for `Log.*(ProdConn|Scp|ApiKey|Password|Secret|KeyPassphrase|ConnectionString)` → no matches.

### T-63-08 — Auto-open browser execution surface (CLOSED, post-plan addition)

- `DeckFlow.Studio/Program.cs:167-168` — `disableAutoBrowser` read from `DECKFLOW_DISABLE_AUTO_BROWSER` env var; feature is suppressible.
- `DeckFlow.Studio/Program.cs:169` — guarded: `if (!app.Environment.IsDevelopment() && !disableAutoBrowser)` — never fires in Development or when suppressed.
- `DeckFlow.Studio/Program.cs:175-177` — URL comes exclusively from `IServerAddressesFeature` (server-controlled Kestrel binding); fallback is the compile-time constant `"http://localhost:5271"`. No user input, no external data, no untrusted string enters the URL.
- `DeckFlow.Studio/Program.cs:179-181` — wildcard bindings (`+`, `0.0.0.0`, `[::]`) normalized to `localhost` before use.
- `DeckFlow.Studio/Program.cs:183` — `Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true })` — `UseShellExecute=true` is the OS shell-open mechanism for URLs; it does not pass arguments or run a shell; the FileName is a `http://localhost:NNNN` URL.
- `DeckFlow.Studio/Program.cs:185-189` — wrapped in `try/catch (Exception browserException)`; launch failure is logged as Warning and execution continues; host is never downed.

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-63-01 | T-63-04 | SQLitePCLRaw.lib.e_sqlite3 2.1.11 (NU1903) known timing side-channel — NOT RCE. Pinned transitively by `Microsoft.Data.Sqlite 10.0.0`; no Phase-63 action possible without upgrading the entire Sqlite stack. Local operator tool on private LAN, no public network exposure. Track for next `Microsoft.Data.Sqlite` bump. | auditor (gsd-security-auditor) | 2026-06-20 |
| AR-63-02 | T-63-05 | `e_sqlite3.dll` self-extraction to `%TEMP%\DeckFlow.Studio\<hash>\` inherits OS temp permissions. This is standard .NET 6+ single-file + native-lib behavior (`IncludeNativeLibrariesForSelfExtract=true`); no mitigation is available without removing single-file packaging. Single-operator local desktop tool; no multi-user or multi-tenant exposure. Acceptable. | auditor (gsd-security-auditor) | 2026-06-20 |

---

## Unregistered Threat Flags

The SUMMARY.md `## Threat Flags` section states: "No new security-relevant surfaces introduced beyond what is already in the plan's threat model (T-63-01 through T-63-SC)."

Post-plan additions (`3bb054da` Serilog file sink, `dbe8f6b9` auto-browser-open) were assessed as T-63-07 and T-63-08 respectively (per audit instructions). Both verified CLOSED. No unregistered flags.

**Minor observation (non-blocking):** `_launch-studio.bat` is not in `.gitignore` by name. Mitigation is documentation-only (`STUDIO-SETUP.md:244`). The file does not exist in the repo tree. Recommend adding a `_launch-studio*.bat` or `**/DeckFlow.Studio.exe` companion exclusion to `.gitignore` in a follow-up to enforce the "not committed" intent at the VCS layer. Not a BLOCKER at ASVS Level 1 for a single-operator tool with clear documented guidance.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-20 | 9 (7 plan + 2 post-plan) | 9 | 0 | Claude (gsd-security-auditor, sonnet-4-6) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log (AR-63-01, AR-63-02)
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-20
