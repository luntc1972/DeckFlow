import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

beforeAll(async () => {
  Element.prototype.scrollIntoView = vi.fn();
  document.body.innerHTML = `
    <div class="error-banner hidden" data-prompt-cedh-validation-error role="alert"></div>
    <form data-prompt-cedh-form data-prompt-cedh-current-step="1">
      <section data-prompt-cedh-step="1">
        <select name="DeckInputSource">
          <option value="PasteText" selected>PasteText</option>
          <option value="PublicUrl">PublicUrl</option>
        </select>
        <input name="DeckUrl" value="" />
        <textarea name="DeckText"></textarea>
        <button type="submit" data-prompt-cedh-submit-step="1">Fetch Top Decks</button>
      </section>
    </form>
    <div class="error-banner hidden" data-prompt-comparison-validation-error role="alert"></div>
    <form data-prompt-comparison-form data-prompt-comparison-current-step="1">
      <section data-prompt-comparison-step="1">
        <select name="DeckAInputSource">
          <option value="PasteText" selected>PasteText</option>
          <option value="PublicUrl">PublicUrl</option>
        </select>
        <input name="DeckAUrl" value="" />
        <textarea name="DeckAText"></textarea>
        <select name="DeckBInputSource">
          <option value="PasteText" selected>PasteText</option>
          <option value="PublicUrl">PublicUrl</option>
        </select>
        <input name="DeckBUrl" value="" />
        <textarea name="DeckBText"></textarea>
        <select name="DeckABracket">
          <option value="">Choose</option>
          <option value="cEDH">cEDH</option>
        </select>
        <select name="DeckBBracket">
          <option value="">Choose</option>
          <option value="cEDH">cEDH</option>
        </select>
        <button type="button" data-prompt-comparison-next-step="2">Next</button>
      </section>
      <section data-prompt-comparison-step="2"></section>
    </form>
  `;
  await import('../wwwroot/ts/busy-indicator');
  await import('../wwwroot/ts/moxfield-extension-bridge');
  await import('../wwwroot/ts/deck-sync');
});

beforeEach(() => {
  document.querySelector<HTMLSelectElement>('form[data-prompt-cedh-form] select[name="DeckInputSource"]')!.value = 'PasteText';
  document.querySelector<HTMLInputElement>('form[data-prompt-cedh-form] input[name="DeckUrl"]')!.value = '';
  document.querySelector<HTMLTextAreaElement>('form[data-prompt-cedh-form] textarea[name="DeckText"]')!.value = '';
  const cedhError = document.querySelector<HTMLElement>('[data-prompt-cedh-validation-error]')!;
  cedhError.textContent = '';
  cedhError.classList.add('hidden');

  document.querySelector<HTMLSelectElement>('form[data-prompt-comparison-form] select[name="DeckAInputSource"]')!.value = 'PasteText';
  document.querySelector<HTMLInputElement>('form[data-prompt-comparison-form] input[name="DeckAUrl"]')!.value = '';
  document.querySelector<HTMLTextAreaElement>('form[data-prompt-comparison-form] textarea[name="DeckAText"]')!.value = '';
  document.querySelector<HTMLSelectElement>('form[data-prompt-comparison-form] select[name="DeckBInputSource"]')!.value = 'PasteText';
  document.querySelector<HTMLInputElement>('form[data-prompt-comparison-form] input[name="DeckBUrl"]')!.value = '';
  document.querySelector<HTMLTextAreaElement>('form[data-prompt-comparison-form] textarea[name="DeckBText"]')!.value = '';
  document.querySelector<HTMLSelectElement>('form[data-prompt-comparison-form] select[name="DeckABracket"]')!.value = '';
  document.querySelector<HTMLSelectElement>('form[data-prompt-comparison-form] select[name="DeckBBracket"]')!.value = '';
  const comparisonError = document.querySelector<HTMLElement>('[data-prompt-comparison-validation-error]')!;
  comparisonError.textContent = '';
  comparisonError.classList.add('hidden');
});

const submit = (form: HTMLFormElement): boolean => form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

describe('deck-sync prompt split-field validation', () => {
  it('accepts cEDH meta-gap deck text in PasteText mode on step 1', () => {
    document.querySelector<HTMLTextAreaElement>('form[data-prompt-cedh-form] textarea[name="DeckText"]')!.value = '1 Sol Ring';

    const form = document.querySelector<HTMLFormElement>('[data-prompt-cedh-form]')!;
    const allowed = submit(form);
    const errorNode = document.querySelector<HTMLElement>('[data-prompt-cedh-validation-error]')!;

    expect(allowed).toBe(true);
    expect(errorNode.textContent).toBe('');
    expect(errorNode.classList.contains('hidden')).toBe(true);
  });

  it('requires a cEDH meta-gap deck when both split inputs are blank', () => {
    const form = document.querySelector<HTMLFormElement>('[data-prompt-cedh-form]')!;
    const allowed = submit(form);
    const errorNode = document.querySelector<HTMLElement>('[data-prompt-cedh-validation-error]')!;

    expect(allowed).toBe(false);
    expect(errorNode.textContent).toContain('Paste your deck URL or deck text');
    expect(errorNode.classList.contains('hidden')).toBe(false);
  });

  it('accepts a cEDH meta-gap deck URL in PublicUrl mode on step 1', () => {
    document.querySelector<HTMLSelectElement>('form[data-prompt-cedh-form] select[name="DeckInputSource"]')!.value = 'PublicUrl';
    document.querySelector<HTMLInputElement>('form[data-prompt-cedh-form] input[name="DeckUrl"]')!.value = 'https://www.moxfield.com/decks/abc123';

    const form = document.querySelector<HTMLFormElement>('[data-prompt-cedh-form]')!;
    const allowed = submit(form);
    const errorNode = document.querySelector<HTMLElement>('[data-prompt-cedh-validation-error]')!;

    expect(allowed).toBe(true);
    expect(errorNode.textContent).toBe('');
    expect(errorNode.classList.contains('hidden')).toBe(true);
  });

  it('accepts deck comparison split text inputs when both brackets are chosen', () => {
    document.querySelector<HTMLTextAreaElement>('form[data-prompt-comparison-form] textarea[name="DeckAText"]')!.value = '1 Sol Ring';
    document.querySelector<HTMLTextAreaElement>('form[data-prompt-comparison-form] textarea[name="DeckBText"]')!.value = '1 Arcane Signet';
    document.querySelector<HTMLSelectElement>('form[data-prompt-comparison-form] select[name="DeckABracket"]')!.value = 'cEDH';
    document.querySelector<HTMLSelectElement>('form[data-prompt-comparison-form] select[name="DeckBBracket"]')!.value = 'cEDH';

    const nextButton = document.querySelector<HTMLButtonElement>('[data-prompt-comparison-next-step="2"]')!;
    const errorNode = document.querySelector<HTMLElement>('[data-prompt-comparison-validation-error]')!;

    nextButton.click();

    expect(errorNode.textContent).toBe('');
    expect(errorNode.classList.contains('hidden')).toBe(true);
  });

  it('requires Deck A before advancing deck comparison', () => {
    document.querySelector<HTMLTextAreaElement>('form[data-prompt-comparison-form] textarea[name="DeckBText"]')!.value = '1 Arcane Signet';
    document.querySelector<HTMLSelectElement>('form[data-prompt-comparison-form] select[name="DeckABracket"]')!.value = 'cEDH';
    document.querySelector<HTMLSelectElement>('form[data-prompt-comparison-form] select[name="DeckBBracket"]')!.value = 'cEDH';

    const nextButton = document.querySelector<HTMLButtonElement>('[data-prompt-comparison-next-step="2"]')!;
    const errorNode = document.querySelector<HTMLElement>('[data-prompt-comparison-validation-error]')!;

    nextButton.click();

    expect(errorNode.textContent).toContain('Enter Deck A URL or deck text');
    expect(errorNode.classList.contains('hidden')).toBe(false);
  });
});
