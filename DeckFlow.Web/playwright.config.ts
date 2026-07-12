import { existsSync } from 'node:fs';
import { defineConfig, devices } from '@playwright/test';

const windowsDotnetPath = '/mnt/c/Program Files/dotnet/dotnet.exe';
const dotnetCommand = existsSync(windowsDotnetPath) ? `"${windowsDotnetPath}"` : 'dotnet';
const reuseExistingServer = !process.env.CI || Boolean(process.env.WSL_DISTRO_NAME);

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    // Force headless so a local WSL run never surfaces a browser window on the Windows host via WSLg.
    headless: true,
    baseURL: 'http://localhost:5173',
    httpCredentials: {
      username: process.env.FEEDBACK_ADMIN_USER ?? 'admin',
      password: process.env.FEEDBACK_ADMIN_PASSWORD ?? 'changeme-local',
      // Send Basic auth proactively (not only after a 401 challenge) so the
      // admin pages don't race the challenge round-trip under parallel workers.
      send: 'always',
    },
  },
  projects: [
    {
      name: 'chromium-desktop',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1280, height: 900 },
      },
    },
    {
      name: 'chromium-mobile',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 390, height: 844 },
      },
    },
  ],
  // NOTE: WSL verification runs start the app first via scripts/run-web-test.sh and
  // then execute Playwright with CI=1 to mirror CI retries/parallelism. Detect
  // WSL so those local CI-mode runs still reuse the already-running headless
  // server, while real CI keeps owning server startup itself.
  webServer: {
    // Use the http-no-browser launch profile, NOT http. The app's Development
    // auto-open-browser is gated on DECKFLOW_DISABLE_AUTO_BROWSER, but a var set
    // in this `env` block does NOT cross the WSL→Windows boundary into the
    // Windows dotnet.exe this command spawns (verified: cmd.exe sees it unset),
    // so a plain `--launch-profile http` run pops a Windows Chrome. The
    // http-no-browser profile bakes DECKFLOW_DISABLE_AUTO_BROWSER=true into the
    // profile's environmentVariables, which `dotnet run` applies in-process
    // Windows-side — the only reliable suppression across WSL interop. The env
    // block below is kept as belt-and-suspenders for native-Linux/CI runs.
    command: `${dotnetCommand} run --launch-profile http-no-browser`,
    url: 'http://localhost:5173',
    reuseExistingServer,
    timeout: 120_000,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      DECKFLOW_DISABLE_AUTO_BROWSER: 'true',
      FEEDBACK_ADMIN_USER: 'admin',
      FEEDBACK_ADMIN_PASSWORD: 'changeme-local',
    },
  },
});
