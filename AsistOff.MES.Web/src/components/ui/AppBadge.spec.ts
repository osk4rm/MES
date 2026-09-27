import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import AppBadge from './AppBadge.vue';

// Covers the slice 3/3 Andon visibility rule (issue #331): severity signals
// must carry color + icon + text, never color alone.
describe('AppBadge icon signaling', () => {
  it('renders icon plus text for a severity signal', () => {
    const wrapper = mount(AppBadge, {
      props: { variant: 'danger', icon: 'pi pi-exclamation-triangle', dot: true },
      slots: { default: 'Active' }
    });

    expect(wrapper.classes()).toContain('app-badge--danger');
    const icon = wrapper.find('.app-badge__icon');
    expect(icon.exists()).toBe(true);
    expect(icon.classes()).toContain('pi-exclamation-triangle');
    expect(icon.attributes('aria-hidden')).toBe('true');
    expect(wrapper.text()).toBe('Active');
  });

  it('renders text-only badges when no icon is given', () => {
    const wrapper = mount(AppBadge, {
      props: { variant: 'neutral' },
      slots: { default: 'On time' }
    });

    expect(wrapper.find('.app-badge__icon').exists()).toBe(false);
    expect(wrapper.text()).toBe('On time');
  });

  it('keeps the dot alongside the icon for redundant signaling', () => {
    const wrapper = mount(AppBadge, {
      props: { variant: 'warning', icon: 'pi pi-eye', dot: true },
      slots: { default: 'Acknowledged' }
    });

    expect(wrapper.find('.app-badge__icon').exists()).toBe(true);
    expect(wrapper.find('.app-badge__dot').exists()).toBe(true);
  });
});
