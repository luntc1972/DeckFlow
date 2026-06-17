---
phase: 50
slug: code-style-enforcement
status: verified
threats_total: 7
threats_open: 0
threats_closed: 7
asvs_level: 1
created: 2026-06-14
last_audited: 2026-06-17
---

# Phase 50 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Threat model source: `.planning/phases/50-code-style-enforcement/50-02-PLAN.md` `<threat_model>` block.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| PR contributor -> CI runner | Untrusted PR code/filenames/diff content reach the `format-gate` bash script and `dotnet format` | Staged file paths, diff hunk content, dotnet format report JSON |
| Developer working tree -> pre-commit hook | Staged hunks (including attacker-influenced filenames) reach the shared script locally | Staged `.cs` file paths, diff content |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-50-01 | Tampering | `scripts/format-check-changed.sh` diff/report parsing | mitigate | Quote all `"$file"` expansions; argv array for `--include`; no `eval`; `set -euo pipefail`; C-quoted/control-char filenames fail CLOSED | closed |
| T-50-02 | Elevation of Privilege | shared script command construction | mitigate | No `eval` anywhere; filenames are argv data passed via `"${CHANGED_FILES[@]}"` array, never interpolated into a command string | closed |
| T-50-03 | Elevation of Privilege | `format-gate` CI job on PR events | mitigate | `pull_request` (not `pull_request_target`); no secrets in `format-gate` job; untrusted fork code runs without repo write token | closed |
| T-50-04 | Repudiation | hook bypass via `git commit --no-verify` | accept | Hook is local opt-in convenience; CI `format-gate` is the authoritative enforcer; documented in `CLAUDE.md:19` | closed |
| T-50-05 | Tampering | EOL normalization on carve-out files | mitigate | Diff and `--include` scoped strictly to `*.cs`; no `git add`; no EOL normalization anywhere in script | closed |
| T-50-06 | Spoofing/Tampering | CI base-ref selection on `push` to main | mitigate | Zero-SHA rejected; `event.before` validated as real object; `before==HEAD` rejected; unresolvable `origin/main` falls to empty-tree sentinel; every base choice logged | closed |
| T-50-SC | Tampering | npm/NuGet supply chain | mitigate | Zero packages added; `format-gate` CI job uses only `actions/checkout`, `actions/setup-dotnet`, and `dotnet restore` (already in `build-and-test`); no `jq`, no husky | closed |

---

## Threat Verification Detail

### T-50-01 — Tampering — CLOSED

**Mitigation verified in `scripts/format-check-changed.sh`:**

- `set -euo pipefail` at line 2 — script-wide fail-fast.
- No `eval` anywhere — `grep -cn 'eval' scripts/format-check-changed.sh` returns 0.
- Changed files passed as array argv: `--include "${CHANGED_FILES[@]}"` at line 299 — never a joined/interpolated string.
- C-quoted diff path guard (line 68-70): `normalize_diff_path` fails CLOSED via `infra_fail` when path starts with `"`.
- Top-level C-quoted guard (lines 283-285): `grep -Eq '^(---|\+\+\+) "'` against full diff before hunk parsing, fails CLOSED.
- Unmappable report paths fail CLOSED at line 62: `infra_fail "report path outside repo root: $path"`.

### T-50-02 — Elevation of Privilege — CLOSED

**Mitigation verified in `scripts/format-check-changed.sh`:**

- Zero `eval` occurrences confirmed by grep (count: 0).
- `CHANGED_FILES` is a bash array built by reading file paths one per element (lines 207-210); passed to `dotnet format` as `"${CHANGED_FILES[@]}"` (line 299) — array expansion, each element a separate argv, not shell-interpreted.

### T-50-03 — Elevation of Privilege — CLOSED

**Mitigation verified in `.github/workflows/ci.yml`:**

- `on:` block (lines 10-12) uses `push:` and `pull_request:` only — `pull_request_target` absent (grep returns zero matches).
- `format-gate` job (lines 16-42) contains no `secrets.*`, no `token:`, no `password:`, no `key:` references — confirmed by grep.
- No explicit `permissions:` block on the job; inherits repository default (public repo: `read` for most scopes, no write token issued to fork PR runners).

### T-50-04 — Repudiation — CLOSED (accepted risk)

**Acceptance documented at `CLAUDE.md` line 19:**

> "New and changed C# lines must satisfy the changed-lines gate locally (`git config core.hooksPath .githooks` opt-in, then the versioned pre-commit hook runs `scripts/format-check-changed.sh staged`) and in CI (`format-gate`, which is the authoritative enforcer)."

The opt-in nature of the local hook (`core.hooksPath .githooks`) is also documented in `.githooks/pre-commit` line 4. A developer bypassing via `git commit --no-verify` is still caught by CI `format-gate` on push/PR. Hook bypass is not prevented by design; the CI gate is the non-bypassable enforcement layer.

### T-50-05 — Tampering (EOL carve-out files) — CLOSED

**Mitigation verified in `scripts/format-check-changed.sh`:**

- Staged mode diff: `git diff --cached --unified=0 -- '*.cs'` (line 277).
- CI three-dot diff: `git diff --unified=0 "$DIFF_BASE"...HEAD -- '*.cs'` (line 150).
- CI two-dot/empty-tree diff: `git diff --unified=0 "$DIFF_BASE" HEAD -- '*.cs'` (line 153).
- `CHANGED_FILES` array populated only from those `*.cs`-scoped diffs (lines 204-210).
- No `git add`, no `dos2unix`, no EOL conversion anywhere in the script (grep clean).

### T-50-06 — Spoofing/Tampering (CI base-ref) — CLOSED

**Mitigation verified in `scripts/format-check-changed.sh` `select_ci_diff_args` function (lines 89-145):**

- `zero_sha` defined at line 90; rejected at line 109: `[ "$before" != "$zero_sha" ]`.
- `event.before` validated as a real commit object via `is_valid_commit_ref "$before"` (line 109), which calls `git cat-file -e "$ref^{commit}"` (line 86).
- `before == HEAD` rejected at line 112: `[ "$before_sha" != "$head_sha" ]`.
- HEAD itself validated at lines 94-96: `infra_fail "HEAD is not a valid commit"` if invalid.
- Every resolution path emits a `format-gate base:` log line (lines 103, 113, 128, 130, 137, 142) — no branch is silent.
- Last-resort empty-tree sentinel (lines 140-144): triggered only when `origin/main` cannot be resolved; uses `git hash-object -t tree /dev/null`.

**Documented deviation from plan spec (WARNING, not BLOCKER):**

The plan-time mitigation specified that `BASE==HEAD` (when `merge-base origin/main == HEAD`) should trigger the empty-tree sentinel. The implemented code instead logs "HEAD already in main history; empty diff is correct" and proceeds with `origin/main...HEAD` (three-dot, producing an empty diff). This is documented in `50-02-SUMMARY.md` lines 23-25 and `50-VERIFICATION.md` lines 105-107.

The deviation is not exploitable: the `merge-base==HEAD` path in the code only fires when (a) `event.before` is invalid/zero-SHA (the first and highest-priority branch already handles all valid direct pushes to main at lines 109-117), and (b) `origin/main` is resolvable AND merge-base equals HEAD — which means the commits at HEAD are already present in main history and were gated when they entered main. The base choice is logged (line 128), so the empty diff is observable, not silent. The higher-risk scenario (direct push to main with truly new commits) is correctly guarded by the `event.before` branch (lines 109-117).

### T-50-SC — Supply Chain — CLOSED

**Mitigation verified:**

- `scripts/format-check-changed.sh`: no `jq`, no `husky`, no `/tmp` — all confirmed by grep (zero matches each).
- `.github/workflows/ci.yml` `format-gate` job (lines 16-42): uses only `actions/checkout@v6`, `actions/setup-dotnet@v5`, `git fetch`, `dotnet restore`, and `bash scripts/format-check-changed.sh ci` — no new npm/pip/NuGet package installs beyond what `build-and-test` already uses.
- Report stored under `./artifacts/format-report.json` (line 263), not `/tmp`; `artifacts/` is pre-existing in `.gitignore`.

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-50-01 | T-50-04 | Pre-commit hook is a local convenience gate, opt-in via `git config core.hooksPath .githooks`. Bypass via `--no-verify` is possible locally but all bypassed commits are caught by the authoritative CI `format-gate` on push/PR. No secrets or integrity-critical data flow through this hook. | operator (documented in `CLAUDE.md:19`) | 2026-06-14 |

---

## Unregistered Flags

None. The `50-02-SUMMARY.md` "Threat-model notes" section maps cleanly to registered threats:

- No `eval`, filenames as argv → T-50-01/T-50-02
- No `jq`, no husky, no new packages → T-50-SC
- Report under already-gitignored `artifacts/` → infrastructure hygiene (no threat surface)
- `*.cs`-only diff scope → T-50-05
- No `pull_request_target` → T-50-03

No new attack surface appeared during implementation without a threat mapping.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-14 | 7 | 7 | 0 | gsd-security-auditor (claude-opus-4-8) |
| 2026-06-17 | 7 | 7 | 0 | gsd-security-auditor (claude-sonnet-4-6) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log (AR-50-01 / T-50-04)
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-17
