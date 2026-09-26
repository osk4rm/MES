import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import AppDataState from './AppDataState.vue';
import i18n from '../../i18n';

function mountState(props: Record<string, unknown>, slots?: Record<string, string>) {
  return mount(AppDataState, {
    props,
    slots,
    global: { plugins: [i18n] }
  });
}

const t = (key: string): string => String(i18n.global.t(key));

// Covers the shared list-region state machine (issue #314): a fixed
// error > loading > empty > content precedence, so every browse view
// renders the same states and every failure offers a working retry.
describe('AppDataState', () => {
  it('renders content when healthy and non-empty', () => {
    const wrapper = mountState(
      { loading: false, error: null, empty: false },
      { default: '<p class="payload">rows</p>' }
    );

    expect(wrapper.find('.payload').exists()).toBe(true);
    expect(wrapper.find('.app-error-state').exists()).toBe(false);
    expect(wrapper.find('.app-loading-state').exists()).toBe(false);
    expect(wrapper.find('.app-empty-state').exists()).toBe(false);
  });

  it('renders the shared loading state while the first page loads', () => {
    const wrapper = mountState({ loading: true, error: null, empty: true });

    const loading = wrapper.find('.app-loading-state');
    expect(loading.exists()).toBe(true);
    expect(loading.attributes('role')).toBe('status');
    expect(loading.text()).toContain(t('common.loading'));
    expect(wrapper.find('.payload').exists()).toBe(false);
  });

  it('renders the shared empty state when there is no data', () => {
    const wrapper = mountState({
      loading: false,
      error: null,
      empty: true,
      emptyTitle: 'Nothing here'
    });

    const empty = wrapper.find('.app-empty-state');
    expect(empty.exists()).toBe(true);
    expect(empty.text()).toContain('Nothing here');
  });

  it('renders the shared error state with a working retry on failure', async () => {
    const wrapper = mountState({ loading: false, error: 'Timed out', empty: true });

    const error = wrapper.find('.app-error-state');
    expect(error.exists()).toBe(true);
    expect(error.attributes('role')).toBe('alert');
    expect(error.text()).toContain('Timed out');

    const retry = error.find('button');
    expect(retry.exists()).toBe(true);
    expect(retry.text()).toContain(t('common.retry'));

    await retry.trigger('click');

    expect(wrapper.emitted('retry')).toHaveLength(1);
  });

  it('prefers the error state over loading and content', () => {
    const wrapper = mountState(
      { loading: true, error: 'Boom', empty: false },
      { default: '<p class="payload">rows</p>' }
    );

    expect(wrapper.find('.app-error-state').exists()).toBe(true);
    expect(wrapper.find('.app-loading-state').exists()).toBe(false);
    expect(wrapper.find('.payload').exists()).toBe(false);
  });
});
