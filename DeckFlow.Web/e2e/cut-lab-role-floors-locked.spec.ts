import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { expandCutLabSection } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';
import { cutLabPool } from './fixtures/cut-lab-pool';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;
let heldLock: LockHandle | null = null;

const floorRows = (page: Page) => page.locator('table[data-cut-lab-role-floors-table] tbody tr');
const lockedCounts = (page: Page) => floorRows(page).evaluateAll((rows) => rows.map((row) => ({
  role: row.getAttribute('data-cut-lab-floor-row')!,
  count: Number.parseInt(row.querySelector('[data-cut-lab-floor-locked-label]')?.textContent ?? '', 10),
})));

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
  await page.goto('/cut-lab');
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(cutLabPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expandCutLabSection(page, 'cut-lab-section-role-floors');
  await expect(page.locator('table[data-cut-lab-role-floors-table]')).toBeVisible();
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('Locked follows In pool and every role displays a locked count', async ({ page }) => {
  const table = page.locator('table[data-cut-lab-role-floors-table]');
  const headers = await table.locator('thead th').allTextContents();
  const poolIndex = headers.findIndex((header) => header.trim() === 'In pool');
  expect(poolIndex).toBeGreaterThanOrEqual(0);
  expect(headers[poolIndex + 1].trim()).toBe('Locked');
  const rows = floorRows(page);
  expect(await rows.count()).toBeGreaterThan(0);
  for (const row of await rows.all()) {
    await expect(row.locator('td[data-label="Locked"] span[data-cut-lab-floor-locked-label]')).toHaveText(/^\d+ locked$/);
  }
});

test('locking and unlocking a pool card updates every matching role by its quantity', async ({ page }) => {
  const before = await lockedCounts(page);
  const cards = page.locator('tr[data-cut-lab-card][data-cut-lab-commander="false"]');
  const candidates = await cards.evaluateAll((rows) => rows.map((row, index) => ({
    index,
    roles: (row.getAttribute('data-cut-lab-role') ?? '').split(/\s+/).filter(Boolean),
    quantity: Number(row.getAttribute('data-cut-lab-quantity')),
    available: !!row.querySelector('input[data-cut-lab-lock-card]:enabled:not(:checked)'),
  })));
  // Prefer a multi-copy card so this catches implementations that count cards instead of copies.
  const card = candidates.filter((item) => item.available && item.roles.length > 0)
    .sort((a, b) => b.quantity - a.quantity)[0];
  expect(card).toBeDefined();
  expect(card.quantity).toBeGreaterThan(1);
  expect(before.some((row) => card.roles.includes(row.role))).toBe(true);
  await page.locator('[role="tab"][aria-controls="cut-lab-step-panel-1"]').click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  const checkbox = cards.nth(card.index).locator('input[data-cut-lab-lock-card]');
  await checkbox.check();
  await expect(checkbox).toBeChecked();
  await expect.poll(() => lockedCounts(page)).toEqual(before.map((row) => ({
    ...row,
    count: row.count + (card.roles.includes(row.role) ? card.quantity : 0),
  })));
  await checkbox.uncheck();
  await expect(checkbox).not.toBeChecked();
  await expect.poll(() => lockedCounts(page)).toEqual(before);
});

test('commander roles contribute locked copies before any user lock', async ({ page }) => {
  const commander = page.locator('tr[data-cut-lab-card][data-cut-lab-commander="true"]');
  await expect(commander).toHaveCount(1);
  const roles = (await commander.getAttribute('data-cut-lab-role') ?? '').split(/\s+/).filter(Boolean);
  expect(roles.length).toBeGreaterThan(0);
  const checkbox = commander.locator('input[data-cut-lab-lock-card]');
  await expect(checkbox).toBeChecked();
  await expect(checkbox).toBeDisabled();
  const counts = await lockedCounts(page);
  expect(counts.reduce((sum, row) => sum + row.count, 0)).toBeGreaterThanOrEqual(1);
  const quantity = Number(await commander.getAttribute('data-cut-lab-quantity'));
  for (const role of roles) {
    expect(counts.find((row) => row.role === role)?.count).toBeGreaterThanOrEqual(quantity);
  }
});

test('Role floors Lock all stays synchronized with the chip-area role button', async ({ page }) => {
  const floor = page.locator('[data-cut-lab-floor-lock-role="lands"]');
  await expect(floor).toHaveText('Lock all');
  await expect(floor).toHaveAttribute('aria-pressed', 'false');
  await floor.click();
  await expect(floor).toHaveText('Unlock all');
  await expect(floor).toHaveAttribute('aria-pressed', 'true');
  const chip = page.locator('#cut-lab-section-competes [data-cut-lab-lock-role="lands"]');
  await expect(chip).toHaveAttribute('aria-pressed', 'true');
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expandCutLabSection(page, 'cut-lab-section-competes');
  const lands = page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' });
  if (!(await lands.evaluate((element) => (element as HTMLDetailsElement).open))) {
    await lands.locator(':scope > summary').click();
  }
  await chip.click();
  await expect(chip).toHaveAttribute('aria-pressed', 'false');
  await expect(floor).toHaveText('Lock all');
  await expect(floor).toHaveAttribute('aria-pressed', 'false');
});
