import { afterEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

const stubMatchMedia = (options: { mobile: boolean; reducedMotion: boolean }): void => {
  vi.stubGlobal('matchMedia', vi.fn().mockImplementation((query: string) => ({
    matches:
      (query === '(max-width: 767px)' && options.mobile)
      || (query === '(prefers-reduced-motion: reduce)' && options.reducedMotion),
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  } satisfies MediaQueryList)));
};

const renderFixture = (): void => {
  document.body.innerHTML = `
    <nav class="cutlab-anchor-nav" aria-label="Jump to section">
      <a href="#cut-lab-section-lock-pool">Lock your pool</a>
    </nav>
    <div class="prompt-step-nav" role="tablist" aria-label="Cut Lab workflow steps">
      <button type="button" id="cut-lab-step-tab-1" data-cut-lab-step="1">Process</button>
      <button type="submit" id="cut-lab-step-tab-5" form="cut-lab-export-form" data-cut-lab-step="5">Export</button>
    </div>
    <form data-cache-key="cut-lab"></form>
    <form id="cut-lab-export-form"></form>
    <details id="cut-lab-section-lock-pool" data-cutlab-mobile-collapse>
      <summary>Lock your pool</summary>
      <div>Body</div>
    </details>
  `;
};

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  window.localStorage.clear();
  document.body.innerHTML = '';
});

describe('cut-lab jump nav', () => {
  it('opens a collapsed target before scrolling and moves focus to it', () => {
    renderFixture();
    stubMatchMedia({ mobile: false, reducedMotion: false });
    const scrollIntoView = vi.fn();
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      writable: true,
      value: scrollIntoView,
    });

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const target = document.getElementById('cut-lab-section-lock-pool') as HTMLDetailsElement;
    const focusSpy = vi.spyOn(target, 'focus');

    expect(target.open).toBe(false);

    document.querySelector<HTMLAnchorElement>('.cutlab-anchor-nav a')?.click();

    expect(target.open).toBe(true);
    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth', block: 'start' });
    expect(target.getAttribute('tabindex')).toBe('-1');
    expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true });
    expect(window.localStorage.getItem('deckflow.cutlab.sections')).toBe(JSON.stringify([]));
  });

  it('uses auto scroll behavior when prefers-reduced-motion is enabled', () => {
    renderFixture();
    stubMatchMedia({ mobile: false, reducedMotion: true });
    const scrollIntoView = vi.fn();
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      writable: true,
      value: scrollIntoView,
    });

    document.dispatchEvent(new Event('DOMContentLoaded'));

    document.querySelector<HTMLAnchorElement>('.cutlab-anchor-nav a')?.click();

    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'auto', block: 'start' });
  });

  it('does not alter submit-driven step tabs', () => {
    renderFixture();
    stubMatchMedia({ mobile: false, reducedMotion: false });
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      writable: true,
      value: () => {},
    });

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const exportStep = document.getElementById('cut-lab-step-tab-5') as HTMLButtonElement;
    document.querySelector<HTMLAnchorElement>('.cutlab-anchor-nav a')?.click();

    expect(exportStep.type).toBe('submit');
    expect(exportStep.getAttribute('form')).toBe('cut-lab-export-form');
    expect(exportStep.getAttribute('data-cut-lab-step')).toBe('4');
  });
});
