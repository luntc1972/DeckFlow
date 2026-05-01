---
phase: 04-security-bug-fixes
verified: 2026-05-01T19:35:00Z
status: human_needed
score: 11/13 must-haves verified statically; 2 require live UAT after Render deploy
overrides_applied: 0
human_verification:
  - test: "BUG-02 live curl loop — 11 attempts against /Admin/Feedback from one IP"
    expected: "Attempts 1-10 return HTTP 401; attempt 11 returns HTTP 429 with non-empty Retry-After header (1..900 seconds)."
    why_human: "Requires `git push origin main` + ~17 min Render auto-deploy + workstation curl execution against production deckflow.gg. Cannot be exercised against the local WSL build."
  - test: "BUG-02 window-reset check — 15-min wait then single curl"
    expected: "After window expires, single curl returns 401 again (not stuck on 429)."
    why_human: "Real-time wait against live production, cannot be simulated in static verification."
  - test: "BUG-01 Sol Ring browser walk — /suggest-categories mode=ScryfallTagger"
    expected: "/suggest-categories with mode=ScryfallTagger and card=`Sol Ring` returns a non-empty oracle tag list within ~6s; no fallback message; no LogWarning('Tagger has no indexed printing for Sol Ring after 5 probes') in Render logs."
    why_human: "Live integration with Scryfall search + tagger.scryfall.com via Render; cannot be exercised against WSL build."
  - test: "BUG-01 repeat with another cEDH staple (e.g. Counterspell, Mana Crypt, Cyclonic Rift)"
    expected: "Second known staple returns non-empty Tagger tags too; reduces single-card-luck risk."
    why_human: "Live browser walk against production."
  - test: "SC #3 regression matrix — 3a /sync, 3b /chatgpt-packets, 3c /suggest-categories mode=All"
    expected: "All three legacy flows produce same prompt artifacts as pre-deploy; Tagger section in mode=All is now non-empty for Sol Ring."
    why_human: "Visual diff of artifacts vs pre-deploy capture; live deck data; cannot be exercised against WSL build."
---

# Phase 04: Security & Bug Fixes Verification Report

**Phase Goal:** Per-IP rate-limit protects `/Admin/*` from basic-auth brute-force, and Scryfall Tagger lookups either succeed or fall back gracefully instead of returning empty 404 responses — closing the two concrete bugs (BUG-01, BUG-02) that prompted this milestone's quality bar.

**Verified:** 2026-05-01T19:35:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | AdminBruteForceTracker singleton exists with ConcurrentDictionary, 10/15min fixed window, lazy expiry | VERIFIED | `DeckFlow.Web/Infrastructure/AdminBruteForceTracker.cs:9-62` — `BucketEntry` record, `IAdminBruteForceTracker` interface, `AdminBruteForceTracker` sealed class, `PermitLimit = 10`, `Window = TimeSpan.FromMinutes(15)`, `_buckets.AddOrUpdate(...)`, `_buckets.TryRemove(partitionKey, out _);` (lazy expiry) all present and exact |
| 2  | BasicAuthMiddleware throttle gate runs at top of InvokeAsync BEFORE env-var check | VERIFIED | `BasicAuthMiddleware.cs:31-46` — throttle gate (`_tracker.IsThrottled(...)`) runs at lines 33-46 before env-var-missing 503 check at lines 48-56. D-01 invariant honored: 503 path bypasses RecordFailure |
| 3  | Throttled response is HTTP 429 + Retry-After header; does NOT call Challenge (no WWW-Authenticate leakage) | VERIFIED | `BasicAuthMiddleware.cs:43-45` sets `Status429TooManyRequests` + `Headers["Retry-After"]` then `return;` without calling Challenge. Test `ElevenFailedAuthsFromSameIp_TenthReturns401_EleventhReturns429` line 104 asserts `Assert.Empty(lastWwwAuthenticate)` — Pitfall 3 invariant verified in test |
| 4  | Existing per-Challenge `LogWarning("Admin basic-auth challenge issued: ...")` preserved verbatim (D-04) | VERIFIED | `BasicAuthMiddleware.cs:98` — exact literal `_logger.LogWarning("Admin basic-auth challenge issued: {Reason} from {RemoteIp}", reason, remoteIp);` retained byte-for-byte |
| 5  | Counter only increments on Challenge-emitted 401s; success and 503 paths bypass | VERIFIED | `BasicAuthMiddleware.cs:101-103` — `_tracker.RecordFailure(...)` called inside Challenge body only. Success path falls through to `await _next(context)` at line 92 without invoking Challenge. 503 env-missing path returns at line 55 without Challenge |
| 6  | Per-IP isolation, window expiry, retry-after seconds proven by unit tests | VERIFIED | `AdminBruteForceTrackerTests.cs` — 7 tests present: `RecordFailure_TenTimesUnderSameKey_EleventhCheckReturnsThrottled`, `IsThrottled_NinthFailure_StillNotThrottled`, `IsThrottled_DifferentKeys_DoNotInterfere`, `RecordFailure_AfterWindowExpiry_ResetsBucket`, `IsThrottled_ReturnsRemainingSecondsInWindow`, `ElevenFailedAuthsFromSameIp_TenthReturns401_EleventhReturns429`, `SuccessfulAuthDoesNotCountTowardThrottle` |
| 7  | DerivePeerIpKey shared helper extracted; DeriveFeedbackPartitionKey delegates to it | VERIFIED | `Program.cs:354-367` — `DerivePeerIpKey(ctx, prefix)` exists; `DeriveFeedbackPartitionKey => DerivePeerIpKey(ctx, "peer")`; `DeriveAdminPartitionKey => DerivePeerIpKey(ctx, "admin")` mirrors pattern |
| 8  | ScryfallTaggerService.ResolveCardPrintingAsync uses /cards/search?unique=prints + HEAD probes (no order=) | VERIFIED | `ScryfallTaggerService.cs:138-141` — `RestRequest("cards/search", Method.Get)` with `AddQueryParameter("q", $"!\"{cardName}\"")` and `AddQueryParameter("unique", "prints")`. Line 177 — `Method.Head`. `grep -cE 'order=' DeckFlow.Web/Services/ScryfallTaggerService.cs` returns 0 (RESEARCH.md correction to D-10 honored) |
| 9  | MaxProbeAttempts=5 cap; first 200 wins; all-5-404 returns [] + LogWarning | VERIFIED | `ScryfallTaggerService.cs:35` — `MaxProbeAttempts = 5`. Line 170 — `if (probesAttempted >= MaxProbeAttempts) break;`. Line 182-186 — first 200 caches positive + returns. Line 189-191 — all-miss caches negative, emits `LogWarning("Tagger has no indexed printing for {CardName} after {Attempts} probes", ...)` exact template |
| 10 | IMemoryCache 24hr positive / 1hr negative; cache key `tagger-printing:` + CardNormalizer.Normalize | VERIFIED | `ScryfallTaggerService.cs:36-37` — `PositiveCacheDuration = 24h`, `NegativeCacheDuration = 1h`. Line 131 — `$"tagger-printing:{CardNormalizer.Normalize(cardName)}"`. Line 184 positive Set 24h; lines 151, 161, 189 negative Set 1h |
| 11 | ScryfallTaggerService DI converted to factory closure resolving IMemoryCache | VERIFIED | `Program.cs:202-209` — `AddSingleton<IScryfallTaggerService>(sp => new ScryfallTaggerService(...))` factory closure resolving `sp.GetRequiredService<IMemoryCache>()` as 5th positional arg, mirrors CommanderSpellbookService shape (lines 189-194) |
| 12 | All ScryfallTaggerServiceTests compile + 5 new printings-iteration tests added | VERIFIED | `ScryfallTaggerServiceTests.cs` — `CreateService` helper at lines 49-73 takes new optional `IMemoryCache? memoryCache = null` and passes `printingCache` as 5th arg. 5 new tests at lines 230-374: `ColdLookup_ThirdPrintingHits_ReturnsTaggerData`, `AllFiveProbes404_ReturnsEmpty`, `PositiveCacheHit_SkipsScryfall`, `NegativeCacheHit_ReturnsEmptyWithNoUpstream`, `ScryfallSearchEmptyData_ReturnsEmpty` |
| 13 | dotnet build DeckFlow.sln clean (0 errors, 0 warnings) | VERIFIED | Build run at 2026-05-01T19:35Z: `Build succeeded. 0 Warning(s) 0 Error(s) Time Elapsed 00:00:28.26`. All 5 projects (Core, CLI, Web, Core.Tests, Web.Tests) compile clean. Browser-extension zip target ran successfully |

**Score:** 13/13 truths verified statically. Behavioral runtime verification (live HTTP against deckflow.gg) deferred to human UAT — see human_verification frontmatter (5 items).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Web/Infrastructure/AdminBruteForceTracker.cs` | New file: interface + record + sealed class | VERIFIED | 62 lines; all required identifiers present (`public sealed record BucketEntry`, `public interface IAdminBruteForceTracker`, `public sealed class AdminBruteForceTracker : IAdminBruteForceTracker`, `ConcurrentDictionary<string, BucketEntry>`, `_buckets.AddOrUpdate`, `_buckets.TryRemove`) |
| `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` | Modified: 4-arg ctor, throttle gate, RecordFailure in Challenge | VERIFIED | Ctor takes 4 args incl. `IAdminBruteForceTracker tracker`; `_tracker.IsThrottled` at line 36; `_tracker.RecordFailure` at line 103; `Status429TooManyRequests` line 43; `Retry-After` header line 44; existing warn log preserved verbatim line 98 |
| `DeckFlow.Web/Program.cs` | Modified: DI singleton + DerivePeerIpKey helper + Tagger factory closure | VERIFIED | Line 110 `AddSingleton<IAdminBruteForceTracker, AdminBruteForceTracker>()`; lines 354-367 helper hierarchy `DerivePeerIpKey`/`DeriveFeedbackPartitionKey`/`DeriveAdminPartitionKey`; lines 202-209 ScryfallTaggerService factory closure with IMemoryCache |
| `DeckFlow.Web/Services/ScryfallTaggerService.cs` | Modified: ResolveCardPrintingAsync iterate-printings + IMemoryCache | VERIFIED | 5-arg ctor incl. `IMemoryCache memoryCache` (5th positional, before optional ILogger); `MaxProbeAttempts=5`, `PositiveCacheDuration=24h`, `NegativeCacheDuration=1h` constants; `cards/search` + `unique=prints` + `Method.Head`; cache key `tagger-printing:` + `CardNormalizer.Normalize`; warn template `"Tagger has no indexed printing for {CardName} after {Attempts} probes"`; **no `order=` parameter** |
| `DeckFlow.Web.Tests/Security/AdminBruteForceTrackerTests.cs` | New file: 7 tests + EnvScope helper | VERIFIED | All 7 expected test method names present; uses `IPAddress.Parse("10.20.30.40")`, `Status429TooManyRequests`, `Assert.NotEmpty(lastRetryAfter)`, `Assert.Empty(lastWwwAuthenticate)`. Local EnvScope IDisposable helper class at lines 133-163 |
| `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` | Modified: CreateService 5-arg + 5 new tests | VERIFIED | CreateService at lines 49-73 takes optional `IMemoryCache? memoryCache = null`; new fixture `ScryfallSearchJson3Printings`/`5Printings`/`Empty`; 5 new test methods present; `HttpMethod.Head` registrations; literal `"tagger-printing:sol ring"` in cache pre-populate |
| `DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs` | Modified: existing tests pass new ctor arg | VERIFIED | All 5 `new BasicAuthMiddleware(...)` call sites updated to pass `new AdminBruteForceTracker()` as 4th arg (lines 21-25, 37-41, 56-60, 74-78, 93-97) |
| `README.md` | Modified: BUG-02 admin throttle blurb | VERIFIED | Line 28 contains BUG-02 blurb with "10 attempts per 15-minute window", "HTTP 429", "Retry-After header" — matches plan template exactly |
| `.planning/phases/04-security-bug-fixes/04-HUMAN-UAT.md` | New: scaffold for live UAT entries | VERIFIED | File exists with frontmatter (status: partial), 5 Test sections (1, 2, 3a, 3b, 3c) all `result: pending`, curl recipes verbatim, Sol Ring/`/suggest-categories`/`/sync`/`/chatgpt-packets` literals all present |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `BasicAuthMiddleware.InvokeAsync` | `AdminBruteForceTracker.IsThrottled` | `_tracker.IsThrottled` injected ctor field; called BEFORE env-var check | WIRED | Line 36 of BasicAuthMiddleware.cs; runs at top of InvokeAsync (lines 31-46) before env-var-missing check (lines 48-56) — D-01 invariant: 503 path doesn't increment counter |
| `BasicAuthMiddleware.Challenge` | `AdminBruteForceTracker.RecordFailure` | called AFTER 401 emitted on every Challenge call | WIRED | Line 103 of BasicAuthMiddleware.cs; called inside Challenge body after WWW-Authenticate header set; preserves D-04 (warn log verbatim) |
| `Program.cs DeriveFeedbackPartitionKey` | `DerivePeerIpKey` shared helper | extracted internal static helper | WIRED | Line 360-361 — `DeriveFeedbackPartitionKey => DerivePeerIpKey(context, "peer")`; existing test `DeriveFeedbackPartitionKey_IgnoresForwardedForHeader` still compiles |
| `ScryfallTaggerService.ResolveCardPrintingAsync` | Scryfall `/cards/search` | RestSharp + scryfall pipeline + ScryfallThrottle | WIRED | Lines 138-147 — `RestRequest("cards/search", Method.Get)` + `AddQueryParameter("unique", "prints")` + `_scryfallPipeline.ExecuteAsync` wrapped in `ScryfallThrottle.ExecuteAsync` |
| ResolveCardPrintingAsync probe loop | `tagger.scryfall.com/card/{set}/{number}` | RestSharp HEAD + tagger pipeline | WIRED | Lines 166-187 — `taggerRestClient` with `RestRequest($"card/{set}/{number}", Method.Head)` + `_taggerPipeline.ExecuteAsync` |
| ResolveCardPrintingAsync | `IMemoryCache` | `tagger-printing:` cache key suffix `CardNormalizer.Normalize(cardName)` | WIRED | Line 131 cache key construction; lines 133, 151, 161, 184, 189 — full TryGetValue/Set lifecycle for both positive (24h tuple) and negative (null sentinel 1h) paths |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `AdminBruteForceTracker._buckets` | per-IP failure count | Mutated by `RecordFailure` from middleware Challenge() (real `HttpContext.Connection.RemoteIpAddress`) | Yes — middleware writes real per-request IP | FLOWING |
| `BasicAuthMiddleware` 429 response | `retryAfter` int | Computed in `IsThrottled` from `Window - (now - WindowStart)` real-clock delta | Yes — non-zero seconds in 1..900 enforced (test `RecordFailure_TenTimesUnderSameKey_EleventhCheckReturnsThrottled` asserts `InRange(retryAfter, 1, 900)`) | FLOWING |
| `ScryfallTaggerService.ResolveCardPrintingAsync` | `(set, number)` tuple | Live Scryfall `cards/search?q=!"<name>"&unique=prints` JSON `data[]` array, iterated with HEAD probes against tagger.scryfall.com | Yes — RestSharp client uses real `IScryfallRestClientFactory` + tagger HttpClient (`_taggerHttpClient.Inner`); first 200 wins | FLOWING (static); live data flow gated on Render deploy |
| `IMemoryCache` printing cache | `((string, string)?)` tuple/null | Populated by ResolveCardPrintingAsync + read on subsequent calls; key includes normalized card name | Yes — test `PositiveCacheHit_SkipsScryfall` proves cache short-circuits Scryfall (GetMatchCount==0) | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Solution builds clean (0 errors, 0 warnings) | `dotnet build DeckFlow.sln -m:1 -p:BuildInParallel=false` | `Build succeeded. 0 Warning(s) 0 Error(s)` (00:00:28) | PASS |
| All 5 projects assemble (Core, CLI, Web, Core.Tests, Web.Tests) | (see build output) | All 5 dlls produced; browser-extension zip target ran | PASS |
| AdminBruteForceTracker class shape verifiable | `grep -c "public sealed class AdminBruteForceTracker"` | 1 | PASS |
| BasicAuthMiddleware throttle gate present | `grep -c "_tracker.IsThrottled"` | 1 (line 36) | PASS |
| BasicAuthMiddleware RecordFailure call present | `grep -c "_tracker.RecordFailure"` | 1 (line 103) | PASS |
| ScryfallTaggerService no `order=` parameter | `grep -cE 'order=' DeckFlow.Web/Services/ScryfallTaggerService.cs` | 0 (RESEARCH.md correction to D-10 honored) | PASS |
| ScryfallTaggerService cache key shape | `grep -c "tagger-printing:"` | matches in service + tests | PASS |
| README BUG-02 blurb present | `grep -c "BUG-02"` README.md | 1 (line 28) + 2 in unrelated context | PASS |
| 11-attempt curl loop on production | `curl ... https://www.deckflow.gg/Admin/Feedback x11` | NOT RUN (live UAT — gated on git push + Render deploy) | SKIP — routed to human |
| Sol Ring browser walk on /suggest-categories ScryfallTagger | Browser GET on production | NOT RUN (live UAT) | SKIP — routed to human |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| BUG-01 | 04-02-PLAN | Fix Scryfall Tagger 404 — investigate the deck-tagger refresh path that returns 404 for some deck IDs and either correct the URL pattern or fall back gracefully | SATISFIED (static) | ResolveCardPrintingAsync iterate-printings + IMemoryCache lands the fix; first-printing 404 no longer silently fails (sweeps up to 5 printings, caches winner). Graceful fallback path: all-5-probes-404 returns [] + LogWarning. Live behavioral confirmation pending UAT (human_verification items 3-4) |
| BUG-02 | 04-01-PLAN | Per-IP rate-limit on `/Admin/*` routes — add ASP.NET Core rate limiting middleware to throttle basic-auth brute-force attempts | SATISFIED (static) | AdminBruteForceTracker singleton + BasicAuthMiddleware throttle gate land the fix. 11th-attempt 429 + Retry-After + per-IP isolation + lazy expiry all proven in unit/integration tests. Live curl-loop confirmation pending UAT (human_verification items 1-2) |

No orphaned requirements: REQUIREMENTS.md maps BUG-01, BUG-02 → Phase 4 (lines 130-131); both claimed by plan frontmatter (`requirements: [BUG-01]`, `requirements: [BUG-02]`).

### Anti-Patterns Found

Files modified in phase: `AdminBruteForceTracker.cs` (NEW), `BasicAuthMiddleware.cs` (modified), `Program.cs` (modified), `ScryfallTaggerService.cs` (modified), `AdminBruteForceTrackerTests.cs` (NEW), `ScryfallTaggerServiceTests.cs` (modified), `BasicAuthMiddlewareTests.cs` (modified), `README.md` (modified).

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | No TODO/FIXME/PLACEHOLDER comments in any phase-modified file | — | — |
| (none) | — | No empty implementations (no `return null;`/`return [];` stubs that bypass logic) | — | — |
| (none) | — | No console.log-only methods | — | — |
| (none) | — | No hardcoded empty data flowing to user output | — | — |

Empty-array/list returns observed in `ScryfallTaggerService.cs` (lines 153, 163, 191) are the **intended graceful-fallback path** (per success criterion SC #2): when Tagger has no indexed printing, return `(string.Empty, string.Empty)` which the caller treats as "no tags found". Not stubs — these are the documented degradation contract that closes BUG-01.

Empty-list returns in `RefreshSessionAndRetryAsync` (lines 310, 321, 331) are the documented HIGH-1 degradation path on persistent 403 — pre-existing, untouched by this phase.

### Human Verification Required

5 items pending live UAT after `git push origin main` + Render auto-deploy (~17 min). All items are documented as `result: pending` in `04-HUMAN-UAT.md` with verbatim curl recipes / browser walk procedures.

#### 1. BUG-02 11-attempt curl loop (SC #1)
**Test:** From a workstation (not Render-internal), run the loop:
```bash
for i in $(seq 1 11); do
  curl -sS -o /dev/null \
    -w "attempt=%{http_code} retry=%header{retry-after}\n" \
    -u admin:WRONGPASSWORD \
    https://www.deckflow.gg/Admin/Feedback
done | tee /tmp/04-01-prod-curl.log
```
**Expected:** Attempts 1–10 return HTTP 401; attempt 11 returns HTTP 429 with non-empty `retry=` value (1..900 seconds).
**Why human:** Requires production Render deployment + workstation egress; static verification cannot exercise the live ASP.NET Core middleware pipeline against deckflow.gg.

#### 2. BUG-02 window-reset check
**Test:** Wait 15 minutes (or trigger a benign Render redeploy), then run a single curl with wrong password.
**Expected:** Returns 401 again (not stuck on 429) — proves window expires correctly under real clock.
**Why human:** Real-time wait, not simulatable.

#### 3. BUG-01 Sol Ring browser walk (SC #2)
**Test:** Visit https://www.deckflow.gg/suggest-categories, set mode = `ScryfallTagger`, card = `Sol Ring`, submit.
**Expected:** Non-empty oracle tag list within ~6s (e.g. tags like "Ramp", "Mana Rock"). No fallback message. Render logs do NOT contain `LogWarning("Tagger has no indexed printing for Sol Ring after 5 probes")`.
**Why human:** Live integration with Scryfall search + tagger.scryfall.com; visual rendered output verification.

#### 4. BUG-01 repeat with another cEDH staple
**Test:** Repeat #3 with a different known staple (Counterspell, Mana Crypt, or Cyclonic Rift).
**Expected:** Also returns non-empty real Tagger tags.
**Why human:** Reduces single-card-luck risk; live walk only.

#### 5. SC #3 regression matrix
**Test 5a (`/sync`):** Submit two known Moxfield deck URLs → DeckSync diff renders unchanged.
**Test 5b (`/chatgpt-packets`):** Submit a known deck → ChatGPT-paste artifact renders with intact header + per-card sections.
**Test 5c (`/suggest-categories` mode=All):** Card = Sol Ring → Cached + EDHREC + Tagger sections all render; Tagger section is now non-empty (BUG-01 win surfacing in aggregator).
**Why human:** Core value pipeline regression check requires visual diff vs pre-deploy artifact; only verifiable by walking the live site.

### Gaps Summary

**No code-level gaps found.** All 13 observable truths are statically VERIFIED. All 9 required artifacts exist and contain the exact identifiers/literals specified by the plan must_haves. All 6 key links are wired with grep-confirmed code patterns. `dotnet build DeckFlow.sln` is clean (0 errors, 0 warnings).

**The reason status is `human_needed`, not `passed`:** SC #1 (per-IP throttle proves on live curl), SC #2 (Sol Ring returns real Tagger data on live browser walk), and SC #3 (regression matrix on live deckflow.gg) all require execution against the deployed app on Render. The phase plans explicitly mark Task 4 (04-01) and Task 3 (04-02) as `checkpoint:human-verify gate="blocking"` — those are exactly the live UAT items routed to human_verification above.

**Next action:** `git push origin main` to trigger Render auto-deploy, then execute the 5 human_verification items in order. Update `04-HUMAN-UAT.md` with PASS evidence (curl log paste + screenshot/paste of Sol Ring tags + SC #3 artifact spot-checks). Re-run verification when UAT records all PASS.

**Build evidence (fresh):**
```
dotnet build DeckFlow.sln -m:1 -p:BuildInParallel=false
  DeckFlow.Core -> .../DeckFlow.Core.dll
  DeckFlow.CLI -> .../DeckFlow.CLI.dll
  DeckFlow.Web -> .../DeckFlow.Web.dll
  DeckFlow.Core.Tests -> .../DeckFlow.Core.Tests.dll
  DeckFlow.Web.Tests -> .../DeckFlow.Web.Tests.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:28.26
```

**Commit chain on main (8 phase commits):**
```
818dc05 docs(04-02): plan summary + scaffold combined HUMAN-UAT for BUG-01 + BUG-02 + SC#3
4585269 test(04-02): cover ResolveCardPrintingAsync iterate-printings + cache paths (BUG-01)
06ea1a7 feat(04-02): replace ResolveCardPrintingAsync with iterate-printings + IMemoryCache (BUG-01)
de0e6e7 docs(04-01): plan summary — admin brute-force throttle (BUG-02)
aed9ead docs(04-01): note admin throttle in README (BUG-02)
7e08d8c test(04-01): cover AdminBruteForceTracker + middleware throttle integration
50849e9 feat(04-01): wire AdminBruteForceTracker into BasicAuthMiddleware (BUG-02)
7b3c1d6 feat(04-01): add AdminBruteForceTracker singleton + extract DerivePeerIpKey helper (BUG-02)
```

All commits use plain default-author (no Co-Authored-By trailer per PROJECT.md).

### Threat Model Verification

Plan 04-01 declared T-04-01 through T-04-04; plan 04-02 declared T-04-05 through T-04-07. All "mitigate"-disposition threats have observable code-level mitigations:

| Threat | Disposition | Mitigation Verified In Code |
|--------|-------------|------------------------------|
| T-04-01 (Spoofing/Denial — basic-auth brute-force) | mitigate | AdminBruteForceTracker 10/15min throttle wired into BasicAuthMiddleware InvokeAsync gate |
| T-04-02 (Spoofing — X-Forwarded-For partition rotation) | mitigate | `Connection.RemoteIpAddress` direct read at BasicAuthMiddleware.cs:34 — Path B-rawpeer per Phase 03 TD-04 |
| T-04-03 (Repudiation — throttle masking forensics) | mitigate | Existing `LogWarning("Admin basic-auth challenge issued: ...")` preserved at line 98; throttled path also emits `LogWarning("Admin basic-auth throttled: ...")` at line 40-42 |
| T-04-04 (DoS — throttle dict memory growth) | accept | Lazy expiry on dict access (TryRemove at line 40 of AdminBruteForceTracker.cs); no timer; bounded by active-IPs-in-15min |
| T-04-05 (Tampering — Scryfall search response parsing) | mitigate | RestSharp + JsonDocument typed parse with null/empty guards at ScryfallTaggerService.cs:156-164, 172-174; strings used only to construct probe URL |
| T-04-06 (DoS — Tagger probe loop runaway) | mitigate | `MaxProbeAttempts=5` hard cap at line 35; HEAD method skips body; existing tagger pipeline timeouts apply |
| T-04-07 (Information Disclosure — cache key collisions) | accept | CardNormalizer.Normalize is deterministic; cache holds only (set, collector_number) tuples — no PII |

---

_Verified: 2026-05-01T19:35:00Z_
_Verifier: Claude (gsd-verifier)_
