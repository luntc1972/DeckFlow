import { expect, test } from '@playwright/test';

// Guards the Phase-1 mobile UI changes so desktop behavior stays intact while
// mobile-specific navigation, layout defaults, and overflow fixes remain covered.

test('tool nav collapses on mobile, expanded on desktop', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await page.goto('/deck-analysis');

  expect(response?.ok()).toBeTruthy();

  const toggle = page.locator('[data-tool-nav-menu-toggle]');
  const firstGroup = page.locator('.tool-nav__group').first();

  if (isMobile) {
    await expect(toggle).toBeVisible();
    await expect(firstGroup).not.toBeVisible();
    await expect(toggle).toHaveAttribute('aria-expanded', 'false');

    await toggle.click();

    await expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await expect(firstGroup).toBeVisible();
    return;
  }

  await expect(toggle).toBeHidden();
  await expect(firstGroup).toBeVisible();
});

test('verbosity layout picker available on mobile and defaults to Compact', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await page.goto('/deck-analysis');

  expect(response?.ok()).toBeTruthy();

  const picker = page.locator('[data-chatgpt-ui-mode-picker]');
  const form = page.locator('.chatgpt-packets-form');

  await expect(picker).toBeVisible();
  await expect(form).toBeVisible();

  if (isMobile) {
    await expect(form).toHaveAttribute('data-chatgpt-ui-mode', 'focused');
    await expect(page.locator('[data-chatgpt-ui-mode-button="focused"]')).toHaveClass(/is-active/);
    return;
  }

  await expect(form).toHaveAttribute('data-chatgpt-ui-mode', 'guided');
  await expect(page.locator('[data-chatgpt-ui-mode-button="guided"]')).toHaveClass(/is-active/);
});

test('download-session button is not the primary run-button on mobile', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await page.goto('/deck-analysis');

  expect(response?.ok()).toBeTruthy();

  const downloadButton = page.locator('.chatgpt-sticky-download__button');

  await expect(downloadButton).toBeVisible();

  if (!isMobile) {
    return;
  }

  const nextButton = page.locator('[data-chatgpt-next-step]').first();
  await expect(nextButton).toBeVisible();

  const downloadBackground = await downloadButton.evaluate((element) => getComputedStyle(element).backgroundColor);
  const nextBackground = await nextButton.evaluate((element) => getComputedStyle(element).backgroundColor);

  expect(downloadBackground).not.toBe(nextBackground);
});

test('deck primer section groups collapse on mobile', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await page.goto('/deck-primer');

  expect(response?.ok()).toBeTruthy();

  const groups = page.locator('details.primer-group');
  const groupCount = await groups.count();

  expect(groupCount).toBeGreaterThan(1);

  const firstGroup = groups.first();
  const secondGroup = groups.nth(1);

  if (isMobile) {
    expect(await firstGroup.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
    expect(await secondGroup.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(false);
    return;
  }

  expect(await firstGroup.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
  expect(await secondGroup.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
});

test('content kb filters collapse on mobile', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await page.goto('/content-kb');

  expect(response?.ok()).toBeTruthy();

  const filters = page.locator('details.kb-filters');
  const filterCount = await filters.count();

  if (filterCount === 0) {
    test.skip();
  }

  if (isMobile) {
    expect(await filters.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(false);

    await page.locator('.kb-filters__summary').click();

    expect(await filters.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
    return;
  }

  expect(await filters.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
});

for (const route of ['/deck-analysis', '/deck-primer', '/sync', '/card-lookup']) {
  test(`no horizontal overflow on key pages: ${route}`, async ({ page }) => {
    const response = await page.goto(route);

    expect(response?.ok()).toBeTruthy();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
  });
}
