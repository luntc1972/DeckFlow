import { expect, type Page } from '@playwright/test';

export async function expandMobileCollapsibles(page: Page): Promise<void> {
  await page.locator('details[data-cutlab-mobile-collapse]').evaluateAll((details) => {
    for (const detail of details) {
      detail.setAttribute('open', '');
    }
  });
}

// Activate a section's hidden workflow step when needed, then expand only the
// collapsed section a spec needs via its UI.
export async function expandCutLabSection(page: Page, sectionId: string): Promise<void> {
  const section = page.locator(`details#${sectionId}`);
  const panel = section.locator('xpath=ancestor::*[@role="tabpanel"][1]');

  if ((await panel.count()) > 0) {
    await expect(page.locator('[role="tabpanel"]:not([hidden])')).toHaveCount(1);

    const panelId = await panel.getAttribute('id');
    const tabId = await panel.getAttribute('aria-labelledby');
    expect(tabId, `Section ${sectionId} panel ${panelId} should have an aria-labelledby tab id`).toBeTruthy();
    const tab = page.locator(`#${tabId!}`);

    if ((await tab.getAttribute('aria-selected')) !== 'true') {
      const unreachableMessage =
        `Section ${sectionId} is unreachable because its step is disabled (panel ${panelId}, tab ${tabId!})`;

      await expect(tab, unreachableMessage).not.toBeDisabled();
      await tab.click();
      await expect(tab).toHaveAttribute('aria-selected', 'true');
      await expect(panel).not.toHaveAttribute('hidden', /.*/);
    }
  }

  if ((await section.getAttribute('open')) === null) {
    await section.locator(':scope > summary').click();
  }

  await expect(section.locator(':scope > :not(summary)').first()).toBeVisible();
}
