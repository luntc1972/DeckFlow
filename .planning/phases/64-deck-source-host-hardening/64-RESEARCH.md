# Phase 64: Deck-Source Host Hardening - Research

**Researched:** 2026-06-21
**Domain:** Security — URL host validation, SSRF/spoofing prevention
**Confidence:** HIGH (all findings from direct codebase reads)

## Summary

Three call sites validate deck-source URLs using `uri.Host.Contains("moxfield.com")` or
`originalUrl.Contains("moxfield.com")`. The substring check accepts hostile look-alike
hosts (`moxfield.com.evil.tld`, `evilmoxfield.com`) and string-match attacks on the raw
URL value (userinfo, path, query). The fix is a single shared static helper —
`DeckSourceHost` in `DeckFlow.Core/Integration/` — that all three sites adopt, plus a
fourth site (`MoxfieldApiDeckImporter.FetchViaCommanderSpellbookAsync`) that must always
forward a reconstructed URL, never the submitted value.

No framework migration is involved. All changes are pure logic replacements in four
methods across two projects. All regression tests are pure in-memory assertions — no
network mocking required.

**Primary recommendation:** Add `public static class DeckSourceHost` in
`DeckFlow.Core/Integration/DeckSourceHost.cs` with `IsMoxfield(Uri)` and
`IsArchidekt(Uri)` predicates using exact + subdomain matching, then update the four call
sites and add `[Theory][InlineData]` spoof-case tests in existing test files.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Host-trustworthiness check | Core library | — | Pure domain logic, no HTTP; belongs in DeckFlow.Core alongside the URL helpers it gates |
| Spellbook URL construction | Core integration | — | Lives inside MoxfieldApiDeckImporter which already owns this flow |
| Artifact URL classification | Web service | Core helper | PacketArtifactStore calls the helper but lives in DeckFlow.Web |

---

## Spoofable-Site Inventory (Complete)

Every call site that branches on a user-supplied deck URL/host string:

| # | File | Line(s) | Vulnerable pattern | Decision driven | In-scope for phase |
|---|------|---------|--------------------|-----------------|-------------------|
| **S1** | `DeckFlow.Core/Loading/DeckEntryLoader.cs` | 121 | `uri.Host.Contains("moxfield.com", OrdinalIgnoreCase)` | Routes to Moxfield importer | YES — SC1 |
| **S2** | `DeckFlow.Core/Loading/DeckEntryLoader.cs` | 127 | `uri.Host.Contains("archidekt.com", OrdinalIgnoreCase)` | Routes to Archidekt importer | YES — SC1 |
| **S3** | `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` | 105 | `originalUrl.Contains("moxfield.com", OrdinalIgnoreCase)` | Chooses whether to forward submitted URL or reconstruct | YES — SC2 |
| **S4** | `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` | 99–100 | `uri.Host.Contains("moxfield.com"/"archidekt.com", OrdinalIgnoreCase)` | Decides whether to suppress "original deck text" artifact for URL imports | YES — see analysis below |

### PacketArtifactStore (S4) — in-scope determination

`OriginalDeckTextOrNull(string? source)` returns `null` (suppresses the artifact) when
the input looks like a Moxfield/Archidekt URL. The decision is conservative — it causes
no data to be written, not an SSRF or import. However:

- A spoof URL like `https://moxfield.com.evil.tld/decks/x` currently passes the substring
  check and would incorrectly suppress the artifact (returns `null` instead of `trimmed`).
- More importantly, the phase goal is "any deck tool" and the CLAUDE.md mandates a single
  source of truth (SOLID D/O). Leaving this site on the old pattern while the other three
  are fixed creates drift and re-introduces the bug class the SC3 regression tests are
  meant to catch.
- **Verdict: harden S4.** The fix is one-line (call `DeckSourceHost.IsMoxfield(uri) ||
  DeckSourceHost.IsArchidekt(uri)` instead of the two inline Contains calls). Risk = zero.

### Not in scope — error-message string matching

`DeckFlow.Web/Services/UpstreamErrorMessageBuilder.cs` lines 58, 112, 117 use
`exception.Message.Contains("moxfield"/"archidekt")`. These scan **exception text** (an
internal string produced by the importers), not user-supplied input. They cannot be
spoofed by a hostile URL, so they are out of scope for this phase.

---

## Research Question Answers

### 1. The Moxfield→Spellbook fallback flow (SC2 — S3)

**Location:** `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs:103-130`
— method `FetchViaCommanderSpellbookAsync(string originalUrl, string deckId, ...)`

**What it does today:**

```csharp
// Line 105-107 (current — vulnerable)
var moxfieldUrl = originalUrl.Contains("moxfield.com", StringComparison.OrdinalIgnoreCase)
    ? originalUrl          // ← forwards whatever the user submitted
    : $"https://moxfield.com/decks/{deckId}";
```

The method is called from `ImportWithSourceAsync` (line 62) as:

```csharp
var entries = await FetchViaCommanderSpellbookAsync(urlOrDeckId, deckId, cancellationToken)
```

where `urlOrDeckId` is the raw user-submitted string, and `deckId` has already been
parsed by `MoxfieldApiUrl.TryGetDeckId` at line 50 (before this code path).

**SC2 fix — always reconstruct:**

```csharp
// After fix — SC2 compliant
var moxfieldUrl = $"https://moxfield.com/decks/{deckId}";
```

The `deckId` is safe: `MoxfieldApiUrl.TryGetDeckId` already extracted it from
`uri.AbsolutePath.Split('/')` at segments[1], so it is always a path segment from an
already-parsed URI, never raw user input forwarded wholesale.

The check on `originalUrl` is entirely redundant after SC1 is implemented: by the time
`FetchViaCommanderSpellbookAsync` is called, the host has already been validated at S1 in
`DeckEntryLoader.LoadFromSourceAsync`. The fix is to drop the conditional and always
emit the canonical URL. The `originalUrl` parameter can remain for error-message context
(used in `throw new HttpRequestException(...)` at line 119) — only the `moxfieldUrl`
variable assignment changes.

### 2. Canonical host-check helper design

**Recommendation:** `public static class DeckSourceHost` in
`DeckFlow.Core/Integration/DeckSourceHost.cs`

This location follows the existing `MoxfieldApiUrl` / `ArchidektApiUrl` static-class
conventions in the same folder. Both callers from Core (`DeckEntryLoader`,
`MoxfieldApiDeckImporter`) and the Web caller (`PacketArtifactStore`) already reference
`DeckFlow.Core` — no new project reference needed.

```csharp
// DeckFlow.Core/Integration/DeckSourceHost.cs
namespace DeckFlow.Core.Integration;

/// <summary>
/// Validates whether a URI belongs to a trusted deck-source host.
/// Uses exact domain or approved-subdomain matching — never substring.
/// </summary>
public static class DeckSourceHost
{
    private const string MoxfieldApex = "moxfield.com";
    private const string ArchidektApex = "archidekt.com";

    /// <summary>
    /// Returns true when <paramref name="uri"/> targets moxfield.com or an approved
    /// moxfield.com subdomain (e.g. www.moxfield.com). Rejects look-alikes such as
    /// moxfield.com.evil.tld, evilmoxfield.com, and moxfield.com@evil.tld.
    /// </summary>
    public static bool IsMoxfield(Uri uri)
        => IsApprovedHost(uri.Host, MoxfieldApex);

    /// <summary>
    /// Returns true when <paramref name="uri"/> targets archidekt.com or an approved
    /// archidekt.com subdomain.
    /// </summary>
    public static bool IsArchidekt(Uri uri)
        => IsApprovedHost(uri.Host, ArchidektApex);

    private static bool IsApprovedHost(string host, string apex)
    {
        // Uri.Host is already lowercase on .NET (RFC 3986 normalization).
        // The comparison is ordinal because domain names are ASCII.
        return string.Equals(host, apex, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + apex, StringComparison.OrdinalIgnoreCase);
    }
}
```

**Why this satisfies SOLID and SC3:**

- **S:** Single responsibility — one class, one concern (host trust).
- **O:** New platforms added by extending the class, not editing call sites.
- **D:** All call sites depend on the abstraction, not on inline string logic.
- **SC3 regression:** Tests in `DeckSourceHostTests.cs` that fail on `Contains` wording
  provide the safety net. If a future dev reverts S1/S2/S3/S4 to substring, those tests
  catch it immediately.

### 3. Edge cases for the host rule

All `Uri.Host` behaviors below are from .NET's `System.Uri` RFC 3986 implementation
[ASSUMED — consistent with .NET docs and verified via Node URL (same spec) above]:

| Input URL | `Uri.Host` value | IsMoxfield() | Expected behavior |
|-----------|-----------------|-------------|-------------------|
| `https://moxfield.com/decks/x` | `moxfield.com` | ACCEPT | Correct |
| `https://www.moxfield.com/decks/x` | `www.moxfield.com` | ACCEPT | Correct — `www.` is approved subdomain |
| `https://api.moxfield.com/decks/x` | `api.moxfield.com` | ACCEPT | Correct — subdomains accepted |
| `HTTPS://MOXFIELD.COM/decks/x` | `moxfield.com` | ACCEPT | .NET normalizes to lowercase |
| `https://moxfield.com.evil.tld/decks/x` | `moxfield.com.evil.tld` | **REJECT** | Not apex, not `.moxfield.com` suffix |
| `https://evilmoxfield.com/decks/x` | `evilmoxfield.com` | **REJECT** | Not apex, not `.moxfield.com` suffix |
| `https://moxfield.com@evil.tld/decks/x` | `evil.tld` | **REJECT** | .NET strips userinfo; Host = `evil.tld` |
| `https://moxfield.com./decks/x` | `moxfield.com.` | **REJECT** | Trailing-dot FQDN — neither exact match nor suffix match |
| `https://sub.sub.moxfield.com/x` | `sub.sub.moxfield.com` | ACCEPT | EndsWith(`.moxfield.com`) is true — acceptable |
| `https://archidekt.com/decks/123` | `archidekt.com` | IsArchidekt: ACCEPT | Correct |
| `https://www.archidekt.com/decks/123` | `www.archidekt.com` | IsArchidekt: ACCEPT | Correct |

**Trailing-dot FQDN note:** `moxfield.com.` is technically valid DNS FQDN notation.
`Uri.Host` preserves the trailing dot in .NET. The exact-match (`== "moxfield.com"`) does
NOT accept `moxfield.com.`, and `EndsWith(".moxfield.com")` also does NOT accept it
(suffix is `.moxfield.com`, not `.moxfield.com.`). This means trailing-dot FQDNs are
rejected, which is correct — no user would type this intentionally, and the real Moxfield
site does not use it.

**Blast-radius / legitimate URLs:** The existing test at
`DeckEntryLoaderTests.cs:26` uses `https://www.moxfield.com/decks/example`. This host
(`www.moxfield.com`) ends with `.moxfield.com`, so it is accepted by the new helper.
`MoxfieldApiUrl.TryGetDeckId` at line 28-33 parses paths from any host — the host check
lives upstream in `DeckEntryLoader.LoadFromSourceAsync`, not in `TryGetDeckId` itself.
No legitimate URL pattern is broken.

### 4. Existing test patterns to mirror

**DeckEntryLoader tests:**
- File: `DeckFlow.Core.Tests/DeckEntryLoaderTests.cs`
- Framework: xUnit 2.9.3, `[Fact]` only (no `[Theory]` currently)
- Pattern: inline `FakeMoxfieldDeckImporter` / `FakeArchidektDeckImporter` that track
  `LastImportWithSourceArgument` / `LastImportArgument`. Tests call
  `loader.LoadFromSourceAsync(url)` and assert on dispatching.

For spoof-case regression tests in `DeckEntryLoaderTests.cs`, use `[Theory][InlineData]`:

```csharp
[Theory]
[InlineData("https://moxfield.com.evil.tld/decks/abc")]
[InlineData("https://evilmoxfield.com/decks/abc")]
[InlineData("https://moxfield.com@evil.tld/decks/abc")]
public async Task LoadFromSourceAsync_SpoofedMoxfieldUrl_DoesNotRouteTOMoxfieldImporter(string spoofUrl)
{
    var importer = new FakeMoxfieldDeckImporter(_ => []);
    var loader = CreateLoader(importer: importer);

    // Falls through to parser path (not recognized as valid URL platform),
    // throws InvalidOperationException — NOT dispatched to Moxfield importer.
    await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadFromSourceAsync(spoofUrl));
    Assert.Null(importer.LastImportWithSourceArgument); // importer was NOT called
}
```

**MoxfieldApiDeckImporter tests:**
- File: `DeckFlow.Core.Tests/MoxfieldApiDeckImporterTests.cs`
- Framework: xUnit 2.9.3, `[Fact]`
- Pattern: construct `MoxfieldApiDeckImporter(executeAsync: ...)` delegate that captures
  the request URL for assertion.

For SC2 (URL-forwarding) regression test:

```csharp
[Fact]
public async Task FetchViaCommanderSpellbookAsync_AlwaysForwardsCanonicalUrl_NeverSubmittedUrl()
{
    RestRequest? capturedRequest = null;
    var importer = new MoxfieldApiDeckImporter(executeAsync: (request, _) =>
    {
        capturedRequest = request;
        // First call = Moxfield direct (return 403 to trigger fallback)
        if (request.Resource?.Contains("api.moxfield.com") == true)
            return Task.FromResult(new RestResponse { StatusCode = System.Net.HttpStatusCode.Forbidden, ... });
        // Second call = Spellbook fallback — capture it
        return Task.FromResult(/* valid spellbook response */);
    });

    await importer.ImportAsync("https://www.moxfield.com/decks/abc123");

    // The url query parameter forwarded to Spellbook must be canonical
    var urlParam = capturedRequest?.Parameters.FirstOrDefault(p => p.Name == "url")?.Value?.ToString();
    Assert.Equal("https://moxfield.com/decks/abc123", urlParam);
}
```

**DeckSourceHost tests (new file):**
- New file: `DeckFlow.Core.Tests/DeckSourceHostTests.cs`
- Pattern: pure static method, no DI/HTTP, use `[Theory][InlineData]`

```csharp
[Theory]
[InlineData("https://moxfield.com/decks/x", true)]
[InlineData("https://www.moxfield.com/decks/x", true)]
[InlineData("https://api.moxfield.com/v2/decks/x", true)]
[InlineData("https://moxfield.com.evil.tld/decks/x", false)]
[InlineData("https://evilmoxfield.com/decks/x", false)]
[InlineData("https://moxfield.com@evil.tld/decks/x", false)]  // Uri.Host = "evil.tld"
[InlineData("https://moxfield.com./decks/x", false)]           // trailing-dot FQDN
public void IsMoxfield_VariousHosts_ReturnsExpected(string url, bool expected)
{
    var uri = new Uri(url);
    Assert.Equal(expected, DeckSourceHost.IsMoxfield(uri));
}
```

**PacketArtifactStore tests (new case in DeckFlow.Web.Tests):**
- `PacketArtifactStore` is `internal static` in `DeckFlow.Web.Services` — check if
  `[InternalsVisibleTo("DeckFlow.Web.Tests")]` is set. A simpler approach: test via
  the public method `OriginalDeckTextOrNull(string?)` which is already accessible.
- File to add cases to: check `DeckFlow.Web.Tests/` for an existing
  `PacketArtifactStoreTests.cs` or add new file.

### 5. Call site change summary (for planner)

| Site | File:Line | Current | Fix |
|------|-----------|---------|-----|
| S1 | `DeckEntryLoader.cs:121` | `uri.Host.Contains("moxfield.com", OrdinalIgnoreCase)` | `DeckSourceHost.IsMoxfield(uri)` |
| S2 | `DeckEntryLoader.cs:127` | `uri.Host.Contains("archidekt.com", OrdinalIgnoreCase)` | `DeckSourceHost.IsArchidekt(uri)` |
| S3 | `MoxfieldApiDeckImporter.cs:105-107` | Conditional — forward `originalUrl` or reconstruct | Always `$"https://moxfield.com/decks/{deckId}"` |
| S4 | `PacketArtifactStore.cs:99-100` | `uri.Host.Contains(…) || uri.Host.Contains(…)` | `DeckSourceHost.IsMoxfield(uri) || DeckSourceHost.IsArchidekt(uri)` |

---

## Standard Stack

No new packages. All changes are within existing project structure.

| Component | Purpose | Location |
|-----------|---------|----------|
| `System.Uri` | Host parsing (already used at all 4 sites) | BCL |
| xUnit 2.9.3 | Test framework (already present) | DeckFlow.Core.Tests.csproj |

## Package Legitimacy Audit

No external packages added. N/A.

## Architecture Patterns

### Recommended Project Structure (new file only)

```
DeckFlow.Core/
└── Integration/
    ├── MoxfieldApiUrl.cs      (existing)
    ├── ArchidektApiUrl.cs     (existing)
    └── DeckSourceHost.cs      (NEW — host trust predicates)

DeckFlow.Core.Tests/
├── DeckEntryLoaderTests.cs    (add [Theory] spoof cases)
├── MoxfieldApiDeckImporterTests.cs  (add SC2 forwarding test)
└── DeckSourceHostTests.cs     (NEW — host predicate unit tests)
```

### Pattern: Static Host-Trust Predicate (mirrors existing ApiUrl helpers)

The `MoxfieldApiUrl` and `ArchidektApiUrl` classes are both `public static` with
`TryGetDeckId` + `BuildDeckApiUri`. `DeckSourceHost` follows the same pattern: a
`public static` class with boolean predicate methods that accept a parsed `Uri`.
No construction, no DI, no state. [VERIFIED: read DeckFlow.Core/Integration/MoxfieldApiUrl.cs]

### Anti-Patterns to Avoid

- **Passing `string` instead of `Uri`:** The helper takes `Uri` not `string`. The call
  sites already have a `Uri` object at the check point. Never re-parse a `string` inside
  the helper — that hides the attack surface.
- **Case-sensitive comparison:** Use `StringComparison.OrdinalIgnoreCase` throughout.
  `Uri.Host` is lowercased by .NET but be explicit to prevent future regressions.
- **Adding host check inside the importers:** `MoxfieldApiDeckImporter.ImportAsync`
  and `ArchidektApiDeckImporter.ImportAsync` accept raw `urlOrDeckId` which may be a
  bare deck ID (not a URL). The host gate belongs in `DeckEntryLoader.LoadFromSourceAsync`
  (where the `Uri.TryCreate` guard already exists) and in `PacketArtifactStore`, not
  inside the importers.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| URL parsing | Manual string splits on `://` or `/` | `System.Uri` — already used at all sites |
| Subdomain matching | regex on host | `EndsWith("." + apex)` — simple, correct, no allocation |

## Common Pitfalls

### Pitfall 1: Forgetting the trailing-dot FQDN case
**What goes wrong:** `moxfield.com.` is a syntactically valid DNS FQDN. `Uri.Host`
preserves the trailing dot. `== "moxfield.com"` correctly rejects it. Do not add a
`TrimEnd('.')` step — that would re-open the attack surface for `evilmoxfield.com.`
via confusable domain segments.
**How to avoid:** Write a `[InlineData("https://moxfield.com./decks/x", false)]` test case.

### Pitfall 2: Checking `originalUrl` string in the Spellbook path instead of `deckId`
**What goes wrong:** SC2 requires the canonical URL be built from `deckId` (a parsed,
validated path segment), not reconstructed from `originalUrl`. If the fix tests
`originalUrl` for a safe-to-forward heuristic, a spoof that passes that heuristic still
gets forwarded.
**How to avoid:** Delete the conditional entirely. Always `$"https://moxfield.com/decks/{deckId}"`.

### Pitfall 3: Testing only the happy path in spoof regression
**What goes wrong:** Regression test asserts "spoof URL throws" but doesn't assert that
the importer was NOT called. A future bug could call the importer and then throw — and
the test would still pass.
**How to avoid:** Assert `importer.LastImportWithSourceArgument is null` in addition to
the thrown exception.

### Pitfall 4: PacketArtifactStore uses `uri.Scheme` guard that the spoof must also defeat
**Note:** `PacketArtifactStore.OriginalDeckTextOrNull` already checks
`(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)` at line 97.
This is a pre-existing defense and does NOT need to be changed. The host fix is additive.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` |
| Quick run command | `cd /mnt/c/users/chrislunt/source/personal/deckflow-cycle11 && dotnet test DeckFlow.Core.Tests --filter "DeckSourceHost\|DeckEntryLoader\|MoxfieldApiDeckImporter" -x` |
| Full suite command | `dotnet test DeckFlow.Core.Tests` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SEC-01 | Spoof URLs rejected at LoadFromSourceAsync (Moxfield) | unit `[Theory]` | `dotnet test --filter DeckEntryLoaderTests` | ✅ (add cases) |
| SEC-01 | Spoof URLs rejected at LoadFromSourceAsync (Archidekt) | unit `[Theory]` | `dotnet test --filter DeckEntryLoaderTests` | ✅ (add cases) |
| SEC-01 | DeckSourceHost predicates accept/reject correct hosts | unit `[Theory]` | `dotnet test --filter DeckSourceHostTests` | ❌ Wave 0 |
| SEC-02 | Spellbook fallback always forwards canonical URL | unit `[Fact]` | `dotnet test --filter MoxfieldApiDeckImporterTests` | ✅ (add case) |
| SEC-03 | Substring match reintroduction causes test failure | unit `[Theory]` | above | ✅ / ❌ Wave 0 |

### Wave 0 Gaps

- [ ] `DeckFlow.Core.Tests/DeckSourceHostTests.cs` — covers SEC-01 host predicate matrix
- [ ] Cases added to `DeckEntryLoaderTests.cs` — 3× Moxfield spoofs + 3× Archidekt spoofs
- [ ] Case added to `MoxfieldApiDeckImporterTests.cs` — SC2 canonical URL forwarding

## Security Domain

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | Parse-then-validate (Uri.Host) not validate-then-parse |
| V10 Malicious Code / SSRF | yes | Allowlist over blocklist; exact + subdomain match only |
| V4 Access Control | no | — |

### Threat Pattern for this phase

| Pattern | STRIDE | Mitigation |
|---------|--------|-----------|
| URL spoofing via superstring (`moxfield.com.evil.tld`) | Spoofing | Exact + suffix host check |
| URL spoofing via prefix (`evilmoxfield.com`) | Spoofing | Exact + suffix host check |
| URL spoofing via userinfo (`moxfield.com@evil.tld`) | Spoofing | `Uri.Host` strips userinfo automatically |
| SSRF via Spellbook proxy — forwarding user URL | Elevation of Privilege | Always reconstruct from parsed `deckId` |

## Project Constraints (from CLAUDE.md)

- xUnit (not NUnit) — both test projects use xUnit; no new test frameworks.
- No new NuGet packages without asking — confirmed: zero new packages.
- `{ get; init; }` carve-out — helper uses only methods, no properties; not applicable.
- `.editorconfig` changed-lines gate — only new/changed lines are checked; adding
  `DeckSourceHost.cs` and test cases satisfies this.
- Allman braces throughout C# (open brace on new line).
- File-scoped namespaces (`namespace X;`).
- `sealed` on leaf types — `DeckSourceHost` is `static` (implicitly sealed).
- XML doc comments on every `public` type and method.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `.NET Uri.Host` strips userinfo and lowercases the hostname per RFC 3986 | Edge cases table | If .NET doesn't lowercase, `OrdinalIgnoreCase` still covers it; if it doesn't strip userinfo, `moxfield.com@evil.tld` would yield `moxfield.com` as host and be incorrectly accepted — but this would be a .NET bug, not our bug |
| A2 | `.NET Uri.Host` preserves trailing dot for `moxfield.com.` | Edge cases table | If .NET strips the trailing dot, the FQDN is accepted (safe) |
| A3 | `DeckFlow.Web.Tests` has access to `PacketArtifactStore.OriginalDeckTextOrNull` | S4 test section | If the method is not accessible from tests, a trivial `InternalsVisibleTo` or making it `public` is required |

## Open Questions

1. **ArchidektApiDeckImporter: no test file exists.** There is no
   `ArchidektApiDeckImporterTests.cs` in `DeckFlow.Core.Tests/`. The Archidekt host check
   (S2) is at the `DeckEntryLoader` level, not inside `ArchidektApiDeckImporter.ImportAsync`
   — so the `DeckEntryLoaderTests` spoof cases cover S2 adequately. No Archidekt-importer
   unit tests needed unless the planner decides to add them.

2. **PacketArtifactStore S4 test location.** Need to verify `DeckFlow.Web.Tests` has a
   `PacketArtifactStoreTests.cs` or equivalent. If not, the planner should create one for
   the S4 regression (or test via the calling controller). This is a Wave 0 gap if absent.

## Environment Availability

Step 2.6: SKIPPED (no external dependencies — pure code change, no tools/services needed).

## Sources

### Primary (HIGH confidence — direct codebase reads)
- `DeckFlow.Core/Loading/DeckEntryLoader.cs` — confirmed S1, S2 substring matches
- `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` — confirmed S3, full fallback flow
- `DeckFlow.Core/Integration/MoxfieldApiUrl.cs` — confirmed deckId parsing is safe
- `DeckFlow.Core/Integration/ArchidektApiUrl.cs` — confirmed deckId parsing
- `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` — confirmed S4
- `DeckFlow.Core.Tests/DeckEntryLoaderTests.cs` — confirmed test pattern, `www.moxfield.com` used
- `DeckFlow.Core.Tests/MoxfieldApiDeckImporterTests.cs` — confirmed test pattern
- `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` — confirmed xUnit 2.9.3, no mocking library

### Secondary (MEDIUM confidence)
- Node.js URL parser (same RFC 3986 spec as .NET Uri) — verified host behavior for all
  spoof patterns including userinfo stripping and trailing-dot preservation [ASSUMED for
  exact .NET behavior but risk is low — see Assumptions Log A1/A2]

## Metadata

**Confidence breakdown:**
- Spoofable-site inventory: HIGH — read every .cs file, confirmed 4 sites
- Helper design: HIGH — follows established static class pattern in same namespace
- Edge cases / Uri.Host behavior: MEDIUM — Node URL verified, .NET behavior assumed equal
- Test patterns: HIGH — read both existing test files

**Research date:** 2026-06-21
**Valid until:** 90 days (stable, no third-party dependencies)
