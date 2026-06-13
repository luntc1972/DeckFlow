import { expect, test, type Page } from '@playwright/test';

// These guard the theme-aware custom checkbox/radio + textarea scrollbar fixes.

const baseUrl = 'http://localhost:5173';
const themeFiles = [
  'site.css',
  'site-azorius.css',
  'site-dimir.css',
  'site-rakdos.css',
  'site-gruul.css',
  'site-selesnya.css',
  'site-orzhov.css',
  'site-izzet.css',
  'site-golgari.css',
  'site-boros.css',
  'site-simic.css',
  'site-bant.css',
  'site-abzan.css',
  'site-sultai.css',
  'site-mardu.css',
  'site-temur.css',
  'site-esper.css',
  'site-grixis.css',
  'site-jund.css',
  'site-naya.css',
  'site-jeskai.css',
  'site-nyx.css',
  'site-planeswalker-dark.css',
  'site-commander-table.css',
] as const;

type ThemeSnapshot = {
  rootAccent: string;
  checkboxAppearance: string | null;
  checkboxWebkitAppearance: string | null;
  checkboxBackground: string | null;
  checkboxBorderColor: string | null;
  checkboxRenderWidth: number | null;
  checkboxRenderHeight: number | null;
  checkboxPadding: string | null;
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
    const checkboxStyle = checkbox ? getComputedStyle(checkbox) : null;
    const textarea = document.querySelector<HTMLTextAreaElement>('textarea');
    const textareaStyle = textarea ? getComputedStyle(textarea) : null;

    return {
      rootAccent: rootStyle.getPropertyValue('--accent').trim(),
      checkboxAppearance: checkboxStyle ? checkboxStyle.getPropertyValue('appearance').trim() : null,
      checkboxWebkitAppearance: checkboxStyle ? checkboxStyle.getPropertyValue('-webkit-appearance').trim() : null,
      checkboxBackground: checkboxStyle?.backgroundColor ?? null,
      checkboxBorderColor: checkboxStyle?.borderColor ?? null,
      // Computed width/height (not getBoundingClientRect — the first checkbox
      // lives in a collapsed bucket, so its rect is 0 while its box size is
      // still resolved correctly by the cascade).
      checkboxRenderWidth: checkboxStyle ? parseFloat(checkboxStyle.width) : null,
      checkboxRenderHeight: checkboxStyle ? parseFloat(checkboxStyle.height) : null,
      checkboxPadding: checkboxStyle ? checkboxStyle.padding : null,
      textareaFound: textarea !== null,
      textareaScrollbar: textareaStyle?.scrollbarColor ?? '',
      textareaScrollbarProperty: textareaStyle?.getPropertyValue('scrollbar-color') ?? '',
      textareaScrollbarWidth: textareaStyle?.getPropertyValue('scrollbar-width').trim() ?? '',
    };
  });
}

async function setThemedViewport(page: Page): Promise<void> {
  const isMobile = test.info().project.name.includes('mobile');

  if (isMobile) {
    await page.setViewportSize({ width: 390, height: 844 });
    return;
  }

  await page.setViewportSize({ width: 1280, height: 900 });
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

test('checkboxes stay custom-rendered and themed across all themes in light and dark media', async ({ page }) => {
  await setThemedViewport(page);

  for (const colorScheme of ['light', 'dark'] as const) {
    await page.emulateMedia({ colorScheme });

    const borderColorsByTheme = new Map<string, string>();

    for (const themeFile of themeFiles) {
      const snapshot = await readThemeSnapshot(page, themeFile);

      expect(snapshot.rootAccent, `${themeFile} should expose a theme accent`).not.toBe('');

      if (snapshot.checkboxAppearance === null) {
        test.skip(true, `No checkbox found on /deck-analysis for ${themeFile}.`);
      }

      expect(snapshot.checkboxAppearance, `${themeFile} should disable native checkbox rendering in ${colorScheme} mode`).toBe('none');
      expect(snapshot.checkboxWebkitAppearance, `${themeFile} should disable WebKit native checkbox rendering in ${colorScheme} mode`).toBe('none');
      expect(isRealColor(snapshot.checkboxBackground), `${themeFile} should theme the checkbox background in ${colorScheme} mode`).toBeTruthy();
      expect(isRealColor(snapshot.checkboxBorderColor), `${themeFile} should theme the checkbox border in ${colorScheme} mode`).toBeTruthy();

      // Size guard (regression: the generic `input` padding inflated the
      // appearance:none box to ~29x26 and offset the checkmark). The custom
      // box must stay small (~1.05rem) and square, with no inherited padding.
      const w = snapshot.checkboxRenderWidth ?? 0;
      const h = snapshot.checkboxRenderHeight ?? 0;
      expect(w, `${themeFile} checkbox width should stay compact (not inflated by inherited input padding) in ${colorScheme} mode`).toBeGreaterThan(10);
      expect(w, `${themeFile} checkbox width should stay compact in ${colorScheme} mode`).toBeLessThanOrEqual(24);
      expect(h, `${themeFile} checkbox height should stay compact in ${colorScheme} mode`).toBeLessThanOrEqual(24);
      expect(Math.abs(w - h), `${themeFile} checkbox should render square in ${colorScheme} mode`).toBeLessThanOrEqual(2);
      expect(snapshot.checkboxPadding, `${themeFile} checkbox should not inherit text-input padding in ${colorScheme} mode`).toBe('0px');

      borderColorsByTheme.set(themeFile, normalizeColor(snapshot.checkboxBorderColor));
    }

    expect(new Set(borderColorsByTheme.values()).size, `${colorScheme} mode should preserve theme-specific checkbox border colors`).toBeGreaterThanOrEqual(2);
  }
});

test('textarea scrollbar-color is themed (not OS default)', async ({ page }) => {
  await setThemedViewport(page);

  const snapshots = new Map<string, ThemeSnapshot>();

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
