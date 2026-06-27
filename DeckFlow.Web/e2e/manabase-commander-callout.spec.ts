import { expect, test, type Locator, type Page } from '@playwright/test';

// Live-only Phase 72 command-zone castability surfaces.
// Requires:
//   1. Start the app with scripts/run-web-test.sh.
//   2. Enable the analysis.manabase.commander-castability flag.
//   3. Use Casual mode.
//   4. From DeckFlow.Web/, run:
//      DECKFLOW_LIVE_E2E=1 npx --no-install playwright test manabase-commander-callout

const COMMANDER_COMPANION_DECK = [
  'Commander',
  '1 Esika, God of the Tree',
  '',
  'Deck',
  '1 Jegantha, the Wellspring',
  '10 Forest',
  '10 Mountain',
  '10 Plains',
  '10 Island',
  '10 Swamp',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Chromatic Lantern',
  '1 Cultivate',
  "1 Kodama's Reach",
  '1 Farseek',
  '1 Swords to Plowshares',
  '1 Beast Within',
].join('\n');

async function analyzeDeck(page: Page): Promise<Locator> {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(COMMANDER_COMPANION_DECK);
  await page.locator('.manabase-overrides > summary').click();
  await page.locator('#manabase-companion-name').fill('Jegantha, the Wellspring');
  await page.locator('.manabase-pill input[name="Mode"][value="Casual"]').check();
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  await expect(result).toBeVisible({ timeout: 60_000 });
  return result;
}

function parsePercent(text: string | null): number {
  if (!text) {
    throw new Error('Expected percentage text.');
  }

  const match = text.match(/(\d+)%/);
  if (!match) {
    throw new Error(`Could not parse cast percentage from "${text}".`);
  }

  return Number(match[1]);
}

test('command-zone castability callout renders above the table and excludes commanders from visible rows', async ({ page }) => {
  test.skip(!process.env.DECKFLOW_LIVE_E2E, 'live-only: needs Scryfall + commander-castability flag on');

  const result = await analyzeDeck(page);
  const callout = result.locator('.manabase-cmd-castability');
  const heading = result.locator('.manabase-castability-heading');
  const castabilityTable = result.locator('.castability-table');

  await expect(callout).toBeVisible();
  await expect(callout).toContainText('Command-zone castability');
  await expect(callout).toContainText('Esika, God of the Tree');
  await expect(callout).toContainText('Jegantha, the Wellspring');
  await expect(callout).toContainText('This heuristic includes the +3 generic "to hand" tax as an approximation.');

  const order = await result.locator(':scope > *').evaluateAll((nodes) =>
    nodes.map((node) =>
      (node as HTMLElement).classList.contains('manabase-cmd-castability')
        ? 'callout'
        : (node as HTMLElement).classList.contains('manabase-castability-heading')
          ? 'heading'
          : '',
    ),
  );
  expect(order.indexOf('callout')).toBeGreaterThan(-1);
  expect(order.indexOf('heading')).toBeGreaterThan(-1);
  expect(order.indexOf('callout')).toBeLessThan(order.indexOf('heading'));
  const isBeforeHeading = await callout.evaluate((node) => {
    const headingNode = document.querySelector('.manabase-castability-heading');
    return headingNode !== null
      && (node.compareDocumentPosition(headingNode) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
  });
  expect(isBeforeHeading).toBe(true);
  await expect(heading).toBeVisible();

  await expect(castabilityTable).not.toContainText('Esika, God of the Tree');

  const rowPercents = await castabilityTable.locator('tbody tr td:nth-child(3)').allTextContents();
  const avgFromRows = Math.round(
    rowPercents.reduce((sum, text) => sum + parsePercent(text), 0) / rowPercents.length,
  );
  const visibleAvg = parsePercent(await result.locator('.manabase-lens-big').textContent());
  expect(visibleAvg).toBe(avgFromRows);

  const companionLine = callout.locator('.manabase-cmd-castability-line').filter({ hasText: 'Jegantha, the Wellspring' });
  await expect(companionLine).toContainText(/\d+%/);
});
