---
phase: 03-tech-debt-cleanup
verified: 2026-05-01T09:06:00-06:00
status: human_needed
score: 4/4 must-haves verified locally; SC #4 awaits post-deploy spoof curl
overrides_applied: 0
re_verification: null
human_verification:
  - test: "Spoofed X-Forwarded-For curl loop against /feedback/submit on live deckflow.gg"
    expected: "After 5 successful POSTs from same upstream peer, server returns 429 even when X-Forwarded-For is rotated each request"
    why_human: "Requires push to origin/main → Render auto-deploy → live HTTP probe; cannot be exercised locally because the partition key uses the immediate-peer IP and Kestrel-on-localhost dev is single-peer by definition"
---

# Phase 03: Tech-Debt Cleanup — Verification Report

**Phase Goal (ROADMAP §Phase 3):** Move test-only types out of production assembly, standardize service constructors, stop tracking generated JS, tighten trusted forwarded-headers surface — close 4 CONCERNS items (TD-01..TD-04) without UI surface impact.

**Verified:** 2026-05-01 09:06 MDT
**Status:** human_needed (codebase-verifiable assertions all PASS; SC #4 has one residual post-deploy curl spoof test the user must run after `git push`)
**Re-verification:** No — initial verification

## Goal Achievement

### Success Criteria (ROADMAP source of truth)

| #   | Success Criterion                                                                       | Status        | Evidence                                                                                                                                                                                                                            |
| --- | --------------------------------------------------------------------------------------- | ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | TD-01: `NullHttpClientFactory` + `NullScryfallRestClientFactory` no longer exist; production assembly's public surface no longer exposes test-only types | VERIFIED       | `ls DeckFlow.Web/Services/Http/` returns only `ResiliencePipelineFactory.cs`. `grep -rn "Null{Http,Scryfall}*Factory" DeckFlow.Web DeckFlow.Web.Tests DeckFlow.Core DeckFlow.Core.Tests DeckFlow.CLI --include="*.cs"` → zero hits. (Hits in `.claude/worktrees/agent-…` are a transient agent worktree, not production source.) |
| 2   | TD-02: Each of 10 affected services exposes exactly one constructor; test-compat seam removed | VERIFIED       | All 10 services show exactly 1 ctor each, all marked `internal`: `CardLookupService:53`, `CardSearchService:37`, `ScryfallSetService:36`, `ScryfallCommanderSearchService:34`, `CommanderBanListService:36`, `CommanderSpellbookService:67`, `DeckConvertService:39`, `ChatGptDeckPacketService:72`, `ChatGptDeckComparisonService:52`, `ChatGptCedhMetaGapService:50`. `TestServiceFactory.cs` exists at `DeckFlow.Web.Tests/TestDoubles/` with 10 `Create*Service` methods (lines 18–157, namespace `DeckFlow.Web.Tests`, `internal static class`). 34 call-sites across 11 test files migrated to `TestServiceFactory.Create*`. Zero remaining `new Scryfall...Service(` constructor calls in test code. `Program.cs:167-247` uses factory-delegate DI for all 10 services. |
| 3   | TD-03: Generated `wwwroot/js/*.js` no longer tracked; `.gitignore` excludes them; tsc still emits at build; deployed site loads JS correctly | VERIFIED (codebase) / human-spot-needed for deploy parity | `git ls-files DeckFlow.Web/wwwroot/ | grep "\.js$"` returns zero hits under `wwwroot/js/` (only `wwwroot/lib/` vendor JS, expected). `.gitignore:13` contains `DeckFlow.Web/wwwroot/js/*.js`. 10 `.js` files exist on disk after `dotnet build` (build emits via `CompileTypeScriptAssets`). README §"Local development TypeScript toolchain" present. (Live-site parity will be confirmed implicitly when push triggers Render redeploy + user opens the site.) |
| 4   | TD-04: Forwarded-headers surface tightened; non-Render upstream cannot spoof X-Forwarded-For to dodge the feedback rate limiter | VERIFIED (code) / **HUMAN-NEEDED (post-deploy spoof curl)** | `Program.cs:121-134` removed the `KnownIPNetworks.Clear() / KnownProxies.Clear()` calls and the misleading "can't enumerate" comment; new comment cites Render docs URLs + retrieval date 2026-04-30. `Program.cs:140` rate-limit policy uses `DeriveFeedbackPartitionKey(httpContext)`. `Program.cs:349-350`: `internal static string DeriveFeedbackPartitionKey` returns `"peer:" + Connection.RemoteIpAddress` — bypasses forwarded-header trust entirely (Path B-rawpeer per orchestrator). `ForwardedHeadersOptionsTests.cs` has 2 `[Fact]` tests asserting forged `X-Forwarded-For` does NOT appear in the partition key and that the immediate-peer IP DOES. `UseForwardedHeaders()` ordering preserved (`Program.cs:259`). README §"Feedback rate-limit identity" documents the disposition. The remaining live spoof curl against `https://www.deckflow.gg/feedback/submit` is the human-verification gate. |

### Per-Plan Must-Haves (all green summary)

| Plan    | Requirement | Truths verified | Artifacts verified | Key links verified |
| ------- | ----------- | --------------- | ------------------ | ------------------ |
| 03-01   | TD-02       | 7/7             | 4/4                | 2/2                |
| 03-02   | TD-01       | 7/7             | 1/1                | 1/1                |
| 03-03   | TD-03       | 7/7             | 2/2                | 2/2                |
| 03-04   | TD-04       | 11/12 (locally); SC-bound spoof curl deferred to live | 3/3 | 2/2 |

### Build Gate

`dotnet build DeckFlow.sln` → **Build succeeded. 0 Warning(s). 0 Error(s).** (Time elapsed 00:01:51.)

Per CLAUDE.md WSL constraint, `dotnet test` was NOT executed locally; it is unreliable in WSL. Test-suite gating is push-and-watch CI. The two new `[Fact]` tests in `ForwardedHeadersOptionsTests.cs` will execute in CI on the same push that triggers the Render deploy.

### Commits Ahead of `origin/main`

13 commits (`git log --oneline origin/main..HEAD`):

1. `4f20e16` tech-debt(03-03): untrack wwwroot/js + .gitignore + README
2. `cef06e1` docs(03-03): summary
3. `b9c0e38` tech-debt(03-01): collapse 10 service ctors + Program.cs DI
4. `49d6e03` tech-debt(03-01): add TestServiceFactory
5. `ae63282` tech-debt(03-01): migrate 11 test files
6. `7997628` docs(03-01): summary
7. `70e01d2` tech-debt(03-04): partition feedback rate-limit by peer IP
8. `87fa828` docs(03-04): summary
9. `0949b23` tech-debt(03-02): delete Null* orphans
10. `d5a5c3c` docs(03-02): summary

(Plus the 3 earlier docs commits already at HEAD: `41cd807` plan-set, `e749464` state, `3b13549` context — 13 total ahead.)

### Cross-Phase Regression Spot-Check

- Phase 02 layout/CSS work untouched: `.gitignore:12` still contains `DeckFlow.Web/wwwroot/extensions/*.zip` (Phase 02 extensions zip glob).
- Test-side migration to `TestServiceFactory` did not regress any existing test surface — `dotnet build` clean across all 5 projects (`DeckFlow.Core`, `DeckFlow.Core.Tests`, `DeckFlow.Web`, `DeckFlow.Web.Tests`, `DeckFlow.CLI`).

### Anti-Patterns Found

None. Zero `KnownIPNetworks.Clear`, zero `cannot enumerate`/`can't enumerate` strings, zero placeholder `X.X.X.X` / `<RENDER_DOC_URL_FROM_TASK_1>` / `<PASTE EXACT...>` strings, zero stub Null factories.

### Human Verification Required

#### 1. Live spoofed X-Forwarded-For rate-limit smoke test (SC #4 closure)

- **Test:** After `git push origin main` triggers Render redeploy, run `for i in $(seq 1 7); do curl -s -o /dev/null -w "%{http_code}\n" -X POST -H "X-Forwarded-For: $((RANDOM % 256)).$((RANDOM % 256)).$((RANDOM % 256)).$((RANDOM % 256))" -H "Content-Type: application/x-www-form-urlencoded" --data "Message=test$i&__RequestVerificationToken=…" https://www.deckflow.gg/feedback/submit; done` (or follow exact procedure in `03-04-PLAN.md` Task 3 §how-to-verify steps 3-5).
- **Expected:** Within the 7 attempts, at least one response is HTTP 429 — proving the rate limiter partitions on the unspoofable peer IP (Render edge), not the rotated `X-Forwarded-For`.
- **Why human:** The partition key is the immediate TCP peer IP. On localhost dev that's always 127.0.0.1; only the live Render-fronted environment exercises the Render-edge → app peer relationship. Cannot be reproduced locally.

### Gaps Summary

No codebase gaps. All 10 plans-derived artifacts and key links are present, wired, and substantive. Build is clean. The lone outstanding item is the post-deploy live-spoof curl that proves SC #4's threat scenario fails — this is gated on `git push` and Render's redeploy cycle, not on any code state.

---

## Recommended Next Action

1. `git push origin main` → 13 commits land at `origin/main`
2. Wait for Render auto-deploy to complete (typically 3–5 minutes)
3. User runs `03-04-PLAN.md` Task 3 §how-to-verify steps 3–5 (the curl spoof loop)
4. Observe at least one 429 response across the 5+ attempts
5. User replies "approved" to mark SC #4 verified live
6. Phase 3 marked complete in ROADMAP §Progress; advance to Phase 4 planning

---

_Verified: 2026-05-01T09:06:00-06:00_
_Verifier: Claude (gsd-verifier)_
