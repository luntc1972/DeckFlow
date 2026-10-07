import { afterEach, describe, expect, it } from 'vitest';
import '../wwwroot/ts/admin-harvest';

afterEach(() => { document.body.innerHTML = ''; });

describe('admin harvest run duration', () => {
  it('disables duration for Update and links its hint', () => {
    document.body.innerHTML = '<select id="runKind"><option value="bulk">Bulk</option><option value="update" selected>Update</option></select><select id="durationSeconds" aria-describedby="durationSecondsHint"><option>60</option></select><p id="durationSecondsHint"></p>';
    document.dispatchEvent(new Event('DOMContentLoaded'));
    expect(document.querySelector<HTMLSelectElement>('#durationSeconds')!.disabled).toBe(true);
    expect(document.querySelector('#durationSeconds')!.getAttribute('aria-describedby')).toBe('durationSecondsHint');
  });
});
