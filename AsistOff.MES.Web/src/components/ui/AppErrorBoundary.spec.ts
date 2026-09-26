import { describe, expect, it } from 'vitest';
import { createMemoryHistory, createRouter } from 'vue-router';
import { defineComponent, h, nextTick, type Component } from 'vue';
import { mount } from '@vue/test-utils';
import AppErrorBoundary from './AppErrorBoundary.vue';
import i18n from '../../i18n';

const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function makeRouter() {
  const testRouter = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/', component: defineComponent({ setup: () => () => h('div') }) }]
  });
  return testRouter;
}

const Boom = defineComponent({
  setup() {
    throw new Error('view exploded');
    return () => h('div');
  }
});

function mountShell(child: Component) {
  const Parent = defineComponent({
    setup() {
      return () =>
        h('div', [
          h('div', { class: 'shell-chrome' }, 'chrome'),
          h(AppErrorBoundary, null, { default: () => h(child) })
        ]);
    }
  });
  const testRouter = makeRouter();
  return mount(Parent, { global: { plugins: [testRouter, i18n] } });
}

// Covers the global error boundary of issue #273: a render error in a
// child view shows the fallback with a reload action and a correlation id
// while the surrounding shell stays mounted — never a blank page.
describe('AppErrorBoundary', () => {
  it('renders the child view when nothing throws', async () => {
    const Calm = defineComponent({ setup: () => () => h('p', { class: 'calm' }, 'all good') });
    const wrapper = mountShell(Calm);
    await nextTick();

    expect(wrapper.find('.calm').exists()).toBe(true);
    expect(wrapper.find('.app-error-fallback').exists()).toBe(false);
  });

  it('shows the fallback with reload and correlation id while the shell stays mounted', async () => {
    const wrapper = mountShell(Boom);
    await nextTick();

    expect(wrapper.find('.shell-chrome').exists()).toBe(true);

    const fallback = wrapper.find('.app-error-fallback');
    expect(fallback.exists()).toBe(true);
    expect(fallback.attributes('role')).toBe('alert');
    expect(fallback.text()).toContain(String(i18n.global.t('errors.boundaryTitle')));
    expect(fallback.text()).toContain(String(i18n.global.t('common.reload')));
    expect(fallback.text()).toContain(String(i18n.global.t('errors.correlationId')));

    const correlationId = fallback.find('code').text();
    expect(correlationId).toMatch(GUID_PATTERN);
  });
});
