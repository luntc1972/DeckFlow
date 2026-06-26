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
    command: `${dotnetCommand} run --launch-profile http`,
    url: 'http://localhost:5173',
    reuseExistingServer,
    timeout: 120_000,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      // Suppress the Development auto-open browser launch — without this the
      // Playwright-spawned server (CI, or a local run with no server already
      // up) pops a Windows browser window. The Program.cs gate keys on this
      // exact var; env here overrides the launch profile.
      DECKFLOW_DISABLE_AUTO_BROWSER: 'true',
      FEEDBACK_ADMIN_USER: 'admin',
      FEEDBACK_ADMIN_PASSWORD: 'changeme-local',
    },
  },
});
