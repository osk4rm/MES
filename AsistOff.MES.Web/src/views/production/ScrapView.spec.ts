import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ScrapView from './ScrapView.vue';
import { scrapEventService } from '../../services/scrapEventService';
import { machineService } from '../../services/machineService';
import { reasonCodeService } from '../../services/reasonCodeService';
import { productionOrderService } from '../../services/productionOrderService';

// Slice (3/3) F-05/F-06/F-19: every scrap filter select carries a visible
// label, timestamp entry uses the shared touch-friendly field, and the
// report modal follows the canonical confirmation field order
// (Work Center → quantity → reason → notes → timestamp).

vi.mock('../../services/scrapEventService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/scrapEventService')>();
  return {
    ...actual,
    scrapEventService: {
      ...actual.scrapEventService,
      browse: vi.fn(),
      create: vi.fn(),
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

function mountScrap(): VueWrapper {
  return mount(ScrapView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key },
      stubs: {
        // Render modal content inline so Teleport does not move it to
        // document.body (unreachable via wrapper.find in jsdom).
        AppModal: {
          props: ['open', 'title'],
          template: '<div v-if="open" class="modal-stub"><slot /><slot name="footer" /></div>'
        }
      }
    }
  }) as unknown as VueWrapper;
}

describe('ScrapView slice (3/3)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(scrapEventService.browse).mockResolvedValue(emptyPage());
    vi.mocked(machineService.browse).mockResolvedValue(emptyPage());
    vi.mocked(reasonCodeService.browse).mockResolvedValue(emptyPage());
    vi.mocked(productionOrderService.browse).mockResolvedValue(emptyPage());
  });

  it('labels every filter select (F-05)', async () => {
    const wrapper = mountScrap();
    await flushPromises();

    // Each filter select sits inside an AppFormField with a visible label.
    const text = wrapper.text();
    expect(text).toContain('scrap.filters.machine');
    expect(text).toContain('scrap.filters.reasonCode');
    expect(text).toContain('scrap.filters.from');
    expect(text).toContain('scrap.filters.to');

    // Labels are associated with their selects via for/id.
    const selects = wrapper.findAll('.app-filter-bar select');
    expect(selects.length).toBe(2);
    for (const select of selects) {
      const id = select.attributes('id');
      expect(id).toBeTruthy();
      expect(wrapper.find(`label[for="${id}"]`).exists()).toBe(true);
    }
  });

  it('report modal uses the shared date-time field (F-06)', async () => {
    const wrapper = mountScrap();
    await flushPromises();

    const report = wrapper.findAll('button').find((b) => b.text().includes('scrap.report'));
    expect(report).toBeDefined();
    await report?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#scrap-form');
    expect(form.exists()).toBe(true);
    // Shared field renders the local-time hint plus the UTC note.
    expect(form.text()).toContain('common.datetimeLocalHint');
    expect(form.text()).toContain('common.datetimeUtcNote');
    expect(form.find('input[type="datetime-local"]').exists()).toBe(true);
  });

  it('report modal follows the canonical field order (F-19)', async () => {
    const wrapper = mountScrap();
    await flushPromises();

    const report = wrapper.findAll('button').find((b) => b.text().includes('scrap.report'));
    await report?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#scrap-form');
    const html = form.html();
    const order = [
      'scrap.machine',
      'scrap.quantity',
      'scrap.reasonCode',
      'scrap.notes',
      'scrap.reportedAt'
    ].map((key) => html.indexOf(key));
    for (const idx of order) expect(idx).toBeGreaterThanOrEqual(0);
    for (let i = 1; i < order.length; i++) {
      expect(order[i]).toBeGreaterThan(order[i - 1] as number);
    }
  });
});
