---
phase: 64-deck-source-host-hardening
plan: 01
subsystem: DeckFlow.Core/Integration
tags: [security, host-validation, ssrf-prevention, regression-guard]
dependency_graph:
  requires: []
  provides: [DeckSourceHost.IsMoxfield, DeckSourceHost.IsArchidekt]
  affects: []
tech_stack:
  added: []
  patterns: [static-helper, pure-BCL, theory-inlinedata-matrix]
key_files:
  created:
    - DeckFlow.Core/Integration/DeckSourceHost.cs
    - DeckFlow.Core.Tests/DeckSourceHostTests.cs
  modified: []
decisions:
  - "Pure static class mirroring MoxfieldApiUrl/ArchidektApiUrl conventions in same namespace"
  - "Exact-or-subdomain predicate (string.Equals + EndsWith) — no TrimEnd, no Contains"
  - "No -warnaserror flag used in build gate — pre-existing NU1903/CS0618/CS1574 warnings prevent it"
metrics:
  duration: "~10 minutes"
  completed: "2026-06-21"
  tasks_completed: 2
  tasks_total: 2
  files_created: 2
  files_modified: 0
---

# Phase 64 Plan 01: DeckSourceHost Trust Predicate + Regression Matrix Summary

**One-liner:** Exact-or-approved-subdomain host predicate (`host == apex || host.EndsWith("." + apex)`) with 16-case Theory/InlineData matrix rejecting moxfield.com.evil.tld, evilmoxfield.com, and moxfield.com@evil.tld look-alikes.

## What Was Built

### Task 1: DeckSourceHost helper (`4dfefe77`)

`DeckFlow.Core/Integration/DeckSourceHost.cs` — `public static class DeckSourceHost` in `namespace DeckFlow.Core.Integration`.

- `private const string MoxfieldApex = "moxfield.com"` and `ArchidektApex = "archidekt.com"`
- `public static bool IsMoxfield(Uri uri)` — delegates to `IsApprovedHost(uri.Host, MoxfieldApex)`
- `public static bool IsArchidekt(Uri uri)` — delegates to `IsApprovedHost(uri.Host, ArchidektApex)`
- `private static bool IsApprovedHost(string host, string apex)` — `string.Equals(host, apex, OrdinalIgnoreCase) || host.EndsWith("." + apex, OrdinalIgnoreCase)`
- XML `<summary>` on class and both public methods naming the rejected look-alikes
- No `.Contains(`, no `TrimEnd('.')`, no new NuGet packages

### Task 2: DeckSourceHostTests matrix (`c9b018dc`)

`DeckFlow.Core.Tests/DeckSourceHostTests.cs` — `public sealed class DeckSourceHostTests` in `namespace DeckFlow.Core.Tests`.

IsMoxfield — 9 cases:
- ACCEPT: apex exact, www subdomain, api subdomain, uppercase URL (Uri.Host normalizes to lowercase)
- REJECT: superstring look-alike (`moxfield.com.evil.tld`), prefix look-alike (`evilmoxfield.com`), userinfo spoof (`moxfield.com@evil.tld` where Uri.Host = `evil.tld`), trailing-dot FQDN (`moxfield.com.`), cross-platform (archidekt URL)

IsArchidekt — 7 cases:
- ACCEPT: apex exact, www subdomain
- REJECT: superstring, prefix, userinfo, trailing-dot FQDN, cross-platform (moxfield URL)

**Test result: 16/16 passed** (`dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~DeckSourceHost`, net10.0, 126ms).

## Build Result

`dotnet build DeckFlow.Core.Tests` — 0 errors, warnings are pre-existing:
- NU1903: SQLitePCLRaw advisory (pre-existing)
- CS0618: Npgsql TrustServerCertificate obsolete (pre-existing in PostgresConnectionStringNormalizer)
- CS1574: unresolved cref in IContentIndexExporter (pre-existing)

Note: `-warnaserror` flag is not used as the local gate because pre-existing NU1903/CS0618/CS1574 warnings exist in the project that would fail the build regardless of this plan's changes. CI is the authoritative enforcement gate for those.

## Acceptance Criteria Verification

- [x] `DeckSourceHost.cs` exists at `DeckFlow.Core/Integration/DeckSourceHost.cs` in namespace `DeckFlow.Core.Integration`
- [x] File contains `public static class DeckSourceHost`, `public static bool IsMoxfield(Uri` and `public static bool IsArchidekt(Uri`
- [x] File contains `EndsWith("." + ` apex (subdomain match) and `string.Equals(` with `StringComparison.OrdinalIgnoreCase`
- [x] File contains NO `.Contains(` host-match call and NO `TrimEnd('.')`
- [x] `dotnet build DeckFlow.Core` clean — 0 errors, 0 new warnings
- [x] `DeckSourceHostTests.cs` exists with `[Theory]` + `[InlineData]` rows including all three named look-alike reject cases
- [x] Cross-platform isolation rows present (IsMoxfield false for archidekt URL; IsArchidekt false for moxfield URL)
- [x] All 16 DeckSourceHost tests passed locally

## Deviations from Plan

None — plan executed exactly as written.

## Threat Flags

None — this plan builds threat mitigations, adds no new network surface.

## Self-Check: PASSED

- `DeckFlow.Core/Integration/DeckSourceHost.cs` exists: FOUND
- `DeckFlow.Core.Tests/DeckSourceHostTests.cs` exists: FOUND
- Commit `4dfefe77` (feat): FOUND
- Commit `c9b018dc` (test): FOUND
- 16/16 tests passed in local run
