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
  const allFlags = await dataRows.evaluateAll((rows) =>
    rows.map((row) => ({
      key: row.getAttribute('data-flag-key') ?? '',
      enabled: row.getAttribute('data-flag-enabled') === 'true',
    })));
  const enabledKeys = allFlags.filter((flag) => flag.enabled).map((flag) => flag.key);
  const disabledKeys = allFlags.filter((flag) => !flag.enabled).map((flag) => flag.key);
  const serviceKeys = allFlags.filter((flag) => flag.key.startsWith('service.')).map((flag) => flag.key);
  const disabledServiceKeys = allFlags
    .filter((flag) => !flag.enabled && flag.key.startsWith('service.'))
    .map((flag) => flag.key);

  await page.getByLabel('Filter by key prefix, e.g. analysis.').fill('service.');
  const visibleServiceKeys = await getVisibleFlagKeys(page);
  expect(visibleServiceKeys.length).toBeGreaterThan(0);
  expect(visibleServiceKeys.every((key) => key.startsWith('service.'))).toBe(true);
  await expect(page.locator('#flag-filter-count')).toHaveText(`${visibleServiceKeys.length} of ${total} flags shown`);

  await page.getByLabel('Filter by key prefix, e.g. analysis.').fill('');
  const analysisChip = page.getByRole('button', { name: 'analysis', exact: true });
  await analysisChip.click();
  await expect(analysisChip).toHaveAttribute('aria-pressed', 'true');
  const visibleAnalysisKeys = await getVisibleFlagKeys(page);
  expect(visibleAnalysisKeys.length).toBeGreaterThan(0);
  expect(visibleAnalysisKeys.every((key) => key.startsWith('analysis.'))).toBe(true);

  const allChip = page.getByRole('button', { name: 'All', exact: true });
  await allChip.click();
  await expect(allChip).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('tr[data-flag-key]')).toHaveCount(total);
  expect(await getVisibleFlagKeys(page)).toHaveLength(total);

  const enabledChip = page.getByRole('button', { name: 'Enabled', exact: true });
  await enabledChip.click();
  await expect(enabledChip).toHaveAttribute('aria-pressed', 'true');
  const visibleEnabledKeys = await getVisibleFlagKeys(page);
  expect(visibleEnabledKeys).toEqual(enabledKeys);

  const disabledChip = page.getByRole('button', { name: 'Disabled', exact: true });
  await disabledChip.click();
  await expect(disabledChip).toHaveAttribute('aria-pressed', 'true');
  const visibleDisabledKeys = await getVisibleFlagKeys(page);
  expect(visibleDisabledKeys).toEqual(disabledKeys);

  const serviceChip = page.getByRole('button', { name: 'service', exact: true });
  await serviceChip.click();
  await expect(serviceChip).toHaveAttribute('aria-pressed', 'true');
  const visibleDisabledServiceKeys = await getVisibleFlagKeys(page);
  expect(visibleDisabledServiceKeys).toEqual(disabledServiceKeys);
  if (disabledServiceKeys.length === 0) {
    await expect(page.locator('#flag-filter-empty')).toBeVisible();
  }
  await expect(page.locator('#flag-filter-count')).toHaveText(`${visibleDisabledServiceKeys.length} of ${total} flags shown`);

  const allStatusesChip = page.getByRole('button', { name: 'All statuses', exact: true });
  await allStatusesChip.click();
  await expect(allStatusesChip).toHaveAttribute('aria-pressed', 'true');
  const visibleServiceKeysAfterReset = await getVisibleFlagKeys(page);
  expect(visibleServiceKeysAfterReset).toEqual(serviceKeys);

  await allChip.click();
  await expect(allChip).toHaveAttribute('aria-pressed', 'true');
  expect(await getVisibleFlagKeys(page)).toHaveLength(total);

  await page.getByLabel('Filter by key prefix, e.g. analysis.').fill('zzz');
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
