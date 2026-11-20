import { expect, type Locator, type Page } from '@playwright/test';

export async function expandMobileCollapsibles(page: Page): Promise<void> {
  // Why: avoid the race with Cut Lab's DOMContentLoaded hydrate before changing persisted section state.
  await page.waitForLoadState('domcontentloaded');
  await page.locator('details[data-cutlab-mobile-collapse]').evaluateAll((details) => {
    for (const detail of details) {
      detail.setAttribute('open', '');
    }
  });
}

export async function ensureDetailsOpen(details: Locator): Promise<void> {
  // Why: avoid the race with Cut Lab's DOMContentLoaded hydrate before reading the section's open state.
  await details.page().waitForLoadState('domcontentloaded');

  // Why: The retry re-reads open and re-clicks the summary if hydration resets the section between check and click.
  // The 1s inner visibility check runs within a 5s outer bound that matches the old expect default.
  await expect(async () => {
    if ((await details.getAttribute('open')) === null) {
      await details.locator(':scope > summary').click();
    }

    await expect(details.locator(':scope > :not(summary)').first()).toBeVisible({ timeout: 1_000 });
  }).toPass({ timeout: 5_000 });
}

// Activate a section's hidden workflow step when needed, then expand only the
// collapsed section a spec needs via its UI.
export async function expandCutLabSection(page: Page, sectionId: string, enhancedTabs = true): Promise<void> {
  // Why: avoid the race with Cut Lab's DOMContentLoaded hydrate before reading the section's open state.
  await page.waitForLoadState('domcontentloaded');
  const section = page.locator(`details#${sectionId}`);
  const panel = section.locator('xpath=ancestor::*[@role="tabpanel"][1]');

  if ((await panel.count()) > 0 && enhancedTabs) {
    await expect(page.locator('[role="tabpanel"]:not([hidden])')).toHaveCount(1);

    const panelId = await panel.getAttribute('id');
    const tabId = await panel.getAttribute('aria-labelledby');
    expect(tabId, `Section ${sectionId} panel ${panelId} should have an aria-labelledby tab id`).toBeTruthy();
    const tab = page.locator(`#${tabId!}`);

    if ((await tab.getAttribute('aria-selected')) !== 'true') {
      const unreachableMessage =
        `Section ${sectionId} is unreachable because its step is disabled (panel ${panelId}, tab ${tabId!})`;

      await expect(tab, unreachableMessage).not.toHaveAttribute('aria-disabled', 'true');
      await tab.click();
      await expect(tab).toHaveAttribute('aria-selected', 'true');
      await expect(panel).not.toHaveAttribute('hidden', /.*/);
    }
  }

  await ensureDetailsOpen(section);
}
