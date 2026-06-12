import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/primer-selection';

interface PrimerSelectionApi {
  scrollToPrimerResult(): void;
}

let api: PrimerSelectionApi;
let scrollIntoViewSpy: ReturnType<typeof vi.fn>;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlow: PrimerSelectionApi }).DeckFlow;
});

beforeEach(() => {
  vi.useFakeTimers();
  scrollIntoViewSpy = vi.fn();
  Element.prototype.scrollIntoView = scrollIntoViewSpy;
});

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = '';
});

describe('Primer result scrolling', () => {
  it('scrolls the result panel into view when a generated primer exists', () => {
    document.body.innerHTML = `
      <section class="result-panel nested-panel">
        <textarea id="primer-output">SOME PROMPT</textarea>
      </section>
    `;

    const panel = document.querySelector<HTMLElement>('.result-panel');
    const panelScrollSpy = vi.spyOn(panel as HTMLElement, 'scrollIntoView');

    api.scrollToPrimerResult();
    vi.runAllTimers();

    expect(panelScrollSpy).toHaveBeenCalledTimes(1);
    expect(panelScrollSpy).toHaveBeenCalledWith({ behavior: 'smooth', block: 'start' });
  });

  it('does not scroll when the generated primer is empty', () => {
    document.body.innerHTML = `
      <section class="result-panel nested-panel">
        <textarea id="primer-output"></textarea>
      </section>
    `;

    api.scrollToPrimerResult();
    vi.runAllTimers();

    expect(scrollIntoViewSpy).not.toHaveBeenCalled();
  });

  it('does not scroll when the primer output element is missing', () => {
    api.scrollToPrimerResult();
    vi.runAllTimers();

    expect(scrollIntoViewSpy).not.toHaveBeenCalled();
  });
});
