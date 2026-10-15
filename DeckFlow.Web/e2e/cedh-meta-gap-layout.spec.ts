import { expect, test, type Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { uiDesignDir } from './support/ui-design-dir';

const fixturePath = 'e2e/fixtures/cedh-meta-gap-response.json';
const screenshotDirectory = uiDesignDir('cedh-meta-gap');
const viewports = [375, 768, 1280, 1440, 1920];

async function renderResults(page: Page) {
  const response = await page.goto('/cedh-meta-gap');
  expect(response?.ok()).toBeTruthy();

  const tabs = page.locator('form.deck-form [role=tablist]');
  await expect(tabs).toBeVisible();
  await tabs.getByRole('tab').nth(2).click();
  const responseJson = page.locator('textarea[name="MetaGapResponseJson"]');
  await responseJson.evaluate((element) => {
    const disclosure = element.closest('details');
    if (disclosure) disclosure.open = true;
  });
  await responseJson.fill(await readFile(fixturePath, 'utf8'));
  await page.locator('[data-prompt-cedh-submit-step="3"]').click();
  await expect(page.locator('.meta-gap-results')).toBeVisible();
}

async function edges(page: Page, selector: string) {
  return page.locator(selector).first().evaluate((element) => {
    const box = element.getBoundingClientRect();
    return { left: box.left, right: box.right };
  });
}

test('cEDH meta-gap results retain one aligned shell at supported widths', async ({ page }) => {
  for (const width of viewports) {
    await page.setViewportSize({ width, height: 1000 });
    await renderResults(page);

    const header = await edges(page, '.page-header');
    for (const selector of ['.hero', '.info-banner', 'form.deck-form', '.related-tool-link', '.page-footer']) {
      const candidate = await edges(page, selector);
      expect(Math.abs(candidate.left - header.left), `${width}px ${selector} left`).toBeLessThanOrEqual(1);
      expect(Math.abs(candidate.right - header.right), `${width}px ${selector} right`).toBeLessThanOrEqual(1);
    }

    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
    await expect(page.locator('.prompt-sticky-download')).toHaveCSS('position', 'static');
    await expect(page.locator('[data-prompt-download-submit]')).toHaveCount(1);
    const disclosures = page.locator('details.meta-gap-edit-group--staple, details.meta-gap-edit-group--potential-cut');
    await expect(disclosures).toHaveCount(2);
    expect(await disclosures.evaluateAll((elements) => elements.every((element) => (element as HTMLDetailsElement).open))).toBeTruthy();

    const columns = await page.locator('.meta-gap-edit-group--staple ul').evaluate((element) => getComputedStyle(element).gridTemplateColumns.split(' ').length);
    if (width <= 768) {
      expect(columns).toBe(1);
    } else {
      expect(columns).toBeGreaterThanOrEqual(2);
      expect(columns).toBeLessThanOrEqual(3);
    }

    if (width === 375) {
      const cardWidth = await page.locator('.meta-gap-edit-group li').first().evaluate((element) => element.getBoundingClientRect().width);
      expect(cardWidth).toBeGreaterThanOrEqual(265);
    }

    await page.screenshot({ path: `${screenshotDirectory}/cedh-meta-gap-${width}.png`, fullPage: true });
  }
});

test('deck analysis preserves sticky session bar', async ({ page }) => {
  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();
  await expect(page.locator('.prompt-sticky-download')).not.toHaveCSS('position', 'static');
});
