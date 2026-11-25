import { expect, type Locator, type Page } from '@playwright/test';

export async function openDetailsWithRetry(details: Locator): Promise<void> {
  // Why: The retry re-reads open and re-clicks the summary if hydration resets the section between check and click.
  // The 1s inner visibility check runs within a 5s outer bound that matches the old expect default.
  await expect(async () => {
    if ((await details.getAttribute('open')) === null) {
      await details.locator(':scope > summary').click();
    }

    await expect(details.locator(':scope > :not(summary)').first()).toBeVisible({ timeout: 1_000 });
  }).toPass({ timeout: 5_000 });
}

export async function ensureDetailsOpen(details: Locator): Promise<void> {
  // Why: avoid the race with Cut Lab's DOMContentLoaded hydrate before reading the section's open state.
  await details.page().waitForLoadState('domcontentloaded');
  await openDetailsWithRetry(details);
}

export async function openRoleGroup(page: Page, name: string): Promise<Locator> {
  const details = page.locator('details.cutlab-role-group').filter({ hasText: name });
  await ensureDetailsOpen(details);
  return details;
}

export async function openIntake(page: Page): Promise<Locator> {
  const details = page.locator('details.cutlab-intake');
  await ensureDetailsOpen(details);
  return details;
}
