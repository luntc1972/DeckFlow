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

const renderSections = (): void => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab"></form>
    <details data-cutlab-mobile-collapse id="cut-lab-section-packages" open>
      <summary>Packages</summary>
      <div>Packages body</div>
    </details>
    <details data-cutlab-mobile-collapse id="cut-lab-section-structural" open>
      <summary>Structural findings</summary>
      <div>Structural body</div>
    </details>
    <details data-cutlab-mobile-collapse id="cut-lab-section-scenarios" open>
      <summary>Scenarios</summary>
      <div>Scenarios body</div>
    </details>
  `;
};

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  window.localStorage.clear();
  document.body.innerHTML = '';
});

describe('cut-lab section collapse persistence', () => {
  it('restores stored collapsed sections on init, including pre-existing collapsibles', () => {
    renderSections();
    stubMatchMedia(false);
    window.localStorage.setItem(
      'deckflow.cutlab.sections',
      JSON.stringify(['cut-lab-section-packages', 'cut-lab-section-structural']),
    );

    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.getElementById('cut-lab-section-packages')?.hasAttribute('open')).toBe(false);
    expect(document.getElementById('cut-lab-section-structural')?.hasAttribute('open')).toBe(false);
    expect(document.getElementById('cut-lab-section-scenarios')?.hasAttribute('open')).toBe(true);
  });

  it('writes the collapsed section id set when a section toggles', () => {
    renderSections();
    stubMatchMedia(false);

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const structural = document.getElementById('cut-lab-section-structural') as HTMLDetailsElement;
    structural.open = false;
    structural.dispatchEvent(new Event('toggle'));

    expect(window.localStorage.getItem('deckflow.cutlab.sections')).toBe(JSON.stringify(['cut-lab-section-structural']));
  });

  it('falls back to default mobile collapse behavior on parse failure without throwing', () => {
    renderSections();
    stubMatchMedia(true);
    window.localStorage.setItem('deckflow.cutlab.sections', '{not-json');

    expect(() => {
      document.dispatchEvent(new Event('DOMContentLoaded'));
    }).not.toThrow();

    expect(document.getElementById('cut-lab-section-packages')?.hasAttribute('open')).toBe(false);
    expect(document.getElementById('cut-lab-section-scenarios')?.hasAttribute('open')).toBe(false);
    expect(document.getElementById('cut-lab-section-structural')?.hasAttribute('open')).toBe(true);
  });

  it('falls back to defaults when storage access throws a quota error', () => {
    renderSections();
    stubMatchMedia(true);
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('Quota exceeded', 'QuotaExceededError');
    });

    expect(() => {
      document.dispatchEvent(new Event('DOMContentLoaded'));
    }).not.toThrow();

    expect(document.getElementById('cut-lab-section-packages')?.hasAttribute('open')).toBe(false);
    expect(document.getElementById('cut-lab-section-scenarios')?.hasAttribute('open')).toBe(false);
    expect(document.getElementById('cut-lab-section-structural')?.hasAttribute('open')).toBe(true);
  });
});
