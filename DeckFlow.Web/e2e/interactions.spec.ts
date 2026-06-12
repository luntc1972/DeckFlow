import { expect, test, type Page } from '@playwright/test';

const getVisibleRowCount = async (page: Page): Promise<number> =>
  page.locator('#kb-entries-table tbody tr').evaluateAll((rows) =>
    rows
      .filter((row) => row.id !== 'kb-filter-empty')
      .filter((row) => !(row as HTMLTableRowElement).hidden)
      .length,
  );

test('deck primer exposes bracket and section controls', async ({ page }) => {
  const response = await page.goto('/deck-primer');

  expect(response?.ok()).toBeTruthy();
  expect(await page.locator('script[src*="primer-selection.js"]').count()).toBeGreaterThan(0);
  await expect(page.locator('[data-primer-bracket]')).toBeVisible();
  await expect(page.locator('[data-primer-section-checkbox]').first()).toBeVisible();
});

test('admin content kb filter wires without console errors and narrows rows', async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') {
      consoleErrors.push(message.text());
    }
  });

  const response = await page.goto('/Admin/ContentKb?visibilityFilter=all');

  expect(response?.ok()).toBeTruthy();
  expect(await page.locator('script[src*="admin-modal.js"]').count()).toBeGreaterThan(0);
  expect(await page.locator('script[src*="kb-entry-filter.js"]').count()).toBeGreaterThan(0);
  expect(await page.locator('script[src*="content-kb-admin.js"]').count()).toBeGreaterThan(0);
  await expect(page.locator('#kb-filter-search')).toBeVisible();

  const totalRows = await page.locator('#kb-entries-table tbody tr').count();
  const visibleRowsBefore = await getVisibleRowCount(page);

  await page.locator('#kb-filter-search').fill('zzzznomatch');

  expect(consoleErrors).toEqual([]);

  if (totalRows > 1) {
    await expect.poll(async () => getVisibleRowCount(page)).toBeLessThan(visibleRowsBefore);
  }
});

test('admin content kb delete requires an arm click before submission', async ({ page }) => {
  const response = await page.goto('/Admin/ContentKb?visibilityFilter=all');

  expect(response?.ok()).toBeTruthy();

  const deleteButtons = page.locator('form[data-admin-confirm-twoclick] button.danger');
  const deleteButtonCount = await deleteButtons.count();
  test.skip(deleteButtonCount === 0, 'no KB rows seeded');

  const firstDeleteButton = deleteButtons.first();
  const startingUrl = page.url();

  await firstDeleteButton.click();

  await expect(page).toHaveURL(startingUrl);

  const buttonText = (await firstDeleteButton.textContent())?.trim() ?? '';
  const isArmed = await firstDeleteButton.evaluate((button) => button.classList.contains('is-armed'));

  expect(isArmed || buttonText === 'Confirm delete').toBeTruthy();
});
