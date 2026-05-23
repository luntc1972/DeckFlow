# Phase 14: Broader Codebase Name-vs-Behavior Audit - Context

**Gathered:** 2026-05-17
**Status:** Ready for planning
**Mode:** interactive `discuss` (4 gray areas selected, all answered)

<domain>
## Phase Boundary

Sweep every public class + interface across `DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`, `DeckFlow.Core.Tests`, `DeckFlow.Web.Tests` for two things in lockstep:

1. **Name-vs-behavior mismatches** — types whose names no longer describe their current responsibility. Rename to AI-agnostic / responsibility-accurate names.
2. **Missing XML `<summary>` doc-comments** — every public class + interface across all 5 projects must carry one. `<GenerateDocumentationFile>true</GenerateDocumentationFile>` flipped ON in all 5 csproj files and `dotnet build DeckFlow.sln --configuration Release` must compile clean with zero new warnings.

**What this phase does NOT do:**

- DeckController god-class split — own refactor milestone per CLAUDE.md "Out of Scope"
- ChatGPT services extraction into PromptBuilder / ScryfallReferenceResolver helpers — own refactor milestone
- AiPlatform value object refactor — that's AIPLATFORM-01/02, Phase 15
- TS / CSS / data-* internal identifier rename — Phase 16 hygiene per Phase 13 D-08
- Responsibility splits — Phase 14 renames class NAMES only; if a class is mis-named because it does too much, the audit captures it in deferred for a future refactor phase and renames to the best single-line summary of current behavior

</domain>

<decisions>
## Implementation Decisions

### Audit method (AUDIT-01)

- **D-01:** Scripted grep for smells + targeted manual review of the 3 candidates named in REQUIREMENTS.md AUDIT-01.

  Smells the grep pass MUST surface:
  - Services whose ctor/field set spans 3+ distinct collaborators (suggests integration layer not just RPC)
  - Classes ending in `Service` whose method bodies do not call any HTTP client (often pure helpers or facades — name implies stateful operation)
  - Classes ending in `Client` whose method bodies depend on app-scoped state (often facades not clients)
  - Classes whose XML `<summary>` (where present) mentions 2+ responsibilities joined by "and" / ";"
  - Files whose primary type name differs from their filename
  - Test doubles whose prefix is NOT in the canonical {`Fake`, `Stub`, `Throwing`} taxonomy (see D-05)

  Targeted manual review of:
  - `ScryfallTaggerService` — does it just call tagger, or also normalize/cache?
  - `CommanderSpellbookService` — lookup vs full client?
  - Test-double scoping (covered by D-05)

  Grep results land in `14-AUDIT-REPORT.md` (Plan 14-01 deliverable) before any rename commits. Plan 14-02 reads that report.

### Rename trigger (AUDIT-01)

- **D-02:** Loose — rename if any reader would benefit from a more descriptive name. Not just strict "name actively misleads" — also "name could be clearer about responsibility scope, side effects, or integration partners". Accept the git-blame churn cost; future readers benefit more than blame-spelunkers lose.

  Test-double prefix renames (D-05) follow this same standard.

### XML doc-comment scope (AUDIT-02)

- **D-03:** Backfill `<summary>` on every public class + interface across all 5 projects. Style anchor: `DeckFlow.Web/Services/CardLookupService.cs` + `DeckFlow.Web/Services/CommanderSpellbookService.cs` (terse, single-sentence; same anchor used in Phase 13 D-03).

  Backfill also covers public properties and public ctors on renamed types — same as Phase 13 Wave 1's 101-summary sweep on the rename target Models.

  Non-public types (internal, private, nested) are out of scope for the universal backfill — only public surface. Internal helpers that get renamed during D-02 get a summary in lockstep, but no separate sweep of internal-only code.

### GenerateDocumentationFile enablement (AUDIT-02)

- **D-04:** Flip `<GenerateDocumentationFile>true</GenerateDocumentationFile>` ON in ALL 5 csproj files (Core, Web, CLI, Core.Tests, Web.Tests). DeckFlow.Web is already ON. Backfill summaries to a clean build with zero new CS1591 / CS1573 / CS1587 warnings.

  No `NoWarn` suppression for those three IDs is added to the 4 newly-flipped projects — the suppression in `DeckFlow.Web.csproj` was a v1.1-era partial-coverage compromise and stays there for now (removing it is a separate cleanup). The 4 newly-flipped projects must hit ZERO warnings without suppression.

  CLI csproj is flipped even though CLI has zero public types today — future-proof: any new public type in CLI must carry `<summary>`. No-op cost today, costs nothing later.

### Test-double prefix canonicalization (AUDIT-01)

- **D-05:** Consolidate the 4 one-off test-double prefixes into the {`Fake`, `Stub`, `Throwing`} taxonomy per `.planning/codebase/CONVENTIONS.md`. CONVENTIONS.md already defines:

  - `Fake*` — stateful behavior fakes (e.g., `FakeCategoryKnowledgeStore`)
  - `Stub*` — queue-driven stubs (e.g., `StubHttpMessageHandler`)
  - `Throwing*` — exception injection (e.g., `ThrowingCardSearchService`)

  Renames required (from scout count 2026-05-17):
  - `Null*` (1 instance) → either `Stub*` (if no-op fallback) or `Fake*` (if stateful default) — case-by-case
  - `Test*` (1 instance) → `Fake*` (Test prefix is too generic; doesn't communicate behavior)
  - `Configurable*` (1 instance) → `Fake*` (configurable behavior = stateful fake)
  - `Capturing*` (1 instance) → `Fake*` (captures call args = stateful fake — note state-capture semantics in `<summary>`)

  Total: 4 renames + 4 `<summary>` rewrites describing the state-capture/configurable behavior in the doc-comment instead of the prefix.

  Update `.planning/codebase/CONVENTIONS.md` if the audit surfaces an additional legitimate prefix worth codifying (don't expect any).

### Internal class scope

- **D-06:** Public + internal types are in audit scope for D-02 rename trigger (any reader benefits), but `<summary>` backfill (D-03) is public-only. Internal classes renamed under D-02 also get a `<summary>` in lockstep because the rename commit touches them anyway — cheap to add at edit time.

### Wave decomposition (execution strategy)

- **D-07:** 4 plans by surface type:

  - **Plan 14-01 (Baseline + Audit Report):** Capture pre-phase warning count per csproj. Run the scripted grep-for-smells pass. Manual review of the 3 REQUIREMENTS.md candidates. Emit `14-AUDIT-REPORT.md` enumerating: candidate renames (with old/new names + rationale), test-double rename list (D-05), files where summaries are missing per project. No source-code commits in this plan beyond the report doc.

  - **Plan 14-02 (Renames):** Execute every rename from the audit report — production-code candidates + test-double consolidation. `git mv` for file renames. Single-purpose commits per rename for clean git blame. Update DI registrations, namespace imports, `InternalsVisibleTo`, Razor `@model` directives. Build expected to stay GREEN throughout this plan (each rename + all references updated in lockstep — D-08 mid-rename red is NOT acceptable for Phase 14, unlike Phase 13).

  - **Plan 14-03 (Doc-comment backfill):** Backfill `<summary>` on every public class + interface across all 5 projects that's missing one (excluding the renamed types from Plan 14-02 which already got summaries). Style anchor: `CardLookupService.cs` / `CommanderSpellbookService.cs`. NO csproj changes yet — GenerateDocumentationFile still OFF in 4 of 5 projects so summaries don't block build. Backfill is correctness work, not warning-driven.

  - **Plan 14-04 (GenDocFile flip + final build gate):** Flip `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in 4 csproj files (Core, CLI, Core.Tests, Web.Tests). Run `dotnet build DeckFlow.sln --configuration Release`. Plan 14-04 PASSES when build exits 0 with zero new CS1591/1573/1587 warnings (and zero of anything else). If warnings surface, fix in place (likely missed summaries from Plan 14-03 — backfill them). End with full test discovery via `dotnet test --no-build` if WSL permits, or push-and-watch CI on `v1.3`.

  Sequential between plans (each depends on prior). Within a plan, file edits can parallelize via `isolation="worktree"` per the project's standard plan execution rules — Plan 14-02's renames each touch their own surface so no DeckController.cs / Program.cs convergence issue like Phase 13 had.

- **D-08:** Mid-plan build state — each Plan 14-02 rename commit must leave build GREEN. Phase 13 D-05 allowed intermediate red builds because every wave overlapped DeckController.cs; Phase 14's renames are smaller-surface and decoupled. Per-rename build-clean check is fast and avoids the D-05 risk of letting a downstream wave miss a reference.

### Baseline warning capture (AUDIT-03)

- **D-09:** Plan 14-01 captures pre-phase warning count per csproj BEFORE any source edits, via:
  ```
  dotnet build DeckFlow.sln --configuration Release --verbosity quiet 2>&1 | grep -cE '^.*warning '
  ```
  Result lands in `14-BASELINE.md`. SC3 "zero new warnings" is verified by re-running same command at end of Plan 14-04 and confirming the count is ≤ baseline. Strict equality is preferred; a one-warning-larger result blocks completion.

### Preservation discipline

- **D-10:** What stays unchanged this phase (carried from Phase 13 / Phase 10):
  - `"ChatGPT"` / `"Claude"` / `"Gemini"` string literal values in `AiPlatform.Key`
  - `request.TargetAiPlatform` property name + `targetAiPlatform` form field
  - `"chatgpt"` zip filename fallback in `PacketArtifactStore`
  - Internal HTML/JS identifiers (`data-cache-key="chatgpt-packets"`, `class="chatgpt-packets-form"`, TS const names) — Phase 999.x deferred per Phase 13 D-08
  - Razor visible prose mentioning "ChatGPT" — Phase 999.1 deferred per Phase 13 D-07 #6
  - All 22 guild theme CSS forks under `wwwroot/css/site-*.css` — Phase 14 does not touch CSS
  - `Co-Authored-By` trailer: NEVER added (CLAUDE.md commit hygiene)

### Claude's Discretion

- Specific old-name → new-name mapping for production-code candidates surfaced by the audit (Plan 14-01 report) — within the constraints of D-02. Defaults to "Wave 14-02 executor picks the clearest name that fits the file-per-type rule".
- Order of file renames within Plan 14-02 — alphabetical by old filename is fine.
- Whether to fold a discovered class-name-vs-behavior mismatch into deferred (if rename uncovers a responsibility split that's bigger than rename) vs renaming to a "best single-line summary of current behavior" — case-by-case judgment in Plan 14-02.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + spec
- `.planning/REQUIREMENTS.md` (AUDIT-01, AUDIT-02, AUDIT-03 acceptance gates with 3 named candidates: ScryfallTaggerService, CommanderSpellbookService, test-double scoping)
- `.planning/ROADMAP.md` (Phase 14 entry — Success Criteria 1..4)

### Prior-phase context (binding decisions)
- `.planning/phases/13-chatgpt-class-rename-summary-doc-comments/13-CONTEXT.md` — D-03 XML `<summary>` tone anchor (CardLookupService / CommanderSpellbookService), D-07 preservation list (`"ChatGPT"` keys, `targetAiPlatform`, `"chatgpt"` fallback), D-08 deferred identifiers, D-09 verification grep pattern (adapt for AUDIT-03)
- `.planning/phases/13-chatgpt-class-rename-summary-doc-comments/13-VERIFICATION.md` — what Phase 13 closed cleanly; Phase 14 must not regress
- `.planning/phases/12-ai-agnostic-url-page-rename/12-CONTEXT.md` — Phase 12 URL slug invariants stay frozen

### Future-phase coupling (do NOT break)
- `.planning/milestones/v1.2-phases/10-claude-gemini-artifact-optimization/10-AISEL-PLATFORM-DESIGN.md` — AIPLATFORM-01 value-object refactor that Phase 15 implements on top of Phase 14's verified-clean naming. Phase 14 renames MUST not block the value-object insertion.
- `.planning/milestones/v1.2-MILESTONE-AUDIT.md` — T1-T8 manual integration suite. Phase 14 changes are non-functional but a full T1-T8 round-trip is the human-side verification for SC4 ("scope discipline observed").

### Project constraints
- `CLAUDE.md` — VSTest WSL constraint; commit hygiene (no Co-Authored-By trailer); plain default author; one logical change per commit; Formatting constraint (do NOT run Format Document / Code Cleanup — pinned in `.editorconfig`)
- `.editorconfig` — pins Allman, file-scoped namespace, switch expressions, separate-line attributes, raw-string preservation; preserves `init` accessors (do NOT auto-convert)
- `.gitattributes` — LF line endings repo-wide
- `DeckFlow.Web/DeckFlow.Web.csproj` — already has `<GenerateDocumentationFile>true</GenerateDocumentationFile>` + `NoWarn 1591;1573;1587`; Phase 14 leaves this combo as-is

### Codebase intel
- `.planning/codebase/STRUCTURE.md` — project layout
- `.planning/codebase/CONVENTIONS.md` — Fake/Stub/Throwing test-double taxonomy (D-05 source of truth); sealed class + record + file-per-type rule; naming conventions
- `.planning/codebase/INTEGRATIONS.md` — RestSharp + Polly resilience pipeline names (D-02 candidates likely live here)
- `.planning/codebase/TESTING.md` — test fixture conventions

</canonical_refs>

<code_context>
## Existing Code Insights

### Public type counts (scouted 2026-05-17)

| Project | Public type count | GenerateDocumentationFile | Action in Phase 14 |
|---|---:|---|---|
| `DeckFlow.Core` | 26 | OFF | Flip ON; backfill summaries |
| `DeckFlow.Web` | 188 | ON (NoWarn 1591;1573;1587) | Audit names; backfill summaries on any without; keep NoWarn for v1.1-era types not yet doc'd |
| `DeckFlow.CLI` | 0 (internal-static only) | OFF | Flip ON (future-proof; no-op today) |
| `DeckFlow.Core.Tests` | 10 | OFF | Flip ON; backfill summaries |
| `DeckFlow.Web.Tests` | 55 (most are test classes + fakes) | OFF | Flip ON; backfill summaries; consolidate test-double prefixes (D-05) |

Total renames projected: 4 (test-double consolidation) + an estimated 5-15 from D-02 audit (audit pass produces exact count in Plan 14-01).

### Test-double prefix distribution (scouted 2026-05-17)

| Prefix | Count | Status |
|---|---:|---|
| `Fake` | 55 | Canonical per CONVENTIONS.md |
| `Throwing` | 8 | Canonical per CONVENTIONS.md |
| `Stub` | 2 | Canonical per CONVENTIONS.md |
| `Null` | 1 | RENAME under D-05 |
| `Test` | 1 | RENAME under D-05 |
| `Configurable` | 1 | RENAME under D-05 |
| `Capturing` | 1 | RENAME under D-05 |

### REQUIREMENTS.md named candidates (manual review per D-01)

- `ScryfallTaggerService` (in `DeckFlow.Web/Services/ScryfallTaggerService.cs`) — verify whether name describes current responsibility or whether it does more than just tagger RPC (likely also normalizes + caches via `TaggerSessionCache`).
- `CommanderSpellbookService` — lookup vs full client?
- Test-double scoping — covered systematically by D-05.

### Reusable patterns to follow

- XML doc style: terse single-sentence `<summary>`, optional `<remarks>` for non-obvious behavior. Anchor: `CardLookupService.cs:13-42` + `CommanderSpellbookService.cs:13-54`.
- Sealed leaf classes per CONVENTIONS.md — preserve `sealed` modifier on every rename.
- Interface + class colocation in the same file — preserve.

### Integration points to watch

- DI container (`Program.cs:60-180`-ish) — every renamed service needs DI registration updated in lockstep.
- `[InternalsVisibleTo("DeckFlow.Web.Tests")]` in `AssemblyInfo.cs` — assembly name unchanged; no edit needed.
- Razor `@model` directives — only Web has views; Plan 14-02 renames touching ViewModels propagate to `.cshtml` `@model` line.

</code_context>

<specifics>
## Specific Ideas

- Use Phase 13's grep-gate verification pattern (from 13-RESEARCH.md) as the AUDIT-03 verification template: capture pre-phase command output → diff against post-phase command output.
- The audit report file `14-AUDIT-REPORT.md` is the human-readable handoff between Plan 14-01 and Plan 14-02. It must be small + actionable — one-line-per-rename format, not narrative essays.
- Consider whether `GenerateDocumentationFile` ON in test projects produces noisy IntelliSense in IDE — Web.Tests has 55 public types, most are test classes whose `<summary>` is "Tests for X". Short summaries acceptable; pure-mechanical "Tests for X" wording is fine for test classes.

</specifics>

<deferred>
## Deferred Ideas

- **`NoWarn 1591;1573;1587` removal from `DeckFlow.Web.csproj`** — would force the v1.1-era 88 still-undoc'd public types in Web to all get summaries. Out of Phase 14 scope; candidate for a future hygiene phase.
- **Responsibility splits surfaced by D-02 audit** — if Plan 14-01 audit identifies a class whose name-vs-behavior gap is large enough that rename alone is insufficient (e.g., `ScryfallTaggerService` actually being three classes), capture the split as a deferred refactor candidate in `14-AUDIT-REPORT.md`. Phase 14 renames to "best single-line summary"; the split lands in its own future refactor milestone.
- **Internal-only class summaries** — out of Phase 14 scope per D-06. Could be a future cleanup phase, but low leverage (internal types don't appear in IntelliSense for external consumers).
- **`.planning/codebase/CONVENTIONS.md` evolution** — if audit surfaces an additional legitimate test-double prefix (e.g., `Recording*` for state-capture semantics distinct from `Fake*`), update CONVENTIONS.md in Plan 14-02. Otherwise no doc change beyond reaffirming the existing taxonomy.

</deferred>

---

*Phase: 14-Broader Codebase Name-vs-Behavior Audit*
*Context gathered: 2026-05-17*
