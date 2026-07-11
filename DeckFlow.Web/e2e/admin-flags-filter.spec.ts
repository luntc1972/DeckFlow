import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('admin flags supports instant prefix filtering and namespace chips', async ({ page }) => {
  test.skip(!!process.env.CI, 'Flaky under CI admin-lock contention (per-test timeout blown by mutex wait); runs locally. Tracked: .planning/debug/e2e-admin-beforeeach-timeout.md');
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

async function getVisibleFlagKeys(page: Page): Promise<string[]> {
  return page.locator('tr[data-flag-key]').evaluateAll((rows) =>
    rows
      .filter((row) => !row.classList.contains('hidden'))
      .map((row) => row.getAttribute('data-flag-key') ?? ''));
}
