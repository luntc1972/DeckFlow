# Phase 28: Housekeeping Bundle — Pattern Map

**Mapped:** 2026-06-04
**Files analyzed:** 5 (1 code, 2 tests, 1 CLI utility, 1 planning-doc batch)
**Analogs found:** 5 / 5

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Core/Integration/CliLlmDistillationService.cs` (modify — add codex branch to `BuildCommandSpec`) | service | request-response (CLI subprocess) | self — existing claude branch in `BuildCommandSpec` (lines 106–128) | exact |
| `DeckFlow.Core/Integration/LlmDistillationProviderFactory.cs` (modify — replace `NotSupportedException` stub) | utility/factory | request-response | self — existing claude branch (lines 44–47); also `LlmDistillationProviderFactoryTests.cs` shows expected test shape | exact |
| `DeckFlow.Core.Tests/CliLlmDistillationServiceTests.cs` (modify — add codex-provider and sentinel-exfil tests) | test | request-response | self — existing `CreateService`, `WithCommandOverrideAsync`, `ClaudeEnvelope` helpers (lines 341–390) | exact |
| `DeckFlow.Core.Tests/LlmDistillationProviderFactoryTests.cs` (modify — add `Resolve_Codex_ReturnsCliImpl`, update stale test) | test | request-response | self — existing `Resolve_Claude_ReturnsCliImpl` test (lines 27–35) | exact |
| `DeckFlow.CLI/CommandRunners.cs` (modify — `ResolveContentKbArtifactRoot` default path, D-11) | utility | file-I/O | self — `ResolveContentKbDatabasePath` (line 1653) and `ResolveContentKbArtifactRoot` (lines 1656–1668) | exact |
| `.planning/milestones/v1.4-phases/*/XX-VERIFICATION.md` (create ×7 retro docs, HSK-03) | planning doc | n/a | `27-VERIFICATION.md` frontmatter + table structure | exact |
| `.planning/milestones/v1.4-phases/26-*/26-0{1,2}-SUMMARY.md` (create ×2 retro SUMMARYs, HSK-04) | planning doc | n/a | `21.2-01-SUMMARY.md` frontmatter + body | exact |
| `.planning/milestones/v1.4-phases/24-*/24-SUMMARY.md` + `24-VERIFICATION.md` (create retro, HSK-04) | planning doc | n/a | `21.2-01-SUMMARY.md` + `27-VERIFICATION.md` | exact |

---

## Pattern Assignments

### `DeckFlow.Core/Integration/LlmDistillationProviderFactory.cs` (factory, request-response)

**Analog:** self — existing claude branch

**Current stub to replace** (`LlmDistillationProviderFactory.cs` lines 49–53):
```csharp
if (string.Equals(provider, CodexProvider, StringComparison.OrdinalIgnoreCase))
{
    throw new NotSupportedException(
        "The codex LLM distillation provider is deferred to Phase 21.3 / KB-12 and is not yet supported.");
}
```

**Pattern to copy from — claude branch** (lines 44–47):
```csharp
if (string.Equals(provider, ClaudeProvider, StringComparison.OrdinalIgnoreCase))
{
    return new CliLlmDistillationService(ClaudeProvider);
}
```

**Replacement pattern (codex branch):**
```csharp
if (string.Equals(provider, CodexProvider, StringComparison.OrdinalIgnoreCase))
{
    return new CliLlmDistillationService(CodexProvider);
}
```

**Error message update — last throw** (line 56, update `"claude"` list entry):
```csharp
throw new NotSupportedException(
    $"Unsupported {EnvironmentVariableName} '{provider}'. Supported: openai, claude, codex.");
```

---

### `DeckFlow.Core/Integration/CliLlmDistillationService.cs` (service, CLI subprocess)

**Analog:** self — claude branch of `BuildCommandSpec` (lines 106–128)

**Guard to extend — provider check** (lines 106–108):
```csharp
if (!string.Equals(_provider, ClaudeProvider, StringComparison.OrdinalIgnoreCase))
{
    throw new NotSupportedException($"Unsupported CLI distillation provider '{_provider}'.");
}
```
This hard-coded single-provider guard must become a two-branch conditional (claude → existing path; codex → new codex command spec). Both share the same `BuildOverrideCommandSpec` override-parsing path (already envelope-agnostic since it always sets `CliEnvelopeKind.ClaudeJson` at line 174 — the codex branch sets `CliEnvelopeKind.Raw` instead).

**Codex command spec shape — copy from claude branch** (lines 117–123, adapt):
```csharp
// Claude (existing, CliEnvelopeKind.ClaudeJson):
return new CliCommandSpec(
    "claude",
    ["-p", instruction, "--output-format", "json", "--allowedTools", string.Empty],
    CliEnvelopeKind.ClaudeJson);

// Codex (new, CliEnvelopeKind.Raw — model text arrives on raw stdout):
// Exact arguments decided by researcher (D-02); use Raw envelope, sandbox flags per KB-12.
return new CliCommandSpec(
    "codex",
    [/* researcher-confirmed isolation flags */, instruction],
    CliEnvelopeKind.Raw);
```

**`ExtractModelText` already handles `Raw`** (lines 271–275) — no change needed:
```csharp
if (kind == CliEnvelopeKind.Raw)
{
    return stdout.Trim();
}
```

**Model env-var pattern — copy from `CliTimeoutEnvironmentKey`** (lines 13–14, 389–403):
```csharp
internal const string CliTimeoutEnvironmentKey = "DECKFLOW_LLM_CLI_TIMEOUT_SECONDS";
// ...
private static TimeSpan ReadTimeout()
{
    var timeoutValue = Environment.GetEnvironmentVariable(CliTimeoutEnvironmentKey);
    if (string.IsNullOrWhiteSpace(timeoutValue))
    {
        return DefaultTimeout;
    }
    if (!double.TryParse(timeoutValue, out var seconds) || seconds <= 0)
    {
        throw new InvalidOperationException($"{CliTimeoutEnvironmentKey} must be a positive number.");
    }
    return TimeSpan.FromSeconds(seconds);
}
```
D-07 requires `DECKFLOW_LLM_CODEX_MODEL` (or equivalent) env var with a mini-tier default. Add a new `internal const string` + a `ReadCodexModel()` helper that follows the same null-check → default pattern.

**Override path — `BuildOverrideCommandSpec` envelope kind** (line 174):
```csharp
return new CliCommandSpec(parts[0], arguments, CliEnvelopeKind.ClaudeJson);
```
When the codex provider uses override, the envelope kind should be `CliEnvelopeKind.Raw`. The override parser must receive the target envelope kind as a parameter rather than hard-coding `ClaudeJson`.

---

### `DeckFlow.Core.Tests/CliLlmDistillationServiceTests.cs` (tests, request-response)

**Analog:** self — existing fake-runner + queue pattern

**Service factory pattern** (lines 341–345) — copy for codex tests, passing `"codex"` as provider:
```csharp
private static CliLlmDistillationService CreateService(Queue<string> stdoutQueue, TimeSpan? timeout = null)
    => new(
        "claude",
        (_, _, _) => Task.FromResult(stdoutQueue.Dequeue()),
        timeout);
```

**Raw-envelope helper** — add alongside `ClaudeEnvelope` (lines 365–371):
```csharp
// Existing: ClaudeEnvelope wraps in {"type":"result","is_error":false,"result":"..."}
private static string ClaudeEnvelope(string result, bool isError = false)
    => JsonSerializer.Serialize(new { type = "result", is_error = isError, result });

// New: Raw — codex stdout is the model text verbatim
private static string RawOutput(string modelText) => modelText;
```

**Override helper pattern** (lines 376–390) — codex tests should use `DECKFLOW_LLM_CODEX_MODEL` override (or `DECKFLOW_LLM_CLI_COMMAND`) following the same env-var set/restore idiom:
```csharp
private static async Task<T> WithCommandOverrideAsync<T>(
    string? overrideValue,
    Func<Task<T>> action)
{
    var previous = Environment.GetEnvironmentVariable(CliLlmDistillationService.CliCommandEnvironmentKey);
    Environment.SetEnvironmentVariable(CliLlmDistillationService.CliCommandEnvironmentKey, overrideValue);
    try { return await action().ConfigureAwait(false); }
    finally { Environment.SetEnvironmentVariable(CliLlmDistillationService.CliCommandEnvironmentKey, previous); }
}
```

**Sentinel exfil test pattern** (D-04) — copy from existing sanitized-error pattern (lines 122–138):
```csharp
// Existing: Summarize_ClaudeIsError_ThrowsSanitizedNoResultBody
// sentinel asserted DoesNotContain in exception.ToString()

// New sentinel exfil test:
// 1. Write a temp file with sentinel content.
// 2. Inject a transcript containing a prompt-injection that tries to cat/read the sentinel.
// 3. Run through the fake runner (seam level — no real codex spawn needed for isolation proof).
// 4. Assert sentinel content DoesNotContain in all outputs (result, exception.ToString()).
// For the live probe (VERIFICATION), run against real codex CLI per D-05.
```

---

### `DeckFlow.Core.Tests/LlmDistillationProviderFactoryTests.cs` (tests, request-response)

**Analog:** self — `Resolve_Claude_ReturnsCliImpl` (lines 27–35)

**Pattern to copy — new `Resolve_Codex_ReturnsCliImpl` test:**
```csharp
[Fact]
public void Resolve_Claude_ReturnsCliImpl()
{
    using var httpClient = new HttpClient();

    var service = LlmDistillationProviderFactory.Resolve("claude", httpClient);

    Assert.IsType<CliLlmDistillationService>(service);
}
```

**Stale test to update** — `Resolve_Codex_ThrowsNotSupportedPointingAt213` (lines 48–56) becomes:
```csharp
[Fact]
public void Resolve_Codex_ReturnsCliImpl()
{
    using var httpClient = new HttpClient();

    var service = LlmDistillationProviderFactory.Resolve("codex", httpClient);

    Assert.IsType<CliLlmDistillationService>(service);
}
```

**Error-message test to update** — `Resolve_Unknown_ThrowsNotSupportedListingSupported` (lines 59–68) should also assert `"codex"` in the message now that codex is supported:
```csharp
Assert.Contains("openai", exception.Message, StringComparison.OrdinalIgnoreCase);
Assert.Contains("claude", exception.Message, StringComparison.OrdinalIgnoreCase);
Assert.Contains("codex", exception.Message, StringComparison.OrdinalIgnoreCase);
```

---

### `DeckFlow.CLI/CommandRunners.cs` — `ResolveContentKbArtifactRoot` (utility, file-I/O)

**Analog:** `ResolveContentKbDatabasePath` (line 1653) — single-line fallback chain

**Current implementation** (lines 1656–1668):
```csharp
private static string ResolveContentKbArtifactRoot(FileInfo? db)
{
    var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
    if (!string.IsNullOrWhiteSpace(dataDir))
    {
        return Path.GetFullPath(Path.Combine(dataDir, "content-kb"));
    }

    var dbPath = ResolveContentKbDatabasePath(db);
    var dbDirectory = Path.GetDirectoryName(Path.GetFullPath(dbPath))
        ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
    return Path.Combine(dbDirectory, "content-kb");
}
```

**D-11 fix target** — the `else` branch (lines 1664–1667) currently resolves relative to the DB file location inside `artifacts/`. Change it to resolve to repo-root `content-kb/` so the CLI default matches the deploy source of truth and drift is impossible:
```csharp
// Replace the else branch with:
return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "content-kb"));
```
`ResolveContentKbDatabasePath` (line 1653) is NOT changed — it stays in `artifacts/`. Only the artifact-root (prompt `.md` files) moves to repo-root `content-kb/`.

---

## Shared Patterns

### Provider string constants
**Source:** `LlmDistillationProviderFactory.cs` lines 13–15
**Apply to:** factory file + any test that asserts provider names
```csharp
private const string OpenAiProvider = "openai";
private const string ClaudeProvider = "claude";
private const string CodexProvider  = "codex";
```

### Env-var read with typed parse + default
**Source:** `CliLlmDistillationService.cs` lines 389–403 (`ReadTimeout`)
**Apply to:** new `ReadCodexModel()` helper for D-07 model configurability
Pattern: read env var → whitespace-null → return constant default → else parse/validate → throw `InvalidOperationException` with key name in message.

### Fake process-runner seam (test isolation)
**Source:** `CliLlmDistillationService.cs` lines 40–48 (internal ctor) + tests lines 341–345
**Apply to:** all new codex seam-level tests in `CliLlmDistillationServiceTests.cs`
```csharp
internal CliLlmDistillationService(
    string provider,
    Func<CliCommandSpec, string, CancellationToken, Task<string>>? processRunnerOverride,
    TimeSpan? timeoutOverride = null)
```
Pass `"codex"` as provider; inject a `Queue<string>`-backed runner returning `RawOutput(...)`. No real codex process spawned in unit tests.

### Env-var set/restore for test isolation
**Source:** `CliLlmDistillationServiceTests.cs` lines 376–390 (`WithCommandOverrideAsync`)
**Apply to:** any new codex tests that need `DECKFLOW_LLM_CLI_COMMAND` or model env var overrides.
Always restore via `finally` block.

### VERIFICATION.md frontmatter + table structure
**Source:** `27-VERIFICATION.md` lines 1–10 (frontmatter) + lines 22–30 (SC table)
**Apply to:** all 7 retro VERIFICATION files (HSK-03)
```yaml
---
phase: <phase-slug>
verified: <ISO-8601 date>
status: passed
score: N/N must-haves verified
overrides_applied: 0
re_verification:
  previous_status: none
  previous_score: n/a
---
```
Add an explicit `retroactive: true` field and `evidence_source:` list per D-08. SC table columns: `#`, `Success Criterion`, `Verdict`, `Evidence`.

### SUMMARY.md frontmatter structure
**Source:** `21.2-01-SUMMARY.md` lines 1–38
**Apply to:** 26-01, 26-02, 24 retro SUMMARYs (HSK-04)
Key fields: `phase`, `plan`, `subsystem`, `tags`, `requires`, `provides`, `affects`, `key-files` (created/modified), `key-decisions`, `patterns-established`, `requirements-completed`, `duration`, `completed`. Add `retroactive: true` and `evidence:` list per D-08/D-12.

---

## No Analog Found

None — all files have close analogs in the codebase or are retro planning docs following established GSD conventions.

---

## Metadata

**Analog search scope:** `DeckFlow.Core/Integration/`, `DeckFlow.Core.Tests/`, `DeckFlow.CLI/`, `.planning/milestones/v1.4-phases/`
**Files scanned:** 9 source/test files + 3 planning doc exemplars
**Pattern extraction date:** 2026-06-04
