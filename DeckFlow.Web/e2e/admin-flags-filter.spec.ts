import { expect, test, type Page } from '@playwright/test';
import { open, unlink } from 'node:fs/promises';

const adminUser = process.env.FEEDBACK_ADMIN_USER ?? 'admin';
const adminPassword = process.env.FEEDBACK_ADMIN_PASSWORD ?? 'changeme-local';
const basicAuthHeader = `Basic ${Buffer.from(`${adminUser}:${adminPassword}`).toString('base64')}`;
const adminLockPath = '/tmp/deckflow-admin-e2e.lock';
const adminLockTimeoutMs = 90_000;

type LockHandle = Awaited<ReturnType<typeof open>>;

let heldLock: LockHandle | null = null;

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

test.afterEach(async () => {
  await releaseAdminLock(heldLock);
  heldLock = null;
});

test('admin flags supports instant prefix filtering and namespace chips', async ({ page }) => {
  const response = await page.goto('/Admin/Flags');
  expect(response?.ok()).toBeTruthy();

  const dataRows = page.locator('tr[data-flag-key]');
  const total = await dataRows.count();
  expect(total).toBeGreaterThan(1);

  await page.getByLabel('Filter by key prefix, e.g. tool.').fill('service.');
  const visibleServiceKeys = await getVisibleFlagKeys(page);
  expect(visibleServiceKeys.length).toBeGreaterThan(0);
  expect(visibleServiceKeys.every((key) => key.startsWith('service.'))).toBe(true);
  await expect(page.locator('#flag-filter-count')).toHaveText(`${visibleServiceKeys.length} of ${total} flags shown`);

  await page.getByLabel('Filter by key prefix, e.g. tool.').fill('');
  const toolChip = page.getByRole('button', { name: 'tool', exact: true });
  await toolChip.click();
  await expect(toolChip).toHaveAttribute('aria-pressed', 'true');
  const visibleToolKeys = await getVisibleFlagKeys(page);
  expect(visibleToolKeys.length).toBeGreaterThan(0);
  expect(visibleToolKeys.every((key) => key.startsWith('tool.'))).toBe(true);

  const allChip = page.getByRole('button', { name: 'All', exact: true });
  await allChip.click();
  await expect(allChip).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('tr[data-flag-key]')).toHaveCount(total);
  expect(await getVisibleFlagKeys(page)).toHaveLength(total);

  await page.getByLabel('Filter by key prefix, e.g. tool.').fill('zzz');
  await expect(page.locator('#flag-filter-empty')).toBeVisible();
  expect(await getVisibleFlagKeys(page)).toHaveLength(0);
});

test('admin flags stays within the viewport at mobile width', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 900 });

  const response = await page.goto('/Admin/Flags');
  expect(response?.ok()).toBeTruthy();

  const hasNoOverflow = await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1);
  expect(hasNoOverflow).toBeTruthy();
});

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

async function getVisibleFlagKeys(page: Page): Promise<string[]> {
  return page.locator('tr[data-flag-key]').evaluateAll((rows) =>
    rows
      .filter((row) => !row.classList.contains('hidden'))
      .map((row) => row.getAttribute('data-flag-key') ?? ''));
}
