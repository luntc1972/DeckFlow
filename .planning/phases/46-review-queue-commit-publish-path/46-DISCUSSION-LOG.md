# Phase 46: Review Queue + Commit-Publish Path - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-15
**Phase:** 46-review-queue-commit-publish-path
**Areas discussed:** Git push mechanics, Approve/reject behavior, Entry preview depth, Diff preview + LF

---

## Git push mechanics

### Push execution
| Option | Description | Selected |
|--------|-------------|----------|
| Studio pushes | Stage 2 runs `git push` after the reviewed-diff checkbox; needs git creds in shell | |
| Commit-only, you push | Studio does `git add`+`git commit` only; operator pushes from terminal | ✓ |

### Branch
| Option | Description | Selected |
|--------|-------------|----------|
| Current branch (v1.7) | Commit onto checked-out branch; merge→main later | |
| main directly | Studio targets main; push triggers Render deploy | |
| Detect & show current | Read+display current branch, commit there, no switching | ✓ |

### Artifacts in commit
| Option | Description | Selected |
|--------|-------------|----------|
| Seed + markdown both | Commit index-seed.json AND artifact_path .md files (PUB-03) | ✓ |
| Seed only | Commit just index-seed.json | |

### Two-stage gate mapping (follow-up — SC4 reinterpretation)
| Option | Description | Selected |
|--------|-------------|----------|
| Diff → Commit | Stage 1 export+diff; Stage 2 commit gated by reviewed-diff checkbox; manual push is implicit 3rd step | ✓ |
| Commit → Push-cmd | Stage 1 commit; Stage 2 shows push command + checkbox, never runs it | |
| Diff→Commit + push hint | Diff→Commit plus post-commit push-command display + deploy reminder | |

**User's choice:** Commit-only / detect+show current branch / seed+markdown both / Diff→Commit.
**Notes:** Studio never pushes — strongest accidental-deploy safeguard. Intentional divergence
from ROADMAP SC4's literal "commit then push" wording; SC4 intent honored more strongly.

---

## Approve/reject behavior

### Write timing
| Option | Description | Selected |
|--------|-------------|----------|
| Immediate per-click | Each approve/reject writes to DB right away (optimistic) | ✓ |
| Staged + Save | Accumulate in component state, explicit Save commits batch | |

### Store API shape
| Option | Description | Selected |
|--------|-------------|----------|
| Single + batch | SetApprovalStatusAsync(key,status) + (IReadOnlyList<key>,status) | ✓ |
| Single only | Single method; UI loops for batch | |

### Batch + filter UX
| Option | Description | Selected |
|--------|-------------|----------|
| Checkboxes + filter tabs | Row checkboxes + bulk buttons + Pending/Approved/Rejected/All tabs | ✓ |
| Per-row only now | Per-row buttons + tabs, defer batch (misses SC2) | |

**User's choice:** Immediate per-click / single+batch / checkboxes + filter tabs.
**Notes:** Matches SC1 immediate update + SC2 batch.

---

## Entry preview depth

### Preview source
| Option | Description | Selected |
|--------|-------------|----------|
| Read markdown artifact | Render artifact_path .md (summary+clips) + DB tags | ✓ |
| Tags-only from DB | Title + tag sets only, no clip/summary body | |

### Preview UI
| Option | Description | Selected |
|--------|-------------|----------|
| Inline expand row | Click-to-expand in place (Phase 45 pattern) | ✓ |
| Modal / side panel | Full entry in modal/drawer | |
| You decide | Defer expand-vs-modal to UI-SPEC | |

### Missing artifact file
| Option | Description | Selected |
|--------|-------------|----------|
| Degrade to tags + warn | Tags-only + warning, still approvable | |
| Block approve | Disable approve for unreadable artifact; reject still allowed | ✓ |

**User's choice:** Read markdown artifact / inline expand / block approve.
**Notes:** Review exactly what ships; can't approve content you can't see.

---

## Diff preview + LF

### Diff calculation
| Option | Description | Selected |
|--------|-------------|----------|
| Shell git diff | Process.Start `git diff`; parse for counts | |
| In-memory compare | Compare new approved set vs HEAD JSON by natural key | |
| Both | git diff for raw text + in-memory key compare for counts | ✓ |

### Diff summary content
| Option | Description | Selected |
|--------|-------------|----------|
| Counts + raw diff | Added/Updated/Removed counts + scrollable raw git diff | ✓ |
| Counts only | Just the counts (SC3 minimum) | |

### LF enforcement point
| Option | Description | Selected |
|--------|-------------|----------|
| Export writes LF | ExportIndexAsync writes explicit `\n` regardless of OS | ✓ |
| Rely on .gitattributes | No writer change; risk CRLF in working tree (SC5 checks file) | |
| Both (write LF + verify) | Write LF + publish-time byte-scan verify | |

**User's choice:** Both / counts + raw diff / export writes LF.
**Notes:** SC3 counts + ground-truth raw diff; SC5 satisfied deterministically at write time.

---

## Claude's Discretion

- Repo-root / git working-dir resolution for diff/commit calls.
- Commit-message default (operator-editable optional).
- Dirty/conflicted working-tree handling — prefer scoping the commit to known seed + artifact paths.
- StateHasChanged / async bridging for live queue updates (mirror Phase 45).

## Deferred Ideas

- Studio executing `git push` (rejected — D-01).
- Branch switching / merge-to-main from Studio (out of scope — D-02).
- Direct prod-DB + SCP publish path (Phase 47).
- Operator-editable commit message in UI (optional).
- Page/nav layout, expand-vs-modal markup, visual styling → `46-UI-SPEC.md` (`/gsd-ui-phase 46`).
- Reviewed-but-not-folded todos: expert-context pin, combo-data spike, KB value A/B (all unrelated).
