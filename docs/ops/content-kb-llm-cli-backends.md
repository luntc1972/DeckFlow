# RUNBOOK - Content KB LLM CLI backends

**Date:** 2026-06-01
**Scope:** Content KB `distill` LLM backend selection for `openai` and `claude`.
**Who runs it:** an operator with local CLI credentials.

## Overview

`DECKFLOW_LLM_PROVIDER` selects the distillation backend:

- unset or `openai`: default OpenAI backend, unchanged.
- `claude`: local Claude CLI backend.
- `codex`: not yet supported; selecting it throws a clear Phase 21.3 / KB-12 error.

The Content KB transcript is piped to the CLI child process on stdin. It is never placed in the command line. Command parts are passed through `ProcessStartInfo.ArgumentList`, so each argument is a separate value with no shell quoting or space splitting.

On Linux/WSL, the claude default is auto-detected as:

```bash
claude -p "<instruction>" --output-format json --allowedTools ""
```

The real instruction replaces the placeholder at runtime. The transcript is stdin.

## WSL invocation

Use this from a WSL bash shell where `claude` is on `PATH` and logged in:

```bash
DECKFLOW_LLM_PROVIDER=claude dotnet run --project DeckFlow.CLI -- distill --db artifacts/uat-content-kb.db
```

Dry-run first:

```bash
DECKFLOW_LLM_PROVIDER=claude dotnet run --project DeckFlow.CLI -- distill --db artifacts/uat-content-kb.db --dry-run
```

Expected dry-run copy includes `WOULD distill ($0, subscription)` and `projected spend $0 (subscription)`.

All invocations also accept `--video-ids "id1,id2"` (v1.5) to distill exactly those natural keys (YouTube video ids or RSS guids) instead of the next pending batch; `--limit` is ignored when it is supplied. The matching `harvest --video-ids` flag (plus `--source-id` when several YouTube sources are enabled) fetches exactly those videos.

## Windows invocation

If DeckFlow is running under Windows `dotnet` and `claude` is installed only inside WSL, set the command override as a JSON array:

```cmd
set DECKFLOW_LLM_PROVIDER=claude
set DECKFLOW_LLM_CLI_COMMAND=["wsl.exe","claude","-p","{instruction}","--output-format","json","--allowedTools",""]
dotnet run --project DeckFlow.CLI -- distill --db artifacts/uat-content-kb.db
```

PowerShell equivalent:

```powershell
$env:DECKFLOW_LLM_PROVIDER = "claude"
$env:DECKFLOW_LLM_CLI_COMMAND = '["wsl.exe","claude","-p","{instruction}","--output-format","json","--allowedTools",""]'
dotnet run --project DeckFlow.CLI -- distill --db artifacts/uat-content-kb.db
```

If `claude` is installed natively on Windows as `claude.cmd`, use `cmd.exe /c` because `UseShellExecute=false` does not run `.cmd` shims directly:

```cmd
set DECKFLOW_LLM_PROVIDER=claude
set DECKFLOW_LLM_CLI_COMMAND=["cmd.exe","/c","claude.cmd","-p","{instruction}","--output-format","json","--allowedTools",""]
dotnet run --project DeckFlow.CLI -- distill --db artifacts/uat-content-kb.db
```

The Windows auto-detect path throws a clear error without this override, because this machine's Windows `PATH` does not include `claude`.

## Windows dotnet.exe launched from WSL

This is the hard case: from a WSL shell, running Windows `/mnt/c/Program Files/dotnet/dotnet.exe` starts DeckFlow as a Windows process. The child `claude` process then inherits the Windows `dotnet.exe` environment, not the WSL bash `PATH`. A bare `claude` is not found even if `claude` works in the WSL shell.

The owner of `PATH` and env is the process that launched DeckFlow.CLI:

- native WSL `dotnet`: child process uses WSL env and WSL `PATH`.
- Windows `dotnet.exe`: child process uses Windows env and Windows `PATH`.

Set the provider and command inside the Windows-side launcher, and call back into WSL explicitly:

```bash
powershell.exe -NoProfile -Command "\$env:DECKFLOW_LLM_PROVIDER='claude'; \$env:DECKFLOW_LLM_CLI_COMMAND='[\"wsl.exe\",\"claude\",\"-p\",\"{instruction}\",\"--output-format\",\"json\",\"--allowedTools\",\"\"]'; & 'C:\Program Files\dotnet\dotnet.exe' run --project DeckFlow.CLI -- distill --db artifacts/uat-content-kb.db"
```

For a dry run:

```bash
powershell.exe -NoProfile -Command "\$env:DECKFLOW_LLM_PROVIDER='claude'; \$env:DECKFLOW_LLM_CLI_COMMAND='[\"wsl.exe\",\"claude\",\"-p\",\"{instruction}\",\"--output-format\",\"json\",\"--allowedTools\",\"\"]'; & 'C:\Program Files\dotnet\dotnet.exe' run --project DeckFlow.CLI -- distill --db artifacts/uat-content-kb.db --dry-run"
```

Confirm the resolved WSL process can reach `~/.claude/.credentials.json`:

```bash
wsl.exe claude -p "say OK" --output-format json --allowedTools ""
```

If you run a native WSL `dotnet`, the override may use an absolute WSL binary path as element 0 instead, for example `["/home/clunt/.local/bin/claude","-p","{instruction}","--output-format","json","--allowedTools",""]`.

This mirrors the project memory `reference_wsl_dotnet_env_secret`: env vars exported in WSL bash do not reliably reach Windows `dotnet.exe`; set env in the Windows-side launcher.

## DECKFLOW_LLM_CLI_COMMAND

`DECKFLOW_LLM_CLI_COMMAND` is a JSON ARRAY of strings, not a space-separated command.

Rules:

- element 0 is the executable file name.
- remaining elements become `ArgumentList` entries verbatim.
- empty strings are preserved as empty args; this is how `--allowedTools ""` is expressed.
- exactly one element must be `{instruction}`.
- `{instruction}` is substituted in place with the per-extraction instruction; it is not appended.
- a non-JSON value, empty array, missing `{instruction}`, or duplicate `{instruction}` fails fast before any process is spawned.

The JSON array matters because a space split cannot reliably preserve the empty `--allowedTools ""` argument and cannot safely carry paths with spaces.

Every claude override must include:

```json
["wsl.exe","claude","-p","{instruction}","--output-format","json","--allowedTools",""]
```

or the equivalent native Windows shim:

```json
["cmd.exe","/c","claude.cmd","-p","{instruction}","--output-format","json","--allowedTools",""]
```

## Timeout

`DECKFLOW_LLM_CLI_TIMEOUT_SECONDS` optionally overrides the per-extraction timeout. The default is 10 minutes. On timeout, DeckFlow kills the child process tree and marks the video failed so a later run can retry it.

## Auth

Claude uses the operator's OAuth subscription session, usually `~/.claude/.credentials.json`. Do not use `--bare` for this backend, and do not set an API key for the normal subscription path.

Starting June 15, 2026, `claude -p` usage on subscription plans draws from Claude's monthly Agent SDK credit, separate from interactive usage. If auth succeeds but the CLI reports a billing failure after that date, check the subscription's Agent SDK credit.

## Ledger behavior

For `openai`, `DECKFLOW_LLM_MONTHLY_CAP_USD` still governs the spend ledger cap gate and token pricing.

For provider values other than `openai`, including `claude`, DeckFlow bypasses the LLM spend ledger cap gate and bypasses OpenAI token pricing math. It still writes LLM call ledger rows with `cost_usd = 0` and completes the harvest run with `spend_usd = 0` so the run remains auditable.

## Security

Treat transcripts as untrusted input. DeckFlow writes transcript text to stdin only; never put transcript text in `DECKFLOW_LLM_CLI_COMMAND` or any command arguments.

The claude command uses `--allowedTools ""` so no tools are enabled. Output is parsed as the claude JSON envelope, then shape-validated and tag-allowlist constrained. Logs do not include prompts, transcripts, stdout, or full command lines; failures include only sanitized status and a bounded stderr tail.
