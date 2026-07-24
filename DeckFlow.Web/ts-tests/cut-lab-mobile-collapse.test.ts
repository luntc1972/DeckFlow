import { afterEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

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

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
});

describe('cut-lab mobile collapse', () => {
  // D-23: on mobile only the three auxiliary sections (packages/scenarios/whatif)
  // collapse by default; the primary sections stay open on mobile.
  it('removes open from an auxiliary mobile-collapse section on mobile', () => {
    document.body.innerHTML = `
      <details id="cut-lab-section-packages" data-cutlab-mobile-collapse open>
        <summary>Packages</summary>
        <div>Body</div>
      </details>
    `;
    stubMatchMedia(true);

    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.querySelector('#cut-lab-section-packages')?.hasAttribute('open')).toBe(false);
  });

  it('leaves a primary mobile-collapse section open on mobile (D-23)', () => {
    document.body.innerHTML = `
      <details id="cut-lab-section-lock-pool" data-cutlab-mobile-collapse open>
        <summary>Lock your pool</summary>
        <div>Body</div>
      </details>
    `;
    stubMatchMedia(true);

    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.querySelector('#cut-lab-section-lock-pool')?.hasAttribute('open')).toBe(true);
  });

  it('leaves an auxiliary mobile-collapse section open on desktop', () => {
    document.body.innerHTML = `
      <details id="cut-lab-section-packages" data-cutlab-mobile-collapse open>
        <summary>Packages</summary>
        <div>Body</div>
      </details>
    `;
    stubMatchMedia(false);

    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.querySelector('#cut-lab-section-packages')?.hasAttribute('open')).toBe(true);
  });
});
