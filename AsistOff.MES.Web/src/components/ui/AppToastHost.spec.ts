import { beforeEach, describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import AppToastHost from './AppToastHost.vue';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';
import i18n from '../../i18n';

const MARKUP_PAYLOAD = '<img src="x" onerror="alert(1)">Session expired';

// Error rendering stays text-only (issue #376): server error text travels
// through extractErrorMessage into the toast store, and the host must
// interpolate it as text — a markup-bearing payload must never become DOM.
describe('AppToastHost text-only error rendering', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    document.body.querySelector('.app-toast-host')?.remove();
  });

  it('renders formatter output containing markup as literal text', () => {
    const message = extractErrorMessage(
      { response: { data: { detail: MARKUP_PAYLOAD } } },
      'Sign-in failed'
    );
    expect(message).toBe(MARKUP_PAYLOAD);

    useToastStore().error(message, 0);
    const wrapper = mount(AppToastHost, {
      attachTo: document.body,
      global: { plugins: [i18n] }
    });

    try {
      const text = document.body.querySelector('.app-toast__message');
      expect(text?.textContent).toBe(MARKUP_PAYLOAD);
      expect(text?.querySelectorAll('*').length).toBe(0);
      expect(document.body.querySelector('.app-toast__message img')).toBeNull();
    } finally {
      wrapper.unmount();
      document.body.querySelector('.app-toast-host')?.remove();
    }
  });

  it('renders plain formatter fallbacks unchanged', () => {
    useToastStore().info('Saved', 0);
    const wrapper = mount(AppToastHost, {
      attachTo: document.body,
      global: { plugins: [i18n] }
    });

    try {
      expect(document.body.querySelector('.app-toast__message')?.textContent).toBe('Saved');
    } finally {
      wrapper.unmount();
      document.body.querySelector('.app-toast-host')?.remove();
    }
  });
});
