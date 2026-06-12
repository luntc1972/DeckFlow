import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/primer-selection';

interface PrimerSelectionApi {
  attachPrimerCopyButtons(): void;
}

let api: PrimerSelectionApi;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlow: PrimerSelectionApi }).DeckFlow;
});

beforeEach(() => {
  document.body.innerHTML = `
    <button type="button" data-copy-target="primer-output">Copy</button>
    <textarea id="primer-output">PRIMER PROMPT BODY</textarea>
  `;
});

describe('Primer copy buttons', () => {
  it('copies the textarea value for a data-copy-target button', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    api.attachPrimerCopyButtons();

    const button = document.querySelector<HTMLButtonElement>('[data-copy-target="primer-output"]');
    button?.click();
    await Promise.resolve();

    expect(writeText).toHaveBeenCalledTimes(1);
    expect(writeText).toHaveBeenCalledWith('PRIMER PROMPT BODY');
  });

  it('does not copy when the target value is empty', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    document.body.innerHTML = `
      <button type="button" data-copy-target="primer-output">Copy</button>
      <textarea id="primer-output"></textarea>
    `;

    api.attachPrimerCopyButtons();

    const button = document.querySelector<HTMLButtonElement>('[data-copy-target="primer-output"]');
    button?.click();
    await Promise.resolve();

    expect(writeText).not.toHaveBeenCalled();
  });
});
