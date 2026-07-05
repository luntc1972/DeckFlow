import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

// Why (Phase 82 SRP split): dedicated Vitest coverage for moxfield-extension-bridge.ts's mobile-
// browser guard (isMobileBrowser -> abortBridgeBusy), extracted from deck-sync.ts. The existing
// Playwright e2e (e2e/deck-sync-bridge-busy.spec.ts) covers the "extension not installed" desktop
// path in real headless Chromium; it cannot exercise the mobile branch (Chromium there reports as
// desktop). Loaded via deck-sync.ts's bootstrap — the extracted module attaches no listeners of
// its own until bootstrapDeckSync() calls attachMoxfieldExtensionImport(), matching every other
// deck-sync-adjacent Vitest test in this suite (no exports under tsconfig's `module: "none"`).
document.body.innerHTML = `
  <div id="busy-indicator" class="busy-indicator"></div>
  <form data-cache-key="deck-convert">
    <select name="SourceFormat"><option value="Moxfield" selected>Moxfield</option></select>
    <select name="InputSource">
      <option value="PublicUrl" selected>PublicUrl</option>
      <option value="PasteText">PasteText</option>
    </select>
    <input name="DeckUrl" value="https://www.moxfield.com/decks/abc123" />
    <textarea name="DeckText"></textarea>
  </form>
`;

await import('../wwwroot/ts/busy-indicator');
await import('../wwwroot/ts/moxfield-extension-bridge');
await import('../wwwroot/ts/deck-sync');

beforeAll(() => {
  document.dispatchEvent(new Event('DOMContentLoaded'));
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('moxfield extension bridge — mobile browser guard', () => {
  it('alerts and hides the busy overlay instead of attempting the extension handshake on a mobile browser', () => {
    vi.useFakeTimers();
    const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
    Object.defineProperty(window.navigator, 'userAgentData', {
      configurable: true,
      value: { mobile: true },
    });

    const overlay = document.getElementById('busy-indicator')!;
    overlay.classList.remove('hidden');

    const form = document.querySelector('form[data-cache-key="deck-convert"]') as HTMLFormElement;
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    expect(alertSpy).toHaveBeenCalledTimes(1);
    expect(alertSpy.mock.calls[0][0]).toContain('desktop DeckFlow Bridge extension');

    // abortBridgeBusy() defers the hide to a macrotask (setTimeout 0) so it runs after the
    // bubble-phase showBusyIndicator() listener — see busy-indicator.ts's "Why" comment.
    vi.runOnlyPendingTimers();
    expect(overlay.classList.contains('hidden')).toBe(true);
  });
});
