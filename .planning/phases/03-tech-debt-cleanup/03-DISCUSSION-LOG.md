# Phase 03: Tech-Debt Cleanup - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-30
**Phase:** 03-tech-debt-cleanup
**Areas discussed:** TD-01 strategy, TD-01 sequence, TD-02 scope, TD-02 mechanism, TD-03 bootstrap, TD-03 gitignore, TD-04 CIDR source, TD-04 environment scope, plan grouping

---

## Phase plan grouping

| Option | Description | Selected |
|--------|-------------|----------|
| One plan per requirement (4 plans) | 03-01 = TD-01, 03-02 = TD-02, 03-03 = TD-03, 03-04 = TD-04. Atomic commits, small revert blast radius. | ✓ |
| Group by concern (2-3 plans) | 03-01 = TD-01 + TD-02 bundled, 03-02 = TD-03, 03-03 = TD-04. | |
| Single plan covering all 4 | One plan does everything. | |

**User's choice:** One plan per requirement (4 plans).
**Notes:** Plan ordering refined later — 03-01 = TD-02 (collapse ctors first), 03-02 = TD-01 (delete orphans), 03-03 = TD-03, 03-04 = TD-04.

---

## TD-01: Null vs Fake reconciliation strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Delete outright when TD-02 collapses ctors | Null* factories have zero references in test tree; they're orphans after TD-02. Delete the files; don't migrate. | ✓ |
| Move literally to TestDoubles/ and migrate callers | Move + namespace rename + update 9 service test ctors via InternalsVisibleTo. | |
| Replace with Fake* equivalents at call sites | Add no-arg overloads to Fake*; consolidate two parallel families. | |

**User's choice:** Delete outright when TD-02 collapses ctors (Recommended).
**Notes:** Coupling between TD-01 and TD-02 surfaced during scout: Null* factories are referenced ONLY by the internal test-compat ctor defaults of 9 production services (zero hits in DeckFlow.Web.Tests/). Once TD-02 lands the collapse, the deletes are mechanical.

---

## TD-01: Sequencing relative to TD-02

| Option | Description | Selected |
|--------|-------------|----------|
| TD-02 first, TD-01 as verification step within or after | Plan 03-01 = TD-02 (collapse), Plan 03-02 = TD-01 (verify zero refs, delete files). | ✓ |
| TD-01 first as a refactor, TD-02 follows | Forces TD-01 to do work TD-02 will undo; only sensible if Null* survives. | |
| Bundle TD-01 + TD-02 into a single plan | One plan does both. Overrides "one plan per requirement." | |

**User's choice:** TD-02 first, TD-01 as a verification step within or after (Recommended).
**Notes:** Sequence locked: 03-01 = TD-02, 03-02 = TD-01.

---

## TD-02: Scope of "one constructor per service"

| Option | Description | Selected |
|--------|-------------|----------|
| Test-compat seams only (10 services) | Collapse 10 services with public DI ctor + internal test ctor. Leave HelpContentService/VersionService/FeedbackStore convenience overloads alone. | ✓ |
| All multi-ctor services (13 total) | Strict literal reading: every service exactly one ctor. HelpContentService keeps (string), DI ext does Path.Combine; VersionService keeps (Assembly); FeedbackStore keeps (RelationalDatabaseConnection). | |
| Test-seams + FeedbackStore only (11 services) | Collapse worst-case 3-ctor FeedbackStore plus the 10 test seams. | |

**User's choice:** Test-compat seams only (10 services) (Recommended).
**Notes:** ROADMAP wording "test-compat ctor → test-helper factory" explicitly scopes the rule to the test-seam pattern. Convenience overloads aren't test seams.

---

## TD-02: Test-seam mechanism after collapse

| Option | Description | Selected |
|--------|-------------|----------|
| Single internal ctor + named TestServiceFactory | One ctor on the type, marked internal, takes factory + pipeline + nullable Func<...> override delegates. DI uses explicit factory delegate. TestServiceFactory in DeckFlow.Web.Tests calls internal ctor via [InternalsVisibleTo]. | ✓ |
| Single public ctor with optional override delegates | Public ctor with optional Func<...>? trailing params; tests pass overrides directly. Test concerns leak into public surface. | |
| Reflection-based test factory | Single public ctor (DI args only); test factory uses reflection to set private fields. Brittle to renames. | |
| IExecutor strategy interface DI'd into service | Refactor to take IRestExecutor<T> via DI; production binds Real, tests bind Fake. Architecture change for test-only concern. | |

**User's choice:** Single internal ctor + named TestServiceFactory (Recommended).
**Notes:** Existing `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` in DeckFlow.Web/AssemblyInfo.cs:3 makes this the lowest-friction path. Planner verifies DI activator handling for internal ctors and uses explicit factory delegate registration if needed.

---

## TD-03: Bootstrap reliability after untracking *.js

| Option | Description | Selected |
|--------|-------------|----------|
| README + verification commit only | Document `cd DeckFlow.Web && npm install typescript`. MSBuild target unchanged. Verify byte-identical JS output before deletion. | ✓ |
| Track DeckFlow.Web/package.json + lockfile, drop *.js | Stop gitignoring package.json/lockfile so npm pin is reproducible. Out-of-scope per ROADMAP. | |
| Add MSBuild auto-npm-install step | Wrap CompileTypeScriptAssets to run `npm install` if missing. Adds first-build latency + internet dependency. | |
| Defer JS build to npm script + check-in build artifacts elsewhere | Move tsc out of MSBuild into npm run build. Larger change. | |

**User's choice:** README + verification commit only (Recommended).
**Notes:** Lightweight; no new tooling; Render Docker stage already handles its own npm install. The "track package.json/lockfile" idea is captured as a deferred backlog item.

---

## TD-03: .gitignore entry shape

| Option | Description | Selected |
|--------|-------------|----------|
| Glob: `DeckFlow.Web/wwwroot/js/*.js` | One-line entry; future TS modules auto-handled. | ✓ |
| Explicit list of 10 filenames | More auditable; every new TS module needs a .gitignore amendment. | |
| Negate-include pattern | Ignore *.js under wwwroot/, allow lib/**/*.js if vendored 3rd-party JS exists. | |

**User's choice:** Glob: `DeckFlow.Web/wwwroot/js/*.js` (Recommended).
**Notes:** Plan must verify wwwroot/lib/ contents to confirm no vendored JS gets caught.

---

## TD-04: Render CIDR source-of-truth and unsatisfiability fallback

| Option | Description | Selected |
|--------|-------------|----------|
| Research first, decide based on findings | Plan starts with research task. If found: restrict + cite URL. If not found: surface as roadmap concern, ship defense-in-depth alternative. | ✓ |
| Restrict to Render's documented STATIC OUTBOUND IPs | Wrong list (those are outbound, not inbound proxy IPs); included for completeness. | |
| Skip TD-04 if no documented CIDR exists | Mark unsatisfiable, propose alternative. | |

**User's choice:** Research first, decide based on findings (Recommended).
**Notes:** Honest path. Current Program.cs comment ("Render assigns dynamic proxy IPs we can't enumerate") explicitly contradicts the SC, so research must reconcile this.

---

## TD-04: Environment scoping

| Option | Description | Selected |
|--------|-------------|----------|
| Production only; loopback kept in Development | Branch on IWebHostEnvironment.IsProduction(). Prod = Render CIDR + loopback. Dev = loopback only, skip restriction. | ✓ |
| All environments (no branching) | Apply Render CIDR + loopback regardless. Simplest code. | |
| Production only; Development stays fully open | Prod restricted; Development keeps current `KnownIPNetworks.Clear()` behavior. | |

**User's choice:** Production only; loopback kept in Development (Recommended).
**Notes:** Loopback retained in prod too for in-container probes / health checks.

---

## Claude's Discretion

- Internal master ctor parameter ordering for the 10 collapsed services
- Whether `TestServiceFactory.cs` is one big file or split per-service in `TestDoubles/`
- Exact `KnownIPNetworks` CIDR list (planner fills from Render docs research outcome)
- Whether the Production-only branch in Program.cs is inline or extracts to a helper extension method
- README onboarding section copy and placement (new `## Local Development` header vs extending an existing build section)

## Deferred Ideas

- **Track `DeckFlow.Web/package.json` + `package-lock.json` in git** — currently gitignored. CLAUDE.md cites pins (TS 6.0.2, ESLint 10.2.0) that are fictional without lockfile in source control. Future polish item.
- **Feedback rate-limit partition key off `X-Forwarded-For`** — conditional on TD-04 research outcome. If Render publishes no inbound CIDR, switching partition key onto session/CSRF identity removes spoofability risk independent of forwarded-headers trust. Future-phase candidate.
- **Convenience-overload ctor consolidation** for `HelpContentService` / `VersionService` / `FeedbackStore` — explicitly out of TD-02 scope per D-03; future cleanup if scope re-interpreted.
- **Migrate Fly.io configuration** — `fly.toml` exists but Render is the live target; deletion candidate for a future cleanup phase.
