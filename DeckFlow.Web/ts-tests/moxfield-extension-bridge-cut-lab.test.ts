import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

interface MoxfieldImportTask {
  url: string;
  applyImportedText: (deckText: string) => void;
}

interface MoxfieldExtensionBridgeTestApi {
  collectMoxfieldImportTasks: (form: HTMLFormElement) => MoxfieldImportTask[];
}

declare global {
  interface Window {
    DeckInputSource?: {
      PasteText: string;
      PublicUrl: string;
    };
    DeckFlowMoxfieldExtensionBridgeTest?: MoxfieldExtensionBridgeTestApi;
  }
}

beforeAll(async () => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab">
      <select id="cut-lab-input-source" name="DeckInputSource">
        <option value="PasteText">PasteText</option>
        <option value="PublicUrl" selected>PublicUrl</option>
      </select>
      <input id="cut-lab-deck-url" name="DeckUrl" value="https://www.moxfield.com/decks/abc123" />
      <textarea id="cut-lab-deck-text" name="DeckText"></textarea>
      <input type="checkbox" name="IncludeSideboard" value="true" />
    </form>
  `;
  window.DeckInputSource = {
    PasteText: 'PasteText',
    PublicUrl: 'PublicUrl',
  };
  await import('../wwwroot/ts/moxfield-extension-bridge');
});

beforeEach(() => {
  document.querySelector<HTMLSelectElement>('#cut-lab-input-source')!.value = 'PublicUrl';
  document.querySelector<HTMLInputElement>('#cut-lab-deck-url')!.value = 'https://www.moxfield.com/decks/abc123';
  document.querySelector<HTMLTextAreaElement>('#cut-lab-deck-text')!.value = '';
  document.querySelector<HTMLInputElement>('input[name="IncludeSideboard"]')!.checked = false;
});

describe('moxfield extension bridge cut-lab import task collection', () => {
  it('returns one task and applies imported text by switching to paste mode and enabling sideboard', () => {
    const form = document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]')!;
    const sideboardCheckbox = form.querySelector<HTMLInputElement>('input[name="IncludeSideboard"]')!;
    let sideboardChangeCount = 0;
    sideboardCheckbox.addEventListener('change', () => {
      sideboardChangeCount += 1;
    });

    const tasks = window.DeckFlowMoxfieldExtensionBridgeTest!.collectMoxfieldImportTasks(form);

    expect(tasks).toHaveLength(1);
    expect(tasks[0].url).toBe('https://www.moxfield.com/decks/abc123');

    tasks[0].applyImportedText('#Commander\n1 Tymna the Weaver\n#Sideboard\n1 Mana Crypt');

    expect(document.querySelector<HTMLTextAreaElement>('#cut-lab-deck-text')!.value).toBe('#Commander\n1 Tymna the Weaver\n#Sideboard\n1 Mana Crypt');
    expect(document.querySelector<HTMLSelectElement>('#cut-lab-input-source')!.value).toBe('PasteText');
    expect(document.querySelector<HTMLInputElement>('#cut-lab-deck-url')!.value).toBe('');
    expect(sideboardCheckbox.checked).toBe(true);
    expect(sideboardChangeCount).toBe(1);
  });

  it('returns no tasks when Cut Lab is already in paste mode', () => {
    const form = document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]')!;
    document.querySelector<HTMLSelectElement>('#cut-lab-input-source')!.value = 'PasteText';

    const tasks = window.DeckFlowMoxfieldExtensionBridgeTest!.collectMoxfieldImportTasks(form);

    expect(tasks).toEqual([]);
  });
});
