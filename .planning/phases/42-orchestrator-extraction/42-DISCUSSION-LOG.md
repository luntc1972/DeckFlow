# Phase 42: Orchestrator Extraction - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-13
**Phase:** 42-orchestrator-extraction
**Areas discussed:** Interface shape, Output contract, Store wiring, Helper home, Test seam, Reset wiring, Studio scope

---

## Interface Shape

| Option | Description | Selected |
|--------|-------------|----------|
| Single fat interface | One IContentKbOrchestrator with all 8 operations | |
| Facade + sub-interfaces | IContentKbOrchestrator composes IHarvestOrchestrator / IDistillOrchestrator / IContentMaintenance | ✓ |

**User's choice:** Facade + sub-interfaces
**Notes:** ISP-aligned per CLAUDE.md SOLID; facade still satisfies ROADMAP's named contract; Studio can depend on just its slice.

---

## Output Contract

| Option | Description | Selected |
|--------|-------------|----------|
| Result record + injected progress sink | Structured result record per op; progress via IProgress<string>/TextWriter; CLI renders + maps to exit code | ✓ |
| Result record, no live progress | Result record only; CLI logs after the call | |
| Inject ILogger, return exit code int | Keep int return, progress via ILogger | |

**User's choice:** Result record + injected progress sink
**Notes:** Console-free Core; preserves real-time per-video harvest/distill progress for parity; exit-code policy stays in CLI; results reusable by Studio.

---

## Store Wiring

| Option | Description | Selected |
|--------|-------------|----------|
| Per-method injection (keep current seam) | Methods take store interfaces as params (mirrors internal-static signatures) | |
| Constructor injection (DI-style) | Orchestrator takes stores via ctor; methods take only operation args | ✓ |

**User's choice:** Constructor injection (DI-style)
**Notes:** Cleaner Studio DI registration; smaller method signatures. Changes the test seam (see Test Seam below).

---

## Helper Home (validators/constants)

| Option | Description | Selected |
|--------|-------------|----------|
| Move as-is into orchestrator | Relocate helpers verbatim; don't touch Core validation | |
| Consolidate into existing Core validation | Merge into DistillationValidation/Schemas where they overlap | ✓ |

**User's choice:** Consolidate into existing Core validation
**Notes:** Less duplication; semantics must stay byte-identical (behavior-preserving move); cover with re-pointed anchor + new unit tests.

---

## Test Seam

| Option | Description | Selected |
|--------|-------------|----------|
| Rewrite call site, keep assertions | Construct orchestrator (ctor stores), call DistillAsync(args); assertions identical | ✓ |
| Keep a static back-compat shim | Leave thin static that builds orchestrator so test compiles verbatim | |

**User's choice:** Rewrite call site, keep assertions
**Notes:** "Behavior unchanged" = same verified behavior via new seam. No dead static shim (contradicts thin-adapter goal).

---

## Reset Wiring (Postgres-vs-Sqlite provider selection)

| Option | Description | Selected |
|--------|-------------|----------|
| Stays in CLI store construction | CLI resolves provider/connection, constructs stores, injects; orchestrator storage-agnostic | ✓ |
| Moves to Core orchestrator | Orchestrator builds connection from provider info | |

**User's choice:** "what do you recommend" → Claude recommended **Stays in CLI store construction**, locked.
**Notes:** Orchestrator depends only on store interfaces. Provider selection is a composition-root concern (CLI builds one way, Studio another via StudioConfig). Matches SC2 "construct stores from paths"; lowest coupling; ctor injection works identically in both hosts.

---

## Studio Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Add DI registration + smoke call | AddContentKbOrchestrator() extension in Core + minimal Studio service resolving it | ✓ |
| Reference only, no wiring | Compile-check Studio against Core types; defer DI to Phase 43 | |

**User's choice:** Add DI registration + smoke call
**Notes:** Proves SC4 end-to-end (Studio→Core, no CLI ref). No Studio UI feature this phase.

## Claude's Discretion

- Exact sub-interface decomposition and result-record field shapes.
- One facade class vs several classes (one per sub-interface) aggregated.
- Namespace placement (recommended `DeckFlow.Core/Orchestration/`) and file layout.
- Naming of result records and the progress-sink abstraction.
- Additional unit-test coverage beyond the re-pointed anchor.

## Deferred Ideas

- Studio UI feature consuming the orchestrator — Phase 43+ (PUB/REVQ).
- Routing orchestrator into a Web background job — out of scope, not requested.
- Low-score todo matches (combo-data spike, expert-context pin, validate-KB-value) — unrelated, not folded.
