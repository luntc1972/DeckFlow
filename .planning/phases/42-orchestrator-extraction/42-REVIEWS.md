---
phase: 42
reviewers: [codex]
reviewed_at: 2026-06-13
plans_reviewed: [42-01-PLAN.md, 42-02-PLAN.md, 42-03-PLAN.md, 42-04-PLAN.md]
model: gpt-5.5 (medium)
---

# Cross-AI Plan Review — Phase 42

> Reviewer set: Codex only. Running inside Claude Code CLI (claude skipped for independence); gemini/opencode/qwen/cursor not installed. Codex is the authoritative plan reviewer per project workflow.

## Codex Review

## Summary

The four-wave plan is directionally strong and mostly matches the refactor constraints: contracts first, logic lift, CLI adapter shrink, then Studio smoke. The biggest risks are not conceptual; they are parity and DI-resolution details. The plan under-specifies full CLI output parity, result-to-exit mapping tests, and the Studio/AddContentKbOrchestrator registration shape. It also tries to prove Studio can use a slice interface, but the planned DI extension only registers the facade, so that may fail unless all sub-interfaces are registered too.

## Strengths

- Good phase ordering: contract skeleton → implementation lift → adapter rewrite/test seam → Studio proof.
- Correctly identifies the missing all-zero timestamp rule in `DistillationValidation.ValidateClips`; adding the exact CLI message is the right fix.
- D-05/D-06 are mostly respected: provider selection and path resolution stay host-side, and Core receives store/service abstractions.
- The no-static-shim D-09 stance is sound. Re-pointing the test to `ContentKbOrchestrator` is the right architecture check.
- The plan explicitly calls out key parity hazards: dry-run output, metered-provider refusal, JSON seed format, validation short-circuit, spend ordering, block-row-first delete ordering.

## Concerns

- **HIGH: DI slice registration is incomplete.** Plan 04 says Studio may inject `IContentMaintenanceOrchestrator`, but Plan 03 only requires `IContentKbOrchestrator -> ContentKbOrchestrator`. ASP.NET DI will not resolve the slice unless `IHarvestOrchestrator`, `IDistillOrchestrator`, etc. are registered too, ideally all pointing to the same scoped `ContentKbOrchestrator` instance.

- **HIGH: Plan 04 likely cannot resolve the orchestrator with only the ListBlocked store.** `AddContentKbOrchestrator()` registers the full concrete orchestrator, whose constructor needs all stores/services. Even if the smoke call only uses `IBlockedVideoStore`, DI still needs every constructor dependency. The plan handwaves "throwing-default implementation" for unused dependencies, but does not list files or interfaces to implement. This is a build/runtime hazard.

- **HIGH: CLI parity is not tested broadly enough.** Only `CommandRunnerValidateClipsTests` is re-pointed. That pins one distill failure invariant, but not CLI observable behavior: add-source exit codes, list-blocked tab formatting, index export byte shape, dry-run lines, metered-provider refusal, corpus reset dry-run, explicit `--video-ids`, harvest source-selection errors.

- **HIGH: "Byte-identical JSON seed output" is asserted but not verified.** Moving `ContentIndexExportRow` to Core can preserve property names, but byte identity also depends on row ordering, property order, null handling, indentation, trailing newline, and natural-key logic. The plan should add a golden/fixture test or at least export-before/export-after comparison.

- **MEDIUM: Progress parity is fragile.** Plan 01 uses `IProgress<string>` and Plan 03 warns against `Progress<T>` because it posts asynchronously. That is correct, but the contract still invites async behavior. For CLI parity, define a tiny synchronous sink implementation in CLI, or use a delegate abstraction like `Action<string>` / `IOrchestratorProgress.Report` with synchronous semantics.

- **MEDIUM: Result records are under-specified for nullability.** Records with `IReadOnlyList<string>` / rows / items should default to `[]` or be `required`; otherwise nullable warnings or accidental nulls leak into adapters. Same for `Success`: default `false` may make incomplete construction silently look like failure.

- **MEDIUM: Plan 02 may create a new god class in Core.** It says `ContentKbOrchestrator.cs` min 400 lines and lifts all helpers into one class. That satisfies relocation but only partially closes the "god-class" backlog. The interface split helps consumers, but implementation SRP is still weak. Acceptable for behavior-preserving phase, but call it out as a deliberate temporary compromise.

- **MEDIUM: Source-add validation ownership is inconsistent.** Current CLI rejects invalid source type before store construction with exit `2`. Plan 02 moves `IsValidContentSourceType` into the orchestrator, while Plan 03 says CLI maps result to exit `2`. That can work, but the plan must ensure CLI still prints the exact unsupported-type message and does not initialize DB/store unnecessarily before invalid input returns.

- **MEDIUM: AddContentKbOrchestrator may need an explicit DI abstractions reference.** The plan says do not add a package unless required. That is fine, but if Core does not currently compile against `IServiceCollection`, this becomes a blocker. The plan should resolve this before later waves depend on the extension.

- **LOW: File count/objective mismatch in Plan 01.** It says "Eight interface/record files" but lists more than eight. Not a technical blocker, but it signals the contract surface may be larger than intended.

- **LOW: Acceptance greps are brittle.** Grep checks like "no `ContentIndexExportRow` in CLI" could also match comments or summaries; "progress?.Report" misses `progress?.Report(...)` variants if code uses a local helper. Fine as smoke gates, not sufficient as correctness checks.

## Suggestions

- Register every slice interface in `AddContentKbOrchestrator()`: `services.AddScoped<ContentKbOrchestrator>();` then map `IContentKbOrchestrator`, `IHarvestOrchestrator`, `IDistillOrchestrator`, etc. to that same scoped instance.
- For Plan 04, either register real local SQLite-backed dependencies for the full constructor, or change the smoke proof to use a narrower concrete service that depends only on `IContentMaintenanceOrchestrator`. Do not leave "throwing fakes" implicit in `Program.cs`.
- Add parity tests before/with Plan 03: add-source invalid type exits `2` + exact stderr; same URL unique violation exits `0`; slug conflict exits `3`; list-blocked formats `id\t{BlockedUtc:O}\treason`; metered non-dry-run distill exits `1`; content index export matches a golden JSON string including trailing newline.
- Add a before/after CLI smoke script or test harness for representative commands against a temp SQLite DB. Build-only gates are too weak for a behavior-preserving phase.
- Make result records null-safe: `required` for mandatory scalar fields, initialize collections to `Array.Empty<T>()` / `[]`.
- Prefer a synchronous progress abstraction: `public interface IOrchestratorProgress { void Report(string message); }` or a documented synchronous CLI `IProgress<string>` impl. Avoid `Progress<T>` in adapters.
- Clarify the all-zero timestamp rule add to Core is not a CLI behavior change; it is consolidation of an already-enforced CLI rule. Verify the Core-backed distill path calls the consolidated validator before any writes.
- Keep Plan 02's "one big implementation" only as an extraction step; add a follow-up note that splitting implementation by sub-interface is future cleanup unless cheap during extraction.

## Risk Assessment

**Overall risk: MEDIUM-HIGH.** The architecture is sound, and the wave order is mostly right, but this is a large behavior-preserving extraction from a 1,480-line command runner with many observable CLI side effects. The current verification is too thin for parity, and the Studio/DI proof has a real resolution flaw unless sub-interface and constructor dependency registration are tightened. The all-zero timestamp consolidation is handled correctly, but it needs to stay pinned by the re-pointed test plus broader distill-path tests.

---

## Consensus Summary

Single reviewer (Codex). No cross-reviewer consensus to synthesize.

### Agreed Concerns (Codex HIGH — these BLOCK execution per project workflow)

1. **DI slice registration incomplete** — `AddContentKbOrchestrator()` must register every sub-interface mapped to the same scoped `ContentKbOrchestrator`, else Studio slice injection (D-01/D-08) fails to resolve.
2. **Plan 04 cannot build the orchestrator from one store** — full ctor needs all deps; "throwing fakes" must be made explicit (real local SQLite-backed deps or a narrower smoke service depending only on the maintenance slice).
3. **CLI parity under-tested** — only one anchor test re-pointed; add exit-code + output-format parity tests (add-source 2/0/3, list-blocked tab format, metered distill 1, index export golden JSON).
4. **JSON seed byte-identity unverified** — add a golden/fixture before/after export comparison (ordering, property order, indentation, trailing newline, natural-key logic).

### Divergent Views

None (single reviewer).

---

# Round 2 — Codex Re-Review (gpt-5.5 medium, 2026-06-13)

Verdict: **NO-GO as written** · Risk: MEDIUM. 3 of 4 prior HIGH closed; HIGH-3 has a semantic bug.

## Prior HIGH closure
- **HIGH-1 CLOSED** — 42-03 registers concrete once + forwards facade + 5 sub-interfaces (acceptance: 6 forwards).
- **HIGH-2 CLOSED** — 42-04 enumerates all 13 ctor deps, real local SQLite stores, named null-objects only.
- **HIGH-3 NOT CLOSED** — 42-05 metered-distill test is WRONG. Refusal fires only when `!dryRun && !isSubscriptionProvider` (metered = NOT a subscription provider). The plan wrote `DistillAsync(dryRun:false, isSubscriptionProvider:true)` → that is the subscription/non-metered case and will NOT hit the exit-1 refusal path. Fix: the metered test must pass `isSubscriptionProvider:false` to pin Success==false / AbortedReason / exit 1.
- **HIGH-4 CLOSED** — 42-05 golden fixture + ordinal `Assert.Equal` through the CLI serialize path.

## New concerns
- **MEDIUM (DI):** `AddContentKbOrchestrator()` registers `ContentKbOrchestrator` via plain `AddScoped<ContentKbOrchestrator>()`, but its ctor takes a raw `string artifactRoot`. Studio can only satisfy that by registering bare `string` globally — brittle. Prefer binding only this ctor param via a factory registration (`AddScoped<ContentKbOrchestrator>(sp => new ContentKbOrchestrator(... resolved stores ..., artifactRoot))`) or a small options record (e.g. `ContentKbOrchestratorOptions { string ArtifactRoot }`). Apply consistently in 42-01 (ctor shape), 42-03 (extension), 42-04 (Studio wiring).
- **MEDIUM (test doubles):** 42-05 reuses `Fake*` stores that are currently PRIVATE nested types in `CommandRunnerValidateClipsTests`. Plan must explicitly lift them to shared internal test doubles (alongside the `Throwing*` doubles created in 42-03 Task 3) or allow local per-test fakes — don't assume they're already shareable.
- **LOW:** `ContentIndexExportRow` placement — 42-01 says one public sealed record per result file, then allows the Row in the result file or a sibling. Make it a sibling file and list it explicitly.
- **LOW:** Wave-4 `42-04 ∥ 42-05` parallelism confirmed acceptable (disjoint prod/test areas, both depend on 42-03 doubles).

Path to GO: fix HIGH-3 flag (`isSubscriptionProvider:false`) + tighten artifactRoot DI binding → GO.

---

# Round 3 — Codex Confirmation (gpt-5.5 medium, 2026-06-13)

Verdict: **GO** · Planning risk LOW (moderate implementation risk — parity depends on exact behavior copy, now covered by tests).

All round-2 findings CLOSED:
- HIGH-3 metered test → `DistillAsync(dryRun:false, isSubscriptionProvider:false)` hits `!dryRun && !isSubscriptionProvider` refusal.
- MEDIUM DI → `ContentKbOrchestratorOptions { required string ArtifactRoot }`; ctor takes the record; hosts register typed options.
- MEDIUM test doubles → `Fake*` lifted to `DeckFlow.Core.Tests/Orchestration/FakeOrchestratorStores.cs` (internal sealed); 42-05 reuses.
- LOW → `ContentIndexExportRow.cs` own file.
- LOW → wave-4 parallelism acceptable.

Round-1 reconfirmed intact: HIGH-1 (6 forwarding regs), HIGH-2 (all 13 ctor deps real SQLite + typed options), HIGH-4 (golden fixture ordinal assert). No new blocking concerns.

**GO for executing Phase 42.**
