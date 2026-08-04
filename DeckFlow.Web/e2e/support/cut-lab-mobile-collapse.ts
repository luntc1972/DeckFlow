import { expect, type Page } from '@playwright/test';

export async function expandMobileCollapsibles(page: Page): Promise<void> {
  await page.locator('details[data-cutlab-mobile-collapse]').evaluateAll((details) => {
    for (const detail of details) {
      detail.setAttribute('open', '');
    }
  });
}

// Importing completes step 1, selecting step 2 whose default collapse state closes
// sections such as Lock your pool. Expand only the section a spec needs via its UI.
export async function expandCutLabSection(page: Page, sectionId: string): Promise<void> {
  const section = page.locator(`details#${sectionId}`);

  if ((await section.getAttribute('open')) === null) {
    await section.locator(':scope > summary').click();
  }

  await expect(section.locator(':scope > :not(summary)').first()).toBeVisible();
}
