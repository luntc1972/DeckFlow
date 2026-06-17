---
phase: 41
slug: studio-scaffold-secrets-wiring
status: verified
threats_total: 4
threats_open: 0
threats_closed: 4
asvs_level: 1
created: 2026-06-17
auditor: claude-sonnet-4-6 (gsd-security-auditor)
---

# Phase 41 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Phase 41 is the highest-sensitivity scaffold in v1.7: secrets have no safe home until
> .gitignore + user-secrets are wired. All four threats verified by concrete file:line evidence.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| repo working tree → git history | Files staged with `git add` cross into version control; a secret committed here is permanently exposed in the public `luntc1972/DeckFlow` repo | Postgres connection string (secret) |
| application config → log/UI output | Connection string read from user-secrets must never cross into any log line, UI text, or error message | Postgres connection string (secret) |
| application config → DI / UI scope | The raw connection string stays a local in Program.cs; never stored in a DI-registered object (StudioConfig carries presence bools only) | Postgres connection string (secret) |
| Studio process → network | Studio binds a listening socket; localhost-only bind keeps it off the LAN | None (local tool) |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-41-01 | Information Disclosure | `.gitignore` + git working tree (`secrets.json`, `appsettings*.local.json`) | mitigate | .gitignore lines 29-31 add three patterns; `git log --all -- "**/secrets.json"` returns empty; `git grep -niE "postgres\|password\|Host=" -- DeckFlow.Studio/` returns only documentation placeholders in `PROD-CONNECTION.md` (no credential values) | closed |
| T-41-02 | Information Disclosure | `Program.cs` logging + `StudioConfig` + `Home.razor` UI + `DirectPush.razor` (Phase 47) | mitigate | `Program.cs:38-39`: `prodConnStr` is a local variable used only to compute the presence bool; `Program.cs:118-119`: both `Log.Information` calls pass only the string literals `"configured"`/`"not configured"` — the variable itself never appears in any log argument; `StudioConfig.cs:7`: `sealed record StudioConfig(bool IsProdConfigured, bool IsScpConfigured)` — no string connection member; `Home.razor:7`: renders `_isProdConfigured` bool only; `DirectPush.razor:514-515` and `683-684`: `rawConnStr` passed only to `ProdStoreFactory.Create()` — no log call references the variable (grep for log calls + conn-string patterns returns empty across all Studio files) | closed |
| T-41-03 | Elevation of Privilege | `DeckFlow.Studio/Properties/launchSettings.json` bind address | mitigate | `launchSettings.json:7`: `"applicationUrl": "http://localhost:5271"` — localhost-only bind; no `0.0.0.0`, no LAN exposure, no IIS/https profiles, no iisSettings block | closed |
| T-41-SC | Tampering | npm/NuGet supply chain | accept | In-solution Serilog reuse (Serilog.AspNetCore 9.0.0 + Serilog.Sinks.Console 6.0.0) sanctioned per user policy 2026-06-13 for in-solution packages. SSH.NET 2025.1.0 in csproj was added by Phase 47 (commit `a5c291c`) after Phase 41 scaffolding; it is not a Phase 41 artifact and does not affect Phase 41 scope. See Accepted Risks Log. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-41-01 | T-41-SC | Phase 41 adds exactly two PackageReferences: `Serilog.AspNetCore` 9.0.0 and `Serilog.Sinks.Console` 6.0.0, both already referenced by `DeckFlow.Web` (in-solution reuse). No genuinely-new NuGet packages at Phase 41 scope. `SSH.NET` 2025.1.0 visible in the csproj today was added by Phase 47 (commits `a1d14ed`, `a5c291c`) under Phase 47's own supply-chain review — it does not resurface as a Phase 41 debt. Supply-chain risk for SSH.NET is carried by Phase 47's threat model. | operator (user policy 2026-06-13; Phase 47 SSH.NET accepted at Phase 47 audit) | 2026-06-17 |

*Accepted risks do not resurface in future audit runs.*

---

## Unregistered Flags

None. `41-01-SUMMARY.md` contains no `## Threat Flags` section. No new attack surface was detected during implementation that lacks a threat mapping.

---

## Post-Phase Evolution Notes

Phase 47 added two items to Phase 41 artifacts after this phase shipped:
- `StudioConfig.cs:7`: `IsScpConfigured` second presence bool added — the presence-only contract (T-41-02) is preserved; no string member was introduced.
- `DeckFlow.Studio.csproj:22`: `SSH.NET 2025.1.0` PackageReference added — covered by Phase 47's supply-chain review, not Phase 41.
- `DirectPush.razor`: reads `Studio:ProdConnectionString` at lines 514 and 683 as local variable `rawConnStr`, passed only to `ProdStoreFactory.Create()` — verified no log call or UI render references the value; T-41-02 contract holds.

---

## Human Verification Items (from 41-VERIFICATION.md)

Two items require operator runtime confirmation; they are not security blockers but complete the SC1/STU-03 runtime contracts:

1. **SC1** — `dotnet run --project DeckFlow.Studio`, open `http://localhost:5271`, confirm "Studio is running." and "Prod connection: configured" render.
2. **STU-03 runtime** — Confirm startup stdout reads `Studio prod connection: configured` with no connection string value present anywhere in the output.

Static analysis of all code paths (T-41-02) confirms the implementation routes only the presence string to the logger. Runtime confirmation is the final contract seal.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-17 | 4 | 4 | 0 | claude-sonnet-4-6 (gsd-security-auditor) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-17
