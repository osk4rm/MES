import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import KanbanBoardView from './KanbanBoardView.vue';
import RecipeDetailView from './RecipeDetailView.vue';
import { kanbanService } from '../../services/kanbanService';
import { recipeService, RecipeVersionStatus } from '../../services/recipeService';
import { recipeVersionService } from '../../services/recipeVersionService';

// Slice (3/3) F-14/F-19/F-20 parity: Kanban and recipe detail expose the
// shared density control with touch-sized defaults; the operator badge-on
// field carries autofocus plus a scan hint; confirmation-family flows share
// the canonical field order.

vi.mock('../../services/kanbanService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/kanbanService')>();
  return {
    ...actual,
    kanbanService: {
      browseLoops: vi.fn(),
      browseCards: vi.fn(),
      consumeCard: vi.fn(),
      orderCard: vi.fn(),
      replenishCard: vi.fn()
    }
  };
});

vi.mock('../../services/productService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/productService')>();
  return { ...actual, productService: { ...actual.productService, get: vi.fn() } };
});

vi.mock('../../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/machineService')>();
  return { ...actual, machineService: { ...actual.machineService, get: vi.fn() } };
});

vi.mock('../../services/warehouseService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/warehouseService')>();
  return { ...actual, warehouseService: { ...actual.warehouseService, get: vi.fn() } };
});
vi.mock('../../services/recipeService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/recipeService')>();
  return {
    ...actual,
    recipeService: { get: vi.fn(), remove: vi.fn(), update: vi.fn() }
  };
});

vi.mock('../../services/recipeVersionService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/recipeVersionService')>();
  return {
    ...actual,
    recipeVersionService: { get: vi.fn(), create: vi.fn(), clone: vi.fn() }
  };
});

vi.mock('vue-router', () => ({
  useRoute: (): { query: Record<string, unknown>; params: Record<string, string> } => ({
    query: {},
    params: { id: 'recipe-1' }
  }),
  useRouter: (): { replace: (...args: unknown[]) => void; push: (...args: unknown[]) => void } => ({
    replace: vi.fn(),
    push: vi.fn()
  })
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): {
    t: (key: string) => string;
    tm: (key: string) => Record<string, string>;
  } => ({
    t: (key: string): string => key,
    tm: (key: string): Record<string, string> =>
      key === 'kanban.statuses' ? { '1': 'Full', '2': 'Empty', '3': 'Ordered' } : {}
  })
}));

function emptyPage<T>(): { items: T[]; totalCount: number; totalPages: number } {
  return { items: [], totalCount: 0, totalPages: 0 };
}

describe('shopfloor density parity (F-14)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(kanbanService.browseLoops).mockResolvedValue(emptyPage());
  });

  it('Kanban exposes the shared density control with touch-sized defaults', async () => {
    const wrapper = mount(KanbanBoardView, {
      global: {
        plugins: [createPinia()],
        mocks: { $t: (key: string): string => key }
      }
    }) as unknown as VueWrapper;
    await flushPromises();

    expect(wrapper.text()).toContain('shopfloor.density.label');
    expect(wrapper.text()).toContain('shopfloor.density.comfortable');
    expect(wrapper.classes()).toContain('shopfloor-view');
  });

  it('recipe detail exposes the shared density control', async () => {
    vi.mocked(recipeService.get).mockResolvedValue({
      id: 'recipe-1',
      code: 'RX-1',
      name: 'Recipe 1',
      description: null,
      isActive: true,
      primaryProductId: null,
      currentVersionId: 'version-1',
      versions: [{ id: 'version-1', versionNumber: 1, status: RecipeVersionStatus.Released }]
    });
    vi.mocked(recipeVersionService.get).mockResolvedValue({
      id: 'version-1',
      recipeId: 'recipe-1',
      versionNumber: 1,
      status: RecipeVersionStatus.Released,
      operations: []
    });

    const wrapper = mount(RecipeDetailView, {
      global: {
        plugins: [createPinia()],
        mocks: { $t: (key: string): string => key },
        stubs: { RecipeVersionEditor: { template: '<div class="version-editor-stub" />' } }
      }
    }) as unknown as VueWrapper;
    await flushPromises();

    expect(wrapper.text()).toContain('shopfloor.density.label');
    expect(wrapper.classes()).toContain('shopfloor-view');
  });
});

describe('confirmation field order canon (F-19)', () => {
  it('order detail, operator panel, scrap and downtime share Work Center → quantity → reason → notes → timestamp', () => {
    // Source-order evidence: each flow's form lists the canonical fields in
    // the same relative order (per-flow namespaces stay as aliases).
    const flows: Array<{ file: string; keys: string[] }> = [
      {
        file: './ProductionOrderDetailView.vue',
        keys: [
          'productionConfirmations.machine',
          'productionConfirmations.goodQuantity',
          'productionConfirmations.notes',
          'productionConfirmations.reportedAt'
        ]
      },
      {
        file: './OperatorPanelView.vue',
        keys: ['scrap.machine', 'scrap.quantity', 'scrap.reasonCode', 'scrap.notes', 'scrap.reportedAt']
      },
      {
        file: './ScrapView.vue',
        keys: ['scrap.machine', 'scrap.quantity', 'scrap.reasonCode', 'scrap.notes', 'scrap.reportedAt']
      },
      {
        file: './DowntimeView.vue',
        keys: ['downtime.machine', 'downtime.reasonCode', 'downtime.notes', 'downtime.startedAt']
      }
    ];

    for (const flow of flows) {
      const text = viewSources[flow.file] ?? '';
      expect(text.length).toBeGreaterThan(0);
      const positions = flow.keys.map((key) => text.indexOf(key));
      for (const pos of positions) expect(pos).toBeGreaterThanOrEqual(0);
      for (let i = 1; i < positions.length; i++) {
        expect(positions[i]).toBeGreaterThan(positions[i - 1] as number);
      }
    }
  });
});

describe('operator badge-on scan parity (F-20)', () => {
  it('code field mirrors the lot scan pattern: autofocus plus Enter handling', () => {
    const text = viewSources['./OperatorPanelView.vue'] ?? '';

    expect(text).toContain('autofocus');
    expect(text).toContain('@enter="applyOperator"');
    expect(text).toContain('operatorPanel.operatorCodeHint');
  });
});

const viewSources = import.meta.glob<string>(
  [
    './ProductionOrderDetailView.vue',
    './OperatorPanelView.vue',
    './ScrapView.vue',
    './DowntimeView.vue'
  ],
  { eager: true, query: '?raw', import: 'default' }
);
