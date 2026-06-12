import { defineConfig, devices } from '@playwright/test';

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
  // NOTE: On local WSL setups `dotnet` is often not on PATH (only `dotnet.exe`),
  // so start the app first via `scripts/run-web-uat.sh` and let reuseExistingServer
  // attach to it; CI launches `dotnet` directly.
  webServer: {
    command: 'dotnet run --launch-profile http',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      FEEDBACK_ADMIN_USER: 'admin',
      FEEDBACK_ADMIN_PASSWORD: 'changeme-local',
    },
  },
});
