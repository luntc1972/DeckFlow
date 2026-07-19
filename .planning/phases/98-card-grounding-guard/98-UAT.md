---
status: diagnosed
phase: 98-card-grounding-guard
source: [98-01-SUMMARY.md, 98-02-SUMMARY.md, 98-03-SUMMARY.md, 98-04-SUMMARY.md]
started: 2026-07-19T01:05:00Z
updated: 2026-07-19T01:35:00Z
---

## Current Test
<!-- OVERWRITE each test - shows where we are -->

number: 4
name: complete
expected: all tests processed
awaiting: gap-fix execution

## Tests

### 1. Cold Start Smoke Test
expected: Kill any running server. Fresh boot via scripts/run-web-test.sh: server boots without errors, homepage 200, creator-deck-cache.db schema creation does not fault startup, no DI resolution failures.
result: issue
severity: major
reported: |
  Dev cold start CRASHES at Program.cs:200 Build() — Development ValidateOnBuild rejects
  3 scoped registrations with unregistered ctor deps: CreatorProfileDeckCrawler +
  MeasuredStyleProfileBuilder (missing ICreatorProfileSourceStore) and
  CreatorDeckCategoryResolver (missing CategoryKnowledgeRepository).
  PRE-EXISTING: identical at baseline 675b1cf6 (P95 registered the trio at Program.cs:192-194
  without ever registering their stores; only ever constructed in tests). NOT a phase-98
  regression — 98-03 already fixed the third such gap (ICreatorDeckCacheStore).
  Dev-only: ValidateOnBuild is off outside Development, so Production/Render unaffected.
  Note: an initial 200 response was a false positive from a CONCURRENT session's
  main-worktree server (PID 57952, C:\...\personal\deckflow\...) sharing port 5173.

### 2. Card-grounding test suites green
expected: All four new suites pass at HEAD — Core CardGrounding rules 26, Web CardGroundingGuard 13, CreatorWhitelistPool 6 (incl. ValidateOnBuild DI resolution), Hallucination fixtures 7 — plus regressions (ScryfallCardNameGrounder 4, DiComposition canary 1).
result: pass
evidence: Web combined filter 31/31; Core CardGrounding 26/26 (fresh runs at HEAD 2c0b12fe).

### 3. Live Scryfall API contract still matches research
expected: Real api.scryfall.com probes reproduce the implementation's assumptions — fuzzy 404 ambiguous body carries type:"ambiguous", plain miss carries code:"not_found" without type, and a real card's legalities object has lowercase commander key/value.
result: pass
evidence: aust+com => code:"not_found"+type:"ambiguous"; zzzznotacardzzz => code:"not_found" no type; Lightning Bolt legalities.commander="legal" color_identity=["R"]; Dockside Extortionist legalities.commander="banned" (live 2026-07-19).

### 4. Hallucination rejection behavior
expected: Fixture run shows exact reject reasons — fake NotFound, Dockside NotLegal, off-identity IdentityViolation, duplicate SingletonDuplicate, ambiguous Ambiguous, typo heals Accepted.
result: pass
evidence: 7/7 named results — PlausibleFakeName=>NotFound, Dockside=>NotLegal, Counterspell(off-identity)=>IdentityViolation, sol-ring=>SingletonDuplicate canonical "Sol Ring", AmbiguousFuzzy404=>Ambiguous, "Dockside Extortonist"=>Accepted canonical, Forest basic-land=>Accepted.

## Summary

total: 4
passed: 3
issues: 1
pending: 0
skipped: 0

## Gaps

### GAP-98-01: Dev cold start broken — P95 creator-style DI trio missing store registrations (major, pre-existing)
- Root cause: Program.cs:192-194 (P95) registers CreatorProfileDeckCrawler, CreatorDeckCategoryResolver, MeasuredStyleProfileBuilder as scoped services, but ICreatorProfileSourceStore and CategoryKnowledgeRepository were never registered in Web DI. Development's default ValidateOnBuild crashes the boot; Production is not affected (no validation, services unresolved-only-if-used).
- Fix plan: 98-05-PLAN.md (gap_closure: true) — register both deps in Program.cs beside the ICreatorDeckCacheStore registration; regression-lock with a full-Program DI smoke test or documented cold-start check.
- Route: /gsd-execute-phase 98 --gaps-only
