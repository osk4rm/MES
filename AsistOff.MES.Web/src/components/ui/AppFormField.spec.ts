import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import AppFormField from './AppFormField.vue';

// Covers the shared per-field error presentation (issue #325, slice 2/3):
// an invalid field gets the error style, exposes the message as an assertive
// live region for screen readers, and reports `invalid` to the control so
// inputs can set aria-invalid and the focus helper can find them.
describe('AppFormField', () => {
  it('renders the per-field error with an alert role when invalid', () => {
    const wrapper = mount(AppFormField, {
      props: { label: 'Code', required: true, error: 'Field is required' },
      slots: { default: '<input id="code" />' }
    });

    expect(wrapper.classes()).toContain('app-field--invalid');
    const error = wrapper.find('.app-field__error');
    expect(error.exists()).toBe(true);
    expect(error.text()).toBe('Field is required');
    expect(error.attributes('role')).toBe('alert');
  });

  it('reports the invalid flag to the slotted control', () => {
    const wrapper = mount(AppFormField, {
      props: { label: 'Code', error: 'Taken.' },
      slots: {
        default: '<template #default="{ invalid }"><input id="code" :data-invalid="invalid" /></template>'
      }
    });

    expect(wrapper.find('#code').attributes('data-invalid')).toBe('true');
  });

  it('renders the hint instead of an error when valid', () => {
    const wrapper = mount(AppFormField, {
      props: { label: 'Notes', hint: 'Optional' },
      slots: { default: '<input id="notes" />' }
    });

    expect(wrapper.classes()).not.toContain('app-field--invalid');
    expect(wrapper.find('.app-field__error').exists()).toBe(false);
    expect(wrapper.find('.app-field__hint').text()).toBe('Optional');
  });
});
