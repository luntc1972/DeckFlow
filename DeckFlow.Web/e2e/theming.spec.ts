import { expect, test, type Page } from '@playwright/test';

// These guard the theme-aware checkbox accent + textarea scrollbar fixes.

const baseUrl = 'http://localhost:5173';
const themeFiles = ['site.css', 'site-rakdos.css', 'site-nyx.css'] as const;

type ThemeSnapshot = {
  rootAccent: string;
  checkboxAccent: string | null;
  textareaFound: boolean;
  textareaScrollbar: string;
  textareaScrollbarProperty: string;
  textareaScrollbarWidth: string;
};

async function readThemeSnapshot(page: Page, themeFile: string): Promise<ThemeSnapshot> {
  const context = page.context();

  await context.addCookies([
    {
      name: 'deckflow-theme',
      value: themeFile,
      url: baseUrl,
    },
  ]);

  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  return page.evaluate(() => {
    const rootStyle = getComputedStyle(document.documentElement);
    const checkbox = document.querySelector<HTMLInputElement>('input[type="checkbox"]');
    const textarea = document.querySelector<HTMLTextAreaElement>('textarea');
    const textareaStyle = textarea ? getComputedStyle(textarea) : null;

    return {
      rootAccent: rootStyle.getPropertyValue('--accent').trim(),
      checkboxAccent: checkbox ? getComputedStyle(checkbox).accentColor : null,
      textareaFound: textarea !== null,
      textareaScrollbar: textareaStyle?.scrollbarColor ?? '',
      textareaScrollbarProperty: textareaStyle?.getPropertyValue('scrollbar-color') ?? '',
      textareaScrollbarWidth: textareaStyle?.getPropertyValue('scrollbar-width').trim() ?? '',
    };
  });
}

function normalizeColor(value: string | null): string {
  return value?.trim().toLowerCase() ?? '';
}

function isRealColor(value: string | null): boolean {
  const normalized = normalizeColor(value);

  return normalized !== '' && normalized !== 'auto' && normalized !== 'none' && normalized !== 'transparent';
}

function pickScrollbarValue(snapshot: ThemeSnapshot): string {
  return normalizeColor(snapshot.textareaScrollbar) || normalizeColor(snapshot.textareaScrollbarProperty);
}

test('checkbox accent-color tracks theme accent', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const accentsByTheme = new Map<string, string>();

  if (isMobile) {
    await page.setViewportSize({ width: 390, height: 844 });
  } else {
    await page.setViewportSize({ width: 1280, height: 900 });
  }

  for (const themeFile of themeFiles) {
    const snapshot = await readThemeSnapshot(page, themeFile);

    expect(snapshot.rootAccent, `${themeFile} should expose a theme accent`).not.toBe('');

    if (snapshot.checkboxAccent === null) {
      test.skip(true, `No checkbox found on /deck-analysis for ${themeFile}.`);
    }

    expect(isRealColor(snapshot.checkboxAccent), `${themeFile} should compute a non-default checkbox accent`).toBeTruthy();
    accentsByTheme.set(themeFile, normalizeColor(snapshot.checkboxAccent));
  }

  expect(accentsByTheme.get('site.css')).not.toBe(accentsByTheme.get('site-rakdos.css'));
  expect(new Set(accentsByTheme.values()).size).toBeGreaterThanOrEqual(2);
});

test('textarea scrollbar-color is themed (not OS default)', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const snapshots = new Map<string, ThemeSnapshot>();

  if (isMobile) {
    await page.setViewportSize({ width: 390, height: 844 });
  } else {
    await page.setViewportSize({ width: 1280, height: 900 });
  }

  for (const themeFile of themeFiles) {
    const snapshot = await readThemeSnapshot(page, themeFile);

    expect(snapshot.rootAccent, `${themeFile} should expose a theme accent`).not.toBe('');

    if (!snapshot.textareaFound) {
      test.skip(true, `No textarea found on /deck-analysis for ${themeFile}.`);
    }

    snapshots.set(themeFile, snapshot);
  }

  const classic = snapshots.get('site.css');
  const rakdos = snapshots.get('site-rakdos.css');

  expect(classic).toBeTruthy();
  expect(rakdos).toBeTruthy();

  const classicScrollbar = pickScrollbarValue(classic!);
  const rakdosScrollbar = pickScrollbarValue(rakdos!);
  const scrollbarExposed = classicScrollbar !== '' && rakdosScrollbar !== '';

  if (scrollbarExposed) {
    expect(isRealColor(classicScrollbar), 'site.css should compute a non-default textarea scrollbar color').toBeTruthy();
    expect(isRealColor(rakdosScrollbar), 'site-rakdos.css should compute a non-default textarea scrollbar color').toBeTruthy();
    expect(classicScrollbar).not.toBe(rakdosScrollbar);
    return;
  }

  // Some engines do not expose computed scrollbar-color; in that case, assert
  // theme accents differ and scrollbar-width is still computed as thin so the
  // themed rule is at least being applied.
  expect(normalizeColor(classic!.rootAccent)).not.toBe(normalizeColor(rakdos!.rootAccent));
  expect(normalizeColor(classic!.textareaScrollbarWidth)).toBe('thin');
  expect(normalizeColor(rakdos!.textareaScrollbarWidth)).toBe('thin');
});
