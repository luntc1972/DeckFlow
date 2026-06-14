# SECURITY.md — Phase 50: Code-Style Enforcement (format gate)

**Audit date:** 2026-06-14
**Phase:** 50 — Code-Style Enforcement (changed-lines format gate)
**ASVS Level:** 1
**Auditor:** gsd-security-auditor (claude-opus-4-8)
**block_on:** high (BLOCKER = OPEN_THREATS)

---

## Result: SECURED

All 6 `mitigate` threats verified CLOSED against the implemented code. The 1 `accept`
threat (T-50-04) is confirmed documented in CLAUDE.md. Verification was performed against
the CURRENT, hardened code (post commit `7420c21`), not the plan-time wording.

---

## Threat Verification

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-50-01 | Tampering | mitigate | CLOSED | `scripts/format-check-changed.sh:2` `set -euo pipefail`; changed files passed as separate argv via `--include "${CHANGED_FILES[@]}"` (`:299`), not a joined string; every `"$file"`/`"$path"`/`"$current_file"` expansion quoted; C-quoted/control-char diff paths fail CLOSED at `normalize_diff_path` (`:68-70`) and the top-level diff guard (`:283-285`, `^(---|\+\+\+) "` → `infra_fail`); no `eval` (grep clean). |
| T-50-02 | Elevation of Privilege | mitigate | CLOSED | No `eval` anywhere in `scripts/format-check-changed.sh` (verified by grep). Filenames are argv data: `--include "${CHANGED_FILES[@]}"` (`:299`) — array elements, never interpolated into a command string. |
| T-50-03 | Elevation of Privilege | mitigate | CLOSED | `.github/workflows/ci.yml:10-12` uses `on: push: / pull_request:` — NOT `pull_request_target` (grep clean). The `format-gate` job (`:16-41`) requests no secrets; untrusted fork code runs without the repo write token. |
| T-50-04 | Repudiation | accept | CLOSED | Acceptance DOCUMENTED in `CLAUDE.md:19`: hook is a local `git config core.hooksPath .githooks` opt-in convenience and CI `format-gate` "is the authoritative enforcer." Bypass via `--no-verify` is not prevented (by design) but is caught by CI on push/PR. |
| T-50-05 | Tampering | mitigate | CLOSED | Diff filters strictly `-- '*.cs'` in staged mode (`:277`) and both CI diff modes (`:150`, `:153`); `--include` is populated only from those `*.cs` diffs (`extract_changed_files`, `:204-211`); no `git add` and no EOL normalization anywhere (grep clean). `.ps1/.bat/.cmd` carve-out files are never touched. |
| T-50-06 | Spoofing/Tampering | mitigate | CLOSED | `select_ci_diff_args` (`:89-145`) — HEAD validated as a real commit else `infra_fail` (`:94-96`); all-zeros SHA rejected (`:109`); `before` validated as a real object via `is_valid_commit_ref` (`:109`); `before == HEAD` rejected (`:112`); every resolution path `echo`s the chosen base + reason (`:103`, `:113`, `:128`, `:130`, `:137`, `:142`); empty-tree sentinel is LAST-RESORT only, when `origin/main` is wholly unresolvable (`:120/136-144`). No path yields a silent empty-diff pass — every base is logged. |
| T-50-SC | Tampering | mitigate | CLOSED | Zero packages added. `scripts/format-check-changed.sh` uses only git/grep/sed/awk/`dotnet format` — no `jq`, no husky (grep clean). `format-gate` CI job (`ci.yml:16-41`) adds no install step beyond `dotnet restore` (already used by `build-and-test`). |

---

## T-50-06 hardening note (verified against current code, not plan-time wording)

The plan-time mitigation described an empty-tree sentinel as the fallback when `BASE==HEAD`.
The gate was hardened in commit `7420c21`: the new-branch / first-push fallback now resolves
to `origin/main`'s merge-base (three-dot `origin/main...HEAD`), and the empty-tree sentinel is
reserved as a genuine last resort for when `origin/main` cannot be resolved at all.

The declared mitigation INTENT — "reject zero-SHA / invalid / `BASE==HEAD`, ALWAYS log the
chosen base, never a silent empty-diff pass" — is satisfied by the current code:

- **Zero-SHA rejected:** `:109` `[ "$before" != "$zero_sha" ]`.
- **Invalid object rejected:** `:109` `is_valid_commit_ref "$before"`; `:94-96` HEAD itself validated.
- **`BASE==HEAD` handled:** the `before` path rejects `before == HEAD` (`:112`); the `origin/main`
  merge-base path detects `merge_base == HEAD` and LOGS that HEAD is already in main history
  before accepting the (correct) empty diff (`:127-129`). This is correct git semantics — those
  commits were already gated when they entered `main` — and it is logged, not silent.
- **Always logged:** every branch emits a `format-gate base: ...` line (`:103/113/128/130/137/142`),
  so an empty diff is always observable with its justification.
- **No silent skip of a genuinely new commit:** a real, un-gated pushed commit cannot reach an
  empty diff without a logged base choice; an unresolvable integration ref falls to the empty-tree
  sentinel (check everything), logged as last resort (`:140-144`).

CLOSED.

---

## Unregistered Flags

None. The SUMMARY.md "Threat-model notes" section maps cleanly to the registered threats
(no-new-deps → T-50-SC; no-eval/argv → T-50-01/T-50-02; report under gitignored `artifacts/`
→ infrastructure hygiene; `*.cs`-only scope → T-50-05). No new attack surface appeared during
implementation that lacks a threat mapping.

---

## Notes

- Implementation files were NOT modified by this audit. Only this SECURITY.md was written.
- Behavioral FMT-03/FMT-04 proof (live-CI test-PR + hook block/allow) is deferred to Plan 04 per
  the plan's own `<verification>` block; this audit verifies the static mitigation surface, which
  is present and correct.
