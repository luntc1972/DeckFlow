---
phase: 46
slug: review-queue-commit-publish-path
status: verified
threats_open: 0
asvs_level: 2
created: 2026-06-16
---

# Phase 46 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

**Audit date:** 2026-06-16
**Phase:** 46 — Review Queue + Commit-Publish Path (v1.7 Studio)
**Auditor:** gsd-security-auditor (sonnet)
**block_on:** high
**Result:** SECURED — 25 `mitigate` threats verified CLOSED against implemented code; 3 `accept` threats confirmed documented. 28/28 closed, 0 open. `register_authored_at_plan_time: true` — mitigations verified, no retroactive scan.

---

## Threat Verification

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-46-01-01 | Tampering/SQLi | mitigate | CLOSED | `ContentSiteIndexStore.cs:524` — `ValidateApprovalStatus(status)` called before any DB open; allow-list `["pending","approved","rejected"]` at :577; SQL :531–533 uses Dapper `@status/@type/@value` — no string concat |
| T-46-01-02 | Integrity (admin-field clobber) | mitigate | CLOSED | `ContentSiteIndexStore.cs:531` — UPDATE sets only `approval_status = @status`; `is_visible/is_hidden/is_evergreen` absent from both overloads; `SetApprovalStatusAsync_PreservesAdminFields` test confirms |
| T-46-01-03 | Integrity (partial batch) | mitigate | CLOSED | `ContentSiteIndexStore.cs:556` — `BeginTransactionAsync` wraps all per-key UPDATEs; :573 `CommitAsync` only after full loop; `await using` auto-rollback on exception/cancel (D-06) |
| T-46-01-04 | DoS (large batch) | accept | CLOSED | Single-operator local tool; corpus-bounded; one reused transaction (46-01-PLAN/SUMMARY) |
| T-46-02-01 | Tampering/Injection (git args) | mitigate | CLOSED | `GitRepository.cs:208–216` — `UseShellExecute=false`, `CreateNoWindow=true`, `WorkingDirectory` set; 29 `ArgumentList.Add`; no `Arguments =` string-build (grep clean) |
| T-46-02-02 | Elevation (accidental deploy) | mitigate | CLOSED | `GitRepository.cs` — zero "push" occurrences; `IGitRepository.cs:56–58` doc marks no-push intentional (D-01) |
| T-46-02-03 | Tampering (foreign-staged sweep) | mitigate | CLOSED | `GitRepository.cs:148–167` — `git diff --cached --name-only` guard throws `GitForeignStagedChangesException`; `git add -- {paths}` never `-A` (:172–177); pathspec `git commit -- {paths}` (:188–192) |
| T-46-02-04 | Tampering/traversal (seedPath/repoRoot) | mitigate | CLOSED | `GitRepository.cs:75` repoRoot via `git rev-parse --show-toplevel` (ArgumentList); `Publish.razor:333` seed path = `Path.GetFullPath(Path.Combine(_repoRoot, SeedRelative))`, constant rel-path |
| T-46-02-05 | Info disclosure (git output) | accept | CLOSED | Public-repo seed, no secrets, Core console-free (46-02-PLAN) |
| T-46-02-06 | Tampering (artifact copy traversal) | mitigate | CLOSED | `ContentKbOrchestrator.cs:835–876` — `ResolveContainedPath`: rejects null/rooted/leading-`:`/`..`, asserts `StartsWith(root+sep)`; applied to BOTH source (:810) and dest (:811) |
| T-46-02-07 | Integrity (missing artifact ships) | mitigate | CLOSED | `ContentKbOrchestrator.cs:815–818` — `!File.Exists` throws `InvalidOperationException`; `ContentArtifactCopyTests.MissingSource_Throws` verifies |
| T-46-02-SC | Tampering (supply chain) | mitigate | CLOSED | No new NuGet; git is a system tool; 46-02-SUMMARY `tech_stack.added: []` |
| T-46-03-01 | Tampering (approval write from UI) | mitigate | CLOSED | `Review.razor:337,359,387,431` — status are hardcoded literals; store `ValidateApprovalStatus` (:579–587) second gate; Dapper-parameterized |
| T-46-03-02 | DoS (unreadable artifact) | mitigate | CLOSED | `Review.razor:475` read via `Task.Run(ReadArtifactSafe)`; :501–543 catches `FileNotFoundException`/`IOException`→null; warning + Approve disabled (D-10) |
| T-46-03-03 | Info-disclosure/traversal (artifact read) | mitigate | CLOSED | `Review.razor:506,513,522–528` — rejects rooted + `..`, resolves `dataRoot = Directory.GetParent(ArtifactRoot)`, `Path.GetFullPath` + `StartsWith(dataRoot+sep)`; null on violation; doubled-prefix fix noted :498 |
| T-46-03-04 | Tampering (stale circuit) | mitigate | CLOSED | `Review.razor:404–407,448–452,484–488` — `InvokeAsync` wraps `StateHasChanged` with `ObjectDisposed`/`InvalidOperation` catches; `Dispose()` :643–647 cancels+disposes CTS |
| T-46-04-01 | Elevation (accidental deploy) | mitigate | CLOSED | `Publish.razor` — "push" only as static reminder `git push origin @_branch`; no push call/button; IGitRepository has no push method |
| T-46-04-02 | Tampering (foreign-staged / unrelated commit) | mitigate | CLOSED | `Publish.razor:491–495` — `StageAndCommitAsync(_repoRoot, _stagedPaths=[SeedRelative]+copiedArtifactPaths, ...)`; GitRepository foreign-staged guard + scoped add + pathspec commit; `_diffReviewed` checkbox gate :479/:173 |
| T-46-04-03 | Tampering/Injection (git args from page) | mitigate | CLOSED | `Publish.razor:333` seed path constant+GetFullPath; artifact paths from containment-guarded copy; all values via IGitRepository ArgumentList (T-46-02-01) |
| T-46-04-04 | Repudiation/Integrity (unapproved rows) | mitigate | CLOSED | `ContentKbOrchestrator.cs:885–891` — shared `GetApprovedExportRowsAsync`→`GetApprovedRowsAsync` (filters `approval_status='approved'`); seed + copy use same set |
| T-46-04-05 | Integrity (miscount Updated) | mitigate | CLOSED | `Publish.razor:254–259,409–417,429` — canonical per-row JSON (CamelCase+indented) string comparison `newByKey[key] != headByKey[key]`, not reference equality; IReadOnlyList trap documented :390–392 |
| T-46-04-06 | Integrity (broken seed commits) | mitigate | CLOSED | `Publish.razor:357–378` — copy in try/catch; missing source shows `alert-danger` and `return`s before diff/commit; orchestrator throws (T-46-02-07) |
| T-46-04-07 | Tampering (artifact copy escapes repo) | mitigate | CLOSED | Delegates entirely to `CopyApprovedArtifactsToRepoAsync`→`ResolveContainedPath` both-ends guard (T-46-02-06) |
| T-46-04-08 | Info disclosure (git output/repo path in UI) | accept | CLOSED | Public-repo seed+diff carry no secrets; repo root aids operator confirmation; no conn-strings on page (46-04-PLAN) |
| T-46-04-09 | DoS (dropped circuit) | mitigate | CLOSED | `Publish.razor:540–545` Dispose cancels/disposes CTS; InvokeAsync sinks (:318–322,339–347,369–377,443–457,525–529) catch ObjectDisposed/InvalidOperation |
| T-46-04-SC | Tampering (supply chain) | mitigate | CLOSED | No new NuGet; Bootstrap-only UI; 46-04-SUMMARY `tech_stack.added: []` |
| T-46-05-01 | Tampering (broad commit/format reflow) | mitigate | CLOSED | 46-05-SUMMARY: changed-lines format gate CLEAN (exit 0); only `EnsureYoutubeSourceTests.cs` (3 `Assert.Contains`, no logic change, no `-A`); no production code modified |
| T-46-05-02 | Repudiation (unrun test pass) | mitigate | CLOSED | 46-05-SUMMARY documents real `dotnet test`: "Passed! - Failed: 0, Passed: 23, Skipped: 0, Total: 23" |

---

## Accepted Risks Log

| Threat ID | Accepted Risk | Rationale |
|-----------|---------------|-----------|
| T-46-01-04 | DoS via very large approval batch | Single-operator local Studio tool; corpus-bounded; one reused transaction; no public endpoint |
| T-46-02-05 | git diff/show output + stderr surfaced to UI | Public-repo seed/artifacts contain no secrets; Core console-free; stderr is git's own messages |
| T-46-04-08 | Repo root path + raw git diff displayed in Publish page | Public-repo content only; repo root display aids operator confirmation; no credentials on page |

---

## Audit Notes

**T-46-02-02 (no push verb):** `GitRepository.cs` has zero "push" occurrences; IGitRepository has no push method; absence marked intentional at `IGitRepository.cs:56–58` (D-01). Grep 0 matches.

**T-46-03-03 / T-46-02-06 containment-guard shape:** Review.razor implements its own inline guard in `ReadArtifactSafe` (UI layer — graceful `null` on violation, never throws); `CopyApprovedArtifactsToRepoAsync` uses the shared Core `ResolveContainedPath` (throws — publish-blocking). Structurally equivalent, independent, both verified. Behavioral difference (null vs throw) intentional per context. *Informational drift risk: two guard implementations not shared.*

**Human-verify checkpoints:** Plans 03 and 04 had blocking `checkpoint:human-verify` tasks. Core-layer mitigations (ContentSiteIndexStore, GitRepository, ContentKbOrchestrator) are covered by 23 phase Core.Tests. Blazor-layer mitigations (T-46-03/T-46-04 series) were code-verified in this audit AND confirmed via headless-browser verification of Review.razor (filter tabs, optimistic approve/reject tint, expand resolver, missing-artifact disables Approve, batch bar) and Publish.razor (branch+count, Export diff, two-stage commit gate, no push button). DeckFlow.Studio has no automated test project — the two pages are covered by these human-verify checkpoints only.

- Implementation files were NOT modified by this audit.

---

## Audit Trail

| Date | Auditor | Threats | Closed | Open | Result |
|------|---------|---------|--------|------|--------|
| 2026-06-16 | gsd-security-auditor (sonnet) | 28 | 28 | 0 | SECURED |
