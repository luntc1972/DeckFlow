// Moxfield browser-extension bridge (Phase 82 SRP split — concern #1 of deck-sync.ts's 6-concern
// violation per 82-REVIEW.md). Extracted verbatim from deck-sync.ts (lines 100-431).
//
// Depends on `DeckInputSource` (deck-sync.ts) and `abortBridgeBusy` (busy-indicator.ts), read via
// `window.DeckInputSource` / `window.abortBridgeBusy?.()` rather than a bare cross-file identifier
// — see busy-indicator.ts's header comment for why (Vitest's per-file ESM import graph does not
// share bare top-level identifiers the way tsc's unified program + the browser's shared <script>
// scope do). The `prompt-packets` / `prompt-deck-comparison` / `prompt-cedh-meta-gap`
// cache-key string literals in collectMoxfieldImportTasks below were renamed in lockstep with
// deck-sync.ts by the Phase 85 naming cleanup; they are read-only string comparisons that pick
// which form inputs to wire, never the prompt-packets persistence/reset logic (which stays in
// deck-sync.ts, see REFACTOR-TRIAGE.md row 1b).
type MoxfieldImportTask = {
  url: string;
  applyImportedText: (deckText: string) => void;
};

type ExtensionBridgeSuccessResponse = {
  source: 'deckflow-extension';
  type: 'deckflow-moxfield-import-response';
  requestId: string;
  ok: true;
  deckText: string;
  deckName?: string | null;
  cardCount?: number;
  sourceUrl?: string | null;
};

type ExtensionBridgeErrorResponse = {
  source: 'deckflow-extension';
  type: 'deckflow-moxfield-import-response';
  requestId: string;
  ok: false;
  error: string;
  optionsUrl?: string;
};

type ExtensionBridgePingResponse = {
  source: 'deckflow-extension';
  type: 'deckflow-extension-ping-response';
  requestId: string;
  allowed?: boolean;
  optionsUrl?: string;
};

type ExtensionBridgeResponse = ExtensionBridgeSuccessResponse | ExtensionBridgeErrorResponse | ExtensionBridgePingResponse;

const moxfieldUrlPattern = /^https?:\/\/(?:www\.)?moxfield\.com\/decks\/[^/?#\s]+\/?$/i;
let extensionRequestCounter = 0;

const isSingleMoxfieldDeckUrl = (value: string): boolean => moxfieldUrlPattern.test(value.trim());

const createExtensionRequestId = (): string => {
  extensionRequestCounter += 1;
  return `deckflow-extension-${extensionRequestCounter}`;
};

const getExtensionInstallUrl = (): string => document.body.dataset.deckflowExtensionInstallUrl ?? '/extension-install.html';

const isMobileBrowser = (): boolean => {
  const userAgentData = (navigator as any).userAgentData as { mobile?: boolean } | undefined;
  if (typeof userAgentData?.mobile === 'boolean') {
    return userAgentData.mobile;
  }

  return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent);
};

const postExtensionBridgeRequest = async (type: 'deckflow-extension-ping' | 'deckflow-moxfield-import', payload: Record<string, unknown>, timeoutMs = 2500): Promise<ExtensionBridgeResponse> => {
  const requestId = createExtensionRequestId();

  return await new Promise<ExtensionBridgeResponse>((resolve, reject) => {
    const timeoutId = window.setTimeout(() => {
      window.removeEventListener('message', handleMessage);
      reject(new Error('Timed out waiting for the DeckFlow browser extension.'));
    }, timeoutMs);

    const handleMessage = (event: MessageEvent<ExtensionBridgeResponse>): void => {
      if (event.source !== window) {
        return;
      }

      const message = event.data;
      if (!message || message.source !== 'deckflow-extension' || message.requestId !== requestId) {
        return;
      }

      window.clearTimeout(timeoutId);
      window.removeEventListener('message', handleMessage);
      resolve(message);
    };

    window.addEventListener('message', handleMessage);
    window.postMessage({ source: 'deckflow-web', type, requestId, ...payload }, window.location.origin);
  });
};

type DeckFlowExtensionStatus = {
  installed: boolean;
  allowed: boolean;
  optionsUrl?: string;
};

const getDeckFlowExtensionStatus = async (): Promise<DeckFlowExtensionStatus> => {
  try {
    const response = await postExtensionBridgeRequest('deckflow-extension-ping', {}, 1200);
    if (response.type !== 'deckflow-extension-ping-response') {
      return { installed: true, allowed: false };
    }

    return {
      installed: true,
      allowed: response.allowed !== false,
      optionsUrl: response.optionsUrl
    };
  } catch {
    return { installed: false, allowed: false };
  }
};

const importMoxfieldDeckTextViaExtension = async (url: string): Promise<string> => {
  const response = await postExtensionBridgeRequest('deckflow-moxfield-import', { deckUrl: url }, 6000);
  if (response.type !== 'deckflow-moxfield-import-response') {
    throw new Error('The browser extension returned an unexpected response.');
  }

  if (!response.ok) {
    throw new Error(response.error || 'The browser extension could not import this Moxfield deck.');
  }

  return response.deckText;
};

const promptToConfigureMoxfieldExtensionOrigin = (optionsUrl?: string): boolean => {
  const shouldOpenOptions = window.confirm(
    `The DeckFlow extension is installed, but ${window.location.origin} is not allowed yet. Open the extension options to allow this origin?`
  );

  if (shouldOpenOptions && optionsUrl) {
    window.open(optionsUrl, '_blank', 'noopener');
  }

  return shouldOpenOptions;
};

const resubmitFormBypassingExtension = (form: HTMLFormElement, submitter: HTMLElement | null): void => {
  form.dataset.extensionBridgeBypass = 'true';
  if (submitter instanceof HTMLButtonElement || submitter instanceof HTMLInputElement) {
    form.requestSubmit(submitter);
    return;
  }

  form.requestSubmit();
};

const createSelectBackedImportTask = (
  urlInput: HTMLInputElement,
  textInput: HTMLTextAreaElement,
  sourceSelect: HTMLSelectElement
): MoxfieldImportTask | null => {
  if (sourceSelect.value !== window.DeckInputSource!.PublicUrl || !isSingleMoxfieldDeckUrl(urlInput.value)) {
    return null;
  }

  return {
    url: urlInput.value.trim(),
    applyImportedText: (deckText: string) => {
      textInput.value = deckText;
      urlInput.value = '';
      sourceSelect.value = window.DeckInputSource!.PasteText;
      sourceSelect.dispatchEvent(new Event('change', { bubbles: true }));
    }
  };
};

const createTextareaImportTask = (sourceInput: HTMLTextAreaElement): MoxfieldImportTask | null => {
  if (!isSingleMoxfieldDeckUrl(sourceInput.value)) {
    return null;
  }

  return {
    url: sourceInput.value.trim(),
    applyImportedText: (deckText: string) => {
      sourceInput.value = deckText;
    }
  };
};

const collectMoxfieldImportTasks = (form: HTMLFormElement): MoxfieldImportTask[] => {
  const cacheKey = form.dataset.cacheKey;
  if (!cacheKey) {
    return [];
  }

  if (cacheKey === 'deck-sync') {
    const tasks: MoxfieldImportTask[] = [];
    const direction = form.querySelector<HTMLSelectElement>('select[name="Direction"]')?.value ?? 'MoxfieldToArchidekt';
    const leftUsesMoxfield = direction !== 'ArchidektToArchidekt';
    const rightUsesMoxfield = direction === 'MoxfieldToMoxfield';

    if (leftUsesMoxfield) {
      const leftTask = createSelectBackedImportTask(
        form.querySelector<HTMLInputElement>('input[name="MoxfieldUrl"]')!,
        form.querySelector<HTMLTextAreaElement>('textarea[name="MoxfieldText"]')!,
        form.querySelector<HTMLSelectElement>('select[name="MoxfieldInputSource"]')!
      );
      if (leftTask) {
        tasks.push(leftTask);
      }
    }

    if (rightUsesMoxfield) {
      const rightTask = createSelectBackedImportTask(
        form.querySelector<HTMLInputElement>('input[name="ArchidektUrl"]')!,
        form.querySelector<HTMLTextAreaElement>('textarea[name="ArchidektText"]')!,
        form.querySelector<HTMLSelectElement>('select[name="ArchidektInputSource"]')!
      );
      if (rightTask) {
        tasks.push(rightTask);
      }
    }

    return tasks;
  }

  if (cacheKey === 'deck-convert') {
    const sourceFormat = form.querySelector<HTMLSelectElement>('select[name="SourceFormat"]')?.value;
    if (sourceFormat !== 'Moxfield') {
      return [];
    }

    const task = createSelectBackedImportTask(
      form.querySelector<HTMLInputElement>('input[name="DeckUrl"]')!,
      form.querySelector<HTMLTextAreaElement>('textarea[name="DeckText"]')!,
      form.querySelector<HTMLSelectElement>('select[name="InputSource"]')!
    );
    return task ? [task] : [];
  }

  if (cacheKey === 'prompt-packets') {
    const task = createSelectBackedImportTask(
      form.querySelector<HTMLInputElement>('input[name="DeckUrl"]')!,
      form.querySelector<HTMLTextAreaElement>('textarea[name="DeckText"]')!,
      form.querySelector<HTMLSelectElement>('select[name="DeckInputSource"]')!
    );
    return task ? [task] : [];
  }

  if (cacheKey === 'prompt-deck-comparison') {
    return [
      createTextareaImportTask(form.querySelector<HTMLTextAreaElement>('textarea[name="DeckASource"]')!),
      createTextareaImportTask(form.querySelector<HTMLTextAreaElement>('textarea[name="DeckBSource"]')!)
    ].filter((task): task is MoxfieldImportTask => task !== null);
  }

  if (cacheKey === 'prompt-cedh-meta-gap') {
    const task = createTextareaImportTask(form.querySelector<HTMLTextAreaElement>('textarea[name="DeckSource"]')!);
    return task ? [task] : [];
  }

  return [];
};

const attachMoxfieldExtensionImport = (): void => {
  document.addEventListener('submit', async event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) {
      return;
    }

    if (form.dataset.extensionBridgeBypass === 'true') {
      delete form.dataset.extensionBridgeBypass;
      return;
    }

    const tasks = collectMoxfieldImportTasks(form);
    if (tasks.length === 0) {
      return;
    }

    event.preventDefault();
    const submitter = (event as SubmitEvent).submitter as HTMLElement | null;

    if (isMobileBrowser()) {
      window.alert(
        'Moxfield URLs require the desktop DeckFlow Bridge extension, which is not available on mobile browsers. '
        + 'Open your deck in Moxfield, tap Bulk Edit, copy the Main Deck contents, and paste them into the text field here. '
        + 'Tags are preserved.'
      );
      window.abortBridgeBusy?.();
      return;
    }

    const extensionStatus = await getDeckFlowExtensionStatus();

    if (!extensionStatus.installed) {
      window.alert(
        'Moxfield URLs require the DeckFlow Bridge extension. '
        + 'Opening the install page now — come back and retry after installing. '
        + 'If you cannot install the extension, switch this field to Paste text and use Moxfield Bulk Edit instead.'
      );
      window.open(getExtensionInstallUrl(), '_blank', 'noopener');
      window.abortBridgeBusy?.();
      return;
    }

    if (!extensionStatus.allowed) {
      window.alert(
        `The DeckFlow Bridge extension is installed but ${window.location.origin} is not on its allow list. `
        + 'Opening extension Options now — add this origin, then retry.'
      );
      if (extensionStatus.optionsUrl) {
        window.open(extensionStatus.optionsUrl, '_blank', 'noopener');
      }
      window.abortBridgeBusy?.();
      return;
    }

    try {
      for (const task of tasks) {
        const deckText = await importMoxfieldDeckTextViaExtension(task.url);
        task.applyImportedText(deckText);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      const optionsUrl = error && typeof error === 'object' && 'optionsUrl' in error
        ? String((error as { optionsUrl?: string }).optionsUrl ?? '')
        : '';

      if (optionsUrl && /not allowed/i.test(message)) {
        promptToConfigureMoxfieldExtensionOrigin(optionsUrl);
      } else {
        window.alert(
          `DeckFlow could not import this Moxfield URL through the browser extension:\n\n${message}\n\nRetry, or switch to Paste text and use Moxfield Bulk Edit.`
        );
      }

      window.abortBridgeBusy?.();
      return;
    }

    resubmitFormBypassingExtension(form, submitter);
  }, true);
};

// Why: cross-file bridge (Phase 82 SRP split) — see deck-sync.ts's `interface Window` comment.
// deck-sync.ts's bootstrap calls this by its `window.*` name, and this file reads
// `window.DeckInputSource` (set by deck-sync.ts) rather than the bare identifier.
interface Window {
  attachMoxfieldExtensionImport?: () => void;
  DeckInputSource?: typeof DeckInputSource;
}

window.attachMoxfieldExtensionImport = attachMoxfieldExtensionImport;
