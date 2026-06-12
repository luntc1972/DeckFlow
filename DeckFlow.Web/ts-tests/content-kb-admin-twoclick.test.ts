import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

document.body.innerHTML = `
  <form data-admin-confirm-twoclick>
    <button type="submit" data-confirm-label="Confirm delete">Delete</button>
  </form>
`;

await import('../wwwroot/ts/content-kb-admin');

const resetDom = (): void => {
  document.body.innerHTML = `
    <form data-admin-confirm-twoclick>
      <button type="submit" data-confirm-label="Confirm delete">Delete</button>
    </form>
  `;
};

beforeEach(() => {
  vi.useFakeTimers();
  resetDom();
  document.dispatchEvent(new Event('DOMContentLoaded'));
});

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = '';
});

describe('Admin Content KB two-click confirm', () => {
  it('arms and prevents submit on the first submit', () => {
    const form = document.querySelector<HTMLFormElement>('form[data-admin-confirm-twoclick]');
    const button = document.querySelector<HTMLButtonElement>('button[data-confirm-label]');

    expect(form).not.toBeNull();
    expect(button).not.toBeNull();

    const submitEvent = new Event('submit', { bubbles: true, cancelable: true });
    form!.dispatchEvent(submitEvent);

    expect(submitEvent.defaultPrevented).toBe(true);
    expect(form!.dataset.armed).toBe('true');
    expect(button!.textContent).toBe('Confirm delete');
    expect(button!.classList.contains('is-armed')).toBe(true);
  });

  it('allows submit on the second submit after the form is armed', () => {
    const form = document.querySelector<HTMLFormElement>('form[data-admin-confirm-twoclick]');

    expect(form).not.toBeNull();

    form!.dataset.armed = 'true';

    const submitEvent = new Event('submit', { bubbles: true, cancelable: true });
    form!.dispatchEvent(submitEvent);

    expect(submitEvent.defaultPrevented).toBe(false);
  });
});
