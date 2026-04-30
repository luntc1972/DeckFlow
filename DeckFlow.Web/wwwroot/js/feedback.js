"use strict";
(() => {
    'use strict';
    let initialized = false;
    const attachFeedbackBusyState = () => {
        if (initialized) {
            return;
        }
        initialized = true;
        const form = document.querySelector('form.feedback-form');
        if (!form) {
            return;
        }
        const button = form.querySelector('button.feedback-submit');
        if (!button) {
            return;
        }
        form.addEventListener('submit', () => {
            // D-08: do NOT cancel the submit — let the browser POST normally.
            // D-11: disabled flag prevents double-submit.
            button.disabled = true;
            button.classList.add('feedback-submit--busy');
            // D-09: text swap for the duration of the request.
            button.textContent = 'Sending…';
        });
    };
    document.addEventListener('DOMContentLoaded', attachFeedbackBusyState);
    if (document.readyState !== 'loading') {
        attachFeedbackBusyState();
    }
})();
