import { afterEach, describe, expect, it, vi } from 'vitest';

const QUESTION = 'Can I cast this spell after a replacement effect changes how it resolves?';

const mountPage = (): void => {
  document.body.innerHTML = `
    <details class="cutlab-intake" open>
      <summary class="cutlab-intake-summary"><span class="cutlab-intake-summary__commander">Ask a question</span><span class="cutlab-intake-summary__change">Change question</span></summary>
      <div class="field-grid">
        <input data-judge-card-input />
        <textarea data-judge-question-input></textarea>
      </div>
      <div class="toolbar">
        <button type="button" data-judge-generate>Generate Prompt</button>
        <button type="button" data-judge-clear>Clear</button>
      </div>
    </details>
    <div class="error-banner hidden" data-judge-error></div>
    <section class="result-panel hidden" data-judge-result><textarea data-judge-prompt-output></textarea></section>
  `;
};

const bootstrap = async (): Promise<void> => {
  vi.resetModules();
  await import('../wwwroot/ts/judge-questions');
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
});

describe('Judge questions intake', () => {
  it('keeps the intake open with the default summary on DOMContentLoaded', async () => {
    mountPage();
    await bootstrap();

    expect(document.querySelector<HTMLDetailsElement>('.cutlab-intake')!.open).toBe(true);
    expect(document.querySelector<HTMLElement>('.cutlab-intake-summary__commander')!.textContent).toBe('Ask a question');
  });

  it('collapses the intake with a truncated question after a successful generate', async () => {
    mountPage();
    await bootstrap();
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ verifiedText: 'Oracle text' }) }));
    document.querySelector<HTMLInputElement>('[data-judge-card-input]')!.value = 'Sol Ring';
    document.querySelector<HTMLTextAreaElement>('[data-judge-question-input]')!.value = QUESTION;

    document.querySelector<HTMLButtonElement>('[data-judge-generate]')!.click();
    await vi.waitFor(() => expect(document.querySelector<HTMLDetailsElement>('.cutlab-intake')!.open).toBe(false));

    expect(document.querySelector<HTMLElement>('.cutlab-intake-summary__commander')!.textContent).toBe(`${QUESTION.slice(0, 40)}...`);
  });

  it('reopens the intake and resets its summary when cleared after a successful generate', async () => {
    mountPage();
    await bootstrap();
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ verifiedText: 'Oracle text' }) }));
    document.querySelector<HTMLTextAreaElement>('[data-judge-question-input]')!.value = QUESTION;

    document.querySelector<HTMLButtonElement>('[data-judge-generate]')!.click();
    await vi.waitFor(() => expect(document.querySelector<HTMLDetailsElement>('.cutlab-intake')!.open).toBe(false));
    document.querySelector<HTMLButtonElement>('[data-judge-clear]')!.click();

    expect(document.querySelector<HTMLDetailsElement>('.cutlab-intake')!.open).toBe(true);
    expect(document.querySelector<HTMLElement>('.cutlab-intake-summary__commander')!.textContent).toBe('Ask a question');
  });
});
