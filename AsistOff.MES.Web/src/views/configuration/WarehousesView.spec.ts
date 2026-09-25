import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import WarehousesView from './WarehousesView.vue';
import { warehouseService, type WarehouseResponse } from '../../services/warehouseService';
import { stockOnHandService, type StockOnHandBalance } from '../../services/stockOnHandService';

// Verifier gap for #200 / PR #205 (TESTS_INSUFFICIENT): backend unit +
// endpoint suites proved the signed PW/RW aggregation, but the Warehouses-view
// stock section (list balances on mount, refresh after a new confirmation,
// unassigned bucket label) had no committed coverage and the PR body deferred
// the click-through to the e2e stage. These component tests close that gap by
// driving the real Warehouses UI with mocked services: open view -> see the
// seeded balance, refresh -> see the updated balance after a confirmation.

vi.mock('../../services/warehouseService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/warehouseService')>();
  return {
    ...actual,
    warehouseService: {
      ...actual.warehouseService,
      browse: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      remove: vi.fn()
    }
  };
});

vi.mock('../../services/stockOnHandService', () => ({
  stockOnHandService: {
    browse: vi.fn()
  }
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const browseWarehousesMock = vi.mocked(warehouseService.browse);
const browseStockMock = vi.mocked(stockOnHandService.browse);

function warehouse(overrides: Partial<WarehouseResponse> = {}): WarehouseResponse {
  return {
    id: 'warehouse-1',
    name: 'Main',
    syncId: null,
    ...overrides
  };
}

function balance(overrides: Partial<StockOnHandBalance> = {}): StockOnHandBalance {
  return {
    productId: 'product-1',
    warehouseId: 'warehouse-1',
    quantityOnHand: 6,
    ...overrides
  };
}

function page<T>(items: T[]): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, items };
}

function mountWarehouses(): VueWrapper {
  return mount(WarehousesView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

function stockCard(wrapper: VueWrapper): ReturnType<VueWrapper['find']> {
  return wrapper.find('.stock-card');
}

describe('WarehousesView stock on hand', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    browseWarehousesMock.mockResolvedValue(page([warehouse()]));
    browseStockMock.mockResolvedValue([balance()]);
  });

  it('lists the stock balances on mount with resolved warehouse names', async () => {
    const wrapper = mountWarehouses();
    await flushPromises();

    expect(browseStockMock).toHaveBeenCalledOnce();
    expect(browseStockMock).toHaveBeenCalledWith();

    const card = stockCard(wrapper);
    expect(card.exists()).toBe(true);
    expect(card.text()).toContain('product-1');
    expect(card.text()).toContain('Main');
    expect(card.text()).toContain('6');
    expect(card.find('table.app-table').exists()).toBe(true);
  });

  it('aggregates null-warehouse lines under the unassigned bucket label', async () => {
    browseStockMock.mockResolvedValue([
      balance(),
      balance({ productId: 'product-2', warehouseId: null, quantityOnHand: 7 })
    ]);

    const wrapper = mountWarehouses();
    await flushPromises();

    const card = stockCard(wrapper);
    expect(card.text()).toContain('product-2');
    expect(card.text()).toContain('warehouses.stock.unassigned');
    expect(card.text()).toContain('7');
  });

  it('refreshes the balances after a new confirmation is posted', async () => {
    const wrapper = mountWarehouses();
    await flushPromises();
    expect(browseStockMock).toHaveBeenCalledTimes(1);
    expect(stockCard(wrapper).text()).toContain('6');

    // A new RW issue of 4 pcs posted elsewhere drops the balance from 6 to 2.
    browseStockMock.mockResolvedValue([balance({ quantityOnHand: 2 })]);

    const refreshBtn = stockCard(wrapper).findAll('button').find((b) => b.text().includes('common.refresh'));
    expect(refreshBtn).toBeDefined();
    await refreshBtn?.trigger('click');
    await flushPromises();

    expect(browseStockMock).toHaveBeenCalledTimes(2);
    expect(stockCard(wrapper).text()).toContain('2');
  });

  it('shows the empty state when there are no balances', async () => {
    browseStockMock.mockResolvedValue([]);

    const wrapper = mountWarehouses();
    await flushPromises();

    expect(stockCard(wrapper).text()).toContain('warehouses.stock.empty');
  });
});
