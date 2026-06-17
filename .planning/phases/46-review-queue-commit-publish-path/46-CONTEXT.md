# Phase 46: Review Queue + Commit-Publish Path - Context

**Gathered:** 2026-06-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the **Review Queue + Commit-Publish** flow in the standalone local Blazor Server
`DeckFlow.Studio` app (v1.7 milestone). The operator can:

1. **Review queue (REVQ-02/03):** see `approval_status='pending'` distilled entries in a UI
   queue, preview each entry's summary + timestamped clips + tags, and approve / reject / leave
   pending — individually or in filtered batches; queue filters by status.
2. **Commit-publish (PUB-03):** export the approved-only seed (`index-seed.json`), see a diff of
   what will change vs HEAD, and commit the seed + markdown artifacts onto the current branch.
   **Studio commits only — it never pushes.** The operator pushes manually from a terminal;
   pushing `main` is what triggers Render auto-deploy.

**This phase is a UI + thin-wiring wrapper, not a rewrite.** The data layer (Phase 43) already
provides `approval_status`, `GetApprovedRowsAsync`, the approved-only `ExportIndexAsync` filter,
and the safe upsert. The only NEW Core surface needed is an approval-status *mutation* method
(none exists yet). Studio mirrors the Phase 45 `Harvest.razor` wiring patterns. No new NuGet
packages.

**UI hint = yes.** Visual layout, spacing/type/color, and page/nav structure are deferred to a
`46-UI-SPEC.md` (run `/gsd-ui-phase 46` next). The decisions below cover data + operator-behavior
choices the UI-SPEC leaves open.

**Out of scope (own phases):** direct prod-DB + SCP publish path (Phase 47 / PUB-04/05);
Studio executing `git push` (deliberately excluded — see D-01).

</domain>

<decisions>
## Implementation Decisions

### Git publish mechanics (PUB-03)
- **D-01:** **Studio commits only — it never runs `git push`.** Stage 2 stops at the commit; the
  operator runs `git push` manually from the terminal. This is the strongest possible
  accidental-deploy safeguard (Render auto-deploys on push to `main`, and Studio cannot push).
  Resolves the recent "push intent" ambiguity (mem S1385) and respects the never-push-main rule.
- **D-02:** Studio **detects and prominently displays the current branch** in the publish UI and
  commits onto that branch — **no branch switching**. Today the working branch is `v1.7`; the
  operator merges→main and pushes out-of-band. Committing on a non-`main` branch also means a
  commit alone can never trigger a deploy.
- **D-03:** The publish commit includes **both** `index-seed.json` AND the markdown artifacts
  (the `artifact_path` files), staged together — per PUB-03 ("seed + markdown artifacts"). The
  diff preview reflects both.
- **D-04:** **Two-stage gate reframed (intentional divergence from ROADMAP SC4's literal
  "commit then push" wording):** Stage 1 = export approved seed + render the diff preview;
  Stage 2 = **commit**, with the commit button enabled only after the operator checks
  "I have reviewed the diff above." Studio halts at commit; the manual `git push` is the implicit
  third, out-of-app step. SC4's *intent* — prevent accidental auto-deploy and force a deliberate,
  diff-reviewed action — is honored more strongly than the literal text, since Studio never pushes
  at all. Planner should record this reinterpretation against SC4.

### Approve/reject behavior (REVQ-02/03)
- **D-05:** Approve/reject writes the new `approval_status` to the DB **immediately on click**
  (optimistic UI update) — no separate Save step. Matches SC1 ("immediately updates its status").
  Consistent with a single-operator local tool; avoids the staged-state loss risk of Phase 45 D-05.
- **D-06:** Add a NEW mutation surface to `IContentSiteIndexStore`:
  `SetApprovalStatusAsync(naturalKey, status)` (single) **and** a batch overload
  `SetApprovalStatusAsync(IReadOnlyList<key>, status)` (one round-trip). This is the only new Core
  data method this phase requires. `status` is constrained to `pending`/`approved`/`rejected`.
- **D-07:** Queue UX: row **checkboxes** + "Approve selected" / "Reject selected" bulk buttons,
  plus **status filter tabs** (Pending / Approved / Rejected / All). "Batch-approve a filtered
  set" (SC2) = filter to Pending → select-all → approve. Covers SC2 fully.

### Entry preview (REVQ-02)
- **D-08:** Full preview **reads the markdown artifact** at `artifact_path` (summary +
  timestamped clips) and combines it with the DB tag sets (archetype/bracket/card-category).
  You review exactly what will ship, not just tags. Cache the file read per expand to avoid
  re-reading on every render.
- **D-09:** Preview renders as an **inline expand/collapse row** (mirrors Phase 45 patterns;
  stays in list context for scan-approving down the queue). Exact expand markup is UI-SPEC's call.
- **D-10:** If a row's `artifact_path` file is missing/unreadable, **block approve** for that row
  (you cannot approve content you cannot see) — show a visible "artifact missing" warning;
  **reject remains allowed**. No crash (graceful-degradation convention).

### Diff preview + LF normalization (PUB-03 / SC3 / SC5)
- **D-11:** Diff is computed **both ways**: shell `git diff` (via `Process.Start` — precedent in
  Core, e.g. `CliCommandSpec`) for the raw textual preview of `index-seed.json` + artifact paths,
  AND an in-memory natural-key comparison (new approved set vs HEAD's parsed `index-seed.json`)
  for friendly Added / Updated / Removed counts.
- **D-12:** The diff summary surfaces **counts + raw diff**: Added/Updated/Removed row counts
  (SC3) plus the raw git-diff text in a scrollable box, so the operator sees both the summary and
  the exact bytes changing before committing.
- **D-13:** LF is enforced at **export-write time**: `ExportIndexAsync` writes the seed with
  explicit `\n` newlines (not `Environment.NewLine`) regardless of OS, so a Windows-run Studio
  never produces CRLF. Belt-and-suspenders with the repo `.gitattributes` LF rule. Satisfies SC5
  (`file index-seed.json` reports `ASCII text`, not CRLF) deterministically.

### Claude's Discretion
- **Repo-root / git working-dir resolution** for the `git diff` + `git add`/`git commit` calls
  (relative to Studio's configured `ArtifactRoot` / seed path). Planner/researcher pick the safest
  resolution; surface the resolved repo path in the publish UI.
- **Commit-message default** (e.g. `content: publish approved KB seed (N entries)`) — sensible
  default, operator-editable in the UI is acceptable but not required.
- **Dirty/conflicted working-tree handling** before commit (e.g. refuse to commit / warn if there
  are unrelated staged changes, or scope the commit strictly to the seed + artifact paths).
  Prefer scoping the commit to known paths so unrelated working-tree state is never swept in.
- **StateHasChanged / async bridging** details for the live queue updates (mirror Phase 45's
  progress-sink bridge pattern).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope
- `.planning/ROADMAP.md` §"Phase 46: Review Queue + Commit-Publish Path" — goal + 5 success
  criteria (note D-04 reinterprets SC4: commit-only, Studio never pushes).
- `.planning/REQUIREMENTS.md` — REVQ-02, REVQ-03, PUB-03 (this phase); REVQ-01, PUB-01, PUB-02
  (Phase 43 prerequisites, already shipped); PUB-04/05 (Phase 47, downstream only).

### Data layer (Phase 43 — already built; this phase consumes + extends)
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — interface to EXTEND with
  `SetApprovalStatusAsync` (single + batch). Existing: `GetApprovedRowsAsync`,
  `UpsertContentColumnsOnlyAsync`, `GetAllRowsAsync`, `GetPublishedRowsAsync`.
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `approval_status` self-healing ALTER
  (@86-101), `GetApprovedRowsAsync` (@301, `WHERE approval_status='approved'`), upsert SQL —
  templates for the new mutation method.
- `.planning/phases/43-approval-status-safe-upsert/43-CONTEXT.md` — approval_status semantics,
  grandfather backfill (D-01), admin-preserved field set, approved-only export (D-07/D-08).

### Export / orchestrator (Phase 42/43 — diff + LF touchpoints)
- `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` — `ExportIndexAsync` (already filters to
  approved via `GetApprovedRowsAsync`); enforce explicit `\n` LF write here (D-13). Seed path:
  `content-kb/seed/index-seed.json`.
- `DeckFlow.Core/Orchestration/IContentIndexExporter.cs` / `ContentIndexExportRow.cs` — exported
  JSON byte-shape is pinned by the Phase 42 golden test; do NOT change the shape (LF + membership
  only).

### Git shell-out precedent
- `DeckFlow.Core/Integration/CliCommandSpec.cs`, `CliLlmDistillationService.cs`,
  `FfmpegAudioChunker.cs` — existing `Process.Start`/`ProcessStartInfo` patterns to mirror for
  `git diff` / `git add` / `git commit`.

### Studio host (integration point — mirror Phase 45)
- `DeckFlow.Studio/Pages/Harvest.razor` — closest existing page; live updates, store/orchestrator
  resolution, status badges, button-lock state machine.
- `DeckFlow.Studio/Program.cs` (DI), `DeckFlow.Studio/Shared/NavMenu.razor` (add review/publish
  nav entry), `DeckFlow.Studio/Services/ContentKbOrchestratorSmokeService.cs` /
  `ActionOrchestratorProgress.cs` (Studio→Core resolution + progress-sink patterns).
- `.planning/phases/45-harvest-distill-ui/45-CONTEXT.md` + `45-UI-SPEC.md` — Studio wiring
  decisions (D-05 in-memory state, D-06 dispose-cancels) and the UI design-contract pattern this
  phase's UI-SPEC will follow.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Phase 43 data layer: `approval_status` column + `GetApprovedRowsAsync` + approved-only
  `ExportIndexAsync` are done. Phase 46 adds only the mutation method + UI + git/diff wiring.
- `Harvest.razor` (Phase 45): full template for a Studio page that resolves stores/orchestrator,
  renders status-driven rows, locks buttons during in-flight ops, and bridges progress to the UI.
- `Process.Start` git/CLI precedent in `DeckFlow.Core/Integration/*` — reuse for diff/commit.

### Established Patterns
- Self-healing ALTER + `WHERE <col>=...` filtered reads (Phase 43) — template for the
  `SetApprovalStatusAsync` UPDATE (single + batched IN-list).
- Studio = storage-agnostic UI over host-wired Core stores/orchestrator (Phase 42/45); Studio
  must NOT reference `DeckFlow.CLI`; Core stays console-free.
- Optimistic UI + immediate DB write (no staged-save) consistent with single-operator tool.

### Integration Points
- New Core method `IContentSiteIndexStore.SetApprovalStatusAsync` (single + batch).
- New Studio review/publish UI (page(s) + NavMenu entry — exact structure per UI-SPEC).
- `ExportIndexAsync` LF-write hardening (D-13).
- Git diff/commit shell-out from Studio against the resolved repo working dir.

</code_context>

<specifics>
## Specific Ideas

- Studio **commit-only**; operator pushes manually. Render auto-deploys only on push to `main`.
- Commit onto the **current branch** (today `v1.7`), branch name shown in the publish UI.
- Diff box shows **Added/Updated/Removed counts + raw git diff** before the commit is enabled.
- Seed path: `content-kb/seed/index-seed.json`; written with explicit `\n` LF.
- Approval status mutation: immediate per-click; batch via filter→select-all→bulk action.

</specifics>

<deferred>
## Deferred Ideas

- **Studio executing `git push`** — explicitly rejected (D-01) for accidental-deploy safety;
  revisit only if the operator later wants a fully in-app publish with credential handling.
- **Branch switching / merge-to-main from Studio** — out of scope (D-02); operator does this
  out-of-band.
- **Direct prod-DB + SCP publish path** — Phase 47 (PUB-04/05).
- **Operator-editable commit message in the UI** — optional nicety; default message is acceptable
  (Claude's discretion above).
- **Page/nav layout (combined Review+Publish page vs two nav entries), expand-vs-modal markup,
  visual styling** — deferred to `46-UI-SPEC.md` (`/gsd-ui-phase 46`).

### Reviewed Todos (not folded)
- *User-selectable Expert Context — pin a KB video/tag into the analysis prompt* — out of scope
  (deckflow.gg prompt feature, not the Studio review/publish flow).
- *Spike — combo data richness for primer pilot lines* — unrelated (primer/combo data).
- *Validate Content KB value — A/B ChatGPT output with vs without expert context* — unrelated
  (KB value validation, not review/publish). All three matched only on generic keywords.

</deferred>

---

*Phase: 46-review-queue-commit-publish-path*
*Context gathered: 2026-06-15*
