import { describe, expect, it, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import AppDateTimeField from './AppDateTimeField.vue';

// F-06: shared touch-friendly date-time field — labelled, 44 px targets,
// explicit local-time hint plus UTC conversion note, app-locale error.
vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

function mountField(props: Record<string, unknown> = {}) {
  setActivePinia(createPinia());
  return mount(AppDateTimeField, {
    props: { label: 'Reported at', modelValue: '', ...props },
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  });
}

// Raw Vue source via Vite (same ?raw pattern as the operator-panel tablet
// test: no node:fs so vue-tsc stays happy).
const fieldSources = import.meta.glob<string>('./AppDateTimeField.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

describe('AppDateTimeField (F-06)', () => {
  it('renders a visible label associated with the datetime input', () => {
    const wrapper = mountField({ label: 'Reported at' });

    const label = wrapper.find('label');
    expect(label.exists()).toBe(true);
    expect(label.text()).toContain('Reported at');

    const input = wrapper.find('input[type="datetime-local"]');
    expect(input.exists()).toBe(true);
    expect(input.attributes('id')).toBe(label.attributes('for'));
  });

  it('shows the local-time hint plus the UTC conversion note', () => {
    const wrapper = mountField();

    const text = wrapper.text();
    expect(text).toContain('common.datetimeLocalHint');
    expect(text).toContain('common.datetimeUtcNote');
  });

  it('surfaces invalid input as an app-locale error, not a native bubble', () => {
    const wrapper = mountField({ error: 'validation.invalidDate' });

    const alert = wrapper.find('[role="alert"]');
    expect(alert.exists()).toBe(true);
    expect(alert.text()).toContain('validation.invalidDate');
    expect(wrapper.find('input').attributes('aria-invalid')).toBe('true');
  });

  it('emits update:modelValue when the operator edits the timestamp', async () => {
    const wrapper = mountField();

    await wrapper.find('input').setValue('2026-09-24T08:00');

    expect(wrapper.emitted('update:modelValue')).toBeTruthy();
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['2026-09-24T08:00']);
  });

  it('keeps 44 px touch targets via scoped CSS', () => {
    const text = fieldSources['./AppDateTimeField.vue'] ?? '';

    expect(text).toContain('--control-height-touch');
  });
});
