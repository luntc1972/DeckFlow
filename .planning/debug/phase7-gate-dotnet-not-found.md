# Debug: phase 7 gate "Restore .NET dependencies" — `dotnet: command not found`

- **Status:** fix in flight (Codex `gpt-5.6-luna`), environment layer resolved and verified
- **Opened:** 2026-08-22
- **Workstream / phase:** cycle21-cut-lab / 7
- **Run:** `.claude/scratch/dev-agent/runs/2026-08-22-21e357f8.json`

## Symptom

```
bash: line 1: dotnet: command not found
```

`verification.failures[0]`: gate `Restore .NET dependencies`, command `dotnet restore DeckFlow.sln`,
cwd `.`, `exitCode: 127`, `fatal: false`.

## Reproduction

```
$ bash -l -e -o pipefail -c 'dotnet restore DeckFlow.sln'
bash: line 1: dotnet: command not found
exit=127
```

Byte-identical to the recorded failure, and it is the exact argv `runChecks`
(`dev-agent/src/checks.js:527`) builds.

## Two distinct root causes

### RC-1 — environment: the SDK was on PATH only as `dotnet.exe`

`dev-agent` discovers gates from `.github/workflows/ci.yml` and runs the CI `run:` line verbatim
(`checks.js:7-16`, deliberately — a hardcoded gate list would drift from CI). CI provisions the
toolchain with `actions/setup-dotnet@v5`, a `uses:` step with no `run:`, so it is correctly not a
gate; on a dev box the login profile is the contract instead (`checks.js:521-525`).

This box's login PATH carried `~/.local/bin/dotnet.exe -> /mnt/c/Program Files/dotnet/dotnet.exe`
but no `dotnet`. The repo's own scripts already paper over this per-script — `scripts/format-check-changed.sh:14-30`
`resolve_dotnet()` and `scripts/run-web-test.sh:36` both probe `dotnet` then `dotnet.exe` — so nothing
had ever forced the profile itself to be correct.

**Fix:** `~/.local/bin/dotnet -> /mnt/c/Program Files/dotnet/dotnet.exe`, sibling of the existing
`dotnet.exe` symlink, in a directory already on the login PATH.

**Verified:** the reproduction command above now exits 0, "All projects are up-to-date for restore."
Same binary the two repo scripts already fell through to, so their behaviour is unchanged.

### RC-2 — dev-agent: exit 127 was retried as a code regression

`checks.js:549`:

```js
fatal: check.fatalExitCodes.includes(exitCode) || exitCode === null,
```

`fatalExitCodes` defaults to `[]` for every gate except `format-check-changed.sh`, which gets `[2]`
(`checks.js:496-503`). So exit 127 came back `fatal: false`, and `decide.js:105-118` escalates only
on `fatal` — otherwise it calls `boundedFix`. A missing toolchain would therefore be handed to the
fix loop as a code defect and burn Codex fix attempts on a bug that does not exist. That is the same
failure mode the existing `[2]` carve-out was written to prevent; 126/127 were the missed case.

**Fix (in flight):** `UNRUNNABLE_EXIT_CODES = [126, 127]` folded into the `fatal:` expression inside
`runChecks`, *not* into `normalizeCheck` defaults — a caller-supplied `fatalExitCodes` array replaces
the default, and must not be able to drop these.

**Regression test:** `test/checks.test.js` — 127 and 126 fatal, exit 1 non-fatal as a control, and a
check declaring `fatalExitCodes: [2]` still fatal on 127. Written before the fix and confirmed to
fail on `fatal === false`.

Branch: `fix/gate-127-infra-fatal` in `/mnt/c/users/chrislunt/source/Personal/dev-agent`.

## Deliberately not done

- No gate was weakened, skipped or deleted. RC-2 changes only how a *non-running* gate is classified;
  a gate that runs and fails is still a code failure and still drives the fix loop.
- `checksOverride` was not used to re-point the gate at `dotnet.exe`. That would encode the drift from
  CI that `discoverChecks` exists to prevent.
- Installing a Linux .NET 10 SDK in WSL was not attempted; the repo builds through the Windows SDK by
  design.

## Follow-up

The recorded run still carries `verification.passed: false` with the stale 127 failure. Re-run
verification for run `2026-08-22-21e357f8` once the dev-agent fix lands.
