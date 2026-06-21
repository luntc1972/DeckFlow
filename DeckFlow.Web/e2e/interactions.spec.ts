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

test('content kb detail copy button signals it builds an AI prompt', async ({ page }) => {
  const listResponse = await page.goto('/content-kb');
  expect(listResponse?.ok()).toBeTruthy();

  const firstCardLink = page.locator('[data-kb-entry] .hub-card__title a').first();
  test.skip((await firstCardLink.count()) === 0, 'no KB entries seeded');

  const href = await firstCardLink.getAttribute('href');
  expect(href).toBeTruthy();

  const detailResponse = await page.goto(href!);
  expect(detailResponse?.ok()).toBeTruthy();

  const copyButton = page.locator('button[data-copy-target="kb-artifact-text"]');
  test.skip((await copyButton.count()) === 0, 'artifact unavailable for this entry');

  // The label and lede must tell the user the copied text is a paste-ready AI
  // prompt, not just an opaque "Copy" action.
  await expect(copyButton).toBeVisible();
  await expect(copyButton).toContainText(/ChatGPT/i);

  const hint = page.locator('.kb-artifact-hint');
  await expect(hint).toBeVisible();
  await expect(hint).toContainText(/ChatGPT|Claude|Gemini/i);

  // The longer label must not overflow the viewport or wrap to a giant button
  // on the 390px mobile project. Use viewport-relative rect coords.
  const rect = await copyButton.evaluate((el) => {
    const r = el.getBoundingClientRect();
    return { right: r.right, height: r.height, innerWidth: window.innerWidth };
  });
  expect(rect.right).toBeLessThanOrEqual(rect.innerWidth + 1);
  expect(rect.height).toBeLessThanOrEqual(80);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
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

test('admin content kb keeps creator + search filter across visibility tab switches', async ({ page }) => {
  // Re-navigate on a transient non-2xx: parallel CI workers can momentarily hit a
  // "database is locked" (500) on the shared SQLite admin store. A real failure
  // still surfaces after the retries.
  let response = await page.goto('/Admin/ContentKb?visibilityFilter=all');
  for (let attempt = 0; attempt < 2 && !response?.ok(); attempt++) {
    response = await page.goto('/Admin/ContentKb?visibilityFilter=all');
  }
  expect(response?.ok()).toBeTruthy();

  const creator = page.locator('#kb-creator-filter');
  const optionCount = await creator.locator('option').count();
  test.skip(optionCount < 2, 'no creator options seeded');

  // Index 0 is the "All creators" placeholder; pick the first real creator.
  const chosenCreator = await creator.locator('option').nth(1).getAttribute('value');
  await creator.selectOption(chosenCreator!);
  await page.locator('#kb-filter-search').fill('combo');

  // Switching the visibility tab is a full-page <a> navigation (not a form
  // submit), so the creator + search filter must be restored from sessionStorage
  // rather than reset. Target by href: "Published" as text also substring-matches
  // the "Unpublished" tab, so a text filter resolves to two elements (strict-mode
  // violation). The href filter is unambiguous.
  await page.locator('.admin-kb-toggle a[href*="visibilityFilter=published"]').click();
  await expect(page).toHaveURL(/visibilityFilter=published/);

  await expect(page.locator('#kb-creator-filter')).toHaveValue(chosenCreator!);
  await expect(page.locator('#kb-filter-search')).toHaveValue('combo');
});
