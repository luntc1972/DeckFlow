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
  it('removes open from mobile-collapse details on mobile', () => {
    document.body.innerHTML = `
      <details data-cutlab-mobile-collapse open>
        <summary>Packages</summary>
        <div>Body</div>
      </details>
    `;
    stubMatchMedia(true);

    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.querySelector('details[data-cutlab-mobile-collapse]')?.hasAttribute('open')).toBe(false);
  });

  it('leaves mobile-collapse details open on desktop', () => {
    document.body.innerHTML = `
      <details data-cutlab-mobile-collapse open>
        <summary>Packages</summary>
        <div>Body</div>
      </details>
    `;
    stubMatchMedia(false);

    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.querySelector('details[data-cutlab-mobile-collapse]')?.hasAttribute('open')).toBe(true);
  });
});
