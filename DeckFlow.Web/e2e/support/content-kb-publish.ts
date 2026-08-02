import { type Page } from '@playwright/test';

type PublishedEntry = {
  id: number;
  title: string;
};

const adminIndexUrl = '/Admin/ContentKb?visibilityFilter=all';

export async function publishFirstUnpublishedEntry(page: Page): Promise<PublishedEntry> {
  const response = await page.goto(adminIndexUrl);
  if (!response?.ok()) {
    throw new Error(`Could not load the Content KB admin index (status ${response?.status() ?? 'unknown'}).`);
  }

  const form = page
    .locator('form.admin-action-form')
    .filter({ has: page.getByRole('button', { name: /^Publish '/ }) })
    .first();
  const button = form.getByRole('button', { name: /^Publish '/ });

  if ((await button.count()) !== 1) {
    throw new Error('No unpublished Content KB entry was available to publish.');
  }

  const ariaLabel = await button.getAttribute('aria-label');
  const titleMatch = ariaLabel?.match(/^Publish '(.+)'$/);
  if (!titleMatch) {
    throw new Error('The Content KB publish action did not include its entry title.');
  }

  const entryId = Number(await form.locator('input[name="entryId"]').inputValue());
  if (!Number.isInteger(entryId) || entryId <= 0) {
    throw new Error('The Content KB publish action did not include a valid entry id.');
  }

  await clickAndWaitForAdminIndex(page, button);
  return { id: entryId, title: titleMatch[1] };
}

export async function setEntryVisibility(page: Page, id: number, visible: boolean): Promise<void> {
  const response = await page.goto(adminIndexUrl);
  if (!response?.ok()) {
    throw new Error(`Could not load the Content KB admin index (status ${response?.status() ?? 'unknown'}).`);
  }

  const action = visible ? 'Publish' : 'Unpublish';
  const form = page
    .locator('form.admin-action-form')
    .filter({ has: page.locator(`input[name="entryId"][value="${id}"]`) })
    .filter({ has: page.getByRole('button', { name: new RegExp(`^${action} '`) }) });
  const formCount = await form.count();

  // Cleanup may run after an assertion already changed visibility, so the desired action can be absent.
  if (formCount === 0) {
    return;
  }

  if (formCount !== 1) {
    throw new Error(`Expected one ${action.toLowerCase()} action for Content KB entry ${id}, found ${formCount}.`);
  }

  const button = form.getByRole('button', { name: new RegExp(`^${action} '`) });
  if ((await button.count()) !== 1) {
    throw new Error(`Expected one ${action.toLowerCase()} button for Content KB entry ${id}.`);
  }

  await clickAndWaitForAdminIndex(page, button);
}

async function clickAndWaitForAdminIndex(page: Page, button: ReturnType<Page['getByRole']>): Promise<void> {
  await Promise.all([
    page.waitForURL(/\/Admin\/ContentKb\?visibilityFilter=all$/, { waitUntil: 'domcontentloaded' }),
    button.click(),
  ]);
}
