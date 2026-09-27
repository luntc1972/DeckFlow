import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { type Page } from '@playwright/test';

type PublishedEntry = {
  id: number;
  title: string;
};

const adminIndexUrl = '/Admin/ContentKb?visibilityFilter=all';

type ContentKbFixtureSeedEntry = {
  naturalKeyValue: string;
  title: string;
};

export type ContentKbPublishSlot = 'notice' | 'public';

function getFixtureEntryTitle(slot: ContentKbPublishSlot, projectName: string): string {
  const contentBase = process.env.DECKFLOW_E2E_CONTENT_BASE;
  if (!contentBase) {
    throw new Error('DECKFLOW_E2E_CONTENT_BASE was not configured for Content KB E2E tests.');
  }

  const seedPath = resolve(contentBase, 'content-kb', 'seed', 'index-seed.json');
  const entries = JSON.parse(readFileSync(seedPath, 'utf8')) as ContentKbFixtureSeedEntry[];
  const entry = entries.find((candidate) => candidate.naturalKeyValue === `e2e-publish-${slot}-${projectName}`);
  if (!entry) {
    throw new Error(`Content KB E2E fixture seed at ${seedPath} did not contain a ${slot} entry for ${projectName}.`);
  }

  return entry.title;
}

export async function publishFixtureEntry(
  page: Page,
  slot: ContentKbPublishSlot,
  projectName: string,
): Promise<PublishedEntry> {
  const response = await page.goto(adminIndexUrl);
  if (!response?.ok()) {
    throw new Error(`Could not load the Content KB admin index (status ${response?.status() ?? 'unknown'}).`);
  }

  const fixtureTitle = getFixtureEntryTitle(slot, projectName);
  const form = page
    .locator('form.admin-action-form')
    .filter({ has: page.getByRole('button', { name: `Publish '${fixtureTitle}'` }) });
  const button = form.getByRole('button', { name: /^Publish '/ });

  if ((await button.count()) !== 1) {
    throw new Error(`Expected one publish action for Content KB ${slot} fixture entry, found ${await button.count()}.`);
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
