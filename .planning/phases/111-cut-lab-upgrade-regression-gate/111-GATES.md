# Cut Lab Regression Gates

This is the canonical CLUP-10 gate sequence for the Cut Lab regression slice. Run the commands in this order.

## Hard Constraints

- Never open a Windows browser. `scripts/run-web-test.sh` sets `DECKFLOW_DISABLE_AUTO_BROWSER=true`.
- No `gstack`.
- No new packages.
- Do not set `MTG_DATA_DIR`.

## MED-1 Sequencing Note

The three Wave-1 e2e-bearing plans share port `5173`: `111-01` Task 3, `111-02` Task 3, and `111-03` Task 2.

`scripts/run-web-test.sh` runs `fuser -k 5173/tcp` on startup, so these plans must run sequentially, not concurrently. Prefer reusing an already-running `:5173` server instead of launching a second one; Playwright `reuseExistingServer` is already enabled for WSL.

## Command Sequence

1. TypeScript compile

```bash
cd DeckFlow.Web && node ./node_modules/typescript/bin/tsc -p tsconfig.json --noEmit
```

2. Build

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release
```

3. xUnit Cut Lab filter

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release --no-build --filter "FullyQualifiedName~CutLab"
```

If WSL VSTest is unstable, fallback to:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release
```

4. Vitest Cut Lab suite

```bash
cd DeckFlow.Web && npm test -- cut-lab
```

5. Focused e2e pass

If the server is not already running, start exactly one headless server:

```bash
bash scripts/run-web-test.sh
```

Then reuse that server for the focused Cut Lab Playwright pass:

```bash
cd DeckFlow.Web && env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test e2e/cut-lab-smoke.spec.ts e2e/cut-lab-structure.spec.ts e2e/cut-lab-pill-interactions.spec.ts e2e/cut-lab-scenarios.spec.ts e2e/cut-lab-tuning.spec.ts e2e/cut-lab-whatif.spec.ts e2e/cut-lab-export.spec.ts e2e/cut-lab-nav-themes.spec.ts
```

## Observed Results On 2026-07-24

- `cd DeckFlow.Web && node ./node_modules/typescript/bin/tsc -p tsconfig.json --noEmit`
  - Result: PASS
  - Output: completed cleanly with exit code `0` and no diagnostics.
- `cd DeckFlow.Web && npm test -- cut-lab-combo-package-copy`
  - Result: PASS
  - Output: `ts-tests/cut-lab-combo-package-copy.test.ts` passed; `1` file passed, `1` test passed, duration `7.02s`.
- `cd DeckFlow.Web && npm test -- cut-lab`
  - Result: PASS
  - Output: `13` files passed, `55` tests passed, duration `9.58s`.
- Build command
  - Result: NOT RUN in this task; documented only.
- xUnit Cut Lab filter
  - Result: NOT RUN in this task; documented only.
- Focused e2e command
  - Result: NOT RUN in this task per orchestrator instructions.
