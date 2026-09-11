import { afterEach, describe, expect, it, vi } from 'vitest';

const stubMatchMedia = (matches: boolean): void => {
  vi.stubGlobal('matchMedia', vi.fn().mockImplementation((query: string) => ({
    matches,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  } satisfies MediaQueryList)));
};

const renderTabs = (): void => {
  document.body.innerHTML = `
    <a href="#manabase-castability">Castability</a>
    <div data-manabase-analysis-switcher>
      <div data-manabase-analysis-tabs role="tablist">
        <div class="manabase-analysis-tab-indicator" aria-hidden="true"></div>
        <button class="manabase-analysis-tab" role="tab" id="manabase-tab-colors" aria-selected="true" aria-controls="manabase-panel-colors">Color findings</button>
        <button class="manabase-analysis-tab" role="tab" id="manabase-tab-castability" aria-selected="false" aria-controls="manabase-panel-castability" tabindex="-1">Castability</button>
        <button class="manabase-analysis-tab" role="tab" id="manabase-tab-numbers" aria-selected="false" aria-controls="manabase-panel-numbers" tabindex="-1">Numbers</button>
      </div>
      <div id="manabase-panel-colors" role="tabpanel"></div>
      <div id="manabase-panel-castability" role="tabpanel" hidden><section id="manabase-castability"></section></div>
      <div id="manabase-panel-numbers" role="tabpanel" hidden></div>
    </div>`;
};

afterEach(() => {
  vi.resetModules();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
});

describe('manabase analysis tabs', () => {
  it('switches panels on click and arrow-key navigation above desktop breakpoint', async () => {
    stubMatchMedia(true);
    renderTabs();
    await import('../wwwroot/ts/manabase-analysis-tabs');
    document.dispatchEvent(new Event('DOMContentLoaded'));

    const colors = document.getElementById('manabase-tab-colors')!;
    const castability = document.getElementById('manabase-tab-castability')!;
    const numbers = document.getElementById('manabase-tab-numbers')!;
    const colorPanel = document.getElementById('manabase-panel-colors')!;
    const castabilityPanel = document.getElementById('manabase-panel-castability')!;

    castability.click();
    expect(castability.getAttribute('aria-selected')).toBe('true');
    expect(colors.getAttribute('aria-selected')).toBe('false');
    expect(castabilityPanel.hidden).toBe(false);
    expect(colorPanel.hidden).toBe(true);

    castability.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
    expect(colors.getAttribute('aria-selected')).toBe('true');
    expect(colorPanel.hidden).toBe(false);

    colors.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
    expect(numbers.getAttribute('aria-selected')).toBe('true');
    expect(document.getElementById('manabase-panel-numbers')!.hidden).toBe(false);
  });

  it('selects the castability tab for its quick-nav link and URL hash', async () => {
    stubMatchMedia(true);
    renderTabs();
    window.history.replaceState({}, '', '#manabase-castability');
    await import('../wwwroot/ts/manabase-analysis-tabs');
    document.dispatchEvent(new Event('DOMContentLoaded'));

    const castability = document.getElementById('manabase-tab-castability')!;
    expect(castability.getAttribute('aria-selected')).toBe('true');

    document.querySelector<HTMLAnchorElement>('a[href="#manabase-castability"]')!.click();
    expect(document.getElementById('manabase-panel-castability')!.hidden).toBe(false);
  });

  it('leaves both panels visible below the desktop breakpoint', async () => {
    stubMatchMedia(false);
    renderTabs();
    await import('../wwwroot/ts/manabase-analysis-tabs');
    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.getElementById('manabase-panel-colors')!.hidden).toBe(false);
    expect(document.getElementById('manabase-panel-castability')!.hidden).toBe(false);
    expect(document.getElementById('manabase-panel-numbers')!.hidden).toBe(false);
  });
});
