import type { Page } from '@playwright/test';

export async function expandMobileCollapsibles(page: Page): Promise<void> {
  await page.locator('details[data-cutlab-mobile-collapse]').evaluateAll((details) => {
    for (const detail of details) {
      detail.setAttribute('open', '');
    }
  });
}
