# Plan 21-02 Summary — LLM Spend Ledger

## What was built
A separate, token-based LLM spend ledger (D-05), mirroring `WhisperSpendLedger` but independent (own table, own cap).

- `DeckFlow.Core/Content/ILlmSpendLedger.cs` — `RecordCallAsync` / `GetMonthlyTotalAsync` / `WouldExceedCapAsync` interface (token-based mirror of `IWhisperSpendLedger`).
- `DeckFlow.Core/Content/LlmSpendLedger.cs` — net-new `llm_spend_ledger` table (`CREATE TABLE IF NOT EXISTS`, `ix_llm_spend_month` index); `EnsureSchemaAsync` instantiates `ContentVideoStore` first so the FK parent exists; static `ComputeCostUsd` does exact decimal conversion of input/output token counts (HIGH-1, no estimates).
- `DeckFlow.Core.Tests/LlmSpendLedgerTests.cs` — record/total/cap + `ComputeCostUsd` exact-math coverage against a temp SQLite path.

## Locked cost constants (checkpoint A1/A2, user-confirmed)
- gpt-4o-mini input: $0.15 / 1M tokens
- gpt-4o-mini output: $0.60 / 1M tokens
- Cap env var: `DECKFLOW_LLM_MONTHLY_CAP_USD`, default $15.00 (independent of `DECKFLOW_WHISPER_MONTHLY_CAP_USD`)

## Verification
- Build: `DeckFlow.Core` — 0 warnings, 0 errors (Windows dotnet over WSL).
- Tests: `FullyQualifiedName~LlmSpendLedger` — 6 passed, 0 failed, 0 skipped.

## Deviations
- None in code. Zero new NuGet packages; no ALTER of existing tables; ledger fully independent of Whisper ledger.
- SUMMARY authored by orchestrator (Codex dispatch prompt's strict 3-file scope guard prevented the executor from writing it).

## Follow-ups
- Plan 21-04 consumes `WouldExceedCapAsync` (pre-call cap gate) and `RecordCallAsync` (per-call ledgering immediately after each of the 3 OpenAI calls returns — HIGH-1).

## Commits
- `0ed08f0 feat(content): add llm spend ledger`
- `6b733ca test(content): cover llm spend ledger`
