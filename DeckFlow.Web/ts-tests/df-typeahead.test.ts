import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

await import('../wwwroot/ts/df-typeahead');

const flushMicrotasks = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
};

describe('shared typeahead', () => {
  let input: HTMLInputElement;
  let panel: HTMLDivElement;
  let attachTypeahead: NonNullable<Window['DeckFlow']>['attachTypeahead'];

  beforeEach(() => {
    vi.useFakeTimers();
    document.body.innerHTML = '<input id="query"><div id="panel" class="autocomplete-panel hidden"></div>';
    input = document.getElementById('query') as HTMLInputElement;
    panel = document.getElementById('panel') as HTMLDivElement;
    attachTypeahead = window.DeckFlow!.attachTypeahead!;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('does not reopen after an outside click while the fetch is in flight', async () => {
    let resolveJson: (names: string[]) => void = () => {};
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () => new Promise<string[]>(resolve => { resolveJson = resolve; }),
    });
    vi.stubGlobal('fetch', fetchMock);
    attachTypeahead(input, panel, 2, () => {}, { debounceMs: 300 });

    input.value = 'sol';
    input.focus();
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(300);
    await flushMicrotasks();
    input.blur();
    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    resolveJson(['Sol Ring']);
    await flushMicrotasks();

    expect(panel.classList.contains('hidden')).toBe(true);
    expect(panel.children).toHaveLength(0);
  });

  it('opens when the focused input is not dismissed', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ['Sol Ring'],
    });
    vi.stubGlobal('fetch', fetchMock);
    attachTypeahead(input, panel, 2, () => {}, { debounceMs: 300 });

    input.focus();
    input.value = 'sol';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(300);
    await flushMicrotasks();

    expect(panel.classList.contains('hidden')).toBe(false);
    expect(panel.children).toHaveLength(1);
  });
});
