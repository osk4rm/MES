import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import DowntimeView from './DowntimeView.vue';
import { downtimeEventService } from '../../services/downtimeEventService';
import { machineService } from '../../services/machineService';
import { reasonCodeService } from '../../services/reasonCodeService';
import { productionOrderService } from '../../services/productionOrderService';

// Slice (3/3) F-05/F-06/F-19: every downtime filter select carries a visible
// label, timestamp entry uses the shared touch-friendly field, and the start
// modal follows the canonical confirmation field order
// (Work Center → reason → notes → timestamp).

vi.mock('../../services/downtimeEventService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/downtimeEventService')>();
  return {
    ...actual,
    downtimeEventService: {
      ...actual.downtimeEventService,
      browse: vi.fn(),
      start: vi.fn(),
      close: vi.fn(),
      update: vi.fn(),
      remove: vi.fn()
    }
  };
});

vi.mock('../../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/machineService')>();
  return {
    ...actual,
    machineService: { ...actual.machineService, browse: vi.fn() }
  };
});

vi.mock('../../services/reasonCodeService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/reasonCodeService')>();
  return {
    ...actual,
    reasonCodeService: { ...actual.reasonCodeService, browse: vi.fn() }
  };
});

vi.mock('../../services/productionOrderService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/productionOrderService')>();
  return {
    ...actual,
    productionOrderService: { ...actual.productionOrderService, browse: vi.fn() }
  };
});

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

function emptyPage<T>(): { items: T[]; totalCount: number; totalPages: number; pageNumber: number; pageSize: number } {
  return { items: [], totalCount: 0, totalPages: 0, pageNumber: 1, pageSize: 20 };
}

function mountDowntime(): VueWrapper {
  return mount(DowntimeView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key },
      stubs: {
        AppModal: {
          props: ['open', 'title'],
          template: '<div v-if="open" class="modal-stub"><slot /><slot name="footer" /></div>'
        }
      }
    }
  }) as unknown as VueWrapper;
}

describe('DowntimeView slice (3/3)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(downtimeEventService.browse).mockResolvedValue(emptyPage());
    vi.mocked(machineService.browse).mockResolvedValue(emptyPage());
    vi.mocked(reasonCodeService.browse).mockResolvedValue(emptyPage());
    vi.mocked(productionOrderService.browse).mockResolvedValue(emptyPage());
  });

  it('labels every filter select (F-05)', async () => {
    const wrapper = mountDowntime();
    await flushPromises();

    const text = wrapper.text();
    expect(text).toContain('downtime.filters.machine');
    expect(text).toContain('downtime.filters.reason');
    expect(text).toContain('downtime.filters.status');
    expect(text).toContain('downtime.filters.from');
    expect(text).toContain('downtime.filters.to');

    const selects = wrapper.findAll('.app-filter-bar select');
    expect(selects.length).toBe(3);
    for (const select of selects) {
      const id = select.attributes('id');
      expect(id).toBeTruthy();
      expect(wrapper.find(`label[for="${id}"]`).exists()).toBe(true);
    }
  });

  it('start modal uses the shared date-time field (F-06)', async () => {
    const wrapper = mountDowntime();
    await flushPromises();

    const start = wrapper.findAll('button').find((b) => b.text().includes('downtime.start'));
    expect(start).toBeDefined();
    await start?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#downtime-start-form');
    expect(form.exists()).toBe(true);
    expect(form.text()).toContain('common.datetimeLocalHint');
    expect(form.text()).toContain('common.datetimeUtcNote');
    expect(form.find('input[type="datetime-local"]').exists()).toBe(true);
  });

  it('start modal follows the canonical field order (F-19)', async () => {
    const wrapper = mountDowntime();
    await flushPromises();

    const start = wrapper.findAll('button').find((b) => b.text().includes('downtime.start'));
    await start?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#downtime-start-form');
    const html = form.html();
    const order = [
      'downtime.machine',
      'downtime.reasonCode',
      'downtime.notes',
      'downtime.startedAt'
    ].map((key) => html.indexOf(key));
    for (const idx of order) expect(idx).toBeGreaterThanOrEqual(0);
    for (let i = 1; i < order.length; i++) {
      expect(order[i]).toBeGreaterThan(order[i - 1] as number);
    }
  });
});
