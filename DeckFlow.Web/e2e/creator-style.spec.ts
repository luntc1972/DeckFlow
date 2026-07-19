import { expect, test, type Page } from '@playwright/test';
import { open, unlink } from 'node:fs/promises';

const adminUser = process.env.FEEDBACK_ADMIN_USER ?? 'admin';
const adminPassword = process.env.FEEDBACK_ADMIN_PASSWORD ?? 'changeme-local';
const adminToolsUrl = `http://${adminUser}:${adminPassword}@localhost:5173/Admin/Tools`;
const basicAuthHeader = `Basic ${Buffer.from(`${adminUser}:${adminPassword}`).toString('base64')}`;
const adminLockPath = '/tmp/deckflow-admin-e2e.lock';
const adminLockTimeoutMs = 90_000;
const creatorStyleLabel = 'Creator-Style Critique';

type LockHandle = Awaited<ReturnType<typeof open>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  await page.setExtraHTTPHeaders({
    Authorization: basicAuthHeader,
    'CF-Connecting-IP': getAdminForwardedIp(),
  });
  heldLock = await acquireAdminLock();
  await setToolEnabled(page, creatorStyleLabel, false);
});

test.afterEach(async ({ page }) => {
  try {
    await setToolEnabled(page, creatorStyleLabel, false);
  } finally {
    await releaseAdminLock(heldLock);
    heldLock = null;
  }
});

test('creator-style route returns 404 while the flag is off', async ({ page }) => {
  const response = await page.goto('/creator-style');
  expect(response?.status()).toBe(404);
});

test('creator-style route returns the empty-store info state while the flag is on', async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') {
      consoleErrors.push(message.text());
    }
  });

  await setToolEnabled(page, creatorStyleLabel, true);

  const response = await page.goto('/creator-style');
  expect(response?.ok()).toBeTruthy();

  await expect(page.locator('.info-banner')).toContainText('No creator profiles loaded yet.');
  await expect(page.locator('form[action="/creator-style"]')).toHaveCount(0);
  expect(consoleErrors).toEqual([]);
});

function getAdminForwardedIp(): string {
  const info = test.info();
  const key = `${info.project.name}:${info.file}:${info.title}:${info.retry}`;
  let hash = 0;
  for (const char of key) {
    hash = (hash * 31 + char.charCodeAt(0)) % 200;
  }

  return `203.0.113.${hash + 1}`;
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

async function gotoAdminTools(page: Page): Promise<void> {
  const response = await page.goto(adminToolsUrl);
  expect(response?.ok()).toBeTruthy();
}

async function setToolEnabled(page: Page, label: string, enabled: boolean): Promise<void> {
  await gotoAdminTools(page);
  const row = page.locator('tbody tr').filter({
    has: page.locator('td[data-label="Tool"] span', { hasText: label }),
  });
  const status = row.locator('[data-label="Status"]');
  const currentStatus = (await status.textContent())?.trim();
  const desiredStatus = enabled ? 'On' : 'Off';

  if (currentStatus === desiredStatus) {
    return;
  }

  await row.getByRole('button', { name: enabled ? 'Enable' : 'Disable', exact: true }).click();
  await expect(page.locator('.admin-banner--success')).toContainText(`Tool '${label}' is now ${enabled ? 'enabled' : 'disabled'}.`);
  await expect(row.locator('[data-label="Status"]')).toHaveText(desiredStatus);
}
