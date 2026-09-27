import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;
type Tool = { label: string; key: string; enabled: boolean };
let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });
test.beforeEach(async ({ page }) => { heldLock = await acquireAdminLockForTest(page); });
test.afterEach(async () => { await releaseAdminLockForTest(heldLock); heldLock = null; });

const visibleLabels = async (page: Page): Promise<string[]> => page.locator('tr[data-tool-label]:not(.hidden)').evaluateAll((rows) => rows.map((row) => row.getAttribute('data-tool-label') ?? ''));
const matches = (tools: Tool[], query: string, enabled?: boolean): string[] => tools.filter((tool) => (enabled === undefined || tool.enabled === enabled) && (tool.label.toLowerCase().startsWith(query.toLowerCase()) || tool.key.toLowerCase().startsWith(query.toLowerCase()))).map((tool) => tool.label);
const toolsOnPage = async (page: Page): Promise<Tool[]> => page.locator('tr[data-tool-label]').evaluateAll((rows) => rows.map((row) => ({ label: row.getAttribute('data-tool-label') ?? '', key: row.getAttribute('data-tool-flag-key') ?? '', enabled: row.getAttribute('data-tool-enabled') === 'true' })));

test('admin tools filters rows by name or flag key prefix', async ({ page }) => {
  const response = await page.goto('/Admin/Tools');
  expect(response?.ok()).toBeTruthy();
  const tools = await toolsOnPage(page);
  expect(tools.length).toBeGreaterThan(1);
  const filter = page.getByLabel('Filter by tool name or flag key prefix');
  await filter.fill('cut');
  expect(await visibleLabels(page)).toEqual(matches(tools, 'cut'));
  expect(await visibleLabels(page)).toContain('Cut Lab');
  await expect(page.locator('#tools-filter-count')).toHaveText(`${matches(tools, 'cut').length} of ${tools.length} tools shown`);
  await filter.fill('CUT'); expect(await visibleLabels(page)).toEqual(matches(tools, 'CUT'));
  await filter.fill('lab'); expect(await visibleLabels(page)).toEqual(matches(tools, 'lab')); expect(await visibleLabels(page)).not.toContain('Cut Lab');
  await filter.fill('tool.cut'); expect(await visibleLabels(page)).toContain('Cut Lab');
  await filter.fill(''); expect(await visibleLabels(page)).toHaveLength(tools.length);
});

test('admin tools status chips compose with search, show an empty state, and persist across reload', async ({ page }) => {
  await page.goto('/Admin/Tools');
  const tools = await toolsOnPage(page);
  await page.getByRole('button', { name: 'Enabled', exact: true }).click();
  expect(await visibleLabels(page)).toEqual(matches(tools, '', true));
  await page.getByRole('button', { name: 'Disabled', exact: true }).click();
  expect(await visibleLabels(page)).toEqual(matches(tools, '', false));
  const filter = page.getByLabel('Filter by tool name or flag key prefix');
  await filter.fill('c'); expect(await visibleLabels(page)).toEqual(matches(tools, 'c', false));
  await filter.fill('zzz'); await expect(page.locator('#tools-filter-empty')).toBeVisible(); await expect(page.locator('.admin-tools__section')).toHaveCount(4); expect(await page.locator('.admin-tools__section.hidden').count()).toBe(4);
  await filter.fill('cut'); await page.reload(); await expect(filter).toHaveValue('cut'); await expect(page.getByRole('button', { name: 'Disabled', exact: true })).toHaveAttribute('aria-pressed', 'true'); expect(await visibleLabels(page)).toEqual(matches(tools, 'cut', false));
  await page.getByRole('button', { name: 'All statuses', exact: true }).click(); await filter.fill(''); expect(await visibleLabels(page)).toHaveLength(tools.length);
});

test('admin tools stays within the viewport at mobile width', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await page.goto('/Admin/Tools');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
