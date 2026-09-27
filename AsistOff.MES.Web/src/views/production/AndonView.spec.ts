import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import AndonView from './AndonView.vue';
import {
  AndonSignalCategory,
  AndonSignalStatus,
  andonSignalService,
  type AndonSignalResponse
} from '../../services/andonSignalService';
import { machineService } from '../../services/machineService';

// Slice (3/3) F-06/F-11: Andon board Ack/Resolve actions meet the 44 px
// minimum under comfortable density (sm only for compact), timestamp entry
// uses the shared touch-friendly field, and board filters are labelled.

vi.mock('../../services/andonSignalService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/andonSignalService')>();
  return {
    ...actual,
    andonSignalService: {
      ...actual.andonSignalService,
      browse: vi.fn(),
      raise: vi.fn(),
      update: vi.fn(),
      acknowledge: vi.fn(),
      resolve: vi.fn(),
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

vi.mock('vue-i18n', () => ({
  useI18n: (): {
    t: (key: string) => string;
    tm: (key: string) => Record<string, string>;
  } => ({
    t: (key: string): string => key,
    tm: (key: string): Record<string, string> =>
      key === 'andon.categories'
        ? { '1': 'Downtime', '2': 'Quality', '3': 'Material', '4': 'Other' }
        : key === 'andon.statuses'
          ? { '1': 'Active', '2': 'Acknowledged', '3': 'Resolved' }
          : {}
  })
}));

function emptyPage<T>(): { items: T[]; totalCount: number; totalPages: number; pageNumber: number; pageSize: number } {
  return { items: [], totalCount: 0, totalPages: 0, pageNumber: 1, pageSize: 20 };
}

function signalFixture(): AndonSignalResponse {
  return {
    id: 'signal-1',
    machineId: 'machine-1',
    category: AndonSignalCategory.Downtime,
    status: AndonSignalStatus.Active,
    raisedAt: new Date('2026-09-24T08:00:00Z').toISOString(),
    acknowledgedAt: null,
    resolvedAt: null,
    notes: 'line stopped',
    reasonCodeId: null,
    raisedByOperatorId: null,
    productionOrderId: null,
    createdAt: new Date('2026-09-24T08:00:00Z').toISOString(),
    updatedAt: null
  };
}

function mountAndon(): VueWrapper {
  return mount(AndonView, {
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

describe('AndonView slice (3/3)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(machineService.browse).mockResolvedValue(emptyPage());
    vi.mocked(andonSignalService.browse).mockImplementation(async (req) => {
      if ((req as { status?: number }).status === AndonSignalStatus.Active) {
        return { ...emptyPage<AndonSignalResponse>(), items: [signalFixture()], totalCount: 1, totalPages: 1 };
      }
      return emptyPage<AndonSignalResponse>();
    });
  });

  it('labels every board filter select', async () => {
    const wrapper = mountAndon();
    await flushPromises();

    const text = wrapper.text();
    expect(text).toContain('andon.filters.machine');
    expect(text).toContain('andon.filters.category');
    expect(text).toContain('andon.filters.status');
  });

  it('renders board actions at touch height under comfortable density (F-11)', async () => {
    const wrapper = mountAndon();
    await flushPromises();

    const board = wrapper.find('.board');
    expect(board.exists()).toBe(true);
    const ack = board.findAll('button').find((b) => b.text().includes('andon.acknowledge'));
    const resolve = board.findAll('button').find((b) => b.text().includes('andon.resolve'));
    expect(ack).toBeDefined();
    expect(resolve).toBeDefined();
    // Comfortable density: md buttons (shopfloor-view scope enforces 44 px).
    expect(ack?.classes().join(' ')).toContain('md');
    expect(resolve?.classes().join(' ')).toContain('md');
    expect(ack?.classes().join(' ')).not.toContain('sm');
  });

  it('keeps sm board actions for compact density only (F-11)', async () => {
    localStorage.setItem('shopfloor-density', 'compact');

    const wrapper = mountAndon();
    await flushPromises();

    const board = wrapper.find('.board');
    const ack = board.findAll('button').find((b) => b.text().includes('andon.acknowledge'));
    expect(ack).toBeDefined();
    expect(ack?.classes().join(' ')).toContain('sm');
  });

  it('raise modal uses the shared date-time field (F-06)', async () => {
    const wrapper = mountAndon();
    await flushPromises();

    const raise = wrapper.findAll('button').find((b) => b.text().includes('andon.raise'));
    expect(raise).toBeDefined();
    await raise?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#andon-form');
    expect(form.exists()).toBe(true);
    expect(form.text()).toContain('common.datetimeLocalHint');
    expect(form.text()).toContain('common.datetimeUtcNote');
  });
});
