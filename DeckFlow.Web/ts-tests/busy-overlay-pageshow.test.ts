import { beforeAll, describe, expect, it } from 'vitest';

document.body.innerHTML = `
  <div id="busy-indicator" class="busy-indicator hidden">
    <div id="busy-indicator-title"></div>
    <div id="busy-indicator-message"></div>
    <div id="busy-indicator-progress"></div>
  </div>
`;

// Why (Phase 82 SRP split): busy-indicator.ts + moxfield-extension-bridge.ts were extracted from
// deck-sync.ts into their own physical files. bootstrapDeckSync() calls registerBusyIndicator()
// and attachMoxfieldExtensionImport() by bare name (shared global scope — both files compile
// under tsconfig's `module: "none"`), but the browser <script> tag chain that provides that shared
// scope in production doesn't exist under Vitest's per-file ESM import graph — so this harness
// must import both extracted modules before deck-sync itself, exactly mirroring the view's
// <script> load order.
await import('../wwwroot/ts/busy-indicator');
await import('../wwwroot/ts/moxfield-extension-bridge');
await import('../wwwroot/ts/deck-sync');

beforeAll(() => {
  document.dispatchEvent(new Event('DOMContentLoaded'));
});

describe('busy overlay pageshow handling', () => {
  it('hides the busy overlay when the page is shown again', () => {
    const overlay = document.getElementById('busy-indicator');

    expect(overlay).not.toBeNull();

    overlay!.classList.remove('hidden');
    window.dispatchEvent(new Event('pageshow'));

    expect(overlay!.classList.contains('hidden')).toBe(true);
  });
});
