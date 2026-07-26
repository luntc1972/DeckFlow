import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
  window.localStorage.clear();
  window.sessionStorage.clear();
});

describe('cedh meta gap hidden field persistence', () => {
  it('does not restore stale WorkflowStep/FetchedEntriesJson/MetaGapPromptText while still hydrating other fields', async () => {
    window.sessionStorage.setItem(
      'decksync-form-state-prompt-cedh-meta-gap',
      JSON.stringify({
        WorkflowStep: ['1'],
        FetchedEntriesJson: ['[{"stale":true}]'],
        MetaGapPromptText: ['stale prompt'],
        CommanderName: ['Stale Commander'],
      }));
    window.sessionStorage.setItem(
      'decksync-form-state-prompt-cedh-meta-gap:savedAt',
      Date.now().toString());

    document.body.innerHTML = `
      <form data-cache-key="prompt-cedh-meta-gap">
        <input type="hidden" name="WorkflowStep" value="3" />
        <input type="hidden" name="FetchedEntriesJson" value='[{"fresh":true}]' />
        <input type="hidden" name="MetaGapPromptText" value="fresh prompt" />
        <input name="CommanderName" value="Fresh Commander" />
      </form>
    `;

    vi.resetModules();
    await import('../wwwroot/ts/deck-sync');

    expect(document.querySelector<HTMLInputElement>('input[name="WorkflowStep"]')?.value)
      .toBe('3');
    expect(document.querySelector<HTMLInputElement>('input[name="FetchedEntriesJson"]')?.value)
      .toBe('[{"fresh":true}]');
    expect(document.querySelector<HTMLInputElement>('input[name="MetaGapPromptText"]')?.value)
      .toBe('fresh prompt');
    expect(document.querySelector<HTMLInputElement>('input[name="CommanderName"]')?.value)
      .toBe('Stale Commander');
  });
});
