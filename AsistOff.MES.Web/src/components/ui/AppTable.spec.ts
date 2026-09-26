import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import AppTable, { type TableColumn } from './AppTable.vue';
import i18n from '../../i18n';

const columns: TableColumn[] = [
  { key: 'code', label: 'Code' },
  { key: 'name', label: 'Name' }
];

function mountTable(props: Record<string, unknown>) {
  return mount(AppTable, {
    props: { items: [], columns, ...props },
    global: { plugins: [i18n] }
  });
}

// Covers the shared table error row (issue #314): a failed list fetch
// renders the error panel with retry instead of a stuck loader or a
// silent empty table.
describe('AppTable error state', () => {
  it('renders the loading row while fetching', () => {
    const wrapper = mountTable({ loading: true, loadingLabel: 'Hold on…' });

    expect(wrapper.text()).toContain('Hold on…');
    expect(wrapper.find('.app-error-state').exists()).toBe(false);
  });

  it('renders the empty row when there are no items', () => {
    const wrapper = mountTable({ loading: false, emptyLabel: 'Zero rows' });

    expect(wrapper.text()).toContain('Zero rows');
  });

  it('renders the shared error row with retry, winning over loading and data', async () => {
    const wrapper = mountTable({
      loading: true,
      error: 'Request failed',
      items: [{ id: '1', code: 'A', name: 'Alpha' }]
    });

    const error = wrapper.find('.app-error-state');
    expect(error.exists()).toBe(true);
    expect(error.text()).toContain('Request failed');

    await error.find('button').trigger('click');

    expect(wrapper.emitted('retry')).toHaveLength(1);
  });

  it('hides the error row once the error clears', async () => {
    const wrapper = mountTable({ error: 'Request failed' });

    expect(wrapper.find('.app-error-state').exists()).toBe(true);

    await wrapper.setProps({ error: null });

    expect(wrapper.find('.app-error-state').exists()).toBe(false);
  });
});
