import { expect, test, type Locator, type Page } from '@playwright/test';
import { open, unlink } from 'node:fs/promises';

// Smoke for Phase 73: the deck-analysis Step 1 companion designator input is gated on the
// analysis.command-zone-awareness flag. When the flag is ON the single input[name="CompanionName"]
// renders; when OFF it is absent and the page is unchanged.
//
// Start the app with scripts/run-web-test.sh first (sets DECKFLOW_DISABLE_AUTO_BROWSER=true so no
// Windows-host browser is opened), then from DeckFlow.Web/ run:
//   npx --no-install playwright test e2e/deck-analysis-command-zone.spec.ts

const adminUser = process.env.FEEDBACK_ADMIN_USER ?? 'admin';
const adminPassword = process.env.FEEDBACK_ADMIN_PASSWORD ?? 'changeme-local';
const basicAuthHeader = `Basic ${Buffer.from(`${adminUser}:${adminPassword}`).toString('base64')}`;
const adminLockPath = '/tmp/deckflow-admin-e2e.lock';
const adminLockTimeoutMs = 90_000;
const commandZoneFlagKey = 'analysis.command-zone-awareness';

type LockHandle = Awaited<ReturnType<typeof open>>;

let heldLock: LockHandle | null = null;

// /Admin/* specs share one SQLite flag store + a single forwarded-IP throttle, so they must run
// serially behind the shared lock to avoid cross-spec flakes.
test.describe.configure({ mode: 'serial' });

function getAdminForwardedIp(): string {
  const info = test.info();
  const key = `${info.project.name}:${info.file}:${info.title}:${info.retry}`;
  let hash = 0;
  for (const char of key) {
    hash = (hash * 31 + char.charCodeAt(0)) % 200;
  }

  return `203.0.113.${hash + 1}`;
}

test.beforeEach(async ({ page }) => {
  await page.setExtraHTTPHeaders({
    Authorization: basicAuthHeader,
    'CF-Connecting-IP': getAdminForwardedIp(),
  });
  heldLock = await acquireAdminLock();
});

test.afterEach(async ({ page }) => {
  try {
    // Restore the default-OFF state so other specs and the prod default are unaffected.
    await setFlagEnabled(page, commandZoneFlagKey, false);
  } finally {
    await releaseAdminLock(heldLock);
    heldLock = null;
  }
});

test('companion designator renders on deck-analysis Step 1 when the flag is ON', async ({ page }) => {
  await setFlagEnabled(page, commandZoneFlagKey, true);

  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  const companionInput = page.locator('input[name="CompanionName"]');
  await expect(companionInput).toHaveCount(1);

  // The input lives inside a collapsible <details>; open it so visibility holds.
  await page.locator('.deck-analysis-overrides > summary').click();
  await expect(companionInput).toBeVisible();
});

test('companion designator is absent on deck-analysis Step 1 when the flag is OFF', async ({ page }) => {
  await setFlagEnabled(page, commandZoneFlagKey, false);

  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  await expect(page.locator('input[name="CompanionName"]')).toHaveCount(0);
  await expect(page.locator('.deck-analysis-overrides')).toHaveCount(0);
});

async function setFlagEnabled(page: Page, key: string, enabled: boolean): Promise<void> {
  const response = await page.goto('/Admin/Flags');
  expect(response?.ok()).toBeTruthy();

  const row = getFlagRow(page, key);
  const status = row.locator('[data-label="Status"]');
  const currentStatus = (await status.textContent())?.trim();
  const desiredStatus = enabled ? 'On' : 'Off';
  if (currentStatus === desiredStatus) {
    return;
  }

  await row.getByRole('button', { name: enabled ? 'Enable' : 'Disable', exact: true }).click();
  await expect(page.locator('.admin-banner--success')).toBeVisible();
  await expect(getFlagRow(page, key).locator('[data-label="Status"]')).toHaveText(desiredStatus);
}

function getFlagRow(page: Page, key: string): Locator {
  return page.locator(`tr[data-flag-key="${key}"]`);
}

async function acquireAdminLock(): Promise<LockHandle> {
  const startedAt = Date.now();

  while (Date.now() - startedAt < adminLockTimeoutMs) {
    try {
      const handle = await open(adminLockPath, 'wx');
      await handle.writeFile(`${process.pid}\n`);
      return handle;
    } catch (error: unknown) {
      const code = typeof error === 'object' && error !== null && 'code' in error ? String(error.code) : '';
      if (code !== 'EEXIST') {
        throw error;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, 250));
  }

  throw new Error(`Timed out waiting for admin e2e lock at ${adminLockPath}`);
}

async function releaseAdminLock(handle: LockHandle | null): Promise<void> {
  if (!handle) {
    return;
  }

  try {
    await handle.close();
  } finally {
    try {
      await unlink(adminLockPath);
    } catch (error: unknown) {
      const code = typeof error === 'object' && error !== null && 'code' in error ? String(error.code) : '';
      if (code !== 'ENOENT') {
        throw error;
      }
    }
  }
}
