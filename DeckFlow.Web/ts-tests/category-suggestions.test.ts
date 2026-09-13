import { beforeAll, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/category-suggestions';

interface CategorySuggestionsApi {
  renderWeightedCategories(rows?: Array<{
    category: string;
    deckCount: number | null;
    percent: number | null;
    sourceCount: number;
    sourceTotal: number;
  }>): void;
  setCardIntakeState(form: HTMLFormElement, open: boolean, cardName?: string): void;
}

let api: CategorySuggestionsApi;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlowCategorySuggestions: CategorySuggestionsApi }).DeckFlowCategorySuggestions;
});

describe('DeckFlowCategorySuggestions', () => {
  it('closes the intake with the card name after a successful lookup and reopens it when cleared', () => {
    document.body.innerHTML = '<details class="cutlab-intake" open><summary class="cutlab-intake-summary"><span class="cutlab-intake-summary__commander">Look up a card</span><span class="cutlab-intake-summary__change">Change lookup</span></summary><form></form></details>';

    const form = document.querySelector<HTMLFormElement>('form');
    const intake = document.querySelector<HTMLDetailsElement>('.cutlab-intake');
    const summary = document.querySelector<HTMLElement>('.cutlab-intake-summary__commander');
    expect(form).not.toBeNull();
    expect(intake).not.toBeNull();
    expect(summary).not.toBeNull();

    api.setCardIntakeState(form!, false, 'Guardian Project');
    expect(intake!.open).toBe(false);
    expect(summary!.textContent).toBe('Guardian Project');

    api.setCardIntakeState(form!, true);
    expect(intake!.open).toBe(true);
  });

  it('wires lookup, clear, and empty-card input handlers to the card intake state', async () => {
    document.body.innerHTML = '<details class="cutlab-intake" open><summary><span class="cutlab-intake-summary__commander">Look up a card</span></summary><form data-suggestions-type="card" data-suggestion-api="/api/categories" data-cache-key="guardian-project"><input name="CardName" value="Guardian Project"><select name="Mode"><option value="CachedData" selected>Cached data</option></select><button type="button" data-clear-cache>Clear</button></form></details>';
    const payload = {
      cardName: 'Guardian Project',
      exactCategoriesText: '', exactSuggestionContextText: '', inferredCategoriesText: '', inferredSuggestionContextText: '',
      edhrecCategoriesText: '', edhrecSuggestionContextText: '', hasExactCategories: false, hasInferredCategories: false,
      hasEdhrecCategories: false, taggerCategoriesText: '', taggerSuggestionContextText: '', hasTaggerCategories: false,
      noSuggestionsFound: false, cardDeckTotals: { totalDeckCount: 0 }
    };
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => payload });
    vi.stubGlobal('fetch', fetchMock);

    const form = document.querySelector<HTMLFormElement>('form')!;
    const intake = document.querySelector<HTMLDetailsElement>('.cutlab-intake')!;
    const summary = document.querySelector<HTMLElement>('.cutlab-intake-summary__commander')!;
    const cardName = form.elements.namedItem('CardName') as HTMLInputElement;

    document.dispatchEvent(new Event('DOMContentLoaded'));
    form.dispatchEvent(new Event('submit', { cancelable: true }));
    await Promise.resolve();
    await Promise.resolve();

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(intake.open).toBe(false);
    expect(summary.textContent).toBe('Guardian Project');

    form.querySelector<HTMLElement>('[data-clear-cache]')!.click();
    expect(intake.open).toBe(true);
    expect(summary.textContent).toBe('Look up a card');

    intake.open = false;
    summary.textContent = 'Guardian Project';
    cardName.value = '';
    cardName.dispatchEvent(new Event('input', { bubbles: true }));
    expect(intake.open).toBe(true);
    expect(summary.textContent).toBe('Look up a card');
    vi.unstubAllGlobals();
  });

  it('renders weighted rows in response order with unavailable counts shown as em dashes', () => {
    document.body.innerHTML = '<section class="result-panel hidden" data-api-panel="weighted"><table><tbody data-api-field="weighted-body"></tbody></table></section>';

    api.renderWeightedCategories([
      { category: 'Protection', deckCount: 120, percent: 100, sourceCount: 3, sourceTotal: 4 },
      { category: 'Tutor', deckCount: null, percent: null, sourceCount: 1, sourceTotal: 4 },
      { category: 'Trinket', deckCount: 3, percent: 0, sourceCount: 1, sourceTotal: 3 },
      { category: 'Zero', deckCount: 0, percent: 0, sourceCount: 1, sourceTotal: 3 }
    ]);

    const rows = Array.from(document.querySelectorAll('[data-api-field="weighted-body"] tr'));
    expect(rows).toHaveLength(4);
    expect(rows[0].textContent).toBe('Protection120100%3/4');
    expect(rows[1].textContent).toBe('Tutor—Not available—Not available1/4');
    expect(rows[2].textContent).toBe('Trinket3<1%1/3');
    expect(rows[3].textContent).toBe('Zero00%1/3');
    expect(document.querySelector('[data-api-panel="weighted"]')?.classList.contains('hidden')).toBe(false);
  });

  it('keeps the weighted panel hidden when rows are absent or empty', () => {
    document.body.innerHTML = '<section class="result-panel" data-api-panel="weighted"><table><tbody data-api-field="weighted-body"><tr><td>old</td></tr></tbody></table></section>';

    api.renderWeightedCategories();
    expect(document.querySelector('[data-api-field="weighted-body"]')?.children).toHaveLength(0);
    expect(document.querySelector('[data-api-panel="weighted"]')?.classList.contains('hidden')).toBe(true);

    api.renderWeightedCategories([]);
    expect(document.querySelector('[data-api-panel="weighted"]')?.classList.contains('hidden')).toBe(true);
  });
});
