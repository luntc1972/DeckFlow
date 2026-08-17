import { beforeAll, describe, expect, it } from 'vitest';
import '../wwwroot/ts/category-suggestions';

interface CategorySuggestionsApi {
  renderWeightedCategories(rows?: Array<{
    category: string;
    deckCount: number | null;
    percent: number | null;
    sourceCount: number;
    sourceTotal: number;
  }>): void;
}

let api: CategorySuggestionsApi;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlowCategorySuggestions: CategorySuggestionsApi }).DeckFlowCategorySuggestions;
});

describe('DeckFlowCategorySuggestions', () => {
  it('renders weighted rows in response order with unavailable counts shown as em dashes', () => {
    document.body.innerHTML = '<section class="result-panel hidden" data-api-panel="weighted"><table><tbody data-api-field="weighted-body"></tbody></table></section>';

    api.renderWeightedCategories([
      { category: 'Protection', deckCount: 120, percent: 100, sourceCount: 3, sourceTotal: 4 },
      { category: 'Tutor', deckCount: null, percent: null, sourceCount: 1, sourceTotal: 4 }
    ]);

    const rows = Array.from(document.querySelectorAll('[data-api-field="weighted-body"] tr'));
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toBe('Protection1201003/4');
    expect(rows[1].textContent).toBe('Tutor——1/4');
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
