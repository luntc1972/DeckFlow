import { expect, test, type Page } from '@playwright/test';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Part 2 runtime check driver (see .claude/scratch/dispatch/observability/PART2-WEB.md).
// Drives the Manabase analysis repeatedly against the SAME pasted decklist so the
// singleton cache crosses the 1000-operation statistics-log interval without issuing
// more than the first analysis' live Scryfall POSTs.
// Run from DeckFlow.Web/:
//   DECKFLOW_LIVE_E2E=1 npx --no-install playwright test cache-observability-driver --project=chromium-desktop

const WINOTA_DECK = readFileSync(resolve(__dirname, 'fixtures', 'winota-cedh.txt'), 'utf8');
const RUNS = Number(process.env.DECKFLOW_CACHE_RUNS ?? '8');

async function submitDeck(page: Page): Promise<string> {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(WINOTA_DECK);
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  const error = page.locator('.error-banner:not(.hidden)');
  await Promise.race([
    result.waitFor({ state: 'visible', timeout: 120_000 }).catch(() => undefined),
    error.waitFor({ state: 'visible', timeout: 120_000 }).catch(() => undefined),
  ]);

  if ((await error.count()) > 0 && (await error.isVisible())) {
    throw new Error(`analysis error banner: ${await error.innerText()}`);
  }

  return (await result.innerText()).trim();
}

test('drive manabase analysis until the cache statistics interval trips', async ({ page }) => {
  test.skip(!process.env.DECKFLOW_LIVE_E2E, 'live-only: needs Scryfall and a running server');
  test.setTimeout(15 * 60_000);

  const first = await submitDeck(page);
  expect(first.length).toBeGreaterThan(0);

  // Why: dumping run 1 lets the two arms be compared with cmp. The cache must change
  // call volume only, never output, and that claim is only checkable across processes.
  const dump = process.env.DECKFLOW_CACHE_DUMP;
  if (dump) {
    writeFileSync(dump, first, 'utf8');
  }

  for (let run = 2; run <= RUNS; run++) {
    const next = await submitDeck(page);
    expect(next, `run ${run} output diverged from run 1`).toBe(first);
  }
});
