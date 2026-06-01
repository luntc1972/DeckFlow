---
phase: 22
reviewers: [codex]
reviewed_at: 2026-06-01T22:26:42Z
plans_reviewed: [22-01-PLAN.md, 22-02-PLAN.md, 22-03-PLAN.md, 22-04-PLAN.md]
round: 2
verdict: BLOCK
---

# Cross-AI Plan Review — Phase 22 (Round 2 / re-review)

Re-review after the round-1 BLOCK (7 HIGH + 5 MED) replan. Codex verified each prior
item against the actual repo, not just the plan text.

## Codex Review

CLOSED means the revised plan now genuinely instructs the fix, not that the repo is already changed.

Verified repo facts: runtime Docker currently copies only publish output at [Dockerfile](/mnt/c/users/chrislunt/source/personal/deckflow/Dockerfile:52), `.dockerignore` still excludes markdown at [.dockerignore](/mnt/c/users/chrislunt/source/personal/deckflow/.dockerignore:35), `.gitignore` still ignores `content-kb/` at [.gitignore](/mnt/c/users/chrislunt/source/personal/deckflow/.gitignore:5), the artifact writer stores `content-kb/{slug}/{id}.md` at [ContentArtifactWriter.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Core/Knowledge/ContentArtifactWriter.cs:18), and `AdminFlagsController.Toggle` currently has token-only CSRF at [AdminFlagsController.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs:70).

**Prior Highs**
| Prior item | Status | Review |
|---|---:|---|
| 22-03 missing 22-02 dependency | CLOSED | Plan 03 now declares `depends_on: ["22-01", "22-02"]` and explains the sequential dependency in [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:6) and [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:83). |
| Docker delivery misses `.dockerignore` | PARTIALLY-CLOSED | The right edits are specified in Plan 02, including `!content-kb/**` and Dockerfile `COPY content-kb/` at [22-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-02-PLAN.md:123), but they live only inside a `checkpoint:human-verify`, not an executable implementation task. See new HIGH-1. |
| Artifact path convention inconsistent | CLOSED | D-22A is now consistent across all plans: store `content-kb/...`, combine against the directory containing `content-kb/`; see [22-01-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-01-PLAN.md:59), [22-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-02-PLAN.md:58), [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:68). |
| CSRF gap on reused flag toggle | CLOSED | Plan 04 explicitly patches `AdminFlagsController.Toggle` with `SameOriginRequestValidator` and adds a 4/4/4 grep gate at [22-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-04-PLAN.md:103) and [22-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-04-PLAN.md:124). |
| CLI runner references Web layer | CLOSED | Plan 02 now requires `ResolveContentKbDatabasePath` plus `new ContentSiteIndexStore(dbPath)`, matching current CLI patterns at [CommandRunners.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.CLI/CommandRunners.cs:480) and [CommandRunners.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.CLI/CommandRunners.cs:1503). |
| Artifact base local/Docker mismatch | CLOSED | Plan 03 adds ordered candidates and startup logging in [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:71) and [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:124). |
| Interface expansion breaks fakes | CLOSED | Plan 01 now explicitly updates every implementer, including the current fake at [RunDistillAsyncTests.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Core.Tests/RunDistillAsyncTests.cs:603), with acceptance gates in [22-01-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-01-PLAN.md:140). |

**Prior Mediums**
| Prior item | Status | Review |
|---|---:|---|
| Flag-off 503 vs 404 | CLOSED | Plan 03 accepts existing 503 behavior from [FeatureFlagGateAttribute.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs:66) and updates UAT at [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:101). |
| Copy button reuse underspecified | CLOSED | Plan 03 now uses local `content-kb.ts` copy logic instead of importing module-local `attachDynamicCopyButton` from [card-lookup.ts](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/wwwroot/ts/card-lookup.ts:134); see [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:190). |
| Detail route unsafe for RSS GUIDs | CLOSED | Detail route is now `/content-kb/{id:long}` in [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:79). |
| Last-loaded timestamp source | PARTIALLY-CLOSED | Plan 04 defines `max(indexed_utc)` at [22-04-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-04-PLAN.md:59), but that is index generation time, not actual seed reload time. The UI label should be changed to “Index generated” or a real load-status row should be added. |
| Postgres coverage thin | PARTIALLY-CLOSED | Plan 01 documents no Postgres fixture and adds DDL substring checks at [22-01-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-01-PLAN.md:176). That is acceptable as a fallback, but still does not exercise Npgsql bool parameter/read behavior. |

**New Concerns**
HIGH-1: Plan 02’s critical delivery edits are not in an executable task. Task 1 only edits CLI/seed files and explicitly excludes `.gitignore`, `.dockerignore`, and `Dockerfile` at [22-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-02-PLAN.md:115). The only place those edits appear is the human checkpoint at [22-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-02-PLAN.md:120). Add an actual protected-file/artifact-copy task after approval, then keep the checkpoint as verification.

HIGH-2: Plan 03 instructs service registration using `app.Environment` before `app` exists. Current `Program.cs` builds `app` only at [Program.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/Program.cs:349), after service registration. The instruction at [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:128) should use `builder.Environment` or resolve `IWebHostEnvironment` from the service provider inside the singleton factory.

MEDIUM-1: Artifact file serving only guards against escaping `ContentBase`, not escaping the `content-kb/` subtree. Existing store validation rejects rooted and `..` paths only at [ContentSiteIndexStore.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Core/Content/ContentSiteIndexStore.cs:169), so a bad DB/seed row like `appsettings.json` would still resolve under `/app`. Require `artifactPath.StartsWith("content-kb/")` and prefix-check against `{ContentBase}/content-kb`.

MEDIUM-2: Seed loader row construction omits the required `Id = 0` detail. `ContentSiteIndexRow.Id` is required at [ContentArtifactSpec.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Core/Knowledge/ContentArtifactSpec.cs:110), while Plan 02’s export DTO intentionally omits id and Plan 03 just says “Build each ContentSiteIndexRow from the seed fields” at [22-03-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-03-PLAN.md:126). Make `Id = 0` explicit.

LOW-1: The seed safety grep for `transcript|audio|spend` can false-positive on legitimate titles or prose. Prefer a JSON key assertion over text grep in [22-02-PLAN.md](/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/22-content-kb-site-integration/22-02-PLAN.md:112).

Blocking HIGH items: prior HIGH-2 is only partially closed because delivery edits are checkpoint-only; new HIGH-2 is the invalid `app.Environment` DI registration instruction.

**Final verdict: BLOCK**

---

## Consensus Summary

Single external reviewer this round (Codex — primary; gemini/opencode/qwen/cursor not
installed, claude skipped as self).

### Confirmed Closed (round-1 items)
- **5 of 7 prior HIGH fully CLOSED:** 22-03→22-02 dependency, artifact_path convention
  (D-22A), CSRF on `AdminFlagsController.Toggle` (D-22E), CLI stays Core-layer,
  local+Docker artifact-base resolver (D-22B), interface-fake updates (no CS0535).
- **3 of 5 prior MED CLOSED:** 503-vs-404 accepted, copy-button local logic, detail
  route `{id:long}`.

### Agreed Concerns (block execute)
- **HIGH — Docker delivery edits are checkpoint-only (prior HIGH-2 partial + new HIGH-1).**
  Plan 02 Task 1 explicitly EXCLUDES `.gitignore`/`.dockerignore`/`Dockerfile`; the only
  place the negations + runtime `COPY content-kb/` appear is the human checkpoint. No
  executable task performs them → seed/artifacts may never reach the runtime image.
  **Fix:** add a real protected-file/artifact-copy task (gated after the approval
  checkpoint), keep the checkpoint as verification.
- **HIGH — `app.Environment` used before `app` exists (new HIGH-2).** Plan 03 registers the
  artifact-base resolver singleton using `app.Environment`, but `Program.cs` builds `app`
  at line 349 — AFTER service registration. **Fix:** use `builder.Environment` (or resolve
  `IWebHostEnvironment` from the service provider inside the singleton factory).

### Lower-severity (fix in same pass)
- **MED — artifact serving guards `ContentBase` but not the `content-kb/` subtree.** A bad
  seed/DB row (`appsettings.json`) still resolves under `/app`. Require
  `artifactPath.StartsWith("content-kb/")` + prefix-check against `{ContentBase}/content-kb`.
- **MED — seed loader omits `Id = 0`.** `ContentSiteIndexRow.Id` is required; export DTO drops
  id. Make `Id = 0` explicit in the loader row construction.
- **LOW — seed safety grep `transcript|audio|spend` can false-positive on titles/prose.**
  Prefer a JSON-key assertion over text grep.
- **MED (carryover) — last-loaded label.** `max(indexed_utc)` is index-generation time, not
  reload time — relabel UI to "Index generated" or add a real load-status row.
- **MED (carryover) — Postgres bool path still unexercised** (DDL substring check only; no
  Npgsql param/read). Acceptable fallback, noted.

### Divergent Views
None — single reviewer.

**Final verdict: BLOCK** — 2 HIGH open (1 partial-carryover + 1 new). Both concrete and
narrow. Re-run `/gsd-plan-phase 22 --reviews` to close.
